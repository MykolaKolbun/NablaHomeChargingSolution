using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OCPPServer;
using OCPPServer.Data;
using OCPPServer.DataBase.DBModels;
using OCPPServer.OCPP1._6_Models;
using OCPPServer.Tracing;
using Xunit;

namespace OCPPServer.Tests;

/// <summary>
/// OCPP 1.6 handlers end to end (socket → Communicator → DB + published events) for a
/// two-connector charger, replaying the Wallbox Supernova logs of 2026-11-05:
/// one OCPP id, connectorId 1 and 2, a separate meter per connector, two cars at once.
/// </summary>
public sealed class MultiConnectorFlowTests : IDisposable
{
    private readonly SqliteConnection _sql = new("DataSource=:memory:");
    private readonly ChargingDBContext _db;
    private readonly Communicator _ocpp;
    private readonly RecordingSocket _socket = new();
    private readonly string _id = "wbx" + Guid.NewGuid().ToString("N")[..6];
    private readonly ConcurrentQueue<(string Key, JsonElement Body)> _events = new();

    public MultiConnectorFlowTests()
    {
        OcppTrace.Configure(Path.Combine(Path.GetTempPath(), "evocpp-tests-traces"), level: 0);
        _sql.Open();
        _db = new ChargingDBContext(new DbContextOptionsBuilder<ChargingDBContext>().UseSqlite(_sql).Options);
        _db.Database.EnsureCreated();
        _db.Plugs.Add(new Plug { OcppId = _id, CreatedAt = DateTime.UtcNow });
        _db.SaveChanges();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Ocpp:AuthorizeTimeoutSeconds"] = "2" })
            .Build();
        _ocpp = new Communicator(new NullTracer(), config);

        ChargingStationConnections.Add(_id, _socket, "ocpp1.6");
        RabbitMqPublisher.Published += Capture;
    }

    public void Dispose()
    {
        RabbitMqPublisher.Published -= Capture;
        ChargingStationConnections.Remove(_id);
        _db.Dispose();
        _sql.Dispose();
    }

    private void Capture(string key, string json)
    {
        var body = JsonDocument.Parse(json).RootElement.Clone();
        if (body.TryGetProperty("ocppId", out var id) && id.GetString() == _id)
            _events.Enqueue((key, body));
    }

    private Task Call(string action, object payload)
        => _ocpp.RouteOcppMessage(_socket, _id,
            JsonSerializer.Serialize(new object[] { 2, Guid.NewGuid().ToString(), action, payload }), _db);

    private JsonElement Last(string key) => _events.Where(e => e.Key == key).Last().Body;

    private static object Status(int connectorId, string status)
        => new { connectorId, errorCode = "NoError", status };

    private static object Meter(int connectorId, int txId, decimal energyWh, decimal powerW, int soc) => new
    {
        connectorId,
        transactionId = txId,
        meterValue = new[]
        {
            new
            {
                timestamp = "2026-11-05T17:25:26Z",
                sampledValue = new object[]
                {
                    new { measurand = "Energy.Active.Import.Register", unit = "Wh",      value = energyWh.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    new { measurand = "Power.Active.Import",           unit = "W",       value = powerW.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    new { measurand = "SoC",                           unit = "Percent", value = $"{soc}.000" },
                },
            },
        },
    };

    [Fact]
    public async Task Two_cars_on_one_station_keep_separate_sessions_meters_and_soc()
    {
        // Car A on connector 2 (Autocharge, no Authorize — cached VID)
        await Call("StatusNotification", Status(2, "Preparing"));
        await Call("StartTransaction", new { connectorId = 2, meterStart = 28_256_000, idTag = "VID:98ed5cdc5016", timestamp = "2026-11-05T16:52:23Z" });
        var started2 = Last("charger.transaction.started");
        var txA = started2.GetProperty("transactionId").GetInt32();
        Assert.Equal(2, started2.GetProperty("connectorId").GetInt32());

        // Car B on connector 1 with an RFID card
        await Call("StatusNotification", Status(1, "Preparing"));
        await Call("StartTransaction", new { connectorId = 1, meterStart = 19_107_456, idTag = "085f0d98", timestamp = "2026-11-05T17:24:34Z" });
        var started1 = Last("charger.transaction.started");
        var txB = started1.GetProperty("transactionId").GetInt32();
        Assert.Equal(1, started1.GetProperty("connectorId").GetInt32());
        Assert.NotEqual(txA, txB);

        // Interleaved MeterValues from the two meters (real registers from the log)
        await Call("MeterValues", Meter(2, txA, 28_268_884, 19_500, 95));
        var m2 = Last("charger.meter.updated");
        await Call("MeterValues", Meter(1, txB, 19_107_884, 29_100, 44));
        var m1 = Last("charger.meter.updated");

        Assert.Equal(2, m2.GetProperty("connectorId").GetInt32());
        Assert.Equal(28_256_000m, m2.GetProperty("meterStartWh").GetDecimal());
        Assert.Equal(95m, m2.GetProperty("soc").GetDecimal());
        Assert.Equal(txA, m2.GetProperty("transactionId").GetInt32());

        Assert.Equal(1, m1.GetProperty("connectorId").GetInt32());
        Assert.Equal(19_107_456m, m1.GetProperty("meterStartWh").GetDecimal());   // not connector 2's baseline
        Assert.Equal(44m, m1.GetProperty("soc").GetDecimal());                    // not the other car's SoC
        Assert.Equal(txB, m1.GetProperty("transactionId").GetInt32());

        // Car A's session ends (module fault) — StopTransaction has no connectorId
        await Call("StopTransaction", new { transactionId = txA, meterStop = 28_272_076, reason = "Other", timestamp = "2026-11-05T17:37:58Z" });
        var stopped = Last("charger.transaction.stopped");
        Assert.Equal(2, stopped.GetProperty("connectorId").GetInt32());
        Assert.Equal(28_256_000m, stopped.GetProperty("meterStartWh").GetDecimal());

        // Connector 1 is unaffected
        var state = ChargingStationConnections.Get(_id)!;
        Assert.Equal(txB, state.Connector(1).LocalTxId);
        Assert.Null(state.Connector(2).LocalTxId);

        var rows = await _db.PlugConnectors.AsNoTracking().OrderBy(r => r.ConnectorId).ToListAsync();
        Assert.Equal(new[] { 1, 2 }, rows.Select(r => r.ConnectorId));
        Assert.Equal(txB, rows[0].TransactionId);
        Assert.Null(rows[1].TransactionId);
        Assert.Equal("Preparing", rows[1].Status);
    }

    [Fact]
    public async Task Stop_after_server_restart_finds_connector_and_baseline_in_db()
    {
        await Call("StartTransaction", new { connectorId = 2, meterStart = 28_256_000, idTag = "U1", timestamp = "2026-11-05T16:52:23Z" });
        var tx = Last("charger.transaction.started").GetProperty("transactionId").GetInt32();

        // Server restart: in-memory state is gone, the charger reconnects
        ChargingStationConnections.Remove(_id);
        ChargingStationConnections.Add(_id, _socket, "ocpp1.6");

        await Call("MeterValues", Meter(2, tx, 28_257_900, 34_000, 79));
        var meter = Last("charger.meter.updated");
        Assert.Equal(28_256_000m, meter.GetProperty("meterStartWh").GetDecimal());
        Assert.Equal(tx, meter.GetProperty("transactionId").GetInt32());

        ChargingStationConnections.Remove(_id);
        ChargingStationConnections.Add(_id, _socket, "ocpp1.6");

        await Call("StopTransaction", new { transactionId = tx, meterStop = 28_260_000, reason = "Remote", timestamp = "2026-11-05T17:00:00Z" });
        var stopped = Last("charger.transaction.stopped");
        Assert.Equal(2, stopped.GetProperty("connectorId").GetInt32());
        Assert.Equal(28_256_000m, stopped.GetProperty("meterStartWh").GetDecimal());
        Assert.Null((await _db.PlugConnectors.AsNoTracking().SingleAsync()).TransactionId);
    }

    [Fact]
    public async Task Vid_authorize_is_bound_to_the_preparing_connector_and_a_fast_answer_is_not_lost()
    {
        // Backend answers in the same instant the request is published (the 2026-11-05 race)
        void Answer(string key, string json)
        {
            if (key == "charger.authorize.requested" && json.Contains(_id))
                ChargingStationConnections.CompletePendingAuthorize(_id, "Accepted");
        }
        RabbitMqPublisher.Published += Answer;
        try
        {
            await Call("StatusNotification", Status(2, "Charging"));
            await Call("StatusNotification", Status(1, "Preparing"));
            await Call("Authorize", new { idTag = "VID:001823975bef" });
        }
        finally { RabbitMqPublisher.Published -= Answer; }

        var request = Last("charger.authorize.requested");
        Assert.Equal("001823975bef", request.GetProperty("carId").GetString());
        Assert.Equal(1, request.GetProperty("connectorId").GetInt32());
        Assert.Contains("\"Accepted\"", _socket.Sent.Last());   // not the timeout-Rejected

        await Call("StartTransaction", new { connectorId = 1, meterStart = 100, idTag = "VID:001823975bef", timestamp = "2026-11-05T19:16:30Z" });
        Assert.Equal("001823975bef", ChargingStationConnections.Get(_id)!.Connector(1).CarId);
    }

    [Fact]
    public async Task Station_level_status_and_meter_on_connector_0_do_not_create_a_connector()
    {
        await Call("StatusNotification", Status(0, "Faulted"));
        await Call("MeterValues", Meter(0, 0, 1_000, 0, 0));

        Assert.Empty(await _db.PlugConnectors.ToListAsync());
        Assert.DoesNotContain(_events, e => e.Key == "charger.meter.updated");
        Assert.DoesNotContain(_events, e => e.Key == "charger.status.changed");
    }

    // ── fakes ─────────────────────────────────────────────────────────────────

    private sealed class RecordingSocket : WebSocket
    {
        public ConcurrentQueue<string> Sent { get; } = new();
        public override WebSocketCloseStatus? CloseStatus            => null;
        public override string?               CloseStatusDescription => null;
        public override WebSocketState        State                  => WebSocketState.Open;
        public override string?               SubProtocol            => "ocpp1.6";
        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus s, string? d, CancellationToken ct) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus s, string? d, CancellationToken ct) => Task.CompletedTask;
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> b, CancellationToken ct)
            => Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
        public override Task SendAsync(ArraySegment<byte> b, WebSocketMessageType t, bool e, CancellationToken ct)
        {
            Sent.Enqueue(System.Text.Encoding.UTF8.GetString(b.Array!, b.Offset, b.Count));
            return Task.CompletedTask;
        }
        public override void Dispose() { }
    }

    private sealed class NullTracer : ITracingService
    {
        public void Verbose(string s, string m, string? c = null, int? i = null) { }
        public void Info(string s, string m, string? c = null, int? i = null) { }
        public void Warning(string s, string m, string? c = null, int? i = null) { }
        public void Error(string s, string m, string? c = null, int? i = null) { }
        public void Exception(string s, Exception e, string? x = null, string? c = null, int? i = null) => throw new Xunit.Sdk.XunitException($"{x}: {e}");
    }
}
