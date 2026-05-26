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
///     "InactivityThresholdSeconds": 180
///   }
/// </code>
/// </summary>
public sealed class ChargerWatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITracingService      _tracer;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _inactivityThreshold;

    public ChargerWatcherService(
        IServiceScopeFactory scopeFactory,
        ITracingService      tracer,
        IConfiguration       config)
    {
        _scopeFactory = scopeFactory;
        _tracer       = tracer;

        var checkSec     = config.GetValue("ChargerWatcher:CheckIntervalSeconds",       30);
        var thresholdSec = config.GetValue("ChargerWatcher:InactivityThresholdSeconds", 180);

        _checkInterval       = TimeSpan.FromSeconds(checkSec);
        _inactivityThreshold = TimeSpan.FromSeconds(thresholdSec);
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
                await CheckAllAsync(stoppingToken);
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

    private async Task CheckAllAsync(CancellationToken ct)
    {
        var now  = DateTime.UtcNow;
        var all  = ChargingStationConnections.GetAll();
        var dead = all
            .Where(entry => now - entry.State.LastMessageAt > _inactivityThreshold)
            .ToList();

        OcppTrace.Dbg("Watcher", $"Scan: {all.Count} connected, {dead.Count} stale");  // debug-only: too frequent for DB

        if (dead.Count == 0) return;

        // One DB scope per watcher tick (not per charger) to batch the writes.
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ChargingDBContext>();

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

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _tracer.Exception("Watcher", ex, "DB save failed during watcher tick");
        }
    }
}
