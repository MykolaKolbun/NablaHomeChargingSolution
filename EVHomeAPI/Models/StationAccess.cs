namespace EVHomeAPI.Models;

public enum StationRole
{
    Owner  = 0,   // claimed the station; can manage members and settings
    Member = 1,   // family member: can start/stop and view (stage 2)
}

/// <summary>Who may use a station. Composite key (StationId, UserId).</summary>
public class StationAccess
{
    public int         StationId { get; set; }
    public int         UserId    { get; set; }
    public StationRole Role      { get; set; }
    public DateTime    CreatedAt { get; set; } = DateTime.UtcNow;

    public Station Station { get; set; } = null!;
    public User    User    { get; set; } = null!;
}
