namespace EVHomeAPI.Models;

/// <summary>
/// A home charger known to Nabla Home. Created by an admin together with a one-time
/// claim code; a user becomes its Owner by presenting OcppId + claim code.
///
/// Live state (online, connector status, meter) lives in EVOCPP and arrives via
/// RabbitMQ — it is not stored here (stage 1 step 6).
/// </summary>
public class Station
{
    public int      Id        { get; set; }

    /// <summary>Charge point identity as used in the OCPP URL (/ws/{OcppId}). Unique.</summary>
    public string   OcppId    { get; set; } = string.Empty;

    public string   Name      { get; set; } = string.Empty;

    /// <summary>BCrypt hash of the claim code. Null once the station has been claimed.</summary>
    public string?  ClaimCodeHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClaimedAt { get; set; }

    public ICollection<StationAccess> Accesses { get; set; } = [];
}
