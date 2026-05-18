using System.Net.WebSockets;

namespace OCPPServer
{
    public static class ChargingStationConnections
    {
        private static readonly Dictionary<string, WebSocket> _connections = new();
        private static readonly Dictionary<string, int> _activeTransactions = new();
        private static int _transactionCounter = 1;

        public static void Add(string stationId, WebSocket socket)
            => _connections[stationId] = socket;

        public static void Remove(string stationId)
            => _connections.Remove(stationId);

        public static WebSocket? Get(string stationId)
            => _connections.TryGetValue(stationId, out var socket) ? socket : null;

        public static int AssignTransaction(string stationId)
        {
            var id = _transactionCounter++;
            _activeTransactions[stationId] = id;
            return id;
        }

        public static int? GetTransaction(string stationId)
            => _activeTransactions.TryGetValue(stationId, out var id) ? id : null;

        public static void ClearTransaction(string stationId)
            => _activeTransactions.Remove(stationId);
    }
}
