using System.Net;
using System.Net.Http.Json;
using EVHomeAPI.DTOs;
using EVHomeAPI.Hubs;
using EVHomeAPI.Models;
using EVHomeAPI.Ocpp;
using EVHomeAPI.Services;
using EVHomeAPI.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Tests;

/// <summary>Resume after power loss — SessionResumeService + OcppEventProcessor.</summary>
public class PowerLossResumeTests
{
    private const string Id = "30011";

    private static Task Status(ApiFactory f, string status, int connector = 1, bool connected = true) =>
        f.PublishEventAsync(OcppRoutingKeys.StatusChanged, new StatusChangedEvent(Id, status, connector, connected, null));

    /// <summary>Charging session on tx 1 from meter 626 to 1933 Wh, then the charger goes silent.</summary>
    private static async Task<(HttpClient Owner, int StationId, int SessionId)> ChargingThenOutageAsync(ApiFactory f)
    {
        var (owner, stationId) = await f.CreateOwnedStationAsync(Id);
        await Status(f, "Charging");
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent(Id, 1, 626m));
        f.Clock.Advance(TimeSpan.FromMinutes(10));
        await f.PublishEventAsync(OcppRoutingKeys.MeterUpdated, new MeterUpdatedEvent(Id, 1_933m, 626m, 3.5, null));
        var sessionId = f.Notifier.Of<SessionStartedMsg>().Single().SessionId;

        f.Clock.Advance(TimeSpan.FromMinutes(1));
        await Status(f, "Offline", connector: 0, connected: false);      // EVOCPP watcher / socket closed
        return (owner, stationId, sessionId);
    }

    /// <summary>What the Wallbox Copper SB sent when the power came back (2026-10-09).</summary>
    private static async Task PowerBackAsync(ApiFactory f, string connectorStatus, decimal meterStop = 1_933m)
    {
        f.Clock.Advance(TimeSpan.FromHours(4));
        await Status(f, "Available", connector: 0);                      // socket reconnect
        await f.PublishEventAsync(OcppRoutingKeys.Booted, new ChargerBootedEvent(Id));
        await Status(f, connectorStatus);
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStopped, new TransactionStoppedEvent(Id, 1, meterStop, 626m, "PowerLoss"));
    }

    [Fact]
    public async Task Wallbox_power_loss_pauses_then_resumes_same_session()
    {
        using var f = new ApiFactory();
        var (owner, stationId, sessionId) = await ChargingThenOutageAsync(f);

        var paused = await owner.GetFromJsonAsync<SessionDto>($"/api/stations/{stationId}/session");
        Assert.Equal("Paused", paused!.Status);
        Assert.Equal("ChargerOffline", Assert.Single(f.Notifier.Of<SessionPausedMsg>()).Reason);
        Assert.Empty(f.Commands.Sent.OfType<RemoteStartCommand>());       // nothing to do while offline

        await PowerBackAsync(f, "Finishing");

        // Transaction banked, session still open, RemoteStart sent under the same session
        var open = await owner.GetFromJsonAsync<SessionDto>($"/api/stations/{stationId}/session");
        Assert.Equal(sessionId, open!.Id);
        Assert.Equal("Paused", open.Status);
        Assert.Null(open.TransactionId);
        Assert.Equal(1.307m, open.EnergyKwh);
        var cmd = f.Commands.Last<RemoteStartCommand>();
        Assert.Equal(Id, cmd.OcppId);
        Assert.Empty(f.Notifier.Of<SessionFinalizedMsg>());

        // Charger accepts: new transaction — ids restart after a reboot, so tx 1 again
        await Status(f, "Preparing");
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent(Id, 1, 1_929m));
        await f.PublishEventAsync(OcppRoutingKeys.RemoteStartResponse, new RemoteStartResponseEvent(Id, "Accepted", 1, cmd.IdTag, cmd.TrackingId));
        await Status(f, "Charging");
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        await f.PublishEventAsync(OcppRoutingKeys.MeterUpdated, new MeterUpdatedEvent(Id, 2_017m, 1_929m, 5.3, null));

        open = await owner.GetFromJsonAsync<SessionDto>($"/api/stations/{stationId}/session");
        Assert.Equal(sessionId, open!.Id);
        Assert.Equal("Active", open.Status);
        Assert.Equal(1.395m, open.EnergyKwh);                             // 1.307 + 0.088
        Assert.Equal(sessionId, f.Notifier.Of<SessionStartedMsg>().Last().SessionId);

        // User stops: one session in history with the energy of both transactions
        await owner.PostAsync($"/api/stations/{stationId}/stop", null);
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStopped, new TransactionStoppedEvent(Id, 1, 2_100m, 1_929m, "Remote"));

        var history = (await owner.GetFromJsonAsync<List<SessionDto>>($"/api/stations/{stationId}/sessions"))!;
        var done = Assert.Single(history);
        Assert.Equal("Completed", done.Status);
        Assert.Equal("UserInitiated", done.StopReason);
        Assert.Equal(1.478m, done.EnergyKwh);                             // 1.307 + 0.171
        Assert.Single(f.Commands.Sent.OfType<RemoteStartCommand>());
    }

    [Fact]
    public async Task Car_unplugged_during_outage_ends_session_at_outage_time()
    {
        using var f = new ApiFactory();
        var (owner, _, sessionId) = await ChargingThenOutageAsync(f);
        var outageAt = f.Clock.GetUtcNow().UtcDateTime;

        await PowerBackAsync(f, "Available");

        var fin = Assert.Single(f.Notifier.Of<SessionFinalizedMsg>());
        Assert.Equal("PowerLoss", fin.StopReason);
        Assert.Equal(1.307m, fin.EnergyKwh);
        var done = await owner.GetFromJsonAsync<SessionDto>($"/api/sessions/{sessionId}");
        Assert.Equal("Completed", done!.Status);
        Assert.Equal(outageAt, done.EndedAt);
        Assert.Empty(f.Commands.Sent.OfType<RemoteStartCommand>());
    }

    [Fact]
    public async Task Rejected_resume_is_retried_then_given_up()
    {
        using var f = new ApiFactory();
        var (owner, stationId, _) = await ChargingThenOutageAsync(f);
        await PowerBackAsync(f, "Finishing");

        for (var attempt = 1; attempt <= SessionResumeService.MaxAttempts; attempt++)
        {
            var cmd = f.Commands.Last<RemoteStartCommand>();
            Assert.Equal(attempt, f.Commands.Sent.OfType<RemoteStartCommand>().Count());
            await f.PublishEventAsync(OcppRoutingKeys.RemoteStartResponse, new RemoteStartResponseEvent(Id, "Rejected", 1, cmd.IdTag, cmd.TrackingId));

            await f.RunWatchdogAsync();                                   // too early — no new attempt
            Assert.Equal(attempt, f.Commands.Sent.OfType<RemoteStartCommand>().Count());
            f.Clock.Advance(SessionResumeService.RetryAfter);
            await f.RunWatchdogAsync();
        }

        Assert.Equal(SessionResumeService.MaxAttempts, f.Commands.Sent.OfType<RemoteStartCommand>().Count());
        Assert.Equal("PowerLoss", Assert.Single(f.Notifier.Of<SessionFinalizedMsg>()).StopReason);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.GetAsync($"/api/stations/{stationId}/session")).StatusCode);
    }

    [Fact]
    public async Task Network_blip_returns_to_active_without_new_transaction()
    {
        using var f = new ApiFactory();
        var (owner, stationId, sessionId) = await ChargingThenOutageAsync(f);

        await Status(f, "Available", connector: 0);
        await Status(f, "Charging");

        var open = await owner.GetFromJsonAsync<SessionDto>($"/api/stations/{stationId}/session");
        Assert.Equal(sessionId, open!.Id);
        Assert.Equal("Active", open.Status);
        Assert.Equal(1, open.TransactionId);
        Assert.Empty(f.Commands.Sent.OfType<RemoteStartCommand>());
    }

    [Fact]
    public async Task Meter_of_running_transaction_unpauses()
    {
        using var f = new ApiFactory();
        var (owner, stationId, _) = await ChargingThenOutageAsync(f);

        await Status(f, "Available", connector: 0);
        await f.PublishEventAsync(OcppRoutingKeys.MeterUpdated, new MeterUpdatedEvent(Id, 2_000m, 626m, 3.5, null));

        var open = await owner.GetFromJsonAsync<SessionDto>($"/api/stations/{stationId}/session");
        Assert.Equal("Active", open!.Status);
        Assert.Equal(1.374m, open.EnergyKwh);
    }

    [Fact]
    public async Task Charger_boot_reapplies_current_limit()
    {
        using var f = new ApiFactory();
        await f.CreateOwnedStationAsync(Id);
        var before = f.Commands.Sent.OfType<ChargingLimitCommand>().Count();

        await f.PublishEventAsync(OcppRoutingKeys.Booted, new ChargerBootedEvent(Id));

        Assert.Equal(before + 1, f.Commands.Sent.OfType<ChargingLimitCommand>().Count());
        Assert.Equal(32.0, f.Commands.Last<ChargingLimitCommand>().limitA);   // MaxCurrentA, never "no limit"
    }

    [Fact]
    public async Task Stop_while_paused_and_offline_ends_locally_and_ignores_late_stop()
    {
        using var f = new ApiFactory();
        var (owner, stationId, sessionId) = await ChargingThenOutageAsync(f);

        var res = await owner.PostAsync($"/api/stations/{stationId}/stop", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Completed", (await res.Content.ReadFromJsonAsync<SessionDto>())!.Status);
        Assert.Empty(f.Commands.Sent.OfType<RemoteStopCommand>());

        await PowerBackAsync(f, "Finishing");                            // late StopTransaction(PowerLoss)

        Assert.Empty(f.Commands.Sent.OfType<RemoteStartCommand>());
        var done = await owner.GetFromJsonAsync<SessionDto>($"/api/sessions/{sessionId}");
        Assert.Equal("UserInitiated", done!.StopReason);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.GetAsync($"/api/stations/{stationId}/session")).StatusCode);
    }

    [Fact]
    public async Task Stop_while_resume_in_flight_is_409()
    {
        using var f = new ApiFactory();
        var (owner, stationId, _) = await ChargingThenOutageAsync(f);
        await PowerBackAsync(f, "Finishing");

        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync($"/api/stations/{stationId}/stop", null)).StatusCode);
    }

    [Fact]
    public async Task Stopping_session_is_not_resumed()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync(Id);
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent(Id, 1, 0m));
        await owner.PostAsync($"/api/stations/{stationId}/stop", null);
        await Status(f, "Offline", connector: 0, connected: false);

        Assert.Empty(f.Notifier.Of<SessionPausedMsg>());
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStopped, new TransactionStoppedEvent(Id, 1, 500m, 0m, "PowerLoss"));

        Assert.Equal("UserInitiated", Assert.Single(f.Notifier.Of<SessionFinalizedMsg>()).StopReason);
        Assert.Empty(f.Commands.Sent.OfType<RemoteStartCommand>());
    }

    [Fact]
    public async Task Watchdog_finishes_stopping_session_of_offline_charger()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync(Id);
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent(Id, 1, 0m));
        await owner.PostAsync($"/api/stations/{stationId}/stop", null);
        await Status(f, "Offline", connector: 0, connected: false);

        f.Clock.Advance(StaleSessionWatchdog.StaleAfter + TimeSpan.FromSeconds(1));
        await f.RunWatchdogAsync();

        Assert.Equal("UserInitiated", Assert.Single(f.Notifier.Of<SessionFinalizedMsg>()).StopReason);
        Assert.Empty(f.Notifier.Of<SessionStopFailedMsg>());
    }

    [Fact]
    public async Task Pause_longer_than_limit_ends_session()
    {
        using var f = new ApiFactory();
        var (_, _, sessionId) = await ChargingThenOutageAsync(f);

        f.Clock.Advance(SessionResumeService.MaxPause + TimeSpan.FromMinutes(1));
        await f.RunWatchdogAsync();

        var fin = Assert.Single(f.Notifier.Of<SessionFinalizedMsg>());
        Assert.Equal(sessionId, fin.SessionId);
        Assert.Equal("PowerLoss", fin.StopReason);
    }

    [Fact]
    public async Task Lost_stop_transaction_is_banked_and_resumed_by_watchdog()
    {
        using var f = new ApiFactory();
        var (owner, stationId, sessionId) = await ChargingThenOutageAsync(f);

        f.Clock.Advance(TimeSpan.FromHours(1));
        await Status(f, "Available", connector: 0);
        await Status(f, "Finishing");                                     // no StopTransaction follows

        await f.RunWatchdogAsync();
        Assert.Empty(f.Commands.Sent.OfType<RemoteStartCommand>());       // give the charger time

        f.Clock.Advance(SessionResumeService.LostTransactionAfter + TimeSpan.FromSeconds(1));
        await f.RunWatchdogAsync();

        Assert.Single(f.Commands.Sent.OfType<RemoteStartCommand>());
        var open = await owner.GetFromJsonAsync<SessionDto>($"/api/stations/{stationId}/session");
        Assert.Equal(sessionId, open!.Id);
        Assert.Null(open.TransactionId);
        Assert.Equal(1.307m, open.EnergyKwh);                             // last metered energy kept
    }

    [Fact]
    public async Task Other_stop_reasons_still_complete_the_session()
    {
        using var f = new ApiFactory();
        await f.CreateOwnedStationAsync(Id);
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent(Id, 1, 0m));
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStopped, new TransactionStoppedEvent(Id, 1, 800m, 0m, "EVDisconnected"));

        Assert.Equal("ChargerInitiated", Assert.Single(f.Notifier.Of<SessionFinalizedMsg>()).StopReason);
        await f.WithDbAsync(async db => Assert.Equal(0, await db.Sessions.CountAsync(s => s.Status == SessionStatus.Paused)));
    }
}
