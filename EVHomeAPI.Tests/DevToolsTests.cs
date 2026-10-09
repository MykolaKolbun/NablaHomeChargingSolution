using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EVHomeAPI.Controllers;
using EVHomeAPI.Ocpp;
using EVHomeAPI.Tests.Infrastructure;

namespace EVHomeAPI.Tests;

public class DevToolsTests
{
    private static Task<HttpResponseMessage> Call(HttpClient c, int stationId, string action, object? payload = null) =>
        c.PostAsJsonAsync($"/api/stations/{stationId}/dev/call",
            new { action, payload });

    /// <summary>Waits until the API published the dev call, then answers it like EVOCPP would.</summary>
    private static async Task AnswerAsync(ApiFactory f, string status, string resultJson)
    {
        for (var i = 0; i < 100 && !f.Commands.Sent.OfType<DevCallCommand>().Any(); i++) await Task.Delay(20);
        var cmd = f.Commands.Last<DevCallCommand>();
        await f.PublishEventAsync(OcppRoutingKeys.DevCallResponse,
            new DevCallResponseEvent(cmd.ocppId, cmd.requestId, cmd.action, status, JsonDocument.Parse(resultJson).RootElement));
    }

    [Fact]
    public async Task Call_round_trip_returns_raw_charger_answer()
    {
        using var f = new ApiFactory();
        var (owner, id) = await f.CreateOwnedStationAsync();

        var request = Call(owner, id, "GetConfiguration", new { key = new[] { "MeterValueSampleInterval" } });
        await AnswerAsync(f, "Ok", "{\"configurationKey\":[{\"key\":\"MeterValueSampleInterval\",\"readonly\":false,\"value\":\"60\"}]}");
        var res = await request;

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = (await res.Content.ReadFromJsonAsync<DevCallResult>())!;
        Assert.Equal(("GetConfiguration", "Ok"), (body.Action, body.Status));
        Assert.Equal("60", body.Result!.Value.GetProperty("configurationKey")[0].GetProperty("value").GetString());

        var cmd = f.Commands.Last<DevCallCommand>();
        Assert.Equal("30011", cmd.ocppId);
        Assert.Equal("MeterValueSampleInterval", cmd.payload!.Value.GetProperty("key")[0].GetString());
    }

    [Fact]
    public async Task Charger_side_failure_status_is_passed_through()
    {
        using var f = new ApiFactory();
        var (owner, id) = await f.CreateOwnedStationAsync();

        var request = Call(owner, id, "DataTransfer", new { vendorId = "Nabla", messageId = "CpReset" });
        await AnswerAsync(f, "Ok", "{\"status\":\"UnknownVendorId\"}");
        var body = (await (await request).Content.ReadFromJsonAsync<DevCallResult>())!;

        Assert.Equal("UnknownVendorId", body.Result!.Value.GetProperty("status").GetString());
    }

    [Fact]
    public async Task No_answer_is_504_timeout()
    {
        using var f = new ApiFactory();
        var (owner, id) = await f.CreateOwnedStationAsync();

        var res = await Call(owner, id, "TriggerMessage", new { requestedMessage = "Heartbeat" });
        Assert.Equal(HttpStatusCode.GatewayTimeout, res.StatusCode);
        Assert.Equal("Timeout", (await res.Content.ReadFromJsonAsync<DevCallResult>())!.Status);
    }

    [Theory]
    [InlineData("RemoteStartTransaction")]
    [InlineData("SetChargingProfile")]
    public async Task Non_whitelisted_action_is_400_and_not_sent(string action)
    {
        using var f = new ApiFactory();
        var (owner, id) = await f.CreateOwnedStationAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await Call(owner, id, action)).StatusCode);
        Assert.Empty(f.Commands.Sent.OfType<DevCallCommand>());
    }

    [Fact]
    public async Task Disabled_dev_tools_are_404()
    {
        using var f = new ApiFactory();
        f.DevTools.Enabled = false;
        var (owner, id) = await f.CreateOwnedStationAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await Call(owner, id, "GetConfiguration")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/stations/{id}/dev/charger")).StatusCode);
    }

    [Fact]
    public async Task Stranger_gets_404()
    {
        using var f = new ApiFactory();
        var (_, id) = await f.CreateOwnedStationAsync();
        var stranger = await f.CreateUserClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await Call(stranger, id, "GetConfiguration")).StatusCode);
        Assert.Empty(f.Commands.Sent.OfType<DevCallCommand>());
    }

    [Fact]
    public async Task Charger_info_is_proxied_from_evocpp()
    {
        using var f = new ApiFactory();
        var (owner, id) = await f.CreateOwnedStationAsync();

        var res = await owner.GetAsync($"/api/stations/{id}/dev/charger");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("/api/admin/plugs/30011", f.Evocpp.LastPath);
        Assert.Contains("6.7.38", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public void Dev_call_command_uses_evocpp_camelcase()
    {
        var json = JsonSerializer.Serialize(new DevCallCommand("03012", "r1", "Reset", JsonDocument.Parse("{\"type\":\"Soft\"}").RootElement));
        Assert.Equal("{\"ocppId\":\"03012\",\"requestId\":\"r1\",\"action\":\"Reset\",\"payload\":{\"type\":\"Soft\"}}", json);
    }
}
