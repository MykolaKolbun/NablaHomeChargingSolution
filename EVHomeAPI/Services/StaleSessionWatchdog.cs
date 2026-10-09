using EVHomeAPI.Data;
using EVHomeAPI.Hubs;
using EVHomeAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Services;

/// <summary>
/// EVOCPP answers RemoteStart/RemoteStop within ~60 s (Accepted/Rejected/Timeout).
/// If it is down or the response is lost, a session would hang forever:
///   Pending  &gt; StaleAfter → Cancelled + SessionStartFailed("Timeout")
///   Stopping &gt; StaleAfter → back to Active + SessionStopFailed("Timeout")  (user may retry)
///                           or, if the charger is offline (unpowered), Completed — the user
///                           wanted it stopped and nothing is charging
/// Also drives Paused sessions (resume retries, give-up) — see SessionResumeService.SweepAsync.
/// </summary>
public sealed class StaleSessionWatchdog(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<StaleSessionWatchdog> logger) : BackgroundService
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan Interval  = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await RunOnceAsync(scope.ServiceProvider, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Stale session watchdog failed");
            }
        }
    }

    public static async Task RunOnceAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db       = services.GetRequiredService<AppDbContext>();
        var notifier = services.GetRequiredService<INotifier>();
        var clock    = services.GetRequiredService<TimeProvider>();
        var now      = clock.GetUtcNow().UtcDateTime;
        var cutoff   = now - StaleAfter;

        var pending = await db.Sessions
            .Where(s => s.Status == SessionStatus.Pending && s.CreatedAt < cutoff)
            .ToListAsync(ct);
        foreach (var s in pending)
        {
            s.Status  = SessionStatus.Cancelled;
            s.EndedAt = now;
        }

        var stopping = await db.Sessions
            .Include(s => s.Station)
            .Where(s => s.Status == SessionStatus.Stopping && s.StopRequestedAt < cutoff)
            .ToListAsync(ct);
        var reverted  = stopping.Where(s => s.Station.IsOnline).ToList();
        var abandoned = stopping.Where(s => !s.Station.IsOnline).ToList();
        foreach (var s in reverted)
            s.Status = SessionStatus.Active;
        foreach (var s in abandoned)
        {
            s.Status         = SessionStatus.Completed;
            s.StopReason     = SessionStopReason.UserInitiated;
            s.EndedAt        = now;
            s.CurrentPowerKw = 0;
        }

        if (pending.Count + stopping.Count > 0)
        {
            await db.SaveChangesAsync(ct);

            foreach (var s in pending)
                await notifier.SessionStartFailed(new SessionStartFailedMsg(s.StationId, s.Id, "Timeout"));
            foreach (var s in reverted)
                await notifier.SessionStopFailed(new SessionStopFailedMsg(s.StationId, s.Id, "Timeout"));
            foreach (var s in abandoned)
                await notifier.SessionFinalized(new SessionFinalizedMsg(s.StationId, s.Id, s.EnergyKwh, null, s.StopReason.ToString()!));
        }

        await services.GetRequiredService<SessionResumeService>().SweepAsync(ct);
    }
}
