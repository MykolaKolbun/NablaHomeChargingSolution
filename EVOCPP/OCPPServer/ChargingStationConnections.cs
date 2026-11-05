using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace OCPPServer;

/// <summary>
/// In-memory registry of all currently connected charging stations.
///
/// One <see cref="ConnectorState"/> per station (OCPP id / WebSocket); session data is per
/// connector inside it (<see cref="ConnectorState.Connectors"/>). A two-connector charger such
/// as Wallbox Supernova reports both connectors under one OCPP id, so nothing session-related may
/// be keyed by the station alone.
///
/// Thread safety: ConcurrentDictionary protects Add/Remove/Get and the per-station connector map.
/// Individual fields are written from the per-station WebSocket receive loop or from the
/// RabbitMQ command handler (waiters only).
/// </summary>
public static class ChargingStationConnections
{
    private static readonly ConcurrentDictionary<string, ConnectorState> _states = new();

    /// <summary>Monotonically increasing counter for assigning unique local transaction IDs.</summary>
    private static int _transactionCounter = 0;

    // ── Connection lifecycle ───────────────────────────────────────────────────

    public static void Add(string stationId, WebSocket socket, string protocol)
        => _states[stationId] = new ConnectorState { Socket = socket, Protocol = protocol };

    public static void Remove(string stationId) => _states.TryRemove(stationId, out _);

    /// <summary>Returns the full live state for a station, or null if not connected.</summary>
    public static ConnectorState? Get(string stationId)
        => _states.TryGetValue(stationId, out var s) ? s : null;

    /// <summary>Returns the connector's live state (created on first use), or null if the station is not connected.</summary>
    public static ConnectorSession? GetConnector(string stationId, int connectorId)
        => connectorId > 0 ? Get(stationId)?.Connector(connectorId) : null;

    /// <summary>Convenience — returns the open WebSocket, or null if not connected.</summary>
    public static WebSocket? GetSocket(string stationId) => Get(stationId)?.Socket;

    /// <summary>Returns the negotiated OCPP sub-protocol, defaulting to "ocpp1.6" if unknown.</summary>
    public static string GetProtocol(string stationId) => Get(stationId)?.Protocol ?? "ocpp1.6";

    /// <summary>Reverse lookup — returns the negotiated protocol for a given socket instance,
    /// defaulting to "ocpp1.6" if the socket is not found in the registry.</summary>
    public static string GetProtocolBySocket(WebSocket socket)
    {
        foreach (var kv in _states)
            if (ReferenceEquals(kv.Value.Socket, socket)) return kv.Value.Protocol;
        return "ocpp1.6";
    }

    /// <summary>
    /// Returns a snapshot of all currently connected stations.
    /// Used by <see cref="ChargerWatcherService"/> to check inactivity.
    /// </summary>
    public static IReadOnlyList<(string StationId, ConnectorState State)> GetAll()
        => _states.Select(kv => (kv.Key, kv.Value)).ToList();

    // ── Transaction ID generation ──────────────────────────────────────────────

    /// <summary>Generates a process-unique local transaction ID.</summary>
    public static int NextTransactionId() => Interlocked.Increment(ref _transactionCounter);

    /// <summary>
    /// Makes sure future IDs are above <paramref name="lastUsed"/> (called at startup with the highest
    /// transaction still open in the DB, so a restart never re-issues the ID of a running session).
    /// </summary>
    public static void EnsureTransactionIdsAbove(int lastUsed)
    {
        int current;
        while ((current = Volatile.Read(ref _transactionCounter)) < lastUsed)
            Interlocked.CompareExchange(ref _transactionCounter, lastUsed, current);
    }

    // ── OCPP 2.x transaction ID mapping ───────────────────────────────────────

    /// <summary>
    /// Assigns a local int ID for an OCPP 2.x string transaction on <paramref name="evseId"/>.
    /// Returns the assigned local ID.
    /// </summary>
    public static int RegisterOcpp21Transaction(string stationId, int evseId, string ocppTxId)
    {
        var localId = NextTransactionId();
        var conn = GetConnector(stationId, evseId);
        if (conn != null)
        {
            conn.LocalTxId = localId;
            conn.OcppTxId  = ocppTxId;
        }
        return localId;
    }

    /// <summary>OCPP 2.x string transaction ID for a local transaction ID, or null.</summary>
    public static string? GetOcpp21TxId(string stationId, int localTxId)
        => Get(stationId)?.ByTransaction(localTxId)?.OcppTxId;

    // ── Pending start/stop signals ─────────────────────────────────────────────

    /// <summary>
    /// Waits for the StartTransaction that follows an accepted RemoteStart on <paramref name="connectorId"/>.
    /// connectorId 0 = the charger chooses; the first StartTransaction on any connector completes it.
    /// </summary>
    public static TaskCompletionSource<int> RegisterPendingStartTransaction(string stationId, int connectorId)
    {
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = Get(stationId);
        if (state != null)
        {
            if (connectorId > 0) state.Connector(connectorId).PendingStart = tcs;
            else                 state.PendingStartAnyConnector = tcs;
        }
        return tcs;
    }

    public static void CompletePendingStartTransaction(string stationId, int connectorId, int txId)
    {
        var state = Get(stationId);
        if (state == null) return;
        if (connectorId > 0 && state.Connector(connectorId).PendingStart is { } tcs)
        {
            state.Connector(connectorId).PendingStart = null;
            tcs.TrySetResult(txId);
        }
        else if (state.PendingStartAnyConnector is { } any)
        {
            state.PendingStartAnyConnector = null;
            any.TrySetResult(txId);
        }
    }

    public static void CancelPendingStartTransaction(string stationId, int connectorId)
    {
        var state = Get(stationId);
        if (state == null) return;
        if (connectorId > 0)
        {
            var conn = state.Connector(connectorId);
            if (conn.PendingStart is { } tcs) { conn.PendingStart = null; tcs.TrySetCanceled(); }
        }
        else if (state.PendingStartAnyConnector is { } any)
        {
            state.PendingStartAnyConnector = null;
            any.TrySetCanceled();
        }
    }

    /// <summary>Waits for the StopTransaction of <paramref name="transactionId"/> after an accepted RemoteStop.</summary>
    public static TaskCompletionSource<int> RegisterPendingStopTransaction(string stationId, int transactionId)
    {
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Get(stationId)?.PendingStops.AddOrUpdate(transactionId, tcs, (_, _) => tcs);
        return tcs;
    }

    public static void CompletePendingStopTransaction(string stationId, int transactionId)
    {
        if (Get(stationId)?.PendingStops.TryRemove(transactionId, out var tcs) == true)
            tcs.TrySetResult(transactionId);
    }

    public static void CancelPendingStopTransaction(string stationId, int transactionId)
    {
        if (Get(stationId)?.PendingStops.TryRemove(transactionId, out var tcs) == true)
            tcs.TrySetCanceled();
    }

    // ── Pending authorize signal ───────────────────────────────────────────────

    public static TaskCompletionSource<string> RegisterPendingAuthorize(string stationId)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = Get(stationId);
        if (state != null) state.PendingAuthorize = tcs;
        return tcs;
    }

    public static void CompletePendingAuthorize(string stationId, string status)
    {
        var state = Get(stationId);
        if (state?.PendingAuthorize is { } tcs) { state.PendingAuthorize = null; tcs.TrySetResult(status); }
    }

    public static void CancelPendingAuthorize(string stationId)
    {
        var state = Get(stationId);
        if (state?.PendingAuthorize is { } tcs) { state.PendingAuthorize = null; tcs.TrySetCanceled(); }
    }

    // ── Vehicle identity (OCPP 1.6 Authorize has no connectorId) ───────────────

    /// <summary>
    /// Records the EVCC ID from an Authorize.req and returns the connector it belongs to when that is
    /// unambiguous (exactly one connector Preparing without a transaction); otherwise keeps it unbound
    /// until StartTransaction.
    /// </summary>
    public static int? BindAuthorizedCar(string stationId, string carId)
    {
        var state = Get(stationId);
        if (state == null) return null;

        var waiting = state.Connectors.Values
            .Where(c => c.Status == "Preparing" && c.LocalTxId is null)
            .ToList();
        if (waiting.Count == 1)
        {
            waiting[0].CarId   = carId;
            state.UnboundCarId = null;
            return waiting[0].ConnectorId;
        }

        state.UnboundCarId = carId;
        return null;
    }
}
