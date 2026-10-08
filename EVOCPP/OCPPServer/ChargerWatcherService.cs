using Microsoft.EntityFrameworkCore;
using OCPPServer.Data;
using OCPPServer.Tracing;

namespace OCPPServer;

/// <summary>
/// Background service that detects silently dead charger connections.
///
/// Some chargers drop the TCP connection without sending a WebSocket Close frame,
/// leaving the server holding an open-looking socket that will never receive another
/// message. The standard WebSocket keep-alive (Ping/Pong) is unreliable across NAT
/// boxes and carrier-grade firewalls commonly used at charging sites.
///
/// This service instead tracks <see cref="ConnectorState.LastMessageAt"/> — updated
/// on every incoming OCPP message — and aborts sockets that have been silent for
/// longer than <c>ChargerWatcher:InactivityThresholdSeconds</c> (default 180 s).
///
/// Abort mechanics:
///   <see cref="System.Net.WebSockets.WebSocket.Abort"/> causes the pending
///   <c>ReceiveAsync</c> in Program.cs to throw, which exits the receive loop and
///   hits the <c>finally</c> block. That block calls <c>Remove()</c> and
///   <c>PushDisconnect()</c> exactly once — no duplicated RabbitMQ events.
///   This service only handles the DB update (IsOnline = false) before aborting.
///
/// Configuration (appsettings.json / environment variables):
/// <code>
///   "ChargerWatcher": {
///     "CheckIntervalSeconds":       30,
///     "InactivityThresholdSeconds": 180,
///     "HeartbeatTimeoutSeconds":    300
///   }
/// </code>
/// </summary>
public sealed class ChargerWatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITracingService      _tracer;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _inactivityThreshold;
    private readonly TimeSpan _heartbeatTimeout;

    public ChargerWatcherService(
        IServiceScopeFactory scopeFactory,
        ITracingService      tracer,
        IConfiguration       config)
    {
        _scopeFactory = scopeFactory;
        _tracer       = tracer;

        var checkSec     = config.GetValue("ChargerWatcher:CheckIntervalSeconds",       30);
        var thresholdSec = config.GetValue("ChargerWatcher:InactivityThresholdSeconds", 180);
        var heartbeatSec = config.GetValue("ChargerWatcher:HeartbeatTimeoutSeconds",    300);

        _checkInterval       = TimeSpan.FromSeconds(checkSec);
        _inactivityThreshold = TimeSpan.FromSeconds(thresholdSec);
        _heartbeatTimeout    = TimeSpan.FromSeconds(heartbeatSec);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _tracer.Info("Watcher", $"ChargerWatcherService started — " +
            $"check every {_checkInterval.TotalSeconds}s, " +
            $"threshold {_inactivityThreshold.TotalSeconds}s");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);

                // Fix 6: one DB scope shared across both checks per tick.
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ChargingDBContext>();

                await CheckAllAsync(db, stoppingToken);
                await CheckStaleHeartbeatsAsync(db, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown — Task.Delay was cancelled by stoppingToken.
                break;
            }
            catch (Exception ex)
            {
                // Log but keep the loop alive — a transient DB error or RabbitMQ hiccup
                // should not permanently disable the watcher for the rest of the server's lifetime.
                _tracer.Exception("Watcher", ex, "Unhandled exception in scan loop");
            }
        }

        _tracer.Info("Watcher", "ChargerWatcherService stopped.");
    }

    private async Task CheckAllAsync(ChargingDBContext db, CancellationToken ct)
    {
        var now  = DateTime.UtcNow;
        var all  = ChargingStationConnections.GetAll();
        var dead = all
            .Where(entry => now - entry.State.LastMessageAt > _inactivityThreshold)
            .ToList();

        OcppTrace.Dbg("Watcher", $"Scan: {all.Count} connected, {dead.Count} stale");  // debug-only: too frequent for DB

        if (dead.Count == 0) return;

        foreach (var (stationId, state) in dead)
        {
            var silentFor = now - state.LastMessageAt;
            _tracer.Warning("Watcher",
                $"{stationId}: no message for {silentFor.TotalSeconds:F0}s — marking offline and aborting socket");

            // Mark offline in DB so the status survives a server restart.
            var plug = await db.Plugs.FirstOrDefaultAsync(p => p.OcppId == stationId, ct);
            if (plug != null)
            {
                plug.IsOnline = false;
            }
            else
            {
                _tracer.Error("Watcher", $"{stationId}: no Plug record in DB — IsOnline not updated",
                    chargePointId: stationId);
            }

            // Abort the socket — triggers the finally block in Program.cs which calls
            // Remove() and PushDisconnect() (publishing charger.disconnected to RabbitMQ).
            try { state.Socket.Abort(); }
            catch (Exception ex)
            {
                _tracer.Exception("Watcher", ex, $"{stationId}: socket abort failed",
                    chargePointId: stationId);
            }
        }

        // Fix 3: filter OperationCanceledException so clean shutdown is not logged as an error.
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _tracer.Exception("Watcher", ex, "DB save failed during watcher tick");
        }
    }

    /// <summary>
    /// Marks disconnected plugs as offline when <c>LastHeartbeatAt</c> has not been
    /// updated within <c>ChargerWatcher:HeartbeatTimeoutSeconds</c>.
    ///
    /// Only targets plugs with no live in-memory socket — chargers that are currently
    /// connected are excluded regardless of their heartbeat cadence (Fix 2).
    ///
    /// Uses <c>ExecuteUpdateAsync</c> so the <c>WHERE LastHeartbeatAt &lt; cutoff</c>
    /// condition is re-evaluated atomically at the DB: if a Heartbeat arrives between
    /// the diagnostic SELECT and this UPDATE, that plug's <c>LastHeartbeatAt</c> will
    /// be &gt;= cutoff and the UPDATE will skip it — no false-offline overwrite (Fix 1).
    /// </summary>
    private async Task CheckStaleHeartbeatsAsync(ChargingDBContext db, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - _heartbeatTimeout;

        // Fix 2: build the set of currently connected charger IDs so we can exclude
        // live sockets from the stale-heartbeat check. A connected charger with a slow
        // heartbeat interval (> HeartbeatTimeoutSeconds) must not be marked offline.
        var connectedOcppIds = ChargingStationConnections.GetAll()
            .Select(e => e.StationId)
            .ToHashSet();

        // Read the stale list for logging only — AsNoTracking because the actual
        // update goes through ExecuteUpdateAsync, not SaveChanges.
        //
        // Two stale cases:
        //   1. LastHeartbeatAt is set but expired — charger was alive, now silent.
        //   2. LastHeartbeatAt is null AND CreatedAt > 1 h ago — old DB record that
        //      never connected. Newly added stations get a 1-hour grace period so they
        //      are not immediately flipped offline before their first connection.
        var graceCutoff = DateTime.UtcNow.AddHours(-1);
        var stale = await db.Plugs
            .AsNoTracking()
            .Where(p => p.IsOnline &&
                        ((p.LastHeartbeatAt != null && p.LastHeartbeatAt < cutoff) ||
                         (p.LastHeartbeatAt == null && p.CreatedAt < graceCutoff)))
            .Select(p => new { p.OcppId, p.LastHeartbeatAt })
            .ToListAsync(ct);

        // Exclude live sockets in memory (number of stale plugs is expected to be small).
        stale = stale.Where(p => !connectedOcppIds.Contains(p.OcppId)).ToList();

        if (stale.Count == 0) return;

        foreach (var plug in stale)
        {
            var msg = plug.LastHeartbeatAt.HasValue
                ? $"{plug.OcppId}: no heartbeat for {(DateTime.UtcNow - plug.LastHeartbeatAt.Value).TotalSeconds:F0}s — marking offline"
                : $"{plug.OcppId}: IsOnline=true but never sent a heartbeat — marking offline";
            _tracer.Warning("Watcher", msg, chargePointId: plug.OcppId);
        }

        // Fix 1: atomic UPDATE — re-checks LastHeartbeatAt < cutoff at the DB level.
        // If a Heartbeat arrived between the SELECT above and this statement, that
        // plug's LastHeartbeatAt is now >= cutoff → WHERE excludes it → no overwrite.
        // Note: the warning log above was based on the pre-UPDATE snapshot and may
        // rarely mention a plug that heartbeated in the race window; this is harmless.
        var staleIds = stale.Select(p => p.OcppId).ToList();

        // Fix 3: filter OperationCanceledException so clean shutdown is not logged as an error.
        try
        {
            await db.Plugs
                .Where(p => staleIds.Contains(p.OcppId) &&
                            ((p.LastHeartbeatAt != null && p.LastHeartbeatAt < cutoff) ||
                             (p.LastHeartbeatAt == null && p.CreatedAt < graceCutoff)))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsOnline, false), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _tracer.Exception("Watcher", ex, "DB update failed during heartbeat stale check");
        }
    }
}
