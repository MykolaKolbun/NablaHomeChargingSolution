using EVHomeAPI.Data;
using EVHomeAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Services;

/// <summary>Per-connector live status (<see cref="StationConnector"/>). Callers save.</summary>
public static class ConnectorStates
{
    /// <summary>Records the connector's status (row created on first use; tracked, not saved).</summary>
    public static async Task SetAsync(AppDbContext db, int stationId, int connectorId, string status, DateTime now,
        CancellationToken ct = default)
    {
        var row = db.StationConnectors.Local.FirstOrDefault(c => c.StationId == stationId && c.ConnectorId == connectorId)
                  ?? await db.StationConnectors.FirstOrDefaultAsync(c => c.StationId == stationId && c.ConnectorId == connectorId, ct);
        if (row is null)
        {
            row = new StationConnector { StationId = stationId, ConnectorId = connectorId };
            db.StationConnectors.Add(row);
        }
        row.Status       = status;
        row.LastStatusAt = now;
    }

    /// <summary>Last reported status of a connector; connector 1 falls back to Station.ConnectorStatus.</summary>
    public static async Task<(string? Status, DateTime? At)> GetAsync(AppDbContext db, Station station, int connectorId,
        CancellationToken ct = default)
    {
        var row = db.StationConnectors.Local.FirstOrDefault(c => c.StationId == station.Id && c.ConnectorId == connectorId)
                  ?? await db.StationConnectors.AsNoTracking()
                      .FirstOrDefaultAsync(c => c.StationId == station.Id && c.ConnectorId == connectorId, ct);
        if (row is not null) return (row.Status, row.LastStatusAt);
        return connectorId == 1 ? (station.ConnectorStatus, station.LastStatusAt) : (null, null);
    }
}
