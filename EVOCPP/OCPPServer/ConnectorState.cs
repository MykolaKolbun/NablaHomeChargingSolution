using OCPPServer.ChargerAdapters;
using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace OCPPServer;

/// <summary>
/// In-memory live state for one connected charging station (one OCPP id = one WebSocket).
/// Created on WebSocket connect, removed on disconnect.
/// Never persisted — values are meaningless after a server restart.
///
/// A station can have several connectors (Wallbox Supernova: one OCPP id, connectorId 1 and 2,
/// each with its own DC meter). Everything that belongs to a charging session lives in
/// <see cref="ConnectorSession"/>, keyed by connectorId; only the connection itself, the vendor
/// adapter and the Authorize hand-shake are station-wide.
///
/// Stable charger metadata (vendor, model, firmware, OCPP version) lives in the <c>Plug</c> DB
/// entity; per-connector status and the MeterStartWh / transaction needed after a restart live
/// in <c>PlugConnector</c>.
/// </summary>
public sealed class ConnectorState
{
    public required WebSocket Socket { get; init; }
    public string Protocol { get; set; } = "ocpp1.6";

    /// <summary>
    /// UTC timestamp of the last OCPP message received from this charger.
    /// Updated on every incoming message (not just Heartbeat).
    /// Used by <see cref="ChargerWatcherService"/> to detect silently dead connections.
    /// </summary>
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    /// <summary>Vendor-specific behaviour strategy (meter parsing, proprietary field handling).</summary>
    public IChargerAdapter Adapter { get; set; } = new DefaultChargerAdapter();

    /// <summary>Per-connector live state, keyed by OCPP connectorId (OCPP 2.x: evseId). 0 is never stored.</summary>
    public ConcurrentDictionary<int, ConnectorSession> Connectors { get; } = new();

    /// <summary>Returns the connector, creating it on first use. connectorId must be &gt; 0.</summary>
    public ConnectorSession Connector(int connectorId)
        => Connectors.GetOrAdd(connectorId, id => new ConnectorSession { ConnectorId = id });

    /// <summary>The connector currently running local transaction <paramref name="localTxId"/>, or null.</summary>
    public ConnectorSession? ByTransaction(int localTxId)
        => Connectors.Values.FirstOrDefault(c => c.LocalTxId == localTxId);

    /// <summary>The connector currently running OCPP 2.x transaction <paramref name="ocppTxId"/>, or null.</summary>
    public ConnectorSession? ByOcppTransaction(string ocppTxId)
        => Connectors.Values.FirstOrDefault(c => c.OcppTxId == ocppTxId);

    // ── Vehicle identification (ISO 15118 / DIN 70121) ─────────────────────────
    /// <summary>
    /// EVCC ID from an OCPP 1.6 Authorize.req that could not be tied to a connector yet
    /// (Authorize carries no connectorId). It is bound on StartTransaction, or right away when
    /// exactly one connector is Preparing.
    /// </summary>
    public string? UnboundCarId { get; set; }

    // ── Pending command signals ────────────────────────────────────────────────
    /// <summary>
    /// Set while waiting for backend's command.authorize.response to an EVCC-ID Authorize.req.
    /// Station-wide: the response carries only ocppId, and the charger sends one Authorize at a time.
    /// </summary>
    public TaskCompletionSource<string>? PendingAuthorize { get; set; }

    /// <summary>RemoteStart with connectorId 0 (charger chooses): completed by the first StartTransaction.</summary>
    public TaskCompletionSource<int>? PendingStartAnyConnector { get; set; }

    /// <summary>RemoteStop waiters keyed by local transaction ID.</summary>
    public ConcurrentDictionary<int, TaskCompletionSource<int>> PendingStops { get; } = new();
}

/// <summary>Live state of one connector of a station.</summary>
public sealed class ConnectorSession
{
    public required int ConnectorId { get; init; }

    /// <summary>Last OCPP status string (Available, Preparing, Charging, …).</summary>
    public string? Status { get; set; }

    // ── Live meter data ────────────────────────────────────────────────────────
    public decimal? MeterValueWh { get; set; }
    public decimal? MeterStartWh { get; set; }
    public decimal? StateOfCharge { get; set; }
    public double? CurrentPowerKw { get; set; }
    public DateTime? LastMeterValueAt { get; set; }

    // ── Active transaction ─────────────────────────────────────────────────────
    /// <summary>Local int transaction ID (used in RabbitMQ events and RemoteStop).</summary>
    public int? LocalTxId { get; set; }
    /// <summary>OCPP 2.x string transaction ID from TransactionEvent. Null for OCPP 1.6.</summary>
    public string? OcppTxId { get; set; }

    /// <summary>
    /// EVCC ID (MAC address of the vehicle's communication controller) of the car on this connector.
    /// Cleared when the connector returns to Available.
    /// </summary>
    public string? CarId { get; set; }

    /// <summary>RemoteStart waiter: completed with the local transaction ID on StartTransaction.</summary>
    public TaskCompletionSource<int>? PendingStart { get; set; }

    /// <summary>Resets everything that belongs to the finished transaction.</summary>
    public void EndTransaction(decimal? meterStopWh)
    {
        MeterStartWh     = null;
        MeterValueWh     = meterStopWh;
        LastMeterValueAt = null;
        CurrentPowerKw   = null;
        StateOfCharge    = null;
        LocalTxId        = null;
        OcppTxId         = null;
    }
}
