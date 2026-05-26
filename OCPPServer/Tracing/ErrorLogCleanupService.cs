using Microsoft.EntityFrameworkCore;
using OCPPServer.Data;

namespace OCPPServer.Tracing;

/// <summary>
/// Background service that deletes stale <see cref="OCPPServer.DataBase.DBModels.ErrorLog"/> rows
/// once per day at the configured UTC hour.
///
/// Configuration:
/// <code>
///   "Tracing": {
///     "RetentionDays":  3,
///     "CleanupHourUtc": 3
///   }
/// </code>
/// </summary>
public sealed class ErrorLogCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ErrorLogCleanupService> _logger;
    private readonly int _retentionDays;
    private readonly int _cleanupHourUtc;

    private DateTime _lastRanDate = DateTime.MinValue;

    public ErrorLogCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<ErrorLogCleanupService> logger,
        IConfiguration configuration)
    {
        _scopeFactory   = scopeFactory;
        _logger         = logger;
        _retentionDays  = configuration.GetValue("Tracing:RetentionDays",  3);
        _cleanupHourUtc = configuration.GetValue("Tracing:CleanupHourUtc", 3);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) { break; }

            var now = DateTime.UtcNow;
            if (now.Hour == _cleanupHourUtc && now.Date != _lastRanDate)
            {
                _lastRanDate = now.Date;
                await RunCleanupAsync(stoppingToken);
            }
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db      = scope.ServiceProvider.GetRequiredService<ChargingDBContext>();
            var cutoff  = DateTime.UtcNow.AddDays(-_retentionDays);
            var deleted = await db.ErrorLogs
                .Where(e => e.OccurredAt < cutoff)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
            {
                OcppTrace.Msg("Cleanup", $"Deleted {deleted} ErrorLog rows older than {_retentionDays} days");
                _logger.LogInformation("ErrorLogCleanup: deleted {Count} rows", deleted);
            }
        }
        catch (Exception ex)
        {
            OcppTrace.Error("Cleanup", $"ErrorLog cleanup failed: {ex.Message}");
            _logger.LogError(ex, "ErrorLogCleanup failed");
        }
    }
}
