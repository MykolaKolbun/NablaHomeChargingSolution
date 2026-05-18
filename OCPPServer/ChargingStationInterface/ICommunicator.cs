using Newtonsoft.Json.Linq;
using OCPPServer.Data;
using System.Net.WebSockets;

namespace OCPPServer.ChargingStationInterface
{
    public interface ICommunicator
    {
        static abstract Task RouteOcppMessage(System.Net.WebSockets.WebSocket socket, string stationId, string json, Data.ChargingDBContext db);
        static abstract Task HandleBootNotification(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db);
        static abstract Task HandleHeartbeat(WebSocket socket, string messageId);
        static abstract Task HandleStatusNotification(WebSocket socket, string messageId, JObject payload, string stationId, ChargingDBContext db);
        static abstract Task SendCallError(WebSocket socket, string messageId, string errorCode, string errorDescription);
        static abstract Task SendStartCharging(WebSocket socket, string messageId, JObject payload);
        static abstract Task SendStopCharging(WebSocket socket, string messageId, JObject payload);
        static abstract Task SendCallResult(WebSocket socket, string messageId, JObject payload);
        static abstract Task SendAsync(WebSocket socket, JArray message);
    }
}
