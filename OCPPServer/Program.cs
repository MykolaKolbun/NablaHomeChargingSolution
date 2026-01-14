//using Microsoft;
using Newtonsoft.Json;
using OCPP_RD.OCPP1._6_Models;
using OCPPServer;
using OCPPServer.DataBase.DBModels;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. DATABASE CONFIGURATION
// For this example, we use an In-Memory database so it works immediately.
builder.Services.AddDbContext<ChargingDBContext>(options =>
    options.UseInMemoryDatabase("ChargingStations"));

var app = builder.Build();

// 2. MIDDLEWARE
app.UseWebSockets();

// 3. WEBSOCKET ENDPOINT
app.Map("/ws/{stationId}", async (HttpContext context, string stationId, ChargingDBContext db) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        Console.WriteLine($"Station {stationId} connected.");

        await HandleStationCommunication(webSocket, db, stationId);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

// 4. MESSAGE HANDLING LOGIC
async Task HandleStationCommunication(WebSocket socket, ChargingDBContext db, string stationId)
{
    var buffer = new byte[1024 * 4];

    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType == WebSocketMessageType.Text)
        {
            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);

            // Try to parse the BootNotificationRequest
            try
            {
                var bootReq = JsonConvert.DeserializeObject<BootNotificationRequest>(json);

                // SAVE TO DATABASE
                var station = new Connector
                {
                    ChargePointSerialNumber = bootReq.ChargePointSerialNumber ?? stationId,
                    ChargePointVendor = bootReq.ChargePointVendor,
                    ChargePointModel = bootReq.ChargePointModel,
                    LastSeen = DateTime.UtcNow
                };

                db.ChargePoints.Add(station);
                await db.SaveChangesAsync();

                // SEND RESPONSE
                var response = new { status = "Accepted", currentTime = DateTime.UtcNow, interval = 300 };
                var responseJson = JsonConvert.SerializeObject(response);
                await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(responseJson)),
                    WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing message: " + ex.Message);
            }
        }
    }
}

app.Run();