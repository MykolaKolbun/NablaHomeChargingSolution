namespace OCPPServer.ChargingStationInterface
{
    public interface IOcppCommandService
    {
        Task RemoteStartAsync(string stationId, int connectorId, string idTag);
        Task RemoteStopAsync(string stationId, int transactionId);
        Task ResetAsync(string stationId, string type);
    }
}
