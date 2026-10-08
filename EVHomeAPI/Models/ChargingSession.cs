namespace EVHomeAPI.Models;

public enum SessionStatus
{
    Pending   = 0,   // RemoteStart sent, waiting for StartTransaction
    Active    = 1,   // charging (transaction running)
    Stopping  = 2,   // RemoteStop sent, waiting for StopTransaction
    Completed = 3,
    Cancelled = 4,   // start rejected / timed out
}

public enum SessionInitiator
{
    App     = 0,   // started from EVHomeApp (RemoteStartTransaction)
    Charger = 1,   // started at the charger (button, RFID, plug & charge)
}

public enum SessionStopReason
{
    UserInitiated    = 0,   // stop pressed in the app
    ChargerInitiated = 1,   // stopped at the charger / by the car / unplugged
}

/// <summary>A charging session on a home station. No cost/billing — energy only.</summary>
public class ChargingSession
{
    public int     Id          { get; set; }
    public int     StationId   { get; set; }
    public int     UserId      { get; set; }
    public int     ConnectorId { get; set; } = 1;

    public SessionStatus    Status      { get; set; } = SessionStatus.Pending;
    public SessionInitiator InitiatedBy { get; set; }
    public SessionStopReason? StopReason { get; set; }

    /// <summary>Correlates command.remote.start with charger.remote.start.response.</summary>
    public Guid?   TrackingId        { get; set; }
    public int?    OcppTransactionId { get; set; }

    public DateTime  CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }   // transaction confirmed
    public DateTime? EndedAt   { get; set; }
    public DateTime? StopRequestedAt { get; set; }   // RemoteStop sent (watchdog reverts stuck Stopping)

    public decimal? MeterStartWh  { get; set; }
    public decimal? MeterStopWh   { get; set; }
    public decimal  EnergyKwh     { get; set; }
    public double?  CurrentPowerKw { get; set; }
    public decimal? Soc           { get; set; }

    public Station Station { get; set; } = null!;
    public User    User    { get; set; } = null!;
    public ICollection<SessionMeterReading> MeterReadings { get; set; } = [];

    public bool IsOpen => Status is SessionStatus.Pending or SessionStatus.Active or SessionStatus.Stopping;
}
