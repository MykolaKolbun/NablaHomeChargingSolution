using Newtonsoft.Json.Linq;
using OCPPServer;
using Xunit;

namespace OCPPServer.Tests;

public class DevCallsTests
{
    [Theory]
    [InlineData("RemoteStartTransaction")]
    [InlineData("SetChargingProfile")]
    [InlineData("ClearChargingProfile")]
    [InlineData("")]
    [InlineData(null)]
    public void Actions_outside_whitelist_are_rejected(string? action) =>
        Assert.NotNull(DevCalls.Validate(action, new JObject()));

    [Fact]
    public void GetConfiguration_needs_no_payload() =>
        Assert.Null(DevCalls.Validate("GetConfiguration", null));

    [Theory]
    [InlineData("{\"key\":\"MeterValueSampleInterval\",\"value\":\"60\"}", true)]
    [InlineData("{\"key\":\"MeterValueSampleInterval\"}", false)]
    [InlineData("{\"value\":\"60\"}", false)]
    public void ChangeConfiguration_needs_key_and_value(string payload, bool ok) =>
        Assert.Equal(ok, DevCalls.Validate("ChangeConfiguration", JObject.Parse(payload)) is null);

    [Theory]
    [InlineData("{\"type\":\"Soft\"}", true)]
    [InlineData("{\"type\":\"Hard\"}", true)]
    [InlineData("{\"type\":\"Reboot\"}", false)]
    [InlineData("{}", false)]
    public void Reset_type_is_Soft_or_Hard(string payload, bool ok) =>
        Assert.Equal(ok, DevCalls.Validate("Reset", JObject.Parse(payload)) is null);

    [Theory]
    [InlineData("TriggerMessage", "{\"requestedMessage\":\"MeterValues\"}", true)]
    [InlineData("TriggerMessage", "{}", false)]
    [InlineData("DataTransfer", "{\"vendorId\":\"Nabla\",\"messageId\":\"CpReset\"}", true)]
    [InlineData("DataTransfer", "{\"messageId\":\"CpReset\"}", false)]
    [InlineData("UnlockConnector", "{\"connectorId\":1}", true)]
    [InlineData("UnlockConnector", "{\"connectorId\":\"1\"}", false)]
    [InlineData("GetCompositeSchedule", "{\"connectorId\":1,\"duration\":3600}", true)]
    [InlineData("GetCompositeSchedule", "{\"connectorId\":1}", false)]
    public void Required_fields(string action, string payload, bool ok) =>
        Assert.Equal(ok, DevCalls.Validate(action, JObject.Parse(payload)) is null);
}
