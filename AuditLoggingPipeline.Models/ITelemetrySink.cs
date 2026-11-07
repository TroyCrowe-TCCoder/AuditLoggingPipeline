namespace AuditLoggingPipeline;

/// <summary>
/// Pluggable persistence target for batches of <see cref="UsageEvent"/>. Equivalent to
/// <c>IBatchSink&lt;UsageEvent&gt;</c>; kept as a named interface for discoverability.
/// </summary>
public interface ITelemetrySink : IBatchSink<UsageEvent>
{
}
