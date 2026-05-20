using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCPP_RD.OCPP1._6_Models;
using OCPPServer;
using OCPPServer.ChargingStationInterface;
using OCPPServer.Data;
using OCPPServer.DataBase.DBModels;
using OCPPServer.OCPP1._6_Models;
using System;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 1. DATABASE CONFIGURATION
builder.Services.AddDbContext<ChargingDBContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<IOcppCommandService, OcppCommandService>();

builder.Services.AddCors(opt => opt.AddDefaultPolicy(policy =>
    policy.WithOrigins("https://admin.alternatiview.com.ua")
          .AllowAnyHeader()
          .AllowAnyMethod()));

// Wire up SignalR callback to EVChargingApi
Communicator.Configure(
    builder.Configuration["EVChargingApi:BaseUrl"] ?? "",
    builder.Configuration["InternalApiKey"]        ?? "");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChargingDBContext>();
    db.Database.Migrate();
}

// 2. MIDDLEWARE
app.UseCors();
app.UseWebSockets();

// 3. WEBSOCKET ENDPOINT
app.Map("/ws/{stationId}", async (HttpContext context, string stationId, ChargingDBContext db) =>
{
    stationId = stationId.ToLowerInvariant(); // normalise so "U030" == "u030"
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    ChargingStationConnections.Add(stationId, webSocket);

    OcppLog.Write($"Station {stationId} connected.");

    var buffer = new byte[1024 * 16];

    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            // Accumulate fragments until EndOfMessage so large frames aren't truncated
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
                OcppLog.Write($"[OCPP ← IN ] {stationId}: {json}");
                await Communicator.RouteOcppMessage(webSocket, stationId, json, db);
            }
        }
    }
    finally
    {
        ChargingStationConnections.Remove(stationId);
        OcppLog.Write($"Station {stationId} disconnected.");
        // Tell the app immediately so badges flip to "offline"
        Communicator.PushDisconnect(stationId);
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
// 4. REST API ENDPOINTS
app.MapPost("/api/chargers/{stationId}/start-session", async (string stationId) =>
{
    var socket = ChargingStationConnections.Get(stationId);

    if (socket == null || socket.State != WebSocketState.Open)
        return Results.NotFound("Charging station not connected");

    await Communicator.SendStartCharging(socket);
    
    return Results.Ok(new
    {
        stationId,
        status = "SENT"
    });
});
app.MapPost("/api/chargers/{stationId}/stop-session", async (string stationId) =>
{
    var socket = ChargingStationConnections.Get(stationId);
    if (socket == null || socket.State != WebSocketState.Open)
        return Results.NotFound("Charging station not connected");
    await Communicator.SendStopCharging(socket, stationId);
    return Results.Ok(new
    {
        stationId,
        status = "SENT"
    });
});

// 5. LIVE STATUS ENDPOINT (called internally by EVChargingApi)
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

// 5b. METER VALUE ENDPOINT (called internally by EVChargingApi)
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
        meterStartWh = charger.MeterStart,
        meterValueWh = charger.MeterValue,
        meterStopWh  = charger.MeterStop,
        energyKwh    = sessionKwh,          // kWh consumed in current/last session
        lastUpdate   = charger.LastUpdate
    });
});

// 6. ADMIN ENDPOINTS
app.MapGet("/api/admin/chargers", async (ChargingDBContext db) =>
{
    var chargers = await db.Connectors
        .OrderBy(c => c.OcppId)
        .Select(c => new
        {
            c.OcppId,
            c.Name,
            c.Address,
            c.Latitude,
            c.Longitude,
            c.IsFastCharger,
            c.ShowOnMap,
            c.MaxPowerKw,
            c.NumberOfConnectors,
            c.Vendor,
            c.ChargePointModel,
            c.FirmwareVersion,
            status = c.Status.ToString(),
            c.MeterStart,
            c.MeterValue,
            c.MeterStop,
            c.LastUpdate,
            c.CreatedAt,
            isConnected = ChargingStationConnections.Get(c.OcppId) != null
        })
        .ToListAsync();

    return Results.Ok(chargers);
});

app.MapPut("/api/admin/chargers/{ocppId}", async (string ocppId, ChargerInfoUpdate update, ChargingDBContext db) =>
{
    var charger = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == ocppId);
    if (charger is null) return Results.NotFound($"Charger '{ocppId}' not found.");

    charger.Name = update.Name;
    charger.Address = update.Address;
    charger.Latitude = update.Latitude;
    charger.Longitude = update.Longitude;
    charger.IsFastCharger = update.IsFastCharger;
    charger.ShowOnMap = update.ShowOnMap;
    charger.MaxPowerKw = update.MaxPowerKw;
    charger.NumberOfConnectors = update.NumberOfConnectors;
    await db.SaveChangesAsync();

    return Results.Ok(new { charger.OcppId, charger.Name, charger.Address, charger.Latitude, charger.Longitude, charger.IsFastCharger, charger.ShowOnMap, charger.MaxPowerKw });
});

app.MapDelete("/api/admin/chargers/{ocppId}", async (string ocppId, ChargingDBContext db) =>
{
    var charger = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == ocppId);
    if (charger is null) return Results.NotFound($"Charger '{ocppId}' not found.");
    db.Connectors.Remove(charger);
    await db.SaveChangesAsync();
    return Results.Ok(new { deleted = ocppId });
});

// One-shot endpoint: copies all good data from sourceId into targetId, then deletes sourceId.
// Usage: POST /api/admin/chargers/merge?sourceId=U030&targetId=u030
app.MapPost("/api/admin/chargers/merge", async (string sourceId, string targetId, ChargingDBContext db) =>
{
    var src = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == sourceId);
    var tgt = await db.Connectors.FirstOrDefaultAsync(c => c.OcppId == targetId);
    if (src is null) return Results.NotFound($"Source '{sourceId}' not found.");
    if (tgt is null) return Results.NotFound($"Target '{targetId}' not found.");

    // Copy human-visible / configuration fields from src → tgt (keep tgt's OCPP fields)
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

// RUN THE APPLICATION
app.Run();

record ChargerInfoUpdate(string? Name, string? Address, double? Latitude, double? Longitude, bool IsFastCharger, bool ShowOnMap, double? MaxPowerKw, int NumberOfConnectors);