using System.Collections.Concurrent;
using System.Text.Json;

namespace EVHomeAPI.Ocpp;

public class DevToolsOptions
{
    public const string Section = "DevTools";
    /// <summary>Dev endpoints (/api/stations/{id}/dev/*) exist only when true.</summary>
    public bool Enabled            { get; set; }
    public int  CallTimeoutSeconds { get; set; } = 35;   // > EVOCPP Ocpp:CommandTimeoutSeconds (30)
    /// <summary>EVOCPP inside the compose network, for the charger info proxy.</summary>
    public string EvocppBaseUrl    { get; set; } = "http://evocpp:8080";
}

/// <summary>Mirrors EVOCPP DevCalls.AllowedActions — rejected early with 400 instead of a round trip.</summary>
public static class DevCallActions
{
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        "GetConfiguration", "ChangeConfiguration", "TriggerMessage", "DataTransfer", "Reset",
        "UnlockConnector", "ClearCache", "GetCompositeSchedule", "GetLocalListVersion",
    };
}

/// <summary>command.dev.call (camelCase, read by name in EVOCPP).</summary>
public record DevCallCommand(string ocppId, string requestId, string action, JsonElement? payload);

/// <summary>charger.dev.call.response. Status: Ok | Invalid | NotConnected | NotSupported | Timeout | Error.</summary>
public record DevCallResponseEvent(string OcppId, string RequestId, string Action, string Status, JsonElement? Result);

/// <summary>
/// Correlates dev-call requests with their RabbitMQ responses (singleton, in-memory —
/// one API instance; a lost response simply times out).
/// </summary>
public sealed class DevCallRegistry
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DevCallResponseEvent>> _pending = new();

    public Task<DevCallResponseEvent> Register(string requestId)
    {
        var tcs = new TaskCompletionSource<DevCallResponseEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[requestId] = tcs;
        return tcs.Task;
    }

    public void Complete(DevCallResponseEvent e)
    {
        if (_pending.TryRemove(e.RequestId, out var tcs)) tcs.TrySetResult(e);
    }

    public void Forget(string requestId) => _pending.TryRemove(requestId, out _);
}
