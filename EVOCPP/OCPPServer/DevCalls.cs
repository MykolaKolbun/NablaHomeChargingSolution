using Newtonsoft.Json.Linq;

namespace OCPPServer;

/// <summary>
/// Developer tooling: raw OCPP 1.6 calls requested by EVHomeAPI's dev endpoints
/// (command.dev.call → charger.dev.call.response). Only whitelisted actions — no
/// RemoteStart/Stop or charging profiles here, those have their own validated commands.
/// </summary>
public static class DevCalls
{
    public static readonly IReadOnlySet<string> AllowedActions = new HashSet<string>(StringComparer.Ordinal)
    {
        "GetConfiguration",
        "ChangeConfiguration",
        "TriggerMessage",
        "DataTransfer",
        "Reset",
        "UnlockConnector",
        "ClearCache",
        "GetCompositeSchedule",
        "GetLocalListVersion",
    };

    /// <summary>Validates the action and the minimal payload shape. Returns null when OK, else the reason.</summary>
    public static string? Validate(string? action, JObject? payload)
    {
        if (string.IsNullOrEmpty(action) || !AllowedActions.Contains(action))
            return $"Action '{action}' is not allowed";
        payload ??= new JObject();

        return action switch
        {
            "ChangeConfiguration" when payload["key"]?.Type != JTokenType.String || payload["value"] is null
                => "ChangeConfiguration needs string 'key' and 'value'",
            "TriggerMessage" when payload["requestedMessage"]?.Type != JTokenType.String
                => "TriggerMessage needs 'requestedMessage'",
            "DataTransfer" when payload["vendorId"]?.Type != JTokenType.String
                => "DataTransfer needs 'vendorId'",
            "Reset" when (string?)payload["type"] is not ("Soft" or "Hard")
                => "Reset needs 'type' Soft or Hard",
            "UnlockConnector" when payload["connectorId"]?.Type != JTokenType.Integer
                => "UnlockConnector needs integer 'connectorId'",
            "GetCompositeSchedule" when payload["connectorId"]?.Type != JTokenType.Integer || payload["duration"]?.Type != JTokenType.Integer
                => "GetCompositeSchedule needs integer 'connectorId' and 'duration'",
            _ => null,
        };
    }
}
