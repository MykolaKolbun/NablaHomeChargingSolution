using OCPPServer.Data;
using OCPPServer.DataBase.DBModels;

namespace OCPPServer.Tracing;

/// <summary>
/// Singleton tracing service that fans out every trace call to three destinations:
///
///   1. <see cref="OcppTrace"/> — rolling file trace (same as a direct OcppTrace call).
///   2. <see cref="ILogger"/> — container stdout / log sink.
///   3. <see cref="ChargingDBContext.ErrorLogs"/> — persisted when level ≥ MinPersistLevel.
///
/// DB writes are fire-and-forget: callers are synchronous and never blocked.
/// A fresh <see cref="ChargingDBContext"/> scope is opened per write (singleton-safe).
///
/// Configuration:
/// <code>
///   "Tracing": { "MinPersistLevel": "Warning" }
/// </code>
/// Values: <c>Info</c> | <c>Warning</c> | <c>Error</c> | <c>Critical</c>
/// </summary>
public sealed class TracingService : ITracingService
{
    private readonly ILogger<TracingService> _logger;
    private readonly IServiceScopeFactory    _scopeFactory;
    private readonly ErrorLogLevel           _minPersistLevel;

    public TracingService(
        ILogger<TracingService> logger,
        IServiceScopeFactory    scopeFactory,
        IConfiguration          configuration)
    {
        _logger       = logger;
        _scopeFactory = scopeFactory;

        _minPersistLevel = Enum.TryParse<ErrorLogLevel>(
            configuration["Tracing:MinPersistLevel"], ignoreCase: true, out var lvl)
            ? lvl
            : ErrorLogLevel.Warning;
    }

    public void Info(string source, string message, string? chargePointId = null, int? sessionId = null)
    {
        OcppTrace.Msg(source, message);
        _logger.LogInformation("[{Source}] {Message}", source, message);
        if (_minPersistLevel <= ErrorLogLevel.Info)
            _ = PersistAsync(ErrorLogLevel.Info, source, message, chargePointId, sessionId);
    }

    public void Warning(string source, string message, string? chargePointId = null, int? sessionId = null)
    {
        OcppTrace.Msg(source, message);
        _logger.LogWarning("[{Source}] {Message}", source, message);
        if (_minPersistLevel <= ErrorLogLevel.Warning)
            _ = PersistAsync(ErrorLogLevel.Warning, source, message, chargePointId, sessionId);
    }

    public void Error(string source, string message, string? chargePointId = null, int? sessionId = null)
    {
        OcppTrace.Error(source, message);
        _logger.LogError("[{Source}] {Message}", source, message);
        if (_minPersistLevel <= ErrorLogLevel.Error)
            _ = PersistAsync(ErrorLogLevel.Error, source, message, chargePointId, sessionId);
    }

    public void Exception(string source, Exception ex, string? context = null,
        string? chargePointId = null, int? sessionId = null)
    {
        var message = string.IsNullOrEmpty(context)
            ? $"{ex.GetType().Name}: {ex.Message}"
            : $"{context} — {ex.GetType().Name}: {ex.Message}";

        OcppTrace.Error(source, message);
        _logger.LogError(ex, "[{Source}] {Message}", source, message);
        if (_minPersistLevel <= ErrorLogLevel.Critical)
            _ = PersistAsync(ErrorLogLevel.Critical, source,
                $"{message}{Environment.NewLine}{ex.StackTrace}",
                chargePointId, sessionId);
    }

    private async Task PersistAsync(ErrorLogLevel level, string source,
        string message, string? chargePointId, int? sessionId)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ChargingDBContext>();
            db.ErrorLogs.Add(new ErrorLog
            {
                Level          = level,
                Source         = source,
                Message        = message,
                ChargePointId  = chargePointId,
                SessionId      = sessionId,
                OccurredAt     = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Never crash the caller — fall back to file trace only.
            OcppTrace.Error("TracingService", $"Failed to persist ErrorLog ({source}): {ex.Message}");
            _logger.LogError(ex, "TracingService: failed to persist ErrorLog ({Source})", source);
        }
    }
}
