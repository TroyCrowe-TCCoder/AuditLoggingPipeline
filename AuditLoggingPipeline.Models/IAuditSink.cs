namespace AuditLoggingPipeline;

/// <summary>
/// Pluggable persistence target for a drained <see cref="AuditEntry"/>. A consuming application
/// registers zero, one, or multiple implementations (e.g. multiple action-log sinks writing to
/// different destinations). Each registered sink is invoked independently and non-blocking of
/// the others by the Audit plugin's background dispatch loop; a failure in one sink's
/// <see cref="IEntrySink{TEntry}.WriteAsync(TEntry, CancellationToken)"/> must never prevent other sinks from receiving the same entry.
/// Equivalent to <c>IEntrySink&lt;AuditEntry&gt;</c>; kept as a named interface for
/// discoverability and backward compatibility.
/// </summary>
public interface IAuditSink : IEntrySink<AuditEntry>
{
}
