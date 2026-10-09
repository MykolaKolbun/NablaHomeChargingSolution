using System.Collections.Concurrent;
using EVHomeAPI.Hubs;
using EVHomeAPI.Ocpp;

namespace EVHomeAPI.Tests.Infrastructure;

/// <summary>Records commands instead of publishing to RabbitMQ.</summary>
public sealed class FakeCommandPublisher : IOcppCommandPublisher
{
    public ConcurrentQueue<object> Sent { get; } = new();
    public bool Fail { get; set; }

    public Task RemoteStartAsync(RemoteStartCommand cmd, CancellationToken ct = default) => Record(cmd);
    public Task RemoteStopAsync(RemoteStopCommand cmd, CancellationToken ct = default) => Record(cmd);
    public Task AuthorizeResponseAsync(AuthorizeResponseCommand cmd, CancellationToken ct = default) => Record(cmd);
    public Task RequestStatusAsync(StatusRequestCommand cmd, CancellationToken ct = default) => Record(cmd);
    public Task SetChargingLimitAsync(ChargingLimitCommand cmd, CancellationToken ct = default) => Record(cmd);
    public Task ClearChargingLimitAsync(ChargingClearCommand cmd, CancellationToken ct = default) => Record(cmd);
    public Task DevCallAsync(DevCallCommand cmd, CancellationToken ct = default) => Record(cmd);

    private Task Record(object cmd)
    {
        if (Fail) throw new InvalidOperationException("broker down");
        Sent.Enqueue(cmd);
        return Task.CompletedTask;
    }

    public T Last<T>() => Sent.OfType<T>().Last();
}

/// <summary>Records SignalR pushes instead of sending them.</summary>
public sealed class FakeNotifier : INotifier
{
    public ConcurrentQueue<object> Sent { get; } = new();

    public Task StatusUpdated(StatusUpdatedMsg m)           => Record(m);
    public Task SessionStarted(SessionStartedMsg m)         => Record(m);
    public Task SessionStartFailed(SessionStartFailedMsg m) => Record(m);
    public Task MeterUpdated(MeterUpdatedMsg m)             => Record(m);
    public Task SessionFinalized(SessionFinalizedMsg m)     => Record(m);
    public Task SessionStopFailed(SessionStopFailedMsg m)   => Record(m);
    public Task SessionPaused(SessionPausedMsg m)           => Record(m);
    public Task ChargingLimitUpdated(ChargingLimitUpdatedMsg m) => Record(m);

    private Task Record(object m) { Sent.Enqueue(m); return Task.CompletedTask; }

    public IEnumerable<T> Of<T>() => Sent.OfType<T>();
}

/// <summary>Stands in for EVOCPP's REST API (dev charger-info proxy).</summary>
public sealed class FakeEvocppHandler : HttpMessageHandler
{
    public string? LastPath { get; private set; }
    public string  Body     { get; set; } = "{\"ocppId\":\"30011\",\"firmwareVersion\":\"6.7.38\"}";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastPath = request.RequestUri!.AbsolutePath;
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(Body, System.Text.Encoding.UTF8, "application/json"),
        });
    }
}

/// <summary>Manually advanced clock.</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}

public sealed class FakePushQueue : EVHomeAPI.Push.IPushQueue
{
    public ConcurrentQueue<EVHomeAPI.Push.PushEvent> Sent { get; } = new();
    public void Enqueue(EVHomeAPI.Push.PushEvent e) => Sent.Enqueue(e);
}

public sealed class FakePushSender : EVHomeAPI.Push.IPushSender
{
    public ConcurrentQueue<(string Token, string Title, string Body)> Sent { get; } = new();
    public HashSet<string> Invalid { get; } = [];
    public bool Enabled => true;

    public Task<EVHomeAPI.Push.PushResult> SendAsync(string token, string title, string body,
        IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
    {
        if (Invalid.Contains(token)) return Task.FromResult(EVHomeAPI.Push.PushResult.InvalidToken);
        Sent.Enqueue((token, title, body));
        return Task.FromResult(EVHomeAPI.Push.PushResult.Sent);
    }
}
