using System.Net;
using System.Net.Http.Json;
using EVHomeAPI.DTOs;
using EVHomeAPI.Services;
using EVHomeAPI.Tests.Infrastructure;

namespace EVHomeAPI.Tests;

public class StationTests
{
    [Fact]
    public async Task Admin_endpoint_requires_key()
    {
        using var factory = new ApiFactory();
        var noKey = await factory.CreateClient().PostAsJsonAsync("/api/admin/stations", new CreateStationRequest("30011", "DIY"));
        Assert.Equal(HttpStatusCode.Unauthorized, noKey.StatusCode);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Key", "wrong-key");
        var wrongKey = await client.PostAsJsonAsync("/api/admin/stations", new CreateStationRequest("30011", "DIY"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongKey.StatusCode);
    }

    [Fact]
    public async Task Admin_create_duplicate_station_returns_409()
    {
        using var factory = new ApiFactory();
        await factory.CreateStationAsync("30011");
        var res = await factory.CreateAdminClient().PostAsJsonAsync("/api/admin/stations", new CreateStationRequest("30011", "Again"));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Claim_with_valid_code_makes_user_owner_and_lists_station()
    {
        using var factory = new ApiFactory();
        var created = await factory.CreateStationAsync("30011", "DIY Garage");
        var user    = await factory.CreateUserClientAsync();

        // Code is accepted lower-case and without the dash.
        var res = await user.PostAsJsonAsync("/api/stations/claim",
            new ClaimStationRequest("30011", created.ClaimCode.Replace("-", "").ToLowerInvariant()));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var claimed = await res.Content.ReadFromJsonAsync<StationDto>();
        Assert.Equal("Owner", claimed!.Role);

        var mine = await user.GetFromJsonAsync<List<StationDto>>("/api/stations");
        var station = Assert.Single(mine!);
        Assert.Equal("30011", station.OcppId);
        Assert.Equal("DIY Garage", station.Name);
        Assert.NotNull(station.ClaimedAt);

        var one = await user.GetAsync($"/api/stations/{station.Id}");
        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
    }

    [Fact]
    public async Task Claim_code_is_one_time()
    {
        using var factory = new ApiFactory();
        var created = await factory.CreateStationAsync("30011");
        var first   = await factory.CreateUserClientAsync();
        var second  = await factory.CreateUserClientAsync();

        Assert.Equal(HttpStatusCode.OK,
            (await first.PostAsJsonAsync("/api/stations/claim", new ClaimStationRequest("30011", created.ClaimCode))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await second.PostAsJsonAsync("/api/stations/claim", new ClaimStationRequest("30011", created.ClaimCode))).StatusCode);
    }

    [Fact]
    public async Task Claim_wrong_code_and_unknown_station_return_same_400()
    {
        using var factory = new ApiFactory();
        await factory.CreateStationAsync("30011");
        var user = await factory.CreateUserClientAsync();

        var wrongCode = await user.PostAsJsonAsync("/api/stations/claim", new ClaimStationRequest("30011", "AAAA-AAAA"));
        var unknown   = await user.PostAsJsonAsync("/api/stations/claim", new ClaimStationRequest("nope", "AAAA-AAAA"));

        Assert.Equal(HttpStatusCode.BadRequest, wrongCode.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(await wrongCode.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Station_of_another_user_is_404()
    {
        using var factory = new ApiFactory();
        var created = await factory.CreateStationAsync("30011");
        var owner   = await factory.CreateUserClientAsync();
        var other   = await factory.CreateUserClientAsync();
        await owner.PostAsJsonAsync("/api/stations/claim", new ClaimStationRequest("30011", created.ClaimCode));

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/stations/{created.Id}")).StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<List<StationDto>>("/api/stations"))!);
    }

    [Fact]
    public async Task Reset_claim_code_invalidates_old_code()
    {
        using var factory = new ApiFactory();
        var created = await factory.CreateStationAsync("30011");
        var reset   = await factory.CreateAdminClient().PostAsync("/api/admin/stations/30011/claim-code", null);
        var fresh   = (await reset.Content.ReadFromJsonAsync<CreateStationResponse>())!;
        var user    = await factory.CreateUserClientAsync();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await user.PostAsJsonAsync("/api/stations/claim", new ClaimStationRequest("30011", created.ClaimCode))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await user.PostAsJsonAsync("/api/stations/claim", new ClaimStationRequest("30011", fresh.ClaimCode))).StatusCode);
    }

    [Fact]
    public async Task Claim_is_rate_limited_per_client_ip()
    {
        using var factory = new ApiFactory(claimPerMinute: 3);
        await factory.CreateStationAsync("30011");
        var user = await factory.CreateUserClientAsync();
        user.DefaultRequestHeaders.Add("CF-Connecting-IP", "203.0.113.7");

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.BadRequest,
                (await user.PostAsJsonAsync("/api/stations/claim", new ClaimStationRequest("30011", "AAAA-AAAA"))).StatusCode);

        Assert.Equal((HttpStatusCode)429,
            (await user.PostAsJsonAsync("/api/stations/claim", new ClaimStationRequest("30011", "AAAA-AAAA"))).StatusCode);

        // A different client IP has its own bucket.
        user.DefaultRequestHeaders.Remove("CF-Connecting-IP");
        user.DefaultRequestHeaders.Add("CF-Connecting-IP", "203.0.113.8");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await user.PostAsJsonAsync("/api/stations/claim", new ClaimStationRequest("30011", "AAAA-AAAA"))).StatusCode);
    }

    [Fact]
    public async Task Stations_require_auth()
    {
        using var factory = new ApiFactory();
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/stations")).StatusCode);
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        using var factory = new ApiFactory();
        var res = await factory.CreateClient().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public void ClaimCode_format_and_normalize()
    {
        var code = ClaimCode.Generate();
        Assert.Matches("^[2-9A-HJKMNP-Z]{4}-[2-9A-HJKMNP-Z]{4}$", code);
        Assert.Equal("ABCD2345", ClaimCode.Normalize(" abcd-2345 "));
    }
}
