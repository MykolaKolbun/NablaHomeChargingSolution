using System.Net.WebSockets;
using OCPPServer;
using Xunit;

namespace OCPPServer.Tests;

/// <summary>
/// Tests for ChargingStationConnections.
///
/// The class was refactored from five parallel ConcurrentDictionaries to a single
/// ConcurrentDictionary&lt;string, ConnectorState&gt;.  Tests verify:
///   - Connection lifecycle (Add / Get / Remove)
///   - ConnectorState fields (Socket, Protocol)
///   - OCPP 2.x transaction ID assignment and lookup
///   - Thread-safety contract under concurrent operations
/// </summary>
public class ChargingStationConnectionsTests
{
    // ── Connection lifecycle ───────────────────────────────────────────────────

    [Fact]
    public void Get_UnknownStation_ReturnsNull()
    {
        var result = ChargingStationConnections.Get("nonexistent-station");
        Assert.Null(result);
    }

    [Fact]
    public void Add_ThenGet_ReturnsSameSocket()
    {
        var stationId = UniqueId();
        var socket    = CreateFakeSocket();

        ChargingStationConnections.Add(stationId, socket, "ocpp1.6");
        var state = ChargingStationConnections.Get(stationId);

        Assert.NotNull(state);
        Assert.Same(socket, state.Socket);

        ChargingStationConnections.Remove(stationId);
    }

    [Fact]
    public void Add_SetsProtocol()
    {
        var stationId = UniqueId();
        ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp2.1");

        var protocol = ChargingStationConnections.GetProtocol(stationId);
        Assert.Equal("ocpp2.1", protocol);

        ChargingStationConnections.Remove(stationId);
    }

    [Fact]
    public void Remove_ExistingStation_GetReturnsNull()
    {
        var stationId = UniqueId();
        ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp1.6");

        ChargingStationConnections.Remove(stationId);

        Assert.Null(ChargingStationConnections.Get(stationId));
    }

    [Fact]
    public void Remove_NonExistentStation_DoesNotThrow()
    {
        var ex = Record.Exception(() => ChargingStationConnections.Remove("ghost-station"));
        Assert.Null(ex);
    }

    [Fact]
    public void Add_OverwritesPreviousSocket()
    {
        var stationId = UniqueId();
        var first  = CreateFakeSocket();
        var second = CreateFakeSocket();

        ChargingStationConnections.Add(stationId, first,  "ocpp1.6");
        ChargingStationConnections.Add(stationId, second, "ocpp2.1");

        var state = ChargingStationConnections.Get(stationId);
        Assert.Same(second, state?.Socket);
        Assert.Equal("ocpp2.1", state?.Protocol);

        ChargingStationConnections.Remove(stationId);
    }

    [Fact]
    public void GetProtocol_UnknownStation_DefaultsToOcpp16()
    {
        var protocol = ChargingStationConnections.GetProtocol("no-such-station");
        Assert.Equal("ocpp1.6", protocol);
    }

    [Fact]
    public void GetProtocolBySocket_KnownSocket_ReturnsProtocol()
    {
        var stationId = UniqueId();
        var socket    = CreateFakeSocket();
        ChargingStationConnections.Add(stationId, socket, "ocpp2.1");

        var protocol = ChargingStationConnections.GetProtocolBySocket(socket);
        Assert.Equal("ocpp2.1", protocol);

        ChargingStationConnections.Remove(stationId);
    }

    [Fact]
    public void GetProtocolBySocket_UnknownSocket_DefaultsToOcpp16()
    {
        var protocol = ChargingStationConnections.GetProtocolBySocket(CreateFakeSocket());
        Assert.Equal("ocpp1.6", protocol);
    }

    [Fact]
    public void GetAll_ReflectsAddedAndRemovedStations()
    {
        var id1 = UniqueId();
        var id2 = UniqueId();
        ChargingStationConnections.Add(id1, CreateFakeSocket(), "ocpp1.6");
        ChargingStationConnections.Add(id2, CreateFakeSocket(), "ocpp2.1");

        var all = ChargingStationConnections.GetAll();
        Assert.Contains(all, e => e.StationId == id1);
        Assert.Contains(all, e => e.StationId == id2);

        ChargingStationConnections.Remove(id1);
        ChargingStationConnections.Remove(id2);

        var after = ChargingStationConnections.GetAll();
        Assert.DoesNotContain(after, e => e.StationId == id1);
        Assert.DoesNotContain(after, e => e.StationId == id2);
    }

    // ── Transaction ID generation ──────────────────────────────────────────────

    [Fact]
    public void NextTransactionId_ReturnsPositiveId()
    {
        var id = ChargingStationConnections.NextTransactionId();
        Assert.True(id > 0);
    }

    [Fact]
    public void NextTransactionId_IdsAreUnique()
    {
        var ids = Enumerable.Range(0, 10)
            .Select(_ => ChargingStationConnections.NextTransactionId())
            .ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // ── OCPP 2.x transaction management ───────────────────────────────────────

    [Fact]
    public void GetOcpp21LocalTxId_NoActiveTransaction_ReturnsNull()
    {
        var result = ChargingStationConnections.GetOcpp21LocalTxId(UniqueId());
        Assert.Null(result);
    }

    [Fact]
    public void GetOcpp21TxId_NoActiveTransaction_ReturnsNull()
    {
        var result = ChargingStationConnections.GetOcpp21TxId(UniqueId());
        Assert.Null(result);
    }

    [Fact]
    public void RegisterOcpp21Transaction_ReturnsPositiveId()
    {
        var stationId = UniqueId();
        ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp2.1");

        var id = ChargingStationConnections.RegisterOcpp21Transaction(stationId, "TX-001");
        Assert.True(id > 0);

        ChargingStationConnections.Remove(stationId);
    }

    [Fact]
    public void RegisterOcpp21Transaction_ThenGet_ReturnsBothIds()
    {
        var stationId = UniqueId();
        ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp2.1");

        var localId = ChargingStationConnections.RegisterOcpp21Transaction(stationId, "TX-ABC");

        Assert.Equal(localId, ChargingStationConnections.GetOcpp21LocalTxId(stationId));
        Assert.Equal("TX-ABC",  ChargingStationConnections.GetOcpp21TxId(stationId));

        ChargingStationConnections.Remove(stationId);
    }

    [Fact]
    public void ClearOcpp21Transaction_GetReturnsNull()
    {
        var stationId = UniqueId();
        ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp2.1");
        ChargingStationConnections.RegisterOcpp21Transaction(stationId, "TX-XYZ");

        ChargingStationConnections.ClearOcpp21Transaction(stationId);

        Assert.Null(ChargingStationConnections.GetOcpp21LocalTxId(stationId));
        Assert.Null(ChargingStationConnections.GetOcpp21TxId(stationId));

        ChargingStationConnections.Remove(stationId);
    }

    [Fact]
    public void ClearOcpp21Transaction_NonExistent_DoesNotThrow()
    {
        var ex = Record.Exception(() => ChargingStationConnections.ClearOcpp21Transaction("ghost"));
        Assert.Null(ex);
    }

    [Fact]
    public void RegisterOcpp21Transaction_IdsAreUnique()
    {
        // Each call must return a distinct local ID (Interlocked.Increment guarantees this)
        var ids = Enumerable.Range(0, 10).Select(i =>
        {
            var stationId = UniqueId();
            ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp2.1");
            return ChargingStationConnections.RegisterOcpp21Transaction(stationId, $"TX-{i}");
        }).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // ── Thread safety ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentAdd_DoesNotThrow()
    {
        // Simulates 50 chargers connecting simultaneously
        var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(() =>
        {
            var id = $"concurrent-{i}-{UniqueId()}";
            ChargingStationConnections.Add(id, CreateFakeSocket(), "ocpp1.6");
            ChargingStationConnections.Get(id);
            ChargingStationConnections.Remove(id);
        })).ToArray();

        var ex = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
        Assert.Null(ex);
    }

    [Fact]
    public async Task ConcurrentRegisterTransaction_AllIdsDistinct()
    {
        var ids   = new System.Collections.Concurrent.ConcurrentBag<int>();
        var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(() =>
        {
            var stationId = $"tx-station-{i}-{UniqueId()}";
            ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp2.1");
            ids.Add(ChargingStationConnections.RegisterOcpp21Transaction(stationId, $"TX-{i}"));
            ChargingStationConnections.Remove(stationId);
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(50, ids.Distinct().Count());
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string UniqueId() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Creates a WebSocket subclass stub. We can't instantiate WebSocket directly
    /// (it's abstract), but we only need object identity for these tests.
    /// </summary>
    private static WebSocket CreateFakeSocket() => new FakeWebSocket();

    private sealed class FakeWebSocket : WebSocket
    {
        public override WebSocketCloseStatus? CloseStatus              => null;
        public override string?               CloseStatusDescription   => null;
        public override WebSocketState        State                    => WebSocketState.Open;
        public override string?               SubProtocol              => null;
        public override void   Abort()                                 { }
        public override Task   CloseAsync(WebSocketCloseStatus s, string? d, CancellationToken ct) => Task.CompletedTask;
        public override Task   CloseOutputAsync(WebSocketCloseStatus s, string? d, CancellationToken ct) => Task.CompletedTask;
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> b, CancellationToken ct)
            => Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
        public override Task SendAsync(ArraySegment<byte> b, WebSocketMessageType t, bool e, CancellationToken ct)
            => Task.CompletedTask;
        public override void Dispose() { }
    }
}
