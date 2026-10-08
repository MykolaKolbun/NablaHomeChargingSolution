using Newtonsoft.Json.Linq;

namespace OCPPServer.ChargerAdapters;

public interface IChargerAdapter
{
    /// <summary>Human-readable meter type, e.g. "Internal", "External MID".</summary>
    string MeterType { get; }

    /// <summary>
    /// Called on BootNotification so the adapter can read vendor-specific fields
    /// (e.g. Wallbox's proprietary "meterType" key) and update its state.
    /// </summary>
    void OnBootNotification(JObject payload);

    /// <summary>Parses a meterValue JArray into normalised energy/SoC/power readings.</summary>
    MeterValueParser.Readings ParseMeterValues(JArray? meterValues);
}
