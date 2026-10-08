using Newtonsoft.Json.Linq;

namespace OCPPServer;

/// <summary>
/// Extracts energy, SoC, and power from a meterValue array that appears in both
/// OCPP 1.6 MeterValues and OCPP 2.x TransactionEvent / MeterValues payloads.
///
/// Handles both unit encodings:
///   OCPP 1.6  — unit is a plain string:  "Wh", "kWh", "W", "kW", "Percent"
///   OCPP 2.x  — unit is an object:       { "name": "Wh" }
/// </summary>
public static class MeterValueParser
{
    public record Readings(decimal? EnergyWh, decimal? SoC, double? PowerKw);

    public static Readings Parse(JArray? meterValues)
    {
        if (meterValues == null) return new Readings(null, null, null);

        var lastEntry     = meterValues.LastOrDefault();
        var sampledValues = lastEntry?["sampledValue"] as JArray;
        if (sampledValues == null) return new Readings(null, null, null);

        decimal? energyWh = null;
        decimal? soc      = null;
        double?  powerKw  = null;

        foreach (JToken sv in sampledValues)
        {
            // Default measurand per OCPP 1.6 spec is Energy.Active.Import.Register
            var measurand = sv["measurand"]?.Value<string>() ?? "Energy.Active.Import.Register";
            var valueStr  = sv["value"]?.Value<string>() ?? "";
            var unit      = GetUnit(sv);

            if (!decimal.TryParse(valueStr,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var val))
                continue;

            if (measurand.StartsWith("Energy") && energyWh == null)
            {
                energyWh = unit.Equals("kWh", StringComparison.OrdinalIgnoreCase)
                    ? val * 1000m
                    : val; // assume Wh
            }
            else if (measurand == "SoC" && soc == null)
            {
                // SoC is always a percentage 0–100
                soc = val;
            }
            else if (measurand == "Power.Active.Import" && powerKw == null)
            {
                powerKw = unit.Equals("kW", StringComparison.OrdinalIgnoreCase)
                    ? (double)val
                    : Math.Round((double)val / 1000.0, 2); // assume W
            }
        }

        return new Readings(energyWh, soc, powerKw);
    }

    // OCPP 1.6: unit is a plain string  →  sv["unit"] = "Wh"
    // OCPP 2.x: unit is an object       →  sv["unit"] = { "name": "Wh" }
    private static string GetUnit(JToken sv)
    {
        var u = sv["unit"];
        if (u == null) return "Wh";
        return u.Type == JTokenType.Object
            ? u["name"]?.Value<string>() ?? "Wh"
            : u.Value<string>() ?? "Wh";
    }
}
