namespace AuditLoggingPipeline;

/// <summary>
/// Pluggable persistence target for a drained <see cref="AuditEntry"/>. Equivalent to
/// <c>IEntrySink&lt;AuditEntry&gt;</c>; kept as a named interface for discoverability. See
/// <see cref="IEntrySink{TEntry}"/> for the delivery contract (sequential, timeout, retry).
/// </summary>
public interface IAuditSink : IEntrySink<AuditEntry>
{
}
