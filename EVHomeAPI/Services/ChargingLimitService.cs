using EVHomeAPI.Data;
using EVHomeAPI.Models;
using EVHomeAPI.Ocpp;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Services;

/// <summary>
/// Pushes a station's effective current limit to the charger.
///
/// Safety rule: the charger always carries a profile ≤ the installation maximum.
/// CurrentLimitA = null means "at the installation maximum" (MaxCurrentA) — it never clears
/// the profile, because the charger's own default (e.g. 32 A) may exceed what the house supply allows.
/// </summary>
public class ChargingLimitService(
    AppDbContext db,
    IOcppCommandPublisher commands,
    TimeProvider clock,
    ILogger<ChargingLimitService> logger)
{
    public static decimal EffectiveLimit(Station s) => s.CurrentLimitA ?? s.MaxCurrentA;

    /// <summary>
    /// Marks the limit Pending and publishes command.charging.limit with the effective limit
    /// (plus the running transaction, so it applies immediately). Saves the station.
    /// Returns false when the broker is unavailable (LimitStatus = Error).
    /// </summary>
    public async Task<bool> ApplyAsync(Station station, CancellationToken ct = default)
    {
        var runningTx = await db.Sessions
            .Where(s => s.StationId == station.Id && (s.Status == SessionStatus.Active || s.Status == SessionStatus.Stopping))
            .Select(s => s.OcppTransactionId)
            .FirstOrDefaultAsync(ct);

        station.LimitStatus    = "Pending";
        station.LimitUpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        try
        {
            await commands.SetChargingLimitAsync(new ChargingLimitCommand(station.OcppId, (double)EffectiveLimit(station), runningTx), ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Charging limit publish failed for station {OcppId}", station.OcppId);
            station.LimitStatus = "Error";
            await db.SaveChangesAsync(ct);
            return false;
        }
    }
}
