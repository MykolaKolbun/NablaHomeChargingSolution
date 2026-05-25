public class RemoteStartCommand
{
    public string OcppId { get; set; } = null!;
    public int ConnectorId { get; set; } = 1;
    public string IdTag { get; set; } = null!;
}

public class RemoteStopCommand
{
    public string OcppId { get; set; } = null!;
    public int? TransactionId { get; set; }
}