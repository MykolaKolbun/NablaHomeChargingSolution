using System.Text.Json;

namespace OCPPServer;

public interface IOcppCommandHandler
{
    Task HandleAsync(string routingKey, string json, CancellationToken ct);
}
