namespace OCPPServer.ChargingStationInterface
{
    public class OcppCommandService : IOcppCommandService
    {
        public async Task RemoteStartAsync(string stationId, int connectorId, string idTag)
        {
            // Builds OCPP message + sends via WebSocket
        }

        public Task RemoteStopAsync(string stationId, int transactionId)
        {
            throw new NotImplementedException();
        }

        public Task ResetAsync(string stationId, string type)
        {
            throw new NotImplementedException();
        }
    }
}
