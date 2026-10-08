using OCPPServer.ChargerAdapters;
using System.Net.WebSockets;

namespace OCPPServer;

/// <summary>
/// In-memory live state for one connected charging station.
/// Created on WebSocket connect, removed on disconnect.
/// Never persisted — values are meaningless after a server restart.
///
/// Stable charger metadata (vendor, model, firmware, OCPP version, status)
/// lives in the <c>Plug</c> DB entity and survives restarts.
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

    // ── Vehicle identification (ISO 15118 / DIN 70121) ─────────────────────────
    /// <summary>
    /// EVCC ID (MAC address of the vehicle's communication controller) if reported by the charger.
    /// Set from Authorize.req / TransactionEvent when idToken.type is MacAddress or idTag starts with "VID:".
    /// Null for non-ISO15118 sessions (RFID, app) and AC chargers.
    /// Cleared when connector returns to Available.
    /// </summary>
    public string? CarId { get; set; }

    // ── Pending command signals ────────────────────────────────────────────────
    public TaskCompletionSource<int>? PendingStart { get; set; }
    public TaskCompletionSource<int>? PendingStop { get; set; }

    /// <summary>
    /// Set while waiting for backend's command.authorize.response to an EVCC-ID Authorize.req.
    /// Completed with "Accepted" or "Rejected". Null for non-EVCC sessions.
    /// </summary>
    public TaskCompletionSource<string>? PendingAuthorize { get; set; }
}
