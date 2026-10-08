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

public class ChargingTests
{
    // ── App-initiated session: full happy path ────────────────────────────────

    [Fact]
    public async Task Start_meter_stop_full_cycle()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync();

        // Start → Pending + RemoteStart command (PascalCase contract, idTag U{userId})
        var start = await owner.PostAsJsonAsync($"/api/stations/{stationId}/start", new StartChargingRequest());
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var pending = (await start.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Equal("Pending", pending.Status);
        var cmd = f.Commands.Last<RemoteStartCommand>();
        Assert.Equal("30011", cmd.OcppId);
        Assert.StartsWith("U", cmd.IdTag);

        // Charger confirms
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent("30011", 42, 10_000m));
        await f.PublishEventAsync(OcppRoutingKeys.RemoteStartResponse, new RemoteStartResponseEvent("30011", "Accepted", 1, cmd.IdTag, cmd.TrackingId));
        var started = Assert.Single(f.Notifier.Of<SessionStartedMsg>());
        Assert.Equal(pending.Id, started.SessionId);
        Assert.Equal(42, started.TransactionId);

        // Meter: 10 000 → 12 500 Wh = 2.5 kWh
        f.Clock.Advance(TimeSpan.FromMinutes(5));
        await f.PublishEventAsync(OcppRoutingKeys.MeterUpdated, new MeterUpdatedEvent("30011", 12_500m, 10_000m, 3.7, null));
        var meter = f.Notifier.Of<MeterUpdatedMsg>().Last();
        Assert.Equal(2.5m, meter.EnergyKwh);
        Assert.Null(meter.TotalCost);

        // Stop → Stopping + RemoteStop with the transaction id
        var stop = await owner.PostAsync($"/api/stations/{stationId}/stop", null);
        Assert.Equal(HttpStatusCode.Accepted, stop.StatusCode);
        Assert.Equal(42, f.Commands.Last<RemoteStopCommand>().TransactionId);

        await f.PublishEventAsync(OcppRoutingKeys.TransactionStopped, new TransactionStoppedEvent("30011", 42, 13_000m, 10_000m));
        var fin = Assert.Single(f.Notifier.Of<SessionFinalizedMsg>());
        Assert.Equal(3.0m, fin.EnergyKwh);
        Assert.Equal("UserInitiated", fin.StopReason);

        var done = await owner.GetFromJsonAsync<SessionDto>($"/api/sessions/{pending.Id}");
        Assert.Equal("Completed", done!.Status);
        Assert.Equal(3.0m, done.EnergyKwh);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.GetAsync($"/api/stations/{stationId}/session")).StatusCode);

        var chart = await owner.GetFromJsonAsync<List<MeterPointDto>>($"/api/sessions/{pending.Id}/meter-history");
        Assert.Single(chart!);
        Assert.Equal(300, chart![0].ElapsedSec);
    }

    // ── Charger-initiated session (button / LOCAL idTag) ──────────────────────

    [Fact]
    public async Task Transaction_started_at_charger_creates_session_for_owner()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync();

        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent("30011", 7, 500m));
        var open = await owner.GetFromJsonAsync<SessionDto>($"/api/stations/{stationId}/session");
        Assert.Equal("Active", open!.Status);
        Assert.Equal("Charger", open.InitiatedBy);

        await f.PublishEventAsync(OcppRoutingKeys.TransactionStopped, new TransactionStoppedEvent("30011", 7, 1_500m, 500m));
        var fin = Assert.Single(f.Notifier.Of<SessionFinalizedMsg>());
        Assert.Equal("ChargerInitiated", fin.StopReason);
        Assert.Equal(1.0m, fin.EnergyKwh);
    }

    [Fact]
    public async Task Duplicate_transaction_started_is_ignored()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync();

        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent("30011", 7, 500m));
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent("30011", 7, 500m));

        var history = await owner.GetFromJsonAsync<List<SessionDto>>($"/api/stations/{stationId}/sessions");
        Assert.Single(history!);
        Assert.Single(f.Notifier.Of<SessionStartedMsg>());
    }

    [Fact]
    public async Task New_transaction_closes_stale_active_session()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync();

        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent("30011", 1, 0m));
        await f.PublishEventAsync(OcppRoutingKeys.MeterUpdated, new MeterUpdatedEvent("30011", 2_000m, 0m, 3.7, null));
        // charger rebooted, StopTransaction for tx 1 never arrived
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent("30011", 2, 2_000m));

        var history = (await owner.GetFromJsonAsync<List<SessionDto>>($"/api/stations/{stationId}/sessions"))!;
        Assert.Equal(2, history.Count);
        Assert.Equal("Active", history[0].Status);
        Assert.Equal(2, history[0].TransactionId);
        Assert.Equal("Completed", history[1].Status);
        Assert.Equal(2.0m, history[1].EnergyKwh);   // last metered energy kept
    }

    [Fact]
    public async Task Meter_without_start_value_uses_first_reading_as_baseline()
    {
        using var f = new ApiFactory();
        await f.CreateOwnedStationAsync();
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent("30011", 3, null));

        await f.PublishEventAsync(OcppRoutingKeys.MeterUpdated, new MeterUpdatedEvent("30011", 5_000m, null, 3.7, null));
        await f.PublishEventAsync(OcppRoutingKeys.MeterUpdated, new MeterUpdatedEvent("30011", 6_000m, null, 3.7, null));

        Assert.Equal(1.0m, f.Notifier.Of<MeterUpdatedMsg>().Last().EnergyKwh);
    }

    [Fact]
    public async Task Meter_samples_are_downsampled_to_30s()
    {
        using var f = new ApiFactory();
        var (owner, _) = await f.CreateOwnedStationAsync();
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent("30011", 3, 0m));

        for (var i = 0; i < 6; i++)   // every 10 s for 60 s
        {
            f.Clock.Advance(TimeSpan.FromSeconds(10));
            await f.PublishEventAsync(OcppRoutingKeys.MeterUpdated, new MeterUpdatedEvent("30011", i * 10m, 0m, 3.7, null));
        }

        var sessionId = f.Notifier.Of<SessionStartedMsg>().Single().SessionId;
        var chart = await owner.GetFromJsonAsync<List<MeterPointDto>>($"/api/sessions/{sessionId}/meter-history");
        Assert.Equal([10, 40], chart!.Select(p => p.ElapsedSec));
    }

    // ── Failures ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Rejected")]
    [InlineData("Timeout")]
    public async Task Remote_start_rejected_or_timeout_cancels_session(string status)
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync();
        await owner.PostAsJsonAsync($"/api/stations/{stationId}/start", new StartChargingRequest());
        var cmd = f.Commands.Last<RemoteStartCommand>();

        await f.PublishEventAsync(OcppRoutingKeys.RemoteStartResponse, new RemoteStartResponseEvent("30011", status, 1, cmd.IdTag, cmd.TrackingId));

        Assert.Equal(status, Assert.Single(f.Notifier.Of<SessionStartFailedMsg>()).Reason);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.GetAsync($"/api/stations/{stationId}/session")).StatusCode);
    }

    [Fact]
    public async Task Remote_stop_rejected_reverts_to_active()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync();
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent("30011", 9, 0m));
        await owner.PostAsync($"/api/stations/{stationId}/stop", null);

        await f.PublishEventAsync(OcppRoutingKeys.RemoteStopResponse, new RemoteStopResponseEvent("30011", "Rejected", 9));

        Assert.Equal("Rejected", Assert.Single(f.Notifier.Of<SessionStopFailedMsg>()).Reason);
        var open = await owner.GetFromJsonAsync<SessionDto>($"/api/stations/{stationId}/session");
        Assert.Equal("Active", open!.Status);
    }

    [Fact]
    public async Task Start_rejected_when_offline_or_busy()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync(online: false);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"/api/stations/{stationId}/start", new StartChargingRequest())).StatusCode);

        await f.PublishEventAsync(OcppRoutingKeys.StatusChanged, new StatusChangedEvent("30011", "Available", 1, true, null));
        Assert.Equal(HttpStatusCode.Accepted, (await owner.PostAsJsonAsync($"/api/stations/{stationId}/start", new StartChargingRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"/api/stations/{stationId}/start", new StartChargingRequest())).StatusCode);
        Assert.Single(f.Commands.Sent.OfType<RemoteStartCommand>());
    }

    [Fact]
    public async Task Stop_while_pending_or_idle_is_409()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync($"/api/stations/{stationId}/stop", null)).StatusCode);

        await owner.PostAsJsonAsync($"/api/stations/{stationId}/start", new StartChargingRequest());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync($"/api/stations/{stationId}/stop", null)).StatusCode);
    }

    [Fact]
    public async Task Broker_down_on_start_returns_503_and_cancels()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync();
        f.Commands.Fail = true;

        var res = await owner.PostAsJsonAsync($"/api/stations/{stationId}/start", new StartChargingRequest());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);

        f.Commands.Fail = false;   // a retry must be possible (no dangling Pending session)
        Assert.Equal(HttpStatusCode.Accepted, (await owner.PostAsJsonAsync($"/api/stations/{stationId}/start", new StartChargingRequest())).StatusCode);
    }

    [Fact]
    public async Task Watchdog_cancels_stuck_pending_and_reverts_stuck_stopping()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync();
        var (owner2, station2) = await f.CreateOwnedStationAsync("HOME-2");

        await owner.PostAsJsonAsync($"/api/stations/{stationId}/start", new StartChargingRequest());       // stays Pending
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent("HOME-2", 5, 0m));
        await owner2.PostAsync($"/api/stations/{station2}/stop", null);                                    // stays Stopping

        f.Clock.Advance(StaleSessionWatchdog.StaleAfter + TimeSpan.FromSeconds(1));
        await f.WithDbAsync(db => StaleSessionWatchdog.RunOnceAsync(db, f.Notifier, f.Clock));

        Assert.Equal("Timeout", Assert.Single(f.Notifier.Of<SessionStartFailedMsg>()).Reason);
        Assert.Equal("Timeout", Assert.Single(f.Notifier.Of<SessionStopFailedMsg>()).Reason);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.GetAsync($"/api/stations/{stationId}/session")).StatusCode);
        Assert.Equal("Active", (await owner2.GetFromJsonAsync<SessionDto>($"/api/stations/{station2}/session"))!.Status);
    }

    // ── Status, authorize, access ─────────────────────────────────────────────

    [Fact]
    public async Task Status_changed_updates_station_and_notifies()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync();

        await f.PublishEventAsync(OcppRoutingKeys.StatusChanged, new StatusChangedEvent("30011", "Preparing", 1, true, null));
        var st = await owner.GetFromJsonAsync<StationDto>($"/api/stations/{stationId}");
        Assert.True(st!.IsOnline);
        Assert.Equal("Preparing", st.ConnectorStatus);

        await f.PublishEventAsync(OcppRoutingKeys.StatusChanged, new StatusChangedEvent("30011", "Unavailable", 0, false, null));
        st = await owner.GetFromJsonAsync<StationDto>($"/api/stations/{stationId}");
        Assert.False(st!.IsOnline);
        Assert.Equal("Preparing", st.ConnectorStatus);   // station-level (0) does not overwrite connector 1

        Assert.Contains(f.Notifier.Of<StatusUpdatedMsg>(), m => m.StationId == stationId && !m.IsConnected);
    }

    [Fact]
    public async Task Authorize_accepted_only_for_owned_station()
    {
        using var f = new ApiFactory();
        await f.CreateOwnedStationAsync();
        await f.CreateStationAsync("UNCLAIMED");

        await f.PublishEventAsync(OcppRoutingKeys.AuthorizeRequested, new AuthorizeRequestedEvent("30011", "001681020001", null));
        await f.PublishEventAsync(OcppRoutingKeys.AuthorizeRequested, new AuthorizeRequestedEvent("UNCLAIMED", "001681020001", null));
        await f.PublishEventAsync(OcppRoutingKeys.AuthorizeRequested, new AuthorizeRequestedEvent("NOPE", "001681020001", null));

        var replies = f.Commands.Sent.OfType<AuthorizeResponseCommand>().ToList();
        Assert.Equal(["Accepted", "Rejected", "Rejected"], replies.Select(r => r.status));
    }

    [Fact]
    public async Task Events_for_unknown_or_unclaimed_station_are_ignored()
    {
        using var f = new ApiFactory();
        await f.CreateStationAsync("UNCLAIMED");

        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent("UNCLAIMED", 1, 0m));
        await f.PublishEventAsync(OcppRoutingKeys.MeterUpdated, new MeterUpdatedEvent("NOPE", 1m, 0m, 1, null));

        await f.WithDbAsync(async db => Assert.Equal(0, await db.Sessions.CountAsync()));
        Assert.Empty(f.Notifier.Of<SessionStartedMsg>());
    }

    [Fact]
    public async Task Other_user_cannot_start_stop_or_read_sessions()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync();
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent("30011", 1, 0m));
        var sessionId = f.Notifier.Of<SessionStartedMsg>().Single().SessionId;
        var stranger  = await f.CreateUserClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"/api/stations/{stationId}/start", new StartChargingRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/stations/{stationId}/stop", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/sessions/{sessionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/sessions/{sessionId}/meter-history")).StatusCode);
        Assert.Empty(f.Commands.Sent.OfType<RemoteStopCommand>());
    }

    [Fact]
    public void Commands_serialize_with_evocpp_casing()
    {
        // EVOCPP deserializes remote start/stop case-sensitively (PascalCase) but reads
        // authorize.response by exact camelCase names.
        var start = System.Text.Json.JsonSerializer.Serialize(new RemoteStartCommand("30011", 1, "U1", Guid.Empty));
        var auth  = System.Text.Json.JsonSerializer.Serialize(new AuthorizeResponseCommand("30011", "Accepted"));
        Assert.Contains("\"OcppId\":\"30011\"", start);
        Assert.Contains("\"IdTag\":\"U1\"", start);
        Assert.Contains("\"TrackingId\"", start);
        Assert.Equal("{\"ocppId\":\"30011\",\"status\":\"Accepted\"}", auth);
    }
}
