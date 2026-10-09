using EVHomeAPI.Data;
using EVHomeAPI.Hubs;
using EVHomeAPI.Models;
using EVHomeAPI.Ocpp;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Services;

/// <summary>
/// Resume after power loss (architecture.md, stage 2). A session whose charger lost power is
/// Paused instead of Completed; when the charger is back and the car is still plugged in,
/// a new transaction is started under the same session (RemoteStart).
///
/// Observed on the Wallbox Copper SB: after the outage it boots, reports the connector as
/// Finishing (car plugged), then sends the queued StopTransaction(reason PowerLoss), and
/// accepts RemoteStart from Finishing. It never resumes on its own.
///
/// Only RemoteStart is used here. Resetting the car's charge state (DIY charger: IEC 61851
/// B1→B2 PWM pause, never state A) is a separate, opt-in escalation — not implemented.
/// </summary>
public class SessionResumeService(
    AppDbContext db,
    IOcppCommandPublisher commands,
    INotifier notifier,
    TimeProvider clock,
    ILogger<SessionResumeService> logger)
{
    public const int MaxAttempts = 3;
    /// <summary>Spacing between resume attempts (EVOCPP answers RemoteStart within ~60 s).</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(90);
    /// <summary>A pause longer than this ends the session (the outage outlived any useful resume).</summary>
    public static readonly TimeSpan MaxPause   = TimeSpan.FromHours(24);
    /// <summary>Paused with a transaction but no StopTransaction this long after reconnect → treat it as lost.</summary>
    public static readonly TimeSpan LostTransactionAfter = TimeSpan.FromMinutes(3);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Connector states that mean "car still plugged in, not charging".</summary>
    public static bool CarPlugged(string? status) =>
        status is "Preparing" or "Finishing" or "SuspendedEV" or "SuspendedEVSE";

    /// <summary>Connector states that mean the transaction is still running on the charger.</summary>
    public static bool TransactionRunning(string? status) =>
        status is "Charging" or "SuspendedEV" or "SuspendedEVSE";

    /// <summary>Active → Paused (charger went silent). The transaction is kept: it may still be running.</summary>
    public async Task PauseAsync(ChargingSession s, string reason, CancellationToken ct = default)
    {
        s.Status         = SessionStatus.Paused;
        s.PausedAt     ??= Now;
        s.CurrentPowerKw = 0;
        await db.SaveChangesAsync(ct);
        await notifier.SessionPaused(new SessionPausedMsg(s.StationId, s.Id, reason));
    }

    /// <summary>
    /// The charger closed the transaction (or it is considered lost): bank its energy, keep the
    /// session open as Paused, then try to resume. meterStopWh null = keep the last metered energy.
    /// </summary>
    public async Task EndTransactionAndPauseAsync(Station station, ChargingSession s, decimal? meterStopWh, CancellationToken ct = default)
    {
        var wasPaused = s.Status == SessionStatus.Paused;
        if (meterStopWh is not null) s.EnergyKwh = s.EnergyAt(meterStopWh.Value);
        s.CarriedEnergyKwh  = s.EnergyKwh;
        s.MeterStopWh       = meterStopWh ?? s.MeterStopWh;
        s.MeterStartWh      = null;
        s.OcppTransactionId = null;
        s.TrackingId        = null;
        s.ResumeAttempts    = 0;
        s.ResumeRequestedAt = null;
        s.Status            = SessionStatus.Paused;
        s.PausedAt        ??= Now;
        s.CurrentPowerKw    = 0;
        await db.SaveChangesAsync(ct);

        if (!wasPaused)
            await notifier.SessionPaused(new SessionPausedMsg(s.StationId, s.Id, "PowerLoss"));
        await EvaluateAsync(station, s, ct);
    }

    /// <summary>
    /// Decides what a Paused session without a transaction does next: resume (RemoteStart),
    /// wait (charger offline / attempt in flight / connector state unclear) or end (unplugged,
    /// attempts exhausted). Safe to call repeatedly (status events, watchdog).
    /// </summary>
    public async Task EvaluateAsync(Station station, ChargingSession s, CancellationToken ct = default)
    {
        if (s.Status != SessionStatus.Paused || s.OcppTransactionId is not null) return;
        if (!station.IsOnline) return;

        if (station.ConnectorStatus == "Available")
        {
            await GiveUpAsync(s, "car unplugged", ct);
            return;
        }
        if (!CarPlugged(station.ConnectorStatus)) return;   // Faulted / Unavailable / unknown — wait

        if (s.ResumeRequestedAt is { } last && Now - last < RetryAfter) return;   // attempt in flight
        if (s.ResumeAttempts >= MaxAttempts)
        {
            await GiveUpAsync(s, $"{MaxAttempts} resume attempts failed", ct);
            return;
        }

        s.TrackingId        = Guid.NewGuid();
        s.ResumeAttempts   += 1;
        s.ResumeRequestedAt = Now;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Station {OcppId}: resuming session {SessionId} after power loss (attempt {Attempt})",
            station.OcppId, s.Id, s.ResumeAttempts);
        try
        {
            await commands.RemoteStartAsync(new RemoteStartCommand(station.OcppId, s.ConnectorId, $"U{s.UserId}", s.TrackingId.Value), ct);
        }
        catch (Exception ex)
        {
            // Broker down: the watchdog retries after RetryAfter.
            logger.LogError(ex, "Resume RemoteStart publish failed for station {OcppId}", station.OcppId);
        }
    }

    /// <summary>RemoteStart for a resume was rejected / timed out: allow the next attempt (or give up).</summary>
    public async Task OnResumeRejectedAsync(Station station, ChargingSession s, string status, CancellationToken ct = default)
    {
        logger.LogWarning("Station {OcppId}: resume attempt {Attempt} for session {SessionId} → {Status}",
            station.OcppId, s.ResumeAttempts, s.Id, status);
        s.TrackingId = null;
        await db.SaveChangesAsync(ct);
        if (s.ResumeAttempts >= MaxAttempts)
            await GiveUpAsync(s, $"resume {status}", ct);
    }

    /// <summary>Ends a Paused session: energy as banked, end time = when the power went.</summary>
    public async Task GiveUpAsync(ChargingSession s, string why, CancellationToken ct = default)
    {
        logger.LogInformation("Session {SessionId}: not resumed ({Why})", s.Id, why);
        s.Status         = SessionStatus.Completed;
        s.StopReason     = SessionStopReason.PowerLoss;
        s.EndedAt        = s.PausedAt ?? Now;
        s.CurrentPowerKw = 0;
        await db.SaveChangesAsync(ct);
        await notifier.SessionFinalized(new SessionFinalizedMsg(s.StationId, s.Id, s.EnergyKwh, null, s.StopReason.ToString()!));
    }

    /// <summary>Watchdog pass over all Paused sessions.</summary>
    public async Task SweepAsync(CancellationToken ct = default)
    {
        var paused = await db.Sessions
            .Include(s => s.Station)
            .Where(s => s.Status == SessionStatus.Paused)
            .ToListAsync(ct);

        foreach (var s in paused)
        {
            if (s.PausedAt is { } at && Now - at > MaxPause)
            {
                await GiveUpAsync(s, "pause too long", ct);
                continue;
            }

            // Charger back, but the StopTransaction for the old transaction never came and the
            // connector says it is not charging → the transaction is gone; bank it and resume.
            if (s.OcppTransactionId is not null && s.Station.IsOnline &&
                s.Station.LastStatusAt is { } seen && Now - seen > LostTransactionAfter &&
                !TransactionRunning(s.Station.ConnectorStatus))
            {
                logger.LogWarning("Station {OcppId}: transaction {TxId} of paused session {SessionId} lost — resuming",
                    s.Station.OcppId, s.OcppTransactionId, s.Id);
                await EndTransactionAndPauseAsync(s.Station, s, meterStopWh: null, ct);
                continue;
            }

            await EvaluateAsync(s.Station, s, ct);
        }
    }
}
