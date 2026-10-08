namespace EVHomeAPI.Models;

/// <summary>Downsampled power/SoC samples for the session chart (one per ~30 s).</summary>
public class SessionMeterReading
{
    public long     Id             { get; set; }
    public int      SessionId      { get; set; }
    public int      ElapsedSec     { get; set; }
    public double?  CurrentPowerKw { get; set; }
    public decimal? Soc            { get; set; }
    public decimal  EnergyKwh      { get; set; }
    public DateTime RecordedAt     { get; set; } = DateTime.UtcNow;

    public ChargingSession Session { get; set; } = null!;
}
