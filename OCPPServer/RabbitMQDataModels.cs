public class RemoteStartCommand
{
    public string OcppId { get; set; } = null!;
    public int ConnectorId { get; set; } = 1;
    public string IdTag { get; set; } = null!;

    /// <summary>
    /// Idempotency / tracking id forwarded by EVChargingApi (PR-004). The OCPP server
    /// does not use it for logic — it echoes it back in charger.remote.start.response
    /// so the API can correlate the response to the original command exactly.
    /// </summary>
    public Guid? TrackingId { get; set; }
}

public class RemoteStopCommand
{
    public string OcppId { get; set; } = null!;
    public int? TransactionId { get; set; }
}