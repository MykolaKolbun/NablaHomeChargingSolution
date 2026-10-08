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
using OCPPServer.Tracing;
using System;
using System.Net.WebSockets;
using System.Text;
using static OCPP_RD.OCPP1._6_Models.MeterValuesRequest;

var builder = WebApplication.CreateBuilder(args);

// ── Tracing log level — single source of truth ────────────────────────────────
// Tracing:MinPersistLevel drives BOTH the ILogger threshold AND DB persistence.
// Do not add a Logging:LogLevel:Default key to appsettings.json — it would shadow this.
{
    var minLevel = builder.Configuration["Tracing:MinPersistLevel"] ?? "Warning";
    var logLevel = minLevel.ToLowerInvariant() switch
    {
        "verbose"  => LogLevel.Trace,
        "info"     => LogLevel.Information,
        "error"    => LogLevel.Error,
        "critical" => LogLevel.Critical,
        _          => LogLevel.Warning,   // "warning" or unknown
    };
    builder.Logging.SetMinimumLevel(logLevel);
    builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
    builder.Logging.AddFilter("System",    LogLevel.Warning);
}

// ── Swagger (development/admin UI) ─────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── JSON serialisation ─────────────────────────────────────────────────────────
// Serialise all enums as their name string ("Charging") instead of their integer
// value (2). Applies to every Minimal API response in this app.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// ── Database ───────────────────────────────────────────────────────────────────
// The OCPP server has its own PostgreSQL DB (separate from EVChargingApi's DB).
// It stores one Connector row per charger with live meter readings and status.
builder.Services.AddDbContext<ChargingDBContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── CORS ───────────────────────────────────────────────────────────────────────
// Cors:AllowedOrigins lets another deployment (e.g. ocpp-home) point at its own admin UI.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (corsOrigins is null || corsOrigins.Length == 0)
    corsOrigins = ["https://admin.alternatiview.com.ua"];
builder.Services.AddCors(opt => opt.AddDefaultPolicy(policy =>
    policy.WithOrigins(corsOrigins)
          .AllowAnyHeader()
          .AllowAnyMethod()));

//---OCPP message handlers and router---
builder.Services.AddSingleton<Communicator>();           // OCPP 1.6J
builder.Services.AddSingleton<Ocpp21Communicator>();     // OCPP 2.0.1 / 2.1
builder.Services.AddSingleton<ICommunicator, OcppRouter>(); // version dispatcher

// ── Tracing ────────────────────────────────────────────────────────────────────
// Singleton: writes to OcppTrace file, ILogger (stdout), and ErrorLogs DB table.
// MinPersistLevel controls which levels reach the DB (default: Warning and above).
// ErrorLog cleanup is handled centrally by pg_cron (EVChargingApi/cron/init-pgcron.sql).
builder.Services.AddSingleton<ITracingService, TracingService>();

// Shared TimeZoneInfo for local-time writes (OccurredAt / SolvedAt in ErrorLogs).
builder.Services.AddSingleton(sp =>
{
    var cfg  = sp.GetRequiredService<IConfiguration>();
    var tzId = cfg["Tracing:TimezoneId"] ?? "Europe/Kyiv";
    return TimeZoneInfo.FindSystemTimeZoneById(tzId);
});

// ── RabbitMQ consumer (registered before Build so the DI container sees it) ───
builder.Services.AddHostedService<RabbitMqConsumer>();
builder.Services.AddScoped<IOcppCommandHandler, OcppCommandHandler>();

// ── Charger watcher ────────────────────────────────────────────────────────────
// Detects silently dead connections (no OCPP message for > threshold seconds),
// marks the plug offline in DB, then aborts the socket so the Program.cs finally
// block can do the standard Remove + PushDisconnect cleanup.
builder.Services.AddHostedService<ChargerWatcherService>();

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
    host:             app.Configuration["RabbitMQ:Host"]     ?? "rabbitmq",
    username:         app.Configuration["RabbitMQ:Username"] ?? "guest",
    password:         app.Configuration["RabbitMQ:Password"] ?? "guest",
    virtualHost:      app.Configuration["RabbitMQ:VirtualHost"] ?? "/",
    tracer:           app.Services.GetRequiredService<ITracingService>(),
    reconnectDelayMs: app.Configuration.GetValue("RabbitMQ:ReconnectDelayMs", 5_000));

// ── Middleware ──────────────────────────────────────────────────────────────────
app.UseCors();
app.UseWebSockets();   // must be before MapXxx so WebSocket upgrades are handled

// ── WebSocket endpoint: OCPP charger connections ───────────────────────────────
// Each charger connects to /ws/{its-stationId} and stays connected indefinitely.
// Negotiates OCPP sub-protocol at handshake; priority: ocpp2.1 > ocpp2.0.1 > ocpp2.0 > ocpp1.6.

app.Map("/ws/{stationId}", async (HttpContext context, string stationId,
    IServiceScopeFactory scopeFactory, ICommunicator communicator, ITracingService tracer) =>
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
    tracer.Verbose("WS", $"{stationId} offered protocols: {protocolHeader}", chargePointId: stationId);

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

    ChargingStationConnections.Add(stationId, webSocket, selectedProtocol);

    tracer.Info("WS", $"Station {stationId} connected — protocol: {selectedProtocol}", chargePointId: stationId);

    // Publish a reconnect event so EVChargingApi exits the Offline state immediately.
    // connectorId=0 clears all connectors. If the charger sends StatusNotification
    // right after (as most do), that will overwrite this with the real status.
    _ = RabbitMqPublisher.PublishStatusChangedAsync(stationId, "Available", connectorId: 0, isConnected: true);

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
                tracer.Verbose("OCPP", $"[← IN ] {stationId}: {json}", chargePointId: stationId);

                // Stamp every incoming message so ChargerWatcherService can detect silence.
                var connState = ChargingStationConnections.Get(stationId);
                if (connState != null) connState.LastMessageAt = DateTime.UtcNow;

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
        tracer.Info("WS", $"Station {stationId} disconnected.", chargePointId: stationId);
        communicator.PushDisconnect(stationId);
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ── REST API: admin plug endpoints ────────────────────────────────────────────

app.MapGet("/api/admin/plugs", async (ChargingDBContext db) =>
{
    var plugs = await db.Plugs.OrderBy(p => p.OcppId).ToListAsync();

    var result = plugs.Select(p =>
    {
        var state = ChargingStationConnections.Get(p.OcppId);
        return new
        {
            p.OcppId, p.Status, p.IsOnline,
            p.IsFastCharger, p.MaxPower,
            p.Vendor, p.ChargePointModel, p.FirmwareVersion, p.OcppVersion,
            p.LastStatusUpdate, p.CreatedAt,
            isConnected    = state != null,
            meterValueWh   = state?.MeterValueWh,
            meterStartWh   = state?.MeterStartWh,
            currentPowerKw = state?.CurrentPowerKw,
            stateOfCharge  = state?.StateOfCharge,
            meterType      = state?.Adapter.MeterType
        };
    });

    return Results.Ok(result);
});

app.MapGet("/api/admin/plugs/{ocppId}", async (string ocppId, ChargingDBContext db) =>
{
    var plug = await db.Plugs.FirstOrDefaultAsync(p => p.OcppId == ocppId);
    if (plug is null) return Results.NotFound($"Plug '{ocppId}' not found.");

    var state = ChargingStationConnections.Get(plug.OcppId);
    return Results.Ok(new
    {
        plug.OcppId, plug.Status, plug.IsOnline,
        plug.IsFastCharger, plug.MaxPower,
        plug.Vendor, plug.ChargePointModel, plug.ChargePointSN,
        plug.FirmwareVersion, plug.SIMNr, plug.OcppVersion,
        plug.LastStatusUpdate, plug.CreatedAt,
        isConnected    = state != null,
        meterValueWh   = state?.MeterValueWh,
        meterStartWh   = state?.MeterStartWh,
        currentPowerKw = state?.CurrentPowerKw,
        stateOfCharge  = state?.StateOfCharge,
        meterType      = state?.Adapter.MeterType
    });
});

app.MapPost("/api/admin/plugs/{ocppId}/diagnostics", async (string ocppId, GetDiagnosticsBody body, ICommunicator communicator) =>
{
    if (string.IsNullOrEmpty(body.Location))
        return Results.BadRequest("location is required");

    var socket = ChargingStationConnections.GetSocket(ocppId);
    if (socket is null || socket.State != WebSocketState.Open)
        return Results.Problem($"Charger '{ocppId}' is not connected", statusCode: 503);

    var result = await communicator.SendGetDiagnostics(socket, ocppId, new GetDiagnosticsRequest
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

// ── REST API: error logs ──────────────────────────────────────────────────────

/// <summary>
/// Returns persisted error-log entries written by TracingService.
///
/// Optional query parameters:
///   chargePointId — filter to a single charger (exact match on OcppId)
///   minLevel      — minimum severity: Info | Warning | Error | Critical  (default: Warning)
///   limit         — max rows returned, newest first                       (default: 200)
///
/// Example: GET /api/admin/error-logs?chargePointId=u030&amp;minLevel=Error&amp;limit=50
/// </summary>
app.MapGet("/api/admin/error-logs", async (
    ChargingDBContext db,
    string?           chargePointId,
    string?           minLevel,
    int               limit = 200) =>
{
    var floor = Enum.TryParse<ErrorLogLevel>(minLevel, ignoreCase: true, out var parsed)
        ? parsed
        : ErrorLogLevel.Warning;

    var logs = await db.ErrorLogs
        .Where(e => e.Level >= floor)
        .Where(e => chargePointId == null || e.ChargePointId == chargePointId)
        .OrderByDescending(e => e.OccurredAt)
        .Take(limit)
        .Select(e => new
        {
            e.Id,
            e.ChargePointId,
            e.SessionId,
            e.OccurredAt,
            level    = e.Level.ToString(),
            e.Source,
            e.Message,
            e.IsSolved,
            e.SolvedAt,
        })
        .ToListAsync();

    return Results.Ok(logs);
});

/// <summary>
/// Marks an error-log entry as solved.
/// Idempotent — calling it again on an already-solved entry is a no-op (returns 200).
/// </summary>
app.MapPatch("/api/admin/error-logs/{id}/solve", async (int id, ChargingDBContext db, TimeZoneInfo tz) =>
{
    var entry = await db.ErrorLogs.FindAsync(id);
    if (entry is null) return Results.NotFound($"ErrorLog {id} not found.");

    if (!entry.IsSolved)
    {
        entry.IsSolved = true;
        entry.SolvedAt = DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz),
            DateTimeKind.Unspecified);
        await db.SaveChangesAsync();
    }

    return Results.Ok(new { entry.Id, entry.IsSolved, entry.SolvedAt });
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
