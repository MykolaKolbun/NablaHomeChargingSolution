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
    public void GetOcpp21TxId_NoActiveTransaction_ReturnsNull()
    {
        Assert.Null(ChargingStationConnections.GetOcpp21TxId(UniqueId(), 12345));
    }

    [Fact]
    public void RegisterOcpp21Transaction_MapsLocalIdToOcppTxIdOnThatEvse()
    {
        var stationId = UniqueId();
        ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp2.1");

        var localId = ChargingStationConnections.RegisterOcpp21Transaction(stationId, 2, "TX-ABC");

        Assert.True(localId > 0);
        Assert.Equal("TX-ABC", ChargingStationConnections.GetOcpp21TxId(stationId, localId));
        Assert.Equal(2, ChargingStationConnections.Get(stationId)!.ByOcppTransaction("TX-ABC")!.ConnectorId);
        Assert.Null(ChargingStationConnections.Get(stationId)!.Connector(1).LocalTxId);

        ChargingStationConnections.Remove(stationId);
    }

    [Fact]
    public void RegisterOcpp21Transaction_IdsAreUnique()
    {
        var ids = Enumerable.Range(0, 10).Select(i =>
        {
            var stationId = UniqueId();
            ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp2.1");
            return ChargingStationConnections.RegisterOcpp21Transaction(stationId, 1, $"TX-{i}");
        }).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void EnsureTransactionIdsAbove_NextIdIsHigher()
    {
        ChargingStationConnections.EnsureTransactionIdsAbove(1_000_000);
        Assert.True(ChargingStationConnections.NextTransactionId() > 1_000_000);
    }

    // ── Multi-connector stations (one OCPP id, N connectors — Wallbox Supernova) ──

    [Fact]
    public void Connectors_have_independent_session_state()
    {
        var stationId = UniqueId();
        ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp1.6");
        var state = ChargingStationConnections.Get(stationId)!;

        var c1 = state.Connector(1);
        var c2 = state.Connector(2);
        c1.LocalTxId = 21; c1.MeterStartWh = 19_107_456m; c1.StateOfCharge = 44m;
        c2.LocalTxId = 22; c2.MeterStartWh = 28_256_000m; c2.StateOfCharge = 95m;

        Assert.Same(c1, state.ByTransaction(21));
        Assert.Same(c2, state.ByTransaction(22));
        Assert.Equal(19_107_456m, state.Connector(1).MeterStartWh);
        Assert.Equal(95m, state.Connector(2).StateOfCharge);

        c2.EndTransaction(28_272_076m);
        Assert.Null(state.ByTransaction(22));
        Assert.Same(c1, state.ByTransaction(21));      // the other connector keeps its session

        ChargingStationConnections.Remove(stationId);
    }

    [Fact]
    public async Task PendingStart_is_per_connector()
    {
        var stationId = UniqueId();
        ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp1.6");

        var t1 = ChargingStationConnections.RegisterPendingStartTransaction(stationId, 1);
        var t2 = ChargingStationConnections.RegisterPendingStartTransaction(stationId, 2);

        ChargingStationConnections.CompletePendingStartTransaction(stationId, 2, 77);

        Assert.Equal(77, await t2.Task.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.False(t1.Task.IsCompleted);   // a start on connector 2 must not confirm connector 1

        ChargingStationConnections.Remove(stationId);
    }

    [Fact]
    public async Task PendingStart_on_connector_0_is_completed_by_any_connector()
    {
        var stationId = UniqueId();
        ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp1.6");

        var any = ChargingStationConnections.RegisterPendingStartTransaction(stationId, 0);
        ChargingStationConnections.CompletePendingStartTransaction(stationId, 2, 5);

        Assert.Equal(5, await any.Task.WaitAsync(TimeSpan.FromSeconds(1)));
        ChargingStationConnections.Remove(stationId);
    }

    [Fact]
    public async Task PendingStop_is_per_transaction()
    {
        var stationId = UniqueId();
        ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp1.6");

        var s21 = ChargingStationConnections.RegisterPendingStopTransaction(stationId, 21);
        var s22 = ChargingStationConnections.RegisterPendingStopTransaction(stationId, 22);

        ChargingStationConnections.CompletePendingStopTransaction(stationId, 22);

        Assert.Equal(22, await s22.Task.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.False(s21.Task.IsCompleted);

        ChargingStationConnections.Remove(stationId);
    }

    [Fact]
    public void Authorized_car_binds_to_the_only_preparing_connector()
    {
        var stationId = UniqueId();
        ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp1.6");
        var state = ChargingStationConnections.Get(stationId)!;
        state.Connector(1).Status = "Preparing";
        state.Connector(2).Status = "Charging";
        state.Connector(2).LocalTxId = 21;

        var bound = ChargingStationConnections.BindAuthorizedCar(stationId, "001823975bef");

        Assert.Equal(1, bound);
        Assert.Equal("001823975bef", state.Connector(1).CarId);
        Assert.Null(state.UnboundCarId);
        ChargingStationConnections.Remove(stationId);
    }

    [Fact]
    public void Authorized_car_stays_unbound_when_ambiguous()
    {
        var stationId = UniqueId();
        ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp1.6");
        var state = ChargingStationConnections.Get(stationId)!;
        state.Connector(1).Status = "Preparing";
        state.Connector(2).Status = "Preparing";

        Assert.Null(ChargingStationConnections.BindAuthorizedCar(stationId, "98ed5cdc5016"));
        Assert.Equal("98ed5cdc5016", state.UnboundCarId);
        Assert.Null(state.Connector(1).CarId);
        ChargingStationConnections.Remove(stationId);
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
            ids.Add(ChargingStationConnections.RegisterOcpp21Transaction(stationId, 1, $"TX-{i}"));
            ChargingStationConnections.Remove(stationId);
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(50, ids.Distinct().Count());
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    // ── Pending authorize (ISO 15118 VID) ──────────────────────────────────────
    // HandleAuthorize must register the waiter BEFORE publishing the request:
    // an answer that arrives before registration is dropped (Supernova, 2026-11-05).

    [Fact]
    public async Task Authorize_answer_after_register_completes_the_waiter()
    {
        var stationId = UniqueId();
        ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp1.6");

        var tcs = ChargingStationConnections.RegisterPendingAuthorize(stationId);
        ChargingStationConnections.CompletePendingAuthorize(stationId, "Accepted");

        Assert.Equal("Accepted", await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1)));
        ChargingStationConnections.Remove(stationId);
    }

    [Fact]
    public void Authorize_answer_before_register_is_dropped()
    {
        var stationId = UniqueId();
        ChargingStationConnections.Add(stationId, CreateFakeSocket(), "ocpp1.6");

        ChargingStationConnections.CompletePendingAuthorize(stationId, "Accepted"); // too early
        var tcs = ChargingStationConnections.RegisterPendingAuthorize(stationId);

        Assert.False(tcs.Task.IsCompleted);   // why the order in HandleAuthorize matters
        ChargingStationConnections.Remove(stationId);
    }

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
