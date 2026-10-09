using System.Text.Json;
using EVHomeAPI.Data;
using EVHomeAPI.Hubs;
using EVHomeAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Ocpp;

/// <summary>
/// Applies EVOCPP events to the database and pushes SignalR updates.
/// Transport-free (the RabbitMQ consumer only hands over routing key + JSON),
/// so it is unit-tested directly. Events for unknown stations are ignored.
///
/// Ordering: all events arrive through ONE queue with prefetch 1, so they are
/// processed in the order EVOCPP published them (e.g. transaction.started before
/// the meter.updated of the same transaction).
/// </summary>
public class OcppEventProcessor(
    AppDbContext db,
    INotifier notifier,
    IOcppCommandPublisher commands,
    TimeProvider clock,
    ILogger<OcppEventProcessor> logger)
{
    /// <summary>Minimum spacing of stored chart samples.</summary>
    public static readonly TimeSpan MeterSampleInterval = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task HandleAsync(string routingKey, string json, CancellationToken ct = default)
    {
        switch (routingKey)
        {
            case OcppRoutingKeys.StatusChanged:       await OnStatusChanged(Parse<StatusChangedEvent>(json), ct); break;
            case OcppRoutingKeys.AuthorizeRequested:  await OnAuthorizeRequested(Parse<AuthorizeRequestedEvent>(json), ct); break;
            case OcppRoutingKeys.TransactionStarted:  await OnTransactionStarted(Parse<TransactionStartedEvent>(json), ct); break;
            case OcppRoutingKeys.MeterUpdated:        await OnMeterUpdated(Parse<MeterUpdatedEvent>(json), ct); break;
            case OcppRoutingKeys.TransactionStopped:  await OnTransactionStopped(Parse<TransactionStoppedEvent>(json), ct); break;
            case OcppRoutingKeys.RemoteStartResponse: await OnRemoteStartResponse(Parse<RemoteStartResponseEvent>(json), ct); break;
            case OcppRoutingKeys.RemoteStopResponse:  await OnRemoteStopResponse(Parse<RemoteStopResponseEvent>(json), ct); break;
            case OcppRoutingKeys.ChargingLimitResponse: await OnChargingLimitResponse(Parse<ChargingLimitResponseEvent>(json), ct); break;
            default: logger.LogDebug("Ignoring event {RoutingKey}", routingKey); break;
        }
    }

    private static T Parse<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Json) ?? throw new JsonException($"Empty {typeof(T).Name}");

    private Task<Station?> FindStation(string ocppId, CancellationToken ct) =>
        db.Stations.FirstOrDefaultAsync(s => s.OcppId == ocppId, ct);

    /// <summary>The session currently in progress on a station (at most one by construction).</summary>
    private Task<ChargingSession?> OpenSession(int stationId, CancellationToken ct) =>
        db.Sessions
            .Where(s => s.StationId == stationId &&
                        (s.Status == SessionStatus.Pending || s.Status == SessionStatus.Active || s.Status == SessionStatus.Stopping))
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);

    // ── charger.status.changed ────────────────────────────────────────────────

    private async Task OnStatusChanged(StatusChangedEvent e, CancellationToken ct)
    {
        var station = await FindStation(e.OcppId, ct);
        if (station is null) return;

        station.IsOnline     = e.IsConnected;
        station.LastStatusAt = Now;
        if (e.ConnectorId == 1) station.ConnectorStatus = e.Status;   // home chargers: one connector
        await db.SaveChangesAsync(ct);

        await notifier.StatusUpdated(new StatusUpdatedMsg(station.Id, e.ConnectorId, e.Status, e.IsConnected, e.CarId));
    }

    // ── charger.authorize.requested (ISO 15118 vehicle identity) ──────────────

    /// <summary>
    /// Home rule: any vehicle may charge on a station that has an owner. Unknown or
    /// unclaimed stations are rejected. Must answer within 10 s (EVOCPP rejects otherwise).
    /// </summary>
    private async Task OnAuthorizeRequested(AuthorizeRequestedEvent e, CancellationToken ct)
    {
        var station = await FindStation(e.OcppId, ct);
        var owned   = station is not null &&
                      await db.StationAccesses.AnyAsync(a => a.StationId == station.Id && a.Role == StationRole.Owner, ct);

        await commands.AuthorizeResponseAsync(new AuthorizeResponseCommand(e.OcppId, owned ? "Accepted" : "Rejected"), ct);
    }

    // ── charger.transaction.started ───────────────────────────────────────────

    private async Task OnTransactionStarted(TransactionStartedEvent e, CancellationToken ct)
    {
        var station = await FindStation(e.OcppId, ct);
        if (station is null) return;

        var session = await OpenSession(station.Id, ct);

        if (session is not null && session.OcppTransactionId == e.TransactionId)
            return;   // duplicate delivery

        if (session is not null && session.Status != SessionStatus.Pending)
        {
            // An older transaction never got its StopTransaction (e.g. charger rebooted).
            CloseSession(session, meterStopWh: null, SessionStopReason.ChargerInitiated);   // keep last metered energy
            // Save now: the new open session below must not coexist with this one
            // (unique filtered index IX_Sessions_OneOpenPerStation is checked per statement).
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Station {OcppId}: closing stale session {SessionId} on new transaction {TxId}",
                e.OcppId, session.Id, e.TransactionId);
            session = null;
        }

        if (session is null)
        {
            // Started at the charger (button / RFID / plug & charge): attribute to the owner.
            var ownerId = await db.StationAccesses
                .Where(a => a.StationId == station.Id && a.Role == StationRole.Owner)
                .Select(a => (int?)a.UserId)
                .FirstOrDefaultAsync(ct);
            if (ownerId is null)
            {
                logger.LogInformation("Station {OcppId} has no owner — transaction {TxId} not recorded", e.OcppId, e.TransactionId);
                await db.SaveChangesAsync(ct);
                return;
            }

            session = new ChargingSession
            {
                StationId   = station.Id,
                UserId      = ownerId.Value,
                InitiatedBy = SessionInitiator.Charger,
                CreatedAt   = Now,
            };
            db.Sessions.Add(session);
        }

        session.Status            = SessionStatus.Active;
        session.OcppTransactionId = e.TransactionId;
        session.MeterStartWh      = e.MeterStartWh;
        session.StartedAt         = Now;
        await db.SaveChangesAsync(ct);

        await notifier.SessionStarted(new SessionStartedMsg(station.Id, session.Id, e.TransactionId, e.MeterStartWh));
    }

    // ── charger.meter.updated ─────────────────────────────────────────────────

    private async Task OnMeterUpdated(MeterUpdatedEvent e, CancellationToken ct)
    {
        var station = await FindStation(e.OcppId, ct);
        if (station is null) return;

        var session = await OpenSession(station.Id, ct);
        if (session is null || session.Status == SessionStatus.Pending) return;

        // Baseline: what StartTransaction reported, else EVOCPP's restored value, else the first reading.
        session.MeterStartWh ??= e.MeterStartWh ?? e.MeterValueWh;
        session.EnergyKwh      = Math.Max(0, (e.MeterValueWh - session.MeterStartWh.Value) / 1000m);
        session.CurrentPowerKw = e.CurrentPowerKw;
        if (e.Soc is not null) session.Soc = e.Soc;

        var elapsed = (int)(Now - (session.StartedAt ?? session.CreatedAt)).TotalSeconds;
        var lastSec = await db.MeterReadings
            .Where(r => r.SessionId == session.Id)
            .OrderByDescending(r => r.ElapsedSec)
            .Select(r => (int?)r.ElapsedSec)
            .FirstOrDefaultAsync(ct);
        if (lastSec is null || elapsed - lastSec >= MeterSampleInterval.TotalSeconds)
        {
            db.MeterReadings.Add(new SessionMeterReading
            {
                SessionId      = session.Id,
                ElapsedSec     = elapsed,
                CurrentPowerKw = e.CurrentPowerKw,
                Soc            = e.Soc,
                EnergyKwh      = session.EnergyKwh,
                RecordedAt     = Now,
            });
        }
        await db.SaveChangesAsync(ct);

        await notifier.MeterUpdated(new MeterUpdatedMsg(station.Id, session.Id, session.EnergyKwh, e.CurrentPowerKw, null, e.Soc));
    }

    // ── charger.transaction.stopped ───────────────────────────────────────────

    private async Task OnTransactionStopped(TransactionStoppedEvent e, CancellationToken ct)
    {
        var station = await FindStation(e.OcppId, ct);
        if (station is null) return;

        var session = await db.Sessions
            .Where(s => s.StationId == station.Id && s.OcppTransactionId == e.TransactionId &&
                        (s.Status == SessionStatus.Active || s.Status == SessionStatus.Stopping))
            .FirstOrDefaultAsync(ct);
        if (session is null) return;   // unknown or already finalized

        session.MeterStartWh ??= e.MeterStartWh;
        var reason = session.Status == SessionStatus.Stopping ? SessionStopReason.UserInitiated : SessionStopReason.ChargerInitiated;
        CloseSession(session, e.MeterStopWh, reason);
        await db.SaveChangesAsync(ct);

        await notifier.SessionFinalized(new SessionFinalizedMsg(station.Id, session.Id, session.EnergyKwh, null, reason.ToString()));
    }

    private void CloseSession(ChargingSession session, decimal? meterStopWh, SessionStopReason reason)
    {
        session.Status         = SessionStatus.Completed;
        session.StopReason     = reason;
        session.EndedAt        = Now;
        session.CurrentPowerKw = 0;
        session.MeterStopWh    = meterStopWh;
        if (meterStopWh is not null && session.MeterStartWh is not null)
            session.EnergyKwh = Math.Max(0, (meterStopWh.Value - session.MeterStartWh.Value) / 1000m);
    }

    // ── charger.remote.start.response / remote.stop.response ──────────────────

    private async Task OnRemoteStartResponse(RemoteStartResponseEvent e, CancellationToken ct)
    {
        // Accepted is published only after StartTransaction → OnTransactionStarted already ran.
        if (e.Status == "Accepted" || e.TrackingId is null) return;

        var session = await db.Sessions.FirstOrDefaultAsync(s => s.TrackingId == e.TrackingId, ct);
        if (session is null || session.Status != SessionStatus.Pending) return;

        session.Status  = SessionStatus.Cancelled;
        session.EndedAt = Now;
        await db.SaveChangesAsync(ct);

        await notifier.SessionStartFailed(new SessionStartFailedMsg(session.StationId, session.Id, e.Status));
    }

    // ── charger.charging.limit.response ───────────────────────────────────────

    /// <summary>
    /// Records the charger's answer to the latest limit request. A response for an older
    /// request (the owner changed the limit again meanwhile) is ignored.
    /// Status reflects the TxDefaultProfile (future sessions); "Unknown" on a clear just
    /// means there was no profile to remove — the clear is effective.
    /// </summary>
    private async Task OnChargingLimitResponse(ChargingLimitResponseEvent e, CancellationToken ct)
    {
        var station = await FindStation(e.OcppId, ct);
        if (station is null) return;

        var requested = e.LimitA is null ? (decimal?)null : Math.Round((decimal)e.LimitA.Value, 1);
        if (requested != station.CurrentLimitA) return;   // superseded by a newer request

        var applied = e.Status == "Accepted" || (requested is null && e.Status == "Unknown");
        station.LimitStatus    = applied ? "Applied" : e.Status;
        station.LimitUpdatedAt = Now;
        await db.SaveChangesAsync(ct);

        await notifier.ChargingLimitUpdated(new ChargingLimitUpdatedMsg(station.Id, station.CurrentLimitA, station.LimitStatus));
    }

    private async Task OnRemoteStopResponse(RemoteStopResponseEvent e, CancellationToken ct)
    {
        // Accepted is published only after StopTransaction → OnTransactionStopped already ran.
        if (e.Status == "Accepted") return;

        var station = await FindStation(e.OcppId, ct);
        if (station is null) return;

        var session = await db.Sessions
            .Where(s => s.StationId == station.Id && s.Status == SessionStatus.Stopping &&
                        (e.TransactionId == null || s.OcppTransactionId == e.TransactionId))
            .FirstOrDefaultAsync(ct);
        if (session is null) return;

        session.Status = SessionStatus.Active;   // still charging — let the user retry
        await db.SaveChangesAsync(ct);

        await notifier.SessionStopFailed(new SessionStopFailedMsg(station.Id, session.Id, e.Status));
    }
}
