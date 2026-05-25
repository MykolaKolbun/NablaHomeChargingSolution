using Newtonsoft.Json.Linq;
using OCPP_RD.OCPP1._6_Models;
using OCPPServer.Data;
using System.Net.WebSockets;

namespace OCPPServer.ChargingStationInterface
{
    public interface ICommunicator
    {
        void PushDisconnect(string connectorId);

        Task RouteOcppMessage(WebSocket socket, string connectorId, string json, ChargingDBContext db);

        Task<JObject> SendStartCharging(WebSocket socket, string stationId, int connectorId, string idTag);

        Task<JObject> SendStopCharging(WebSocket socket, string stationId, int? transactionId);

        Task<JObject> SendTriggerMessage(WebSocket socket, string requestedMessage, int? connectorId);

        Task<JObject> SendStatusNotificationRequest(WebSocket socket, int? connectorId);

        Task<JObject> SendGetDiagnostics(WebSocket socket, GetDiagnosticsRequest request);

        Task<JObject> SendCallAndWaitAsync(WebSocket socket, string action, JObject payload, TimeSpan timeout);
    }
}
