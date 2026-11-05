namespace EVHomeAPI.Models;

/// <summary>
/// Live state of one connector of a station, mirrored from EVOCPP charger.status.changed.
/// A station is one OCPP id; most home chargers have connector 1 only, a DC charger such as
/// Wallbox Supernova has 1 and 2 under the same id. Created on the first status event for the
/// connector. Station.ConnectorStatus keeps mirroring connector 1 for older clients.
/// </summary>
public class StationConnector
{
    public int       StationId    { get; set; }
    /// <summary>OCPP connectorId (OCPP 2.x: EVSE id), ≥ 1.</summary>
    public int       ConnectorId  { get; set; }
    /// <summary>OCPP 1.6 status as reported (Available, Preparing, Charging, …).</summary>
    public string?   Status       { get; set; }
    public DateTime? LastStatusAt { get; set; }

    public Station Station { get; set; } = null!;
}
