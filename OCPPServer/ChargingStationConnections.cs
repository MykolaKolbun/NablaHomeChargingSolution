using System.Net.WebSockets;

namespace OCPPServer
{
    public static class ChargingStationConnections
    {
        private static readonly Dictionary<string, WebSocket> _connections = new();

        public static void Add(string stationId, WebSocket socket)
            => _connections[stationId] = socket;

        public static void Remove(string stationId)
            => _connections.Remove(stationId);

        public static WebSocket? Get(string stationId)
            => _connections.TryGetValue(stationId, out var socket) ? socket : null;
    }
}
