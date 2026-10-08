namespace EVHomeAPI.Models;

/// <summary>
/// A home charger known to Nabla Home. Created by an admin together with a one-time
/// claim code; a user becomes its Owner by presenting OcppId + claim code.
///
/// Live state (online, connector status) is mirrored from EVOCPP events
/// (charger.status.changed); EVOCPP remains the source of truth.
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

    // ── Live state mirrored from EVOCPP ──────────────────────────────────────
    public bool      IsOnline        { get; set; }
    /// <summary>Connector 1 status as reported by the charger (OCPP 1.6 vocabulary), e.g. Available, Preparing, Charging.</summary>
    public string?   ConnectorStatus { get; set; }
    public DateTime? LastStatusAt    { get; set; }

    public ICollection<StationAccess>   Accesses { get; set; } = [];
    public ICollection<ChargingSession> Sessions { get; set; } = [];
}
