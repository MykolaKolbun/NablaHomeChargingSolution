using Newtonsoft.Json.Linq;

namespace OCPPServer.ChargerAdapters;

/// <summary>
/// Adapter for Wallbox AC chargers (Copper SB, Pulsar Plus, Commander 2, etc.).
///
/// Wallbox sends a proprietary "meterType" key in BootNotification:
///   "Internal NON compliant" — charger's built-in meter (default)
///   "External MID"           — certified upstream meter; measures the whole circuit
///                              including the charger's self-consumption.
///
/// The OCPP measurand name (Energy.Active.Import.Register) is the same in both cases,
/// but the physical source differs. Future versions of this adapter can filter by
/// Location or apply self-consumption correction for External MID readings.
/// </summary>
public sealed class WallboxChargerAdapter : IChargerAdapter
{
    public string MeterType { get; private set; } = "Internal NON compliant";

    public void OnBootNotification(JObject payload)
    {
        var mt = payload["meterType"]?.Value<string>();
        if (!string.IsNullOrEmpty(mt))
            MeterType = mt;
    }

    public MeterValueParser.Readings ParseMeterValues(JArray? meterValues)
        => MeterValueParser.Parse(meterValues);
}
