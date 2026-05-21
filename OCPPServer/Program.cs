/**
 * Program.cs — OCPPServer entry point
 *
 * This server has two jobs:
 *
 *   1. OCPP WebSocket gateway (/ws/{stationId})
 *      Physical EV chargers connect here over WebSocket using the OCPP 1.6J
 *      protocol. Each charger sends messages (BootNotification, Heartbeat,
 *      StatusNotification, StartTransaction, StopTransaction, MeterValues)
 *      which are routed through Communicator.RouteOcppMessage().
 *
 *   2. REST API for EVChargingApi and the admin panel
 *      /api/chargers/{id}/start-session  — sends RemoteStartTransaction to charger
 *      /api/chargers/{id}/stop-session   — sends RemoteStopTransaction to charger
 *      /api/chargers/{id}/status         — returns live connection status
 *      /api/chargers/{id}/meter          — returns current meter readings
 *      /api/admin/chargers               — CRUD for charger records (admin panel)
 *
 * Data flow summary:
 *   Charger → WebSocket → Communicator.RouteOcppMessage() → DB + HTTP callback to EVChargingApi
 *   EVChargingApi → POST /start-session → Communicator.SendStartCharging() → WebSocket → Charger
 *
 * The HttpClient in Communicator (_apiHttp) is configured at startup via
 * Communicator.Configure() with the EVChargingApi base URL and internal API key.
 */

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCPP_RD.OCPP1._6_Models;
using OCPPServer;
using OCPPServer.Data;
using OCPPServer.DataBase.DBModels;
using OCPPServer.OCPP1._6_Models;
using System;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Swagger (development/admin UI) ─────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Database ───────────────────────────────────────────────────────────────────
// The OCPP server has its own PostgreSQL DB (separate from EVChargingApi's DB).
// It stores one Connector row per charger with live meter readings and status.
builder.Services.AddDbContext<ChargingDBContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── CORS ───────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opt => opt.AddDefaultPolicy(policy =>
    policy.WithOrigins("https://admin.alternatiview.com.ua")
          .AllowAnyHeader()
          .AllowAnyMethod()));

var app = builder.Build();

// Auto-apply pending migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChargingDBContext>();
    db.Database.Migrate();
}

// ── Trace system ──────────────────────────────────────────────────────────────
// Configure before anything else so the very first connection events are logged.
OcppTrace.Configure(
    directory: app.Configuration["Trace:Directory"] ?? "/app/traces",
    level:     int.TryParse(app.Configuration["Trace:Level"], out var lvl) ? lvl : 1,
    maxFileMb: int.TryParse(app.Configuration["Trace:MaxFileMb"], out var mb) ? mb : 10);
OcppTrace.Msg("SYS", $"OCPPServer starting — trace level {app.Configuration["Trace:Level"]}");

// ── RabbitMQ publisher ────────────────────────────────────────────────────────
// Connect after migrations so startup ordering issues don't mask DB errors.
// If RabbitMQ is unreachable, events are simply dropped — not a fatal error.
await RabbitMqPublisher.ConfigureAsync(
    host:     app.Configuration["RabbitMQ:Host"]     ?? "rabbitmq",
    username: app.Configuration["RabbitMQ:Username"] ?? "guest",
    password: app.Configuration["RabbitMQ:Password"] ?? "guest");

// ── Middleware ──────────────────────────────────────────────────────────────────
app.UseCors();
app.UseWebSockets();   // must be before MapXxx so WebSocket upgrades are handled

// ── WebSocket endpoint: OCPP charger connections ───────────────────────────────
// Each charger connects to /ws/{its-stationId} and stays connected indefinitely.
// The server reads OCPP messages in a loop until the socket closes.
app.Map("/ws/{stationId}", async (HttpContext context, string stationId, ChargingDBContext db) =>
{
    stationId = stationId.ToLowerInvariant(); // normalise: "U030" == "u030"

    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    ChargingStationConnections.Add(stationId, webSocket);
    OcppTrace.Msg("WS", $"Station {stationId} connected.");

    var buffer = new byte[1024 * 16];

    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            // Accumulate WebSocket frames into a single message.
            // OCPP messages can be split across multiple frames ("fragmentation").
            using var ms = new System.IO.MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;
                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var json = Encoding.UTF8.GetString(ms.ToArray());
                OcppTrace.Msg("OCPP", $"[← IN ] {stationId}: {json}");
                await Communicator.RouteOcppMessage(webSocket, stationId, json, db);
            }
        }
    }
    finally
    {
        // Always clean up the connection entry and notify EVChargingApi on disconnect
        ChargingStationConnections.Remove(stationId);
        OcppTrace.Msg("WS", $"Station {stationId} disconnected.");
        Communicator.PushDisconnect(stationId); // tells the app to show "offline"
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ── REST API: session control ───────────────────────────────────────────────────

/// <summary>
/// Called by EVChargingApi to start a session on a charger.
/// Sends RemoteStartTransaction over the charger's WebSocket.
/// Returns 404 if the charger is not currently connected.
/// </summary>
app.MapPost("/api/chargers/{stationId}/start-session", async (string stationId) =>
{
    var socket = ChargingStationConnections.Get(stationId);
    if (socket == null || socket.State != WebSocketState.Open)
        return Results.NotFound("Charging station not connected");

    await Communicator.SendStartCharging(socket);

    return Results.Ok(new { stationId, status = "SENT" });
});

/// <summary>
/// Called by EVChargingApi (or WalletGuardService) to stop a session.
/// Sends RemoteStopTransaction with the correct transactionId (from session,
/// in-memory, or OCPP DB — whichever is available).
/// </summary>
app.MapPost("/api/chargers/{stationId}/stop-session", async (string stationId, int? transactionId, ChargingDBContext db) =>
{
    var socket = ChargingStationConnections.Get(stationId);
    if (socket == null || socket.State != WebSocketState.Open)
        return Results.NotFound("Charging station not connected");

    await Communicator.SendStopCharging(socket, stationId, transactionId, db);

    return Results.Ok(new { stationId, status = "SENT" });
});

// ── REST API: status and meter polling (called by EVChargingApi) ───────────────

/// <summary>
/// Returns the live OCPP status of a charger and whether its WebSocket is open.
/// Used by EVChargingApi's StationsController to enrich connector status.
/// </summary>
app.MapGet("/api/chargers/{ocppId}/status", async (string ocppId, ChargingDBContext db) =>
{
    var charger = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == ocppId);
    if (charger is null) return Results.NotFound();

    return Results.Ok(new
    {
        ocppId,
        status      = charger.Status.ToString(),
        isConnected = ChargingStationConnections.Get(ocppId) != null,
        lastUpdate  = charger.LastUpdate
    });
});

/// <summary>
/// Returns the current meter readings for a charger.
/// Called by EVChargingApi's GET /api/sessions/{id}/energy every 10 seconds
/// to show live energy consumption to the user.
///
/// energyKwh = (MeterValue − MeterStart) / 1000 — the kWh consumed in the current session.
/// currentPowerKw — instantaneous delivery power (computed from consecutive MeterValues).
/// meterStopWh — set when the charger sends StopTransaction; signals the session is finalised.
/// </summary>
app.MapGet("/api/chargers/{ocppId}/meter", async (string ocppId, ChargingDBContext db) =>
{
    var charger = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == ocppId);
    if (charger is null) return Results.NotFound();

    var sessionKwh = (charger.MeterStart.HasValue && charger.MeterValue.HasValue)
        ? Math.Round((double)(charger.MeterValue.Value - charger.MeterStart.Value) / 1000, 3)
        : Math.Round((double)(charger.MeterValue ?? 0) / 1000, 3);

    return Results.Ok(new
    {
        ocppId,
        meterStartWh     = charger.MeterStart,
        meterValueWh     = charger.MeterValue,
        meterStopWh      = charger.MeterStop,
        energyKwh        = sessionKwh,              // kWh consumed in current/last session
        currentPowerKw   = charger.CurrentPowerKw,  // instantaneous power; null when idle
        sessionStartedAt = charger.SessionStartedAt,
        lastUpdate       = charger.LastUpdate
    });
});

// ── REST API: admin panel endpoints ───────────────────────────────────────────

/// <summary>Returns all charger records with their current status and meter values.</summary>
app.MapGet("/api/admin/chargers", async (ChargingDBContext db) =>
{
    var chargers = await db.Connectors
        .OrderBy(c => c.OcppId)
        .Select(c => new
        {
            c.OcppId, c.Name, c.Address, c.Latitude, c.Longitude,
            c.IsFastCharger, c.ShowOnMap, c.MaxPowerKw, c.NumberOfConnectors,
            c.Vendor, c.ChargePointModel, c.FirmwareVersion,
            status      = c.Status.ToString(),
            c.MeterStart, c.MeterValue, c.MeterStop,
            c.LastUpdate, c.CreatedAt,
            isConnected = ChargingStationConnections.Get(c.OcppId) != null
        })
        .ToListAsync();

    return Results.Ok(chargers);
});

/// <summary>Updates the admin-editable fields of a charger (name, address, location, etc.).</summary>
app.MapPut("/api/admin/chargers/{ocppId}", async (string ocppId, ChargerInfoUpdate update, ChargingDBContext db) =>
{
    var charger = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == ocppId);
    if (charger is null) return Results.NotFound($"Charger '{ocppId}' not found.");

    charger.Name               = update.Name;
    charger.Address            = update.Address;
    charger.Latitude           = update.Latitude;
    charger.Longitude          = update.Longitude;
    charger.IsFastCharger      = update.IsFastCharger;
    charger.ShowOnMap          = update.ShowOnMap;
    charger.MaxPowerKw         = update.MaxPowerKw;
    charger.NumberOfConnectors = update.NumberOfConnectors;
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        charger.OcppId, charger.Name, charger.Address,
        charger.Latitude, charger.Longitude,
        charger.IsFastCharger, charger.ShowOnMap, charger.MaxPowerKw
    });
});

/// <summary>Deletes a charger record from the OCPP DB.</summary>
app.MapDelete("/api/admin/chargers/{ocppId}", async (string ocppId, ChargingDBContext db) =>
{
    var charger = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == ocppId);
    if (charger is null) return Results.NotFound($"Charger '{ocppId}' not found.");
    db.Connectors.Remove(charger);
    await db.SaveChangesAsync();
    return Results.Ok(new { deleted = ocppId });
});

/// <summary>
/// One-shot utility: copies all admin-editable fields from sourceId into targetId,
/// then deletes sourceId. Used to merge duplicate charger records (e.g. "U030" + "u030").
/// </summary>
app.MapPost("/api/admin/chargers/merge", async (string sourceId, string targetId, ChargingDBContext db) =>
{
    var src = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == sourceId);
    var tgt = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == targetId);
    if (src is null) return Results.NotFound($"Source '{sourceId}' not found.");
    if (tgt is null) return Results.NotFound($"Target '{targetId}' not found.");

    // Copy human-visible / config fields from src → tgt; preserve tgt's OCPP runtime fields
    tgt.Name               = src.Name               ?? tgt.Name;
    tgt.Address            = src.Address             ?? tgt.Address;
    tgt.Latitude           = src.Latitude            ?? tgt.Latitude;
    tgt.Longitude          = src.Longitude           ?? tgt.Longitude;
    tgt.IsFastCharger      = src.IsFastCharger;
    tgt.ShowOnMap          = src.ShowOnMap;
    tgt.MaxPowerKw         = src.MaxPowerKw          ?? tgt.MaxPowerKw;
    tgt.NumberOfConnectors = src.NumberOfConnectors;
    tgt.MeterStart         = src.MeterStart          ?? tgt.MeterStart;
    tgt.MeterValue         = (src.MeterValue > tgt.MeterValue) ? src.MeterValue : tgt.MeterValue;
    tgt.MeterStop          = src.MeterStop           ?? tgt.MeterStop;
    tgt.CreatedAt          = src.CreatedAt; // preserve original creation date

    db.Connectors.Remove(src);
    await db.SaveChangesAsync();

    return Results.Ok(new { merged = sourceId, into = targetId });
});

// ── REST API: trace file access ────────────────────────────────────────────────
// Protected by ?key= query parameter (set Trace:DownloadKey in appsettings.json).
// Accessible at app.alternatiview.com.ua/api/admin/traces/...

/// <summary>
/// Lists the available trace files with their current sizes.
/// Example: GET /api/admin/traces?key=mysecret
/// </summary>
app.MapGet("/api/admin/traces", (string? key, IConfiguration config) =>
{
    if (key != config["Trace:DownloadKey"])
        return Results.Unauthorized();

    var files = OcppTrace.GetFileList()
        .Select(f => new
        {
            name     = f.Name,
            sizeKb   = Math.Round(f.Bytes / 1024.0, 1),
            downloadUrl = $"/api/admin/traces/{f.Name}?key={key}"
        });

    return Results.Ok(files);
});

/// <summary>
/// Downloads a single trace file.
/// Example: GET /api/admin/traces/OCPP_Trace1.txt?key=mysecret
/// FileShare.ReadWrite allows reading the current file while writing continues.
/// </summary>
app.MapGet("/api/admin/traces/{filename}", (string filename, string? key, IConfiguration config) =>
{
    if (key != config["Trace:DownloadKey"])
        return Results.Unauthorized();

    var path = OcppTrace.GetFilePath(filename);
    if (path is null) return Results.NotFound($"Trace file '{filename}' not found.");

    // Open with ReadWrite share so the logger can keep appending while we serve
    var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    return Results.File(stream, contentType: "text/plain; charset=utf-8", fileDownloadName: filename);
});

app.Run();

// ── Request body types ─────────────────────────────────────────────────────────
record ChargerInfoUpdate(
    string? Name, string? Address, double? Latitude, double? Longitude,
    bool IsFastCharger, bool ShowOnMap, double? MaxPowerKw, int NumberOfConnectors);
