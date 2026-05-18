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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChargingDBContext>();
    db.Database.Migrate();
}

// 2. MIDDLEWARE
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
    await Communicator.SendStopCharging(socket);
    return Results.Ok(new
    {
        stationId,
        status = "SENT"
    });
});

// RUN THE APPLICATION
app.Run();