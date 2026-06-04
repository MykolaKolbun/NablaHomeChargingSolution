using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace OCPPServer;

/// <summary>
/// In-memory registry of all currently connected charging stations.
///
/// A single <see cref="ConnectorState"/> per station replaces the previous design
/// of five parallel ConcurrentDictionaries. The state object holds the WebSocket,
/// negotiated OCPP protocol, live meter data, active transaction IDs, and pending
/// command signals — everything that only exists while the station is connected.
///
/// Thread safety: ConcurrentDictionary protects Add/Remove/Get.
/// Individual ConnectorState fields are written only from the per-station WebSocket
/// receive loop (single-threaded) or from admin HTTP handlers (read-mostly).
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
    /// Iterates the ConcurrentDictionary; safe to call from any thread.
    /// Used by <see cref="ChargerWatcherService"/> to check inactivity.
    /// </summary>
    public static IReadOnlyList<(string StationId, ConnectorState State)> GetAll()
        => _states.Select(kv => (kv.Key, kv.Value)).ToList();

    // ── Transaction ID generation ──────────────────────────────────────────────

    /// <summary>Generates a process-unique local transaction ID. Resets to 1 on restart.</summary>
    public static int NextTransactionId() => Interlocked.Increment(ref _transactionCounter);

    // ── OCPP 2.x transaction ID mapping ───────────────────────────────────────

    /// <summary>
    /// Assigns a local int ID for an OCPP 2.x string transaction and stores the mapping
    /// in the station's ConnectorState. Returns the assigned local ID.
    /// </summary>
    public static int RegisterOcpp21Transaction(string stationId, string ocppTxId)
    {
        var localId = Interlocked.Increment(ref _transactionCounter);
        var state = Get(stationId);
        if (state != null)
        {
            state.LocalTxId = localId;
            state.OcppTxId  = ocppTxId;
        }
        return localId;
    }

    public static string? GetOcpp21TxId(string stationId)      => Get(stationId)?.OcppTxId;
    public static int?    GetOcpp21LocalTxId(string stationId) => Get(stationId)?.LocalTxId;

    public static void ClearOcpp21Transaction(string stationId)
    {
        var state = Get(stationId);
        if (state != null) { state.LocalTxId = null; state.OcppTxId = null; }
    }

    // ── Pending start/stop signals ─────────────────────────────────────────────

    public static TaskCompletionSource<int> RegisterPendingStartTransaction(string stationId)
    {
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = Get(stationId);
        if (state != null) state.PendingStart = tcs;
        return tcs;
    }

    public static void CompletePendingStartTransaction(string stationId, int txId)
    {
        var state = Get(stationId);
        if (state?.PendingStart is { } tcs) { state.PendingStart = null; tcs.TrySetResult(txId); }
    }

    public static void CancelPendingStartTransaction(string stationId)
    {
        var state = Get(stationId);
        if (state?.PendingStart is { } tcs) { state.PendingStart = null; tcs.TrySetCanceled(); }
    }

    public static TaskCompletionSource<int> RegisterPendingStopTransaction(string stationId)
    {
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = Get(stationId);
        if (state != null) state.PendingStop = tcs;
        return tcs;
    }

    public static void CompletePendingStopTransaction(string stationId, int txId)
    {
        var state = Get(stationId);
        if (state?.PendingStop is { } tcs) { state.PendingStop = null; tcs.TrySetResult(txId); }
    }

    public static void CancelPendingStopTransaction(string stationId)
    {
        var state = Get(stationId);
        if (state?.PendingStop is { } tcs) { state.PendingStop = null; tcs.TrySetCanceled(); }
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
}
