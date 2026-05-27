namespace OCPPServer.Tracing;

/// <summary>
/// Structured tracing service.
///
/// Every call:
///   1. Forwards to <c>OcppTrace</c> for the rolling file trace.
///   2. Writes to <c>ILogger</c> for container stdout.
///   3. Persists to <c>ErrorLogs</c> DB table when level ≥ <c>Tracing:MinPersistLevel</c>.
///
/// Methods are synchronous and fire-and-forget — DB writes happen on a background task
/// so callers are never blocked.
/// </summary>
public interface ITracingService
{
    /// <summary>
    /// Raw in/outgoing message or request/response body.
    /// Persisted only when <c>MinPersistLevel = Verbose</c>. Very high volume — keep disabled
    /// in production unless actively diagnosing a protocol issue.
    /// </summary>
    void Verbose(string source, string message, string? chargePointId = null, int? sessionId = null);

    /// <summary>Normal lifecycle event (charger connected, session started, etc.).</summary>
    void Info(string source, string message, string? chargePointId = null, int? sessionId = null);

    /// <summary>Unexpected but recoverable situation (unknown status code, missing field, etc.).</summary>
    void Warning(string source, string message, string? chargePointId = null, int? sessionId = null);

    /// <summary>
    /// Problem that affects operation of a specific charger or session.
    /// Pass <paramref name="chargePointId"/> and/or <paramref name="sessionId"/> when known —
    /// they help the admin panel identify the affected charger.
    /// </summary>
    void Error(string source, string message, string? chargePointId = null, int? sessionId = null);

    /// <summary>
    /// Unhandled exception. Persists type, message and full stack trace.
    /// </summary>
    void Exception(string source, Exception ex, string? context = null,
        string? chargePointId = null, int? sessionId = null);
}
