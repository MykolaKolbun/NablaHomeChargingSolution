namespace EVHomeAPI.Ocpp;

// Contract with EVOCPP — see EVOCPP/docs/messaging-api-spec.md.
//
// Events (exchange ocpp.events) are camelCase JSON → deserialized with JsonSerializerDefaults.Web.
// Commands (exchange ocpp.commands) are deserialized by EVOCPP with case-SENSITIVE default
// System.Text.Json → they MUST be serialized with PascalCase property names (default options).

// ── Events: EVOCPP → EVHomeAPI ────────────────────────────────────────────────

public record StatusChangedEvent(string OcppId, string Status, int ConnectorId, bool IsConnected, string? CarId);

public record AuthorizeRequestedEvent(string OcppId, string CarId, int? ConnectorId);

public record TransactionStartedEvent(string OcppId, int TransactionId, decimal? MeterStartWh);

public record TransactionStoppedEvent(string OcppId, int TransactionId, decimal MeterStopWh, decimal? MeterStartWh);

public record MeterUpdatedEvent(string OcppId, decimal MeterValueWh, decimal? MeterStartWh, double? CurrentPowerKw, decimal? Soc);

/// <summary>Status: Accepted (StartTransaction received) | Rejected | Timeout.</summary>
public record RemoteStartResponseEvent(string OcppId, string Status, int ConnectorId, string IdTag, Guid? TrackingId);

/// <summary>Status: Accepted (StopTransaction received) | Rejected | Timeout.</summary>
public record RemoteStopResponseEvent(string OcppId, string Status, int? TransactionId);

public static class OcppRoutingKeys
{
    public const string StatusChanged       = "charger.status.changed";
    public const string AuthorizeRequested  = "charger.authorize.requested";
    public const string TransactionStarted  = "charger.transaction.started";
    public const string TransactionStopped  = "charger.transaction.stopped";
    public const string MeterUpdated        = "charger.meter.updated";
    public const string RemoteStartResponse = "charger.remote.start.response";
    public const string RemoteStopResponse  = "charger.remote.stop.response";

    public const string RemoteStart       = "command.remote.start";
    public const string RemoteStop        = "command.remote.stop";
    public const string AuthorizeResponse = "command.authorize.response";
    public const string StatusRequest     = "command.statusreq";
}

// ── Commands: EVHomeAPI → EVOCPP (PascalCase on the wire) ─────────────────────

public record RemoteStartCommand(string OcppId, int ConnectorId, string IdTag, Guid TrackingId);

public record RemoteStopCommand(string OcppId, int? TransactionId);

/// <summary>
/// Exception to the PascalCase rule: EVOCPP reads this command by hand with exact
/// camelCase names (TryGetProperty("ocppId") / ("status")) — see OcppCommandHandler.
/// </summary>
public record AuthorizeResponseCommand(string ocppId, string status);

/// <summary>
/// TriggerMessage(StatusNotification). Also read by EVOCPP with exact camelCase names.
/// connectorId null = all connectors. Discarded by EVOCPP when the charger is offline.
/// </summary>
public record StatusRequestCommand(string ocppId, int? connectorId);
