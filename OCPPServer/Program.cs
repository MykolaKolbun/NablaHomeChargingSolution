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
using OCPPServer.ChargingStationInterface;
using OCPPServer.Data;
using OCPPServer.DataBase.DBModels;
using OCPPServer.OCPP1._6_Models;
using OCPPServer.OCPP2._1_Models;
using System;
using System.Net.WebSockets;
using System.Text;
using static OCPP_RD.OCPP1._6_Models.MeterValuesRequest;

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

//---OCPP message handlers and router---
builder.Services.AddSingleton<Communicator>();           // OCPP 1.6J
builder.Services.AddSingleton<Ocpp21Communicator>();     // OCPP 2.0.1 / 2.1
builder.Services.AddSingleton<ICommunicator, OcppRouter>(); // version dispatcher

// ── RabbitMQ consumer (registered before Build so the DI container sees it) ───
builder.Services.AddHostedService<RabbitMqConsumer>();
builder.Services.AddScoped<IOcppCommandHandler, OcppCommandHandler>();

var app = builder.Build();

// Auto-apply pending migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChargingDBContext>();
    db.Database.Migrate();
}

// ── Trace system ──────────────────────────────────────────────────────────────
OcppTrace.Configure(
    directory: app.Configuration["Trace:Directory"] ?? "/app/traces",
    level:     int.TryParse(app.Configuration["Trace:Level"], out var lvl) ? lvl : 1,
    maxFileMb: int.TryParse(app.Configuration["Trace:MaxFileMb"], out var mb) ? mb : 10);
OcppTrace.Msg("SYS", $"OCPPServer starting — trace level {app.Configuration["Trace:Level"]}");

// ── RabbitMQ publisher ────────────────────────────────────────────────────────
// Connected after migrations. RabbitMqConsumer waits on SharedConnection so
// startup order is safe — the consumer won't try to consume before this returns.
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
//TODO: Uncomment when new version of this part not working, see region "new websocket with getting OCPP versions from charger"
//app.Map("/ws/{stationId}", async (HttpContext context, string stationId, ChargingDBContext db) =>
//{
//    stationId = stationId.ToLowerInvariant(); // normalise: "U030" == "u030"

//    if (!context.WebSockets.IsWebSocketRequest)
//    {
//        context.Response.StatusCode = StatusCodes.Status400BadRequest;
//        return;
//    }

//    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
//    ChargingStationConnections.Add(stationId, webSocket);
//    OcppTrace.Msg("WS", $"Station {stationId} connected.");

//    var buffer = new byte[1024 * 16];

//    try
//    {
//        while (webSocket.State == WebSocketState.Open)
//        {
//            // Accumulate WebSocket frames into a single message.
//            // OCPP messages can be split across multiple frames ("fragmentation").
//            using var ms = new System.IO.MemoryStream();
//            WebSocketReceiveResult result;
//            do
//            {
//                result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
//                if (result.MessageType == WebSocketMessageType.Close) break;
//                ms.Write(buffer, 0, result.Count);
//            }
//            while (!result.EndOfMessage);

//            if (result.MessageType == WebSocketMessageType.Text)
//            {
//                var json = Encoding.UTF8.GetString(ms.ToArray());
//                OcppTrace.Msg("OCPP", $"[← IN ] {stationId}: {json}");
//                await Communicator.RouteOcppMessage(webSocket, stationId, json, db);
//            }
//        }
//    }
//    finally
//    {
//        // Always clean up the connection entry and notify EVChargingApi on disconnect
//        ChargingStationConnections.Remove(stationId);
//        OcppTrace.Msg("WS", $"Station {stationId} disconnected.");
//        Communicator.PushDisconnect(stationId); // tells the app to show "offline"
//    }
//});

#region new websocket with getting OCPP versions from charger

app.Map("/ws/{stationId}", async (HttpContext context, string stationId, IServiceScopeFactory scopeFactory, ICommunicator communicator) =>
{
    stationId = stationId.ToLowerInvariant();

    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    // Negotiate OCPP sub-protocol — pick the highest version both sides support.
    // Priority: ocpp2.1 > ocpp2.0.1 > ocpp1.6 > ocpp1.5
    var protocolHeader = context.Request.Headers["Sec-WebSocket-Protocol"].ToString();
    OcppTrace.Msg("WS", $"{stationId} offered protocols: {protocolHeader}");

    string selectedProtocol = "ocpp1.6";  // default if charger sends nothing

    if (!string.IsNullOrEmpty(protocolHeader))
    {
        var offered = protocolHeader.Split(',').Select(p => p.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if      (offered.Contains("ocpp2.1"))   selectedProtocol = "ocpp2.1";
        else if (offered.Contains("ocpp2.0.1")) selectedProtocol = "ocpp2.0.1";
        else if (offered.Contains("ocpp2.0"))   selectedProtocol = "ocpp2.0";
        else if (offered.Contains("ocpp1.6"))   selectedProtocol = "ocpp1.6";
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync(selectedProtocol);

    ChargingStationConnections.Add(stationId, webSocket);
    ChargingStationConnections.SetProtocol(stationId, selectedProtocol);

    OcppTrace.Msg("WS", $"Station {stationId} connected — protocol: {selectedProtocol}");

    var buffer = new byte[1024 * 16];

    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
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

                // New scope per message — DbContext is borrowed from the pool for the
                // duration of the handler and released immediately after SaveChangesAsync.
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ChargingDBContext>();
                await communicator.RouteOcppMessage(webSocket, stationId, json, db);
            }
        }
    }
    finally
    {
        ChargingStationConnections.Remove(stationId);
        OcppTrace.Msg("WS", $"Station {stationId} disconnected.");
        communicator.PushDisconnect(stationId);
    }
});
#endregion

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//TODO: Remove REST API and handle session control via RabbitMQ messages from EVChargingApi instead.



// ── REST API: session control ───────────────────────────────────────────────────

/// <summary>
/// Called by EVChargingApi to start a session on a charger.
/// Sends RemoteStartTransaction over the charger's WebSocket.
/// Returns 404 if the charger is not currently connected.
/// </summary>
//app.MapPost("/api/chargers/{stationId}/start-session", async (string stationId, ICommunicator communicator) =>
//{
//    var socket = ChargingStationConnections.Get(stationId);
//    if (socket == null || socket.State != WebSocketState.Open)
//        return Results.NotFound("Charging station not connected");

//    await communicator.SendStartCharging(socket);

//    return Results.Ok(new { stationId, status = "SENT" });
//});

/// <summary>
/// Called by EVChargingApi (or WalletGuardService) to stop a session.
/// Sends RemoteStopTransaction with the correct transactionId (from session,
/// in-memory, or OCPP DB — whichever is available).
/// </summary>
//app.MapPost("/api/chargers/{stationId}/stop-session", async (string stationId, int? transactionId, ChargingDBContext db, ICommunicator communicator) =>
//{
//    var socket = ChargingStationConnections.Get(stationId);
//    if (socket == null || socket.State != WebSocketState.Open)
//        return Results.NotFound("Charging station not connected");

//    await communicator.SendStopCharging(socket, stationId, transactionId, db);

//    return Results.Ok(new { stationId, status = "SENT" });
//});

// ── REST API: status and meter polling (called by EVChargingApi) ───────────────

/// <summary>
/// Returns the live OCPP status of a charger and whether its WebSocket is open.
/// Used by EVChargingApi's StationsController to enrich connector status.
/// </summary>
//app.MapGet("/api/chargers/{ocppId}/status", async (string ocppId, ChargingDBContext db) =>
//{
//    var charger = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == ocppId);
//    if (charger is null) return Results.NotFound();

//    return Results.Ok(new
//    {
//        ocppId,
//        status      = charger.Status.ToString(),
//        isConnected = ChargingStationConnections.Get(ocppId) != null,
//        lastUpdate  = charger.LastUpdate
//    });
//});

/// <summary>
/// Returns the current meter readings for a charger.
/// Called by EVChargingApi's GET /api/sessions/{id}/energy every 10 seconds
/// to show live energy consumption to the user.
///
/// energyKwh = (MeterValue − MeterStart) / 1000 — the kWh consumed in the current session.
/// currentPowerKw — instantaneous delivery power (computed from consecutive MeterValues).
/// meterStopWh — set when the charger sends StopTransaction; signals the session is finalised.
/// </summary>
//app.MapGet("/api/chargers/{ocppId}/meter", async (string ocppId, ChargingDBContext db) =>
//{
//    var charger = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == ocppId);
//    if (charger is null) return Results.NotFound();

//    var meterStart = ChargingStationConnections.GetMeterStart(ocppId);
//    var sessionKwh = (meterStart.HasValue && charger.MeterValue.HasValue)
//        ? Math.Round((double)(charger.MeterValue.Value - meterStart.Value) / 1000, 3)
//        : Math.Round((double)(charger.MeterValue ?? 0) / 1000, 3);

//    return Results.Ok(new
//    {
//        ocppId,
//        meterStartWh     = meterStart,
//        meterValueWh     = charger.MeterValue,
//        meterStopWh      = (decimal?)null,           // MeterStop lives in EVChargingDB.ChargingSessions
//        energyKwh        = sessionKwh,              // kWh consumed in current/last session
//        currentPowerKw   = charger.CurrentPowerKw,  // instantaneous power; null when idle
//        sessionStartedAt = charger.SessionStartedAt,
//        lastUpdate       = charger.LastUpdate
//    });
//});

// ── REST API: admin plug endpoints ────────────────────────────────────────────

app.MapGet("/api/admin/plugs", async (ChargingDBContext db) =>
{
    var plugs = await db.Plugs
        .OrderBy(p => p.OcppId)
        .Select(p => new
        {
            p.OcppId, p.Status, p.IsOnline,
            p.IsFastCharger, p.MaxPower,
            p.Vendor, p.ChargePointModel, p.FirmwareVersion, p.OcppVersion,
            p.MeterValue, p.LastStatusUpdate, p.CreatedAt,
            isConnected = ChargingStationConnections.Get(p.OcppId) != null
        })
        .ToListAsync();

    return Results.Ok(plugs);
});

app.MapGet("/api/admin/plugs/{ocppId}", async (string ocppId, ChargingDBContext db) =>
{
    var plug = await db.Plugs.FirstOrDefaultAsync(p => p.OcppId == ocppId);
    if (plug is null) return Results.NotFound($"Plug '{ocppId}' not found.");

    return Results.Ok(new
    {
        plug.OcppId, plug.Status, plug.IsOnline,
        plug.IsFastCharger, plug.MaxPower,
        plug.Vendor, plug.ChargePointModel, plug.ChargePointSN,
        plug.FirmwareVersion, plug.SIMNr, plug.OcppVersion,
        plug.MeterValue, plug.LastStatusUpdate, plug.CreatedAt,
        isConnected = ChargingStationConnections.Get(plug.OcppId) != null
    });
});

app.MapPost("/api/admin/plugs/{ocppId}/diagnostics", async (string ocppId, GetDiagnosticsBody body, ICommunicator communicator) =>
{
    if (string.IsNullOrEmpty(body.Location))
        return Results.BadRequest("location is required");

    var socket = ChargingStationConnections.Get(ocppId);
    if (socket is null || socket.State != WebSocketState.Open)
        return Results.Problem($"Charger '{ocppId}' is not connected", statusCode: 503);

    var result = await communicator.SendGetDiagnostics(socket, new GetDiagnosticsRequest
    {
        Location      = body.Location,
        StartTime     = body.StartTime,
        StopTime      = body.StopTime,
        Retries       = body.Retries,
        RetryInterval = body.RetryInterval
    });

    var fileName = result["fileName"]?.Value<string>();
    return Results.Ok(new { ocppId, fileName });
});

app.MapPut("/api/admin/plugs/{ocppId}", async (string ocppId, PlugAdminUpdate update, ChargingDBContext db) =>
{
    var plug = await db.Plugs.FirstOrDefaultAsync(p => p.OcppId == ocppId);
    if (plug is null) return Results.NotFound($"Plug '{ocppId}' not found.");

    plug.IsFastCharger = update.IsFastCharger;
    plug.MaxPower      = update.MaxPower;
    await db.SaveChangesAsync();

    return Results.Ok(new { plug.OcppId, plug.IsFastCharger, plug.MaxPower });
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

record PlugAdminUpdate(bool IsFastCharger, int MaxPower);
record GetDiagnosticsBody(string Location, DateTime? StartTime, DateTime? StopTime, int? Retries, int? RetryInterval);
