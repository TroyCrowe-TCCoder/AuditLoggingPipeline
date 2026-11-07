namespace AuditLoggingPipeline;

/// <summary>
/// Pluggable persistence target that receives entries in batches, for high-volume streams such as
/// telemetry where one write per entry would be wasteful. The plugin's background service calls every
/// registered sink for each batch, one after another inside a single DI scope, applying a per-attempt
/// timeout and retry. A sink that still fails is logged and skipped; it never prevents the remaining
/// sinks from running.
/// </summary>
/// <typeparam name="TEntry">The entry type this sink persists.</typeparam>
public interface IBatchSink<TEntry>
{
    /// <summary>
    /// Persists a batch. Throw on failure so the pipeline can retry the whole batch, so the write
    /// should be all-or-nothing. The token is cancelled when the attempt times out or the host is
    /// shutting down.
    /// </summary>
    Task WriteBatchAsync(IReadOnlyList<TEntry> entries, CancellationToken cancellationToken);
}
