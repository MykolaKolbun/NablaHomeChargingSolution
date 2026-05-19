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
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    ChargingStationConnections.Add(stationId, webSocket);

    Console.WriteLine($"Station {stationId} connected.");

    var buffer = new byte[1024 * 4];

    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await Communicator.RouteOcppMessage(webSocket, stationId, json, db);
            }
        }
    }
    finally
    {
        ChargingStationConnections.Remove(stationId);
        Console.WriteLine($"Station {stationId} disconnected.");
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

// 5. METER VALUE ENDPOINT (called internally by EVChargingApi)
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

// RUN THE APPLICATION
app.Run();

record ChargerInfoUpdate(string? Name, string? Address, double? Latitude, double? Longitude, bool IsFastCharger, bool ShowOnMap, double? MaxPowerKw, int NumberOfConnectors);