using Newtonsoft.Json.Linq;
using OCPP_RD.OCPP1._6_Models;
using OCPPServer.ChargingStationInterface;
using OCPPServer.Data;
using OCPPServer.OCPP1._6_Models;
using OCPPServer.OCPP2._1_Models;
using System.Net.WebSockets;

namespace OCPPServer;

/// <summary>
/// Dispatches OCPP messages and outbound commands to the correct version-specific
/// handler based on the protocol negotiated at WebSocket handshake time.
/// Registered as ICommunicator in DI, wraps Communicator (1.6) and Ocpp21Communicator (2.1).
/// </summary>
public sealed class OcppRouter : ICommunicator
{
    private readonly Communicator _ocpp16;
    private readonly Ocpp21Communicator _ocpp21;

    public OcppRouter(Communicator ocpp16, Ocpp21Communicator ocpp21)
    {
        _ocpp16 = ocpp16;
        _ocpp21 = ocpp21;
    }

    private static bool Is21(string stationId)
    {
        var p = ChargingStationConnections.GetProtocol(stationId);
        return p is "ocpp2.1" or "ocpp2.0.1" or "ocpp2.0";
    }

    public void PushDisconnect(string stationId)
        => _ocpp16.PushDisconnect(stationId);

    public Task RouteOcppMessage(WebSocket socket, string stationId, string json, ChargingDBContext db)
        => Is21(stationId)
            ? _ocpp21.RouteOcppMessage(socket, stationId, json, db)
            : _ocpp16.RouteOcppMessage(socket, stationId, json, db);

    public Task<JObject> SendStartCharging(WebSocket socket, string stationId, int connectorId, string idTag)
        => Is21(stationId)
            ? _ocpp21.SendStartCharging(socket, stationId, connectorId, idTag)
            : _ocpp16.SendStartCharging(socket, stationId, connectorId, idTag);

    public Task<JObject> SendStopCharging(WebSocket socket, string stationId, int? transactionId)
        => Is21(stationId)
            ? _ocpp21.SendStopCharging(socket, stationId, transactionId)
            : _ocpp16.SendStopCharging(socket, stationId, transactionId);

    public Task<JObject> SendTriggerMessage(WebSocket socket, string requestedMessage, int? connectorId)
        => _ocpp16.SendTriggerMessage(socket, requestedMessage, connectorId);

    public Task<JObject> SendStatusNotificationRequest(WebSocket socket, int? connectorId)
        => _ocpp16.SendStatusNotificationRequest(socket, connectorId);

    public Task<JObject> SendGetDiagnostics(WebSocket socket, GetDiagnosticsRequest request)
        => _ocpp16.SendGetDiagnostics(socket, request);

    public Task<JObject> SendCallAndWaitAsync(WebSocket socket, string action, JObject payload, TimeSpan timeout)
        => _ocpp16.SendCallAndWaitAsync(socket, action, payload, timeout);
}
