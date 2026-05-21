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

using System.Net.WebSockets;

namespace OCPPServer
{
    public static class ChargingStationConnections
    {
        /// <summary>Live WebSocket connections keyed by normalised stationId (lowercase OcppId).</summary>
        private static readonly Dictionary<string, WebSocket> _connections = new();

        /// <summary>
        /// In-memory transactionId assigned at RemoteStartTransaction.
        /// Used to match the correct transactionId in RemoteStopTransaction.
        /// Cleared when StopTransaction is received from the charger.
        /// </summary>
        private static readonly Dictionary<string, int> _activeTransactions = new();

        /// <summary>Monotonically increasing counter for generating unique transactionIds.</summary>
        private static int _transactionCounter = 1;

        // ── Connection management ──────────────────────────────────────────────

        /// <summary>Registers an open WebSocket for a charger. Overwrites any previous connection.</summary>
        public static void Add(string stationId, WebSocket socket)
            => _connections[stationId] = socket;

        /// <summary>Removes the connection entry when the charger disconnects.</summary>
        public static void Remove(string stationId)
            => _connections.Remove(stationId);

        /// <summary>Returns the open WebSocket for a charger, or null if not connected.</summary>
        public static WebSocket? Get(string stationId)
            => _connections.TryGetValue(stationId, out var socket) ? socket : null;

        // ── Transaction management ─────────────────────────────────────────────

        /// <summary>
        /// Assigns and stores a new transactionId for a charger.
        /// Called when sending RemoteStartTransaction so we can include the same
        /// id in RemoteStopTransaction later.
        /// </summary>
        public static int AssignTransaction(string stationId)
        {
            var id = _transactionCounter++;
            _activeTransactions[stationId] = id;
            return id;
        }

        /// <summary>Returns the current transactionId for a charger, or null if none active.</summary>
        public static int? GetTransaction(string stationId)
            => _activeTransactions.TryGetValue(stationId, out var id) ? id : null;

        /// <summary>Removes the transactionId when StopTransaction is received from the charger.</summary>
        public static void ClearTransaction(string stationId)
            => _activeTransactions.Remove(stationId);
    }
}
