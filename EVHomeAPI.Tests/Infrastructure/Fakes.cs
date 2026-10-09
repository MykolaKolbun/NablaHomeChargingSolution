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

    private Task Record(object m) { Sent.Enqueue(m); return Task.CompletedTask; }

    public IEnumerable<T> Of<T>() => Sent.OfType<T>();
}

/// <summary>Manually advanced clock.</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}
