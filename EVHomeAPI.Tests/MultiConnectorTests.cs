using System.Net;
using System.Net.Http.Json;
using EVHomeAPI.DTOs;
using EVHomeAPI.Hubs;
using EVHomeAPI.Ocpp;
using EVHomeAPI.Tests.Infrastructure;

namespace EVHomeAPI.Tests;

/// <summary>
/// One OCPP id, several connectors (Wallbox Supernova: connectorId 1 and 2, a separate meter each).
/// Replays the two-car evening of 2026-11-05.
/// </summary>
public class MultiConnectorTests
{
    private const string Id = "wbx669850";

    private static Task Status(ApiFactory f, int connector, string status, bool connected = true) =>
        f.PublishEventAsync(OcppRoutingKeys.StatusChanged, new StatusChangedEvent(Id, status, connector, connected, null));

    private static Task Started(ApiFactory f, int connector, int tx, decimal meterStart) =>
        f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent(Id, tx, meterStart, connector));

    private static Task Meter(ApiFactory f, int connector, int tx, decimal wh, decimal start, double kw, decimal soc) =>
        f.PublishEventAsync(OcppRoutingKeys.MeterUpdated, new MeterUpdatedEvent(Id, wh, start, kw, soc, connector, tx));

    private static Task Stopped(ApiFactory f, int connector, int tx, decimal stop, decimal start, string reason) =>
        f.PublishEventAsync(OcppRoutingKeys.TransactionStopped, new TransactionStoppedEvent(Id, tx, stop, start, reason, connector));

    [Fact]
    public async Task Two_cars_get_separate_sessions_energy_and_soc()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync(Id);

        // Car A: connector 2, Autocharge at the charger
        await Status(f, 2, "Preparing");
        await Started(f, 2, 21, 28_256_000m);
        await Status(f, 2, "Charging");
        // Car B: connector 1, RFID
        await Status(f, 1, "Preparing");
        await Started(f, 1, 22, 19_107_456m);
        await Status(f, 1, "Charging");

        f.Clock.Advance(TimeSpan.FromMinutes(1));
        await Meter(f, 2, 21, 28_268_884m, 28_256_000m, 19.5, 95m);
        await Meter(f, 1, 22, 19_107_884m, 19_107_456m, 29.1, 44m);

        var open = await owner.GetFromJsonAsync<List<SessionDto>>($"/api/stations/{stationId}/sessions/open");
        Assert.Equal(2, open!.Count);
        var s1 = open.Single(s => s.ConnectorId == 1);
        var s2 = open.Single(s => s.ConnectorId == 2);
        Assert.Equal(0.428m, s1.EnergyKwh);     // 19 107 884 − 19 107 456
        Assert.Equal(44m, s1.Soc);
        Assert.Equal(12.884m, s2.EnergyKwh);    // 28 268 884 − 28 256 000 — not mixed with the other meter
        Assert.Equal(95m, s2.Soc);

        var station = await owner.GetFromJsonAsync<StationDto>($"/api/stations/{stationId}");
        Assert.Equal(new[] { "1:Charging", "2:Charging" },
            station!.Connectors.Select(c => $"{c.ConnectorId}:{c.Status}"));
        Assert.Equal("Charging", station.ConnectorStatus);   // legacy field = connector 1

        // Stop needs a connector while two sessions run
        var ambiguous = await owner.PostAsync($"/api/stations/{stationId}/stop", null);
        Assert.Equal(HttpStatusCode.Conflict, ambiguous.StatusCode);
        var stop = await owner.PostAsync($"/api/stations/{stationId}/stop?connectorId=2", null);
        Assert.Equal(HttpStatusCode.Accepted, stop.StatusCode);
        Assert.Equal(21, f.Commands.Last<RemoteStopCommand>().TransactionId);

        await Stopped(f, 2, 21, 28_272_076m, 28_256_000m, "Remote");

        open = await owner.GetFromJsonAsync<List<SessionDto>>($"/api/stations/{stationId}/sessions/open");
        Assert.Equal(1, Assert.Single(open!).ConnectorId);
        var finalized = Assert.Single(f.Notifier.Of<SessionFinalizedMsg>());
        Assert.Equal(s2.Id, finalized.SessionId);
        Assert.Equal(16.076m, finalized.EnergyKwh);

        // Single open session left → stop without connectorId works again
        Assert.Equal(HttpStatusCode.Accepted, (await owner.PostAsync($"/api/stations/{stationId}/stop", null)).StatusCode);
        Assert.Equal(22, f.Commands.Last<RemoteStopCommand>().TransactionId);
    }

    [Fact]
    public async Task App_start_is_per_connector()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync(Id);

        var first = await owner.PostAsJsonAsync($"/api/stations/{stationId}/start", new StartChargingRequest(1));
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        var busy = await owner.PostAsJsonAsync($"/api/stations/{stationId}/start", new StartChargingRequest(1));
        Assert.Equal(HttpStatusCode.Conflict, busy.StatusCode);

        var second = await owner.PostAsJsonAsync($"/api/stations/{stationId}/start", new StartChargingRequest(2));
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(2, f.Commands.Last<RemoteStartCommand>().ConnectorId);

        // StartTransaction on connector 2 confirms the connector-2 session, not the connector-1 one
        await Started(f, 2, 5, 1_000m);
        var open = await owner.GetFromJsonAsync<List<SessionDto>>($"/api/stations/{stationId}/sessions/open");
        Assert.Equal("Pending", open!.Single(s => s.ConnectorId == 1).Status);
        Assert.Equal("Active",  open!.Single(s => s.ConnectorId == 2).Status);
        Assert.Equal(2, (await owner.GetFromJsonAsync<SessionDto>($"/api/stations/{stationId}/session?connectorId=2"))!.ConnectorId);
    }

    [Fact]
    public async Task Power_loss_resumes_only_the_connector_whose_car_is_still_plugged()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync(Id);
        await Started(f, 1, 1, 100m);
        await Started(f, 2, 2, 5_000m);
        f.Clock.Advance(TimeSpan.FromMinutes(5));
        await Meter(f, 1, 1, 1_100m, 100m, 7, 50m);
        await Meter(f, 2, 2, 6_000m, 5_000m, 7, 60m);

        await Status(f, 0, "Offline", connected: false);
        var paused = await owner.GetFromJsonAsync<List<SessionDto>>($"/api/stations/{stationId}/sessions/open");
        Assert.All(paused!, s => Assert.Equal("Paused", s.Status));

        // Power back: car 1 still plugged (Finishing), car 2 was unplugged (Available)
        f.Clock.Advance(TimeSpan.FromHours(1));
        await Status(f, 1, "Finishing");
        await Status(f, 2, "Available");
        await Stopped(f, 1, 1, 1_100m, 100m, "PowerLoss");
        await Stopped(f, 2, 2, 6_000m, 5_000m, "PowerLoss");

        var resume = Assert.Single(f.Commands.Sent.OfType<RemoteStartCommand>());
        Assert.Equal(1, resume.ConnectorId);

        var open = await owner.GetFromJsonAsync<List<SessionDto>>($"/api/stations/{stationId}/sessions/open");
        var s1 = Assert.Single(open!);
        Assert.Equal(1, s1.ConnectorId);
        Assert.Equal("Paused", s1.Status);       // waiting for the resumed transaction
        Assert.Equal(1.000m, s1.EnergyKwh);

        var history = await owner.GetFromJsonAsync<List<SessionDto>>($"/api/stations/{stationId}/sessions");
        var s2 = history!.Single(s => s.ConnectorId == 2);
        Assert.Equal("Completed", s2.Status);
        Assert.Equal("PowerLoss", s2.StopReason);
    }

    [Fact]
    public async Task Events_without_connector_id_from_older_evocpp_go_to_connector_1()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync(Id);

        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new { ocppId = Id, transactionId = 7, meterStartWh = 10m });
        await f.PublishEventAsync(OcppRoutingKeys.MeterUpdated, new { ocppId = Id, meterValueWh = 1_010m, meterStartWh = 10m, currentPowerKw = 3.5, soc = (decimal?)null });

        var s = await owner.GetFromJsonAsync<SessionDto>($"/api/stations/{stationId}/session");
        Assert.Equal(1, s!.ConnectorId);
        Assert.Equal(1.000m, s.EnergyKwh);
    }
}
