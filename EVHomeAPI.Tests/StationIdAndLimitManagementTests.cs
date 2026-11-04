using System.Net;
using System.Net.Http.Json;
using EVHomeAPI.DTOs;
using EVHomeAPI.Ocpp;
using EVHomeAPI.Services;
using EVHomeAPI.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Tests;

/// <summary>Per-connector ids with '#' (Wallbox Supernova), case normalisation, ManageLimit = false.</summary>
public class StationIdAndLimitManagementTests
{
    private static async Task<CreateStationResponse> CreateAsync(ApiFactory f, CreateStationRequest req)
    {
        var res = await f.CreateAdminClient().PostAsJsonAsync("/api/admin/stations", req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CreateStationResponse>())!;
    }

    [Fact]
    public async Task Supernova_connector_ids_are_stored_lower_case_and_match_evocpp_events()
    {
        using var f = new ApiFactory();
        var c1 = await CreateAsync(f, new CreateStationRequest("U005#1", "Supernova CCS"));
        var c2 = await CreateAsync(f, new CreateStationRequest("U005#2", "Supernova CHAdeMO"));
        Assert.Equal(("u005#1", "u005#2"), (c1.OcppId, c2.OcppId));

        // Claim with the id as printed on the charger (upper case)
        var owner = await f.CreateUserClientAsync();
        (await owner.PostAsJsonAsync("/api/stations/claim", new ClaimStationRequest("U005#1", c1.ClaimCode))).EnsureSuccessStatusCode();

        // EVOCPP lower-cases the WebSocket id
        await f.PublishEventAsync(OcppRoutingKeys.StatusChanged, new StatusChangedEvent("u005#1", "Preparing", 1, true, null));
        var st = await owner.GetFromJsonAsync<StationDto>($"/api/stations/{c1.Id}");
        Assert.True(st!.IsOnline);
        Assert.Equal("Preparing", st.ConnectorStatus);

        // Commands go out lower-case (EVOCPP's connection key)
        await owner.PostAsJsonAsync($"/api/stations/{c1.Id}/start", new StartChargingRequest());
        Assert.Equal("u005#1", f.Commands.Last<RemoteStartCommand>().OcppId);

        // Admin routes accept any case; '#' must be URL-encoded
        var reset = await f.CreateAdminClient().PostAsync("/api/admin/stations/U005%232/claim-code", null);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
    }

    [Fact]
    public async Task Duplicate_id_differing_only_in_case_is_409()
    {
        using var f = new ApiFactory();
        await CreateAsync(f, new CreateStationRequest("ABC1", "One"));
        var res = await f.CreateAdminClient().PostAsJsonAsync("/api/admin/stations", new CreateStationRequest("abc1", "Two"));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Theory]
    [InlineData("U005 1")]
    [InlineData("U005/1")]
    [InlineData("U005?1")]
    public async Task Ids_with_url_unsafe_characters_are_rejected(string id)
    {
        using var f = new ApiFactory();
        var res = await f.CreateAdminClient().PostAsJsonAsync("/api/admin/stations", new CreateStationRequest(id, "x"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Unmanaged_station_never_gets_a_charging_profile()
    {
        using var f = new ApiFactory();
        var created = await CreateAsync(f, new CreateStationRequest("U005#1", "Supernova", ManageLimit: false));
        var owner   = await f.CreateUserClientAsync();
        (await owner.PostAsJsonAsync("/api/stations/claim", new ClaimStationRequest("u005#1", created.ClaimCode))).EnsureSuccessStatusCode();
        await f.PublishEventAsync(OcppRoutingKeys.StatusChanged, new StatusChangedEvent("u005#1", "Available", 1, true, null));
        await f.PublishEventAsync(OcppRoutingKeys.Booted, new ChargerBootedEvent("u005#1"));

        Assert.Empty(f.Commands.Sent.OfType<ChargingLimitCommand>());   // not on claim, not on boot
        var st = await owner.GetFromJsonAsync<StationDto>($"/api/stations/{created.Id}");
        Assert.False(st!.ManageLimit);
        Assert.Equal(ChargingLimitService.Unmanaged, st.LimitStatus);

        var set = await owner.PutAsJsonAsync($"/api/stations/{created.Id}/limit", new SetLimitRequest(16));
        Assert.Equal(HttpStatusCode.Conflict, set.StatusCode);
        Assert.Empty(f.Commands.Sent.OfType<ChargingLimitCommand>());
    }

    [Fact]
    public async Task Limit_management_can_be_switched_off_and_on()
    {
        using var f = new ApiFactory();
        var (owner, id) = await f.CreateOwnedStationAsync("03012");
        var admin  = f.CreateAdminClient();
        var before = f.Commands.Sent.OfType<ChargingLimitCommand>().Count();

        (await admin.PutAsJsonAsync("/api/admin/stations/03012/limit-management", new SetLimitManagementRequest(false))).EnsureSuccessStatusCode();
        await f.PublishEventAsync(OcppRoutingKeys.Booted, new ChargerBootedEvent("03012"));
        Assert.Equal(before, f.Commands.Sent.OfType<ChargingLimitCommand>().Count());

        (await admin.PutAsJsonAsync("/api/admin/stations/03012/limit-management", new SetLimitManagementRequest(true))).EnsureSuccessStatusCode();
        Assert.Equal(before + 1, f.Commands.Sent.OfType<ChargingLimitCommand>().Count());   // pushed right away
        Assert.Equal("Pending", (await owner.GetFromJsonAsync<StationDto>($"/api/stations/{id}"))!.LimitStatus);

        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.PutAsJsonAsync("/api/admin/stations/nope/limit-management", new SetLimitManagementRequest(false))).StatusCode);
    }

    [Fact]
    public async Task Stations_created_by_default_are_managed()
    {
        using var f = new ApiFactory();
        await f.CreateStationAsync("03012");
        await f.WithDbAsync(async db => Assert.True((await db.Stations.SingleAsync()).ManageLimit));
    }
}
