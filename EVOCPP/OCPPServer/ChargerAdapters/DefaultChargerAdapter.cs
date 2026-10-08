using Newtonsoft.Json.Linq;

namespace OCPPServer.ChargerAdapters;

public sealed class DefaultChargerAdapter : IChargerAdapter
{
    public string MeterType => "Internal";

    public void OnBootNotification(JObject payload) { }

    public MeterValueParser.Readings ParseMeterValues(JArray? meterValues)
        => MeterValueParser.Parse(meterValues);
}
