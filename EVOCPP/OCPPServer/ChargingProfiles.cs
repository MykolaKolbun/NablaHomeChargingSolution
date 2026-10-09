using Newtonsoft.Json.Linq;

namespace OCPPServer;

/// <summary>
/// OCPP 1.6 Smart Charging payloads for a current limit (amps).
///
/// Two profiles, fixed ids so a new limit replaces the old one (same id + purpose + stack):
///   • TxDefaultProfile (connector 0)  — applies to every future transaction; persisted by
///     the charger, survives reboots and works without the backend.
///   • TxProfile (connector N + transactionId) — applies the limit to the running session at once
///     (some chargers only re-evaluate TxDefaultProfile when a transaction starts).
///
/// Kind "Relative" needs no clock sync. If a charger rejects it, callers retry with
/// <see cref="Absolute"/> (startSchedule = now, open-ended).
/// </summary>
public static class ChargingProfiles
{
    public const int TxDefaultProfileId = 1001;
    public const int TxProfileId        = 1002;

    /// <summary>IEC 61851: below 6 A the car must stop charging.</summary>
    public const double MinLimitA = 6;
    public const double MaxLimitA = 80;

    public static JObject TxDefault(double limitA, bool absolute = false, DateTime? nowUtc = null) =>
        SetChargingProfile(connectorId: 0, TxDefaultProfileId, "TxDefaultProfile", limitA, transactionId: null, absolute, nowUtc);

    public static JObject Tx(int connectorId, int transactionId, double limitA, bool absolute = false, DateTime? nowUtc = null) =>
        SetChargingProfile(connectorId, TxProfileId, "TxProfile", limitA, transactionId, absolute, nowUtc);

    /// <summary>ClearChargingProfile.req by purpose (all connectors).</summary>
    public static JObject Clear(string purpose) => new() { ["chargingProfilePurpose"] = purpose };

    public static bool IsValidLimit(double limitA) => limitA >= MinLimitA && limitA <= MaxLimitA;

    private static JObject SetChargingProfile(int connectorId, int profileId, string purpose, double limitA,
        int? transactionId, bool absolute, DateTime? nowUtc)
    {
        var schedule = new JObject
        {
            ["chargingRateUnit"] = "A",
            ["chargingSchedulePeriod"] = new JArray
            {
                new JObject { ["startPeriod"] = 0, ["limit"] = Math.Round(limitA, 1) },
            },
        };
        if (absolute)
            schedule["startSchedule"] = (nowUtc ?? DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ");

        var profile = new JObject
        {
            ["chargingProfileId"]      = profileId,
            ["stackLevel"]             = 0,
            ["chargingProfilePurpose"] = purpose,
            ["chargingProfileKind"]    = absolute ? "Absolute" : "Relative",
            ["chargingSchedule"]       = schedule,
        };
        if (transactionId.HasValue) profile["transactionId"] = transactionId.Value;

        return new JObject { ["connectorId"] = connectorId, ["csChargingProfiles"] = profile };
    }
}
