using System.Net;
using System.Net.Http.Json;
using EVHomeAPI.DTOs;
using EVHomeAPI.Hubs;
using EVHomeAPI.Ocpp;
using EVHomeAPI.Tests.Infrastructure;

namespace EVHomeAPI.Tests;

public class ChargingLimitTests
{
    private static Task<HttpResponseMessage> SetLimit(HttpClient c, int stationId, decimal? limitA) =>
        c.PutAsJsonAsync($"/api/stations/{stationId}/limit", new SetLimitRequest(limitA));

    [Fact]
    public async Task Set_limit_publishes_command_and_applies_on_accept()
    {
        using var f = new ApiFactory();
        var (owner, id) = await f.CreateOwnedStationAsync();

        var res = await SetLimit(owner, id, 16);
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var dto = (await res.Content.ReadFromJsonAsync<StationDto>())!;
        Assert.Equal(16m, dto.CurrentLimitA);
        Assert.Equal("Pending", dto.LimitStatus);

        var cmd = f.Commands.Last<ChargingLimitCommand>();
        Assert.Equal(("30011", 16.0, (int?)null), (cmd.ocppId, cmd.limitA, cmd.transactionId));

        await f.PublishEventAsync(OcppRoutingKeys.ChargingLimitResponse, new ChargingLimitResponseEvent("30011", 16, "Accepted", null));

        var st = await owner.GetFromJsonAsync<StationDto>($"/api/stations/{id}");
        Assert.Equal("Applied", st!.LimitStatus);
        var msg = Assert.Single(f.Notifier.Of<ChargingLimitUpdatedMsg>());
        Assert.Equal((id, (decimal?)16m, "Applied"), (msg.StationId, msg.LimitA, msg.Status));
    }

    [Fact]
    public async Task Running_session_transaction_is_targeted()
    {
        using var f = new ApiFactory();
        var (owner, id) = await f.CreateOwnedStationAsync();
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent("30011", 7, 0m));

        await SetLimit(owner, id, 10);
        Assert.Equal(7, f.Commands.Last<ChargingLimitCommand>().transactionId);
    }

    [Theory]
    [InlineData(5.9)]
    [InlineData(16.1)]   // above MaxCurrentA = 16
    public async Task Out_of_range_is_400(double limit)
    {
        using var f = new ApiFactory();
        var created = await f.CreateAdminClient().PostAsJsonAsync("/api/admin/stations", new CreateStationRequest("HOME-16", "16A", MaxCurrentA: 16));
        var station = (await created.Content.ReadFromJsonAsync<CreateStationResponse>())!;
        var owner   = await f.CreateUserClientAsync();
        await owner.PostAsJsonAsync("/api/stations/claim", new ClaimStationRequest("HOME-16", station.ClaimCode));
        await f.PublishEventAsync(OcppRoutingKeys.StatusChanged, new StatusChangedEvent("HOME-16", "Available", 1, true, null));

        Assert.Equal(HttpStatusCode.BadRequest, (await SetLimit(owner, station.Id, (decimal)limit)).StatusCode);
        // Only the claim-time cap at MaxCurrentA (16 A) was sent — never the invalid value.
        Assert.Equal([16.0], f.Commands.Sent.OfType<ChargingLimitCommand>().Select(c => c.limitA));
    }

    [Fact]
    public async Task No_limit_means_installation_maximum_never_clear()
    {
        using var f = new ApiFactory();
        var (owner, id) = await f.CreateOwnedStationAsync();   // MaxCurrentA default 32
        await SetLimit(owner, id, 13);

        var res = await SetLimit(owner, id, null);
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        Assert.Empty(f.Commands.Sent.OfType<ChargingClearCommand>());
        Assert.Equal(32.0, f.Commands.Last<ChargingLimitCommand>().limitA);

        await f.PublishEventAsync(OcppRoutingKeys.ChargingLimitResponse, new ChargingLimitResponseEvent("30011", 32, "Accepted", null));
        var st = await owner.GetFromJsonAsync<StationDto>($"/api/stations/{id}");
        Assert.Null(st!.CurrentLimitA);
        Assert.Equal("Applied", st.LimitStatus);
    }

    [Fact]
    public async Task Claim_caps_charger_at_installation_maximum()
    {
        using var f = new ApiFactory();
        var created = await f.CreateAdminClient().PostAsJsonAsync("/api/admin/stations", new CreateStationRequest("03012", "Wallbox", MaxCurrentA: 16));
        var station = (await created.Content.ReadFromJsonAsync<CreateStationResponse>())!;
        var owner   = await f.CreateUserClientAsync();

        await owner.PostAsJsonAsync("/api/stations/claim", new ClaimStationRequest("03012", station.ClaimCode));

        var cmd = f.Commands.Last<ChargingLimitCommand>();
        Assert.Equal(("03012", 16.0), (cmd.ocppId, cmd.limitA));
    }

    [Fact]
    public async Task Admin_lowering_max_current_lowers_user_limit_and_pushes_it()
    {
        using var f = new ApiFactory();
        var (owner, id) = await f.CreateOwnedStationAsync();
        await SetLimit(owner, id, 20);
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent("30011", 5, 0m));

        var res = await f.CreateAdminClient().PutAsJsonAsync("/api/admin/stations/30011/max-current", new SetMaxCurrentRequest(16));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var cmd = f.Commands.Last<ChargingLimitCommand>();
        Assert.Equal((16.0, (int?)5), (cmd.limitA, cmd.transactionId));   // running session limited at once
        var st = await owner.GetFromJsonAsync<StationDto>($"/api/stations/{id}");
        Assert.Equal((16, (decimal?)16m, "Pending"), (st!.MaxCurrentA, st.CurrentLimitA, st.LimitStatus));
    }

    [Fact]
    public async Task Admin_max_current_requires_key_and_range()
    {
        using var f = new ApiFactory();
        await f.CreateStationAsync("30011");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await f.CreateClient().PutAsJsonAsync("/api/admin/stations/30011/max-current", new SetMaxCurrentRequest(16))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await f.CreateAdminClient().PutAsJsonAsync("/api/admin/stations/30011/max-current", new SetMaxCurrentRequest(5))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await f.CreateAdminClient().PutAsJsonAsync("/api/admin/stations/NOPE/max-current", new SetMaxCurrentRequest(16))).StatusCode);
    }

    [Fact]
    public async Task Clear_answer_is_ignored()
    {
        using var f = new ApiFactory();
        var (owner, id) = await f.CreateOwnedStationAsync();
        await SetLimit(owner, id, 16);

        await f.PublishEventAsync(OcppRoutingKeys.ChargingLimitResponse, new ChargingLimitResponseEvent("30011", null, "Accepted", "Accepted"));
        Assert.Equal("Pending", (await owner.GetFromJsonAsync<StationDto>($"/api/stations/{id}"))!.LimitStatus);
    }

    [Fact]
    public async Task Rejected_is_reported()
    {
        using var f = new ApiFactory();
        var (owner, id) = await f.CreateOwnedStationAsync();
        await SetLimit(owner, id, 16);

        await f.PublishEventAsync(OcppRoutingKeys.ChargingLimitResponse, new ChargingLimitResponseEvent("30011", 16, "Rejected", null));
        Assert.Equal("Rejected", (await owner.GetFromJsonAsync<StationDto>($"/api/stations/{id}"))!.LimitStatus);
    }

    [Fact]
    public async Task Response_for_superseded_request_is_ignored()
    {
        using var f = new ApiFactory();
        var (owner, id) = await f.CreateOwnedStationAsync();
        await SetLimit(owner, id, 16);
        await SetLimit(owner, id, 10);   // changed before the charger answered the first

        await f.PublishEventAsync(OcppRoutingKeys.ChargingLimitResponse, new ChargingLimitResponseEvent("30011", 16, "Accepted", null));
        var st = await owner.GetFromJsonAsync<StationDto>($"/api/stations/{id}");
        Assert.Equal(10m, st!.CurrentLimitA);
        Assert.Equal("Pending", st.LimitStatus);
        Assert.Empty(f.Notifier.Of<ChargingLimitUpdatedMsg>());
    }

    [Fact]
    public async Task Offline_station_is_409_and_stranger_is_404()
    {
        using var f = new ApiFactory();
        var (owner, id) = await f.CreateOwnedStationAsync(online: false);
        Assert.Equal(HttpStatusCode.Conflict, (await SetLimit(owner, id, 16)).StatusCode);

        var stranger = await f.CreateUserClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await SetLimit(stranger, id, 16)).StatusCode);
    }

    [Fact]
    public async Task Broker_down_is_503_with_error_status()
    {
        using var f = new ApiFactory();
        var (owner, id) = await f.CreateOwnedStationAsync();
        f.Commands.Fail = true;

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await SetLimit(owner, id, 16)).StatusCode);
        f.Commands.Fail = false;
        Assert.Equal("Error", (await owner.GetFromJsonAsync<StationDto>($"/api/stations/{id}"))!.LimitStatus);
    }

    [Fact]
    public void Limit_commands_use_evocpp_camelcase()
    {
        Assert.Equal("{\"ocppId\":\"03012\",\"limitA\":16,\"transactionId\":1}",
            System.Text.Json.JsonSerializer.Serialize(new ChargingLimitCommand("03012", 16, 1)));
        Assert.Equal("{\"ocppId\":\"03012\"}",
            System.Text.Json.JsonSerializer.Serialize(new ChargingClearCommand("03012")));
    }
}
