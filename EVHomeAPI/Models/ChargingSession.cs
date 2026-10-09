namespace EVHomeAPI.Models;

public enum SessionStatus
{
    Pending   = 0,   // RemoteStart sent, waiting for StartTransaction
    Active    = 1,   // charging (transaction running)
    Stopping  = 2,   // RemoteStop sent, waiting for StopTransaction
    Completed = 3,
    Cancelled = 4,   // start rejected / timed out
    Paused    = 5,   // charger lost power / went offline mid-session; resumes automatically (SessionResumeService)
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
    PowerLoss        = 2,   // charger lost power and the session could not be resumed
}

/// <summary>
/// A charging session on a home station. No cost/billing — energy only.
/// One session may span several OCPP transactions: after a power loss the charger closes
/// its transaction and EVHomeAPI starts a new one (RemoteStart) under the same session.
/// </summary>
public class ChargingSession
{
    public int     Id          { get; set; }
    public int     StationId   { get; set; }
    public int     UserId      { get; set; }
    public int     ConnectorId { get; set; } = 1;

    public SessionStatus    Status      { get; set; } = SessionStatus.Pending;
    public SessionInitiator InitiatedBy { get; set; }
    public SessionStopReason? StopReason { get; set; }

    /// <summary>Correlates command.remote.start with charger.remote.start.response (new value per resume attempt).</summary>
    public Guid?   TrackingId        { get; set; }
    /// <summary>Current transaction; null while Paused after the charger closed the previous one.</summary>
    public int?    OcppTransactionId { get; set; }

    public DateTime  CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }   // first transaction confirmed
    public DateTime? EndedAt   { get; set; }
    public DateTime? StopRequestedAt { get; set; }   // RemoteStop sent (watchdog reverts stuck Stopping)

    public DateTime? PausedAt          { get; set; }   // when the current pause began
    public DateTime? ResumeRequestedAt { get; set; }   // last resume RemoteStart sent
    public int       ResumeAttempts    { get; set; }   // within the current pause

    /// <summary>Meter values of the CURRENT transaction.</summary>
    public decimal? MeterStartWh  { get; set; }
    public decimal? MeterStopWh   { get; set; }
    /// <summary>Energy of earlier transactions of this session (before a power loss).</summary>
    public decimal  CarriedEnergyKwh { get; set; }
    /// <summary>Whole session: CarriedEnergyKwh + current transaction.</summary>
    public decimal  EnergyKwh     { get; set; }
    public double?  CurrentPowerKw { get; set; }
    public decimal? Soc           { get; set; }

    public Station Station { get; set; } = null!;
    public User    User    { get; set; } = null!;
    public ICollection<SessionMeterReading> MeterReadings { get; set; } = [];

    public static readonly SessionStatus[] OpenStatuses =
        [SessionStatus.Pending, SessionStatus.Active, SessionStatus.Stopping, SessionStatus.Paused];

    public bool IsOpen => OpenStatuses.Contains(Status);

    /// <summary>Energy of the current transaction up to <paramref name="meterWh"/>, plus carried energy.</summary>
    public decimal EnergyAt(decimal meterWh) =>
        CarriedEnergyKwh + (MeterStartWh is { } start ? Math.Max(0, (meterWh - start) / 1000m) : 0);
}
