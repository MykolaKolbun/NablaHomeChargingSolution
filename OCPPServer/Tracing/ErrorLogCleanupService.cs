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
///     "RetentionDays": 3,
///     "CleanupHour":   3,
///     "TimezoneId":    "Europe/Kyiv"
///   }
/// </code>
/// </summary>
public sealed class ErrorLogCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ErrorLogCleanupService> _logger;
    private readonly int          _retentionDays;
    private readonly int          _cleanupHour;
    private readonly TimeZoneInfo _tz;

    private DateTime _lastRanDate = DateTime.MinValue;

    public ErrorLogCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<ErrorLogCleanupService> logger,
        IConfiguration configuration)
    {
        _scopeFactory  = scopeFactory;
        _logger        = logger;
        _retentionDays = configuration.GetValue("Tracing:RetentionDays", 3);
        _cleanupHour   = configuration.GetValue("Tracing:CleanupHour",   3);

        var tzId = configuration["Tracing:TimezoneId"] ?? "Europe/Kyiv";
        _tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
    }

    private DateTime LocalNow() =>
        DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz),
            DateTimeKind.Unspecified);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) { break; }

            var now = LocalNow();
            if (now.Hour == _cleanupHour && now.Date != _lastRanDate)
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
            var cutoff  = LocalNow().AddDays(-_retentionDays);
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
