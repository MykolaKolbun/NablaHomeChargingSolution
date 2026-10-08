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
                await RunOnceAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(),
                                   scope.ServiceProvider.GetRequiredService<INotifier>(), clock, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Stale session watchdog failed");
            }
        }
    }

    public static async Task RunOnceAsync(AppDbContext db, INotifier notifier, TimeProvider clock, CancellationToken ct = default)
    {
        var now    = clock.GetUtcNow().UtcDateTime;
        var cutoff = now - StaleAfter;

        var pending = await db.Sessions
            .Where(s => s.Status == SessionStatus.Pending && s.CreatedAt < cutoff)
            .ToListAsync(ct);
        foreach (var s in pending)
        {
            s.Status  = SessionStatus.Cancelled;
            s.EndedAt = now;
        }

        var stopping = await db.Sessions
            .Where(s => s.Status == SessionStatus.Stopping && s.StopRequestedAt < cutoff)
            .ToListAsync(ct);
        foreach (var s in stopping)
            s.Status = SessionStatus.Active;

        if (pending.Count == 0 && stopping.Count == 0) return;
        await db.SaveChangesAsync(ct);

        foreach (var s in pending)
            await notifier.SessionStartFailed(new SessionStartFailedMsg(s.StationId, s.Id, "Timeout"));
        foreach (var s in stopping)
            await notifier.SessionStopFailed(new SessionStopFailedMsg(s.StationId, s.Id, "Timeout"));
    }
}
