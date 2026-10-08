using System.Net.Http.Json;
using EVHomeAPI.DTOs;
using EVHomeAPI.Tests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace EVHomeAPI.Tests;

public class HubTests
{
    private static async Task<string> TokenAsync(ApiFactory f)
    {
        var res = await f.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Hub User", $"{Guid.NewGuid():N}@test.local", "password123"));
        return (await res.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }

    private static HubConnection Connect(ApiFactory f, string? token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(f.Server.BaseAddress, "/hubs/charger"), o =>
            {
                o.HttpMessageHandlerFactory = _ => f.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;   // TestServer has no WebSockets
                if (token is not null) o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    [Fact]
    public async Task Hub_requires_jwt()
    {
        using var f = new ApiFactory();
        await using var conn = Connect(f, token: null);
        await Assert.ThrowsAnyAsync<Exception>(() => conn.StartAsync());
    }

    [Fact]
    public async Task JoinStation_allowed_for_owner_denied_for_stranger()
    {
        using var f = new ApiFactory();
        var created = await f.CreateStationAsync("30011");
        var ownerToken = await TokenAsync(f);

        var owner = f.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new("Bearer", ownerToken);
        (await owner.PostAsJsonAsync("/api/stations/claim", new ClaimStationRequest("30011", created.ClaimCode))).EnsureSuccessStatusCode();

        await using var ownerConn = Connect(f, ownerToken);
        await ownerConn.StartAsync();
        await ownerConn.InvokeAsync("JoinStation", created.Id);   // no exception

        await using var strangerConn = Connect(f, await TokenAsync(f));
        await strangerConn.StartAsync();
        var ex = await Assert.ThrowsAsync<HubException>(() => strangerConn.InvokeAsync("JoinStation", created.Id));
        Assert.Contains("No access", ex.Message);
    }
}
