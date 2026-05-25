/**
 * ChargingStationConnections.cs — in-memory registry of connected chargers
 *
 * Manages two thread-safe dictionaries:
 *   _connections       — maps stationId (OcppId) → open WebSocket
 *   _activeTransactions — maps stationId → current OCPP transactionId
 *
 * Why in-memory and not in the database?
 *   WebSocket objects cannot be serialised — they represent an open TCP
 *   connection and only exist while the process is alive. The database stores
 *   persistent state (MeterStart, ActiveTransactionId); this class stores the
 *   live connection handle needed to send commands right now.
 *
 * Thread safety: Dictionary is not inherently thread-safe in C#. In the current
 * implementation a single WebSocket loop per charger means concurrent access is
 * rare, but if multiple threads ever access this class, consider ConcurrentDictionary.
 *
 * Note: _transactionCounter resets to 1 on process restart. This is fine because
 * the OCPP DB Connector.ActiveTransactionId (persisted) is the authoritative value
 * for stop commands after a restart.
 */

using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace OCPPServer
{
    public static class ChargingStationConnections
    {
        /// <summary>
        /// Live WebSocket connections keyed by normalised stationId (lowercase OcppId).
        /// ConcurrentDictionary is used because multiple chargers can connect or
        /// disconnect simultaneously on different async threads.
        /// </summary>
        private static readonly ConcurrentDictionary<string, WebSocket> _connections = new();

        /// <summary>Negotiated OCPP sub-protocol per connected station, e.g. "ocpp1.6" or "ocpp2.1".</summary>
        private static readonly ConcurrentDictionary<string, string> _protocols = new();

        /// <summary>
        /// OCPP 2.1 uses string transactionIds. Maps stationId → (localIntId, ocppStringId)
        /// so that RemoteStop commands (which carry our local int) can look up the OCPP string.
        /// </summary>
        private static readonly ConcurrentDictionary<string, (int LocalId, string OcppTxId)> _ocpp21Tx = new();

        /// <summary>
        /// In-memory transactionId assigned at RemoteStartTransaction.
        /// Used to match the correct transactionId in RemoteStopTransaction.
        /// Cleared when StopTransaction is received from the charger.
        /// ConcurrentDictionary avoids KeyNotFoundException on concurrent reconnects.
        /// </summary>
        private static readonly ConcurrentDictionary<string, int> _activeTransactions = new();

        /// <summary>
        /// Pending StartTransaction signals — set by OcppCommandHandler after RemoteStartTransaction
        /// is Accepted, completed by HandleStartTransaction when the charger confirms the session.
        /// Keyed by stationId; value resolves with the assigned transactionId.
        /// </summary>
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<int>> _pendingStartTransactions = new();

        public static TaskCompletionSource<int> RegisterPendingStartTransaction(string stationId)
        {
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingStartTransactions[stationId] = tcs;
            return tcs;
        }

        public static void CompletePendingStartTransaction(string stationId, int transactionId)
        {
            if (_pendingStartTransactions.TryRemove(stationId, out var tcs))
                tcs.TrySetResult(transactionId);
        }

        public static void CancelPendingStartTransaction(string stationId)
        {
            if (_pendingStartTransactions.TryRemove(stationId, out var tcs))
                tcs.TrySetCanceled();
        }

        // ── Pending StopTransaction signals ───────────────────────────────────────
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<int>> _pendingStopTransactions = new();

        public static TaskCompletionSource<int> RegisterPendingStopTransaction(string stationId)
        {
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingStopTransactions[stationId] = tcs;
            return tcs;
        }

        public static void CompletePendingStopTransaction(string stationId, int transactionId)
        {
            if (_pendingStopTransactions.TryRemove(stationId, out var tcs))
                tcs.TrySetResult(transactionId);
        }

        public static void CancelPendingStopTransaction(string stationId)
        {
            if (_pendingStopTransactions.TryRemove(stationId, out var tcs))
                tcs.TrySetCanceled();
        }

        /// <summary>
        /// Meter reading (Wh) at session start, stored in-memory from StartTransaction.
        /// Used to calculate session energy (MeterValue − MeterStart) during charging
        /// and passed to EVChargingApi in StopTransaction so it can persist the delta.
        /// Cleared when StopTransaction is received.
        /// </summary>
        private static readonly ConcurrentDictionary<string, decimal> _meterStarts = new();

        /// <summary>
        /// Monotonically increasing counter for generating unique transactionIds.
        /// Interlocked.Increment is used instead of ++ to guarantee atomicity under
        /// concurrent StartTransaction messages from multiple chargers.
        /// </summary>
        private static int _transactionCounter = 0;

        // ── Connection management ──────────────────────────────────────────────

        /// <summary>Registers an open WebSocket for a charger. Overwrites any previous connection.</summary>
        public static void Add(string stationId, WebSocket socket)
            => _connections[stationId] = socket;

        /// <summary>Removes the connection entry when the charger disconnects.</summary>
        public static void Remove(string stationId)
        {
            _connections.TryRemove(stationId, out _);
            _protocols.TryRemove(stationId, out _);
        }

        /// <summary>Returns the open WebSocket for a charger, or null if not connected.</summary>
        public static WebSocket? Get(string stationId)
            => _connections.TryGetValue(stationId, out var socket) ? socket : null;

        // ── Protocol version management ────────────────────────────────────────

        /// <summary>Stores the negotiated OCPP sub-protocol for a station after WebSocket handshake.</summary>
        public static void SetProtocol(string stationId, string protocol)
            => _protocols[stationId] = protocol;

        /// <summary>Returns the negotiated protocol, defaulting to "ocpp1.6" if unknown.</summary>
        public static string GetProtocol(string stationId)
            => _protocols.TryGetValue(stationId, out var p) ? p : "ocpp1.6";

        // ── OCPP 2.1 transaction ID mapping ───────────────────────────────────

        /// <summary>
        /// Assigns a local int transactionId and maps it to the OCPP 2.1 string transactionId.
        /// Called when TransactionEvent(Started) is received from a 2.1 charger.
        /// </summary>
        public static int RegisterOcpp21Transaction(string stationId, string ocppTxId)
        {
            var localId = Interlocked.Increment(ref _transactionCounter);
            _ocpp21Tx[stationId] = (localId, ocppTxId);
            return localId;
        }

        /// <summary>Returns the OCPP 2.x string transactionId for the active session, or null.</summary>
        public static string? GetOcpp21TxId(string stationId)
            => _ocpp21Tx.TryGetValue(stationId, out var v) ? v.OcppTxId : null;

        /// <summary>Returns the local int transactionId mapped from the OCPP 2.x string, or null.</summary>
        public static int? GetOcpp21LocalTxId(string stationId)
            => _ocpp21Tx.TryGetValue(stationId, out var v) ? v.LocalId : (int?)null;

        /// <summary>Removes the OCPP 2.x transaction mapping when the session ends.</summary>
        public static void ClearOcpp21Transaction(string stationId)
            => _ocpp21Tx.TryRemove(stationId, out _);

        // ── Transaction management ─────────────────────────────────────────────
        //TODO: Not in use. Remove after confirming RemoteStartTransaction and RemoteStopTransaction work without it. The OCPP DB Connector.ActiveTransactionId is the authoritative value for stop commands after a restart, so this in-memory store may be redundant.
        /// <summary>
        /// Assigns and stores a new transactionId for a charger.
        /// Called when sending RemoteStartTransaction so we can include the same
        /// id in RemoteStopTransaction later.
        /// </summary>
        public static int AssignTransaction(string stationId)
        {
            // Interlocked.Increment is atomic — safe when multiple chargers send
            // StartTransaction simultaneously on different async threads.
            var id = Interlocked.Increment(ref _transactionCounter);
            _activeTransactions[stationId] = id;
            return id;
        }


        //TODO: Not in use. Remove after confirming RemoteStartTransaction and RemoteStopTransaction work without it. The OCPP DB Connector.ActiveTransactionId is the authoritative value for stop commands after a restart, so this in-memory store may be redundant.
        /// <summary>Returns the current transactionId for a charger, or null if none active.</summary>
        public static int? GetTransaction(string stationId)
            => _activeTransactions.TryGetValue(stationId, out var id) ? id : null;


        //TODO: Not in use. Remove after confirming RemoteStartTransaction and RemoteStopTransaction work without it. The OCPP DB Connector.ActiveTransactionId is the authoritative value for stop commands after a restart, so this in-memory store may be redundant.
        /// <summary>Removes the transactionId when StopTransaction is received from the charger.</summary>
        public static void ClearTransaction(string stationId)
            => _activeTransactions.TryRemove(stationId, out _);

        // ── MeterStart management ──────────────────────────────────────────────
        //TODO: Not in use. Remove after confirming StartTransaction and StopTransaction work without it. The OCPP DB Connector can persist the MeterStart delta without this in-memory store, so it may be redundant.
        /// <summary>Stores the meter reading (Wh) at session start. Called from HandleStartTransaction.</summary>
        public static void SetMeterStart(string stationId, decimal valueWh)
            => _meterStarts[stationId] = valueWh;

        //TODO: Not in use. Remove after confirming StartTransaction and StopTransaction work without it. The OCPP DB Connector can persist the MeterStart delta without this in-memory store, so it may be redundant.
        /// <summary>Returns the stored MeterStart (Wh) for a charger, or null if not set.</summary>
        public static decimal? GetMeterStart(string stationId)
            => _meterStarts.TryGetValue(stationId, out var v) ? v : null;

        //TODO: Not in use. Remove after confirming StartTransaction and StopTransaction work without it. The OCPP DB Connector can persist the MeterStart delta without this in-memory store, so it may be redundant.
        /// <summary>Removes the MeterStart entry when StopTransaction is received.</summary>
        public static void ClearMeterStart(string stationId)
            => _meterStarts.TryRemove(stationId, out _);
    }
}
