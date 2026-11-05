using Microsoft.EntityFrameworkCore;
using OCPPServer.Data;
using OCPPServer.DataBase.DBModels;

namespace OCPPServer;

/// <summary>
/// Persistence of per-connector data (<see cref="PlugConnector"/>) shared by the OCPP 1.6 and 2.x handlers.
/// Each method saves its own changes.
/// </summary>
public static class ConnectorStore
{
    public static async Task<PlugConnector> GetOrCreateAsync(ChargingDBContext db, string ocppId, int connectorId)
    {
        var row = await db.PlugConnectors.FirstOrDefaultAsync(c => c.OcppId == ocppId && c.ConnectorId == connectorId);
        if (row != null) return row;

        row = new PlugConnector { OcppId = ocppId, ConnectorId = connectorId };
        db.PlugConnectors.Add(row);
        return row;
    }

    public static async Task SetStatusAsync(ChargingDBContext db, string ocppId, int connectorId, string status)
    {
        var row = await GetOrCreateAsync(db, ocppId, connectorId);
        row.Status           = status;
        row.LastStatusUpdate = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public static async Task StartTransactionAsync(ChargingDBContext db, string ocppId, int connectorId,
        int transactionId, decimal? meterStartWh)
    {
        var row = await GetOrCreateAsync(db, ocppId, connectorId);
        row.TransactionId = transactionId;
        row.MeterStartWh  = meterStartWh;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Finds the connector that runs <paramref name="transactionId"/> — used when the in-memory state
    /// was lost (server restart). Falls back to the only open session without a recorded transaction
    /// (rows migrated from the old per-station Plugs.MeterStartWh).
    /// </summary>
    public static async Task<PlugConnector?> FindByTransactionAsync(ChargingDBContext db, string ocppId, int transactionId)
    {
        var row = await db.PlugConnectors.FirstOrDefaultAsync(c => c.OcppId == ocppId && c.TransactionId == transactionId);
        if (row != null) return row;

        var legacy = await db.PlugConnectors
            .Where(c => c.OcppId == ocppId && c.TransactionId == null && c.MeterStartWh != null)
            .ToListAsync();
        return legacy.Count == 1 ? legacy[0] : null;
    }

    public static async Task<decimal?> GetMeterStartAsync(ChargingDBContext db, string ocppId, int connectorId)
        => (await db.PlugConnectors.AsNoTracking()
                .FirstOrDefaultAsync(c => c.OcppId == ocppId && c.ConnectorId == connectorId))?.MeterStartWh;

    public static async Task EndTransactionAsync(ChargingDBContext db, PlugConnector row)
    {
        row.TransactionId = null;
        row.MeterStartWh  = null;
        await db.SaveChangesAsync();
    }

    /// <summary>Highest transaction ID still open in the DB (0 if none) — seeds the ID counter at startup.</summary>
    public static async Task<int> MaxOpenTransactionIdAsync(ChargingDBContext db)
        => await db.PlugConnectors.Where(c => c.TransactionId != null).MaxAsync(c => (int?)c.TransactionId) ?? 0;
}
