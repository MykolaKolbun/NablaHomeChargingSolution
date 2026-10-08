using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OCPPServer.DataBase.DBModels;

/// <summary>
/// Persisted trace entry — written by <see cref="OCPPServer.Tracing.TracingService"/>
/// when the event level is at or above <c>Tracing:MinPersistLevel</c>.
///
/// OCPPServer adaptation of the EVChargingApi ErrorLog pattern:
///   <c>ChargePointId</c> (string) replaces the user FK — chargers, not users, are
///   the relevant context here.
/// </summary>
public class ErrorLog
{
    [Key]
    public int Id { get; set; }

    /// <summary>OcppId of the charger this event relates to. Null for infrastructure events
    /// (RabbitMQ connection, server startup, watcher scan failures).</summary>
    [MaxLength(100)]
    public string? ChargePointId { get; set; }

    /// <summary>Local transaction ID (ConnectorState.LocalTxId) when the event occurs inside
    /// an active session. Null outside a transaction.</summary>
    public int? SessionId { get; set; }

    /// <summary>Local timestamp (Europe/Kyiv) when the event occurred.</summary>
    [Column(TypeName = "timestamp without time zone")]
    public DateTime OccurredAt { get; set; }

    /// <summary>Severity level.</summary>
    [Required]
    public ErrorLogLevel Level { get; set; }

    /// <summary>Short stable identifier for the originating component (e.g. "Authorize", "Watcher").</summary>
    [Required, MaxLength(100)]
    public string Source { get; set; } = string.Empty;

    /// <summary>Human-readable event description. For Exception entries includes stack trace.</summary>
    [Required]
    public string Message { get; set; } = string.Empty;

    /// <summary>Set to true by an administrator once the issue has been investigated.</summary>
    public bool IsSolved { get; set; } = false;

    /// <summary>Local timestamp (Europe/Kyiv) when the issue was marked as solved.</summary>
    [Column(TypeName = "timestamp without time zone")]
    public DateTime? SolvedAt { get; set; }
}

public enum ErrorLogLevel
{
    Verbose  = -1,  // All in/outgoing messages and responses — very high volume
    Info     =  0,  // Normal lifecycle events
    Warning  =  1,  // Unexpected but recoverable
    Error    =  2,  // Operation-impacting problem
    Critical =  3,  // Unhandled exception — always persisted
}
