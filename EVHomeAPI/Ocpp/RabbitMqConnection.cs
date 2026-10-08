using RabbitMQ.Client;

namespace EVHomeAPI.Ocpp;

public class RabbitMqOptions
{
    public const string Section = "RabbitMQ";
    public string Host        { get; set; } = "rabbitmq";
    public string Username    { get; set; } = "guest";
    public string Password    { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
}

/// <summary>
/// One shared AMQP connection for the consumer and the publisher (channels are per user).
/// Connects lazily with retry so the API starts even while RabbitMQ is still booting.
/// </summary>
public sealed class RabbitMqConnection(RabbitMqOptions options, ILogger<RabbitMqConnection> logger) : IAsyncDisposable
{
    public const string EventsExchange   = "ocpp.events";
    public const string CommandsExchange = "ocpp.commands";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true }) return _connection;

        await _lock.WaitAsync(ct);
        try
        {
            while (_connection is not { IsOpen: true })
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var factory = new ConnectionFactory
                    {
                        HostName                 = options.Host,
                        UserName                 = options.Username,
                        Password                 = options.Password,
                        VirtualHost              = options.VirtualHost,
                        AutomaticRecoveryEnabled = true,   // reconnects channels/consumers after broker restart
                        ClientProvidedName       = "evhomeapi",
                    };
                    _connection = await factory.CreateConnectionAsync(ct);
                    logger.LogInformation("RabbitMQ connected to {Host} vhost {VHost}", options.Host, options.VirtualHost);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning("RabbitMQ not ready ({Message}), retrying in 5 s", ex.Message);
                    await Task.Delay(5_000, ct);
                }
            }
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
        _lock.Dispose();
    }
}
