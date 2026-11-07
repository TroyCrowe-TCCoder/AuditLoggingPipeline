namespace AuditLoggingPipeline;

/// <summary>
/// Pluggable persistence target for a drained entry. A consuming application registers zero, one or
/// several sinks per plugin. The plugin's background service calls every registered sink for each
/// entry, one after another inside a single DI scope, applying a per-attempt timeout and retry. A
/// sink that still fails is logged and skipped; it never prevents the remaining sinks from running.
/// </summary>
/// <typeparam name="TEntry">The entry type this sink persists.</typeparam>
public interface IEntrySink<TEntry>
{
    /// <summary>
    /// Persists one entry. Throw on failure so the pipeline can retry; the token is cancelled when
    /// the attempt times out or the host is shutting down.
    /// </summary>
    Task WriteAsync(TEntry entry, CancellationToken cancellationToken);
}
