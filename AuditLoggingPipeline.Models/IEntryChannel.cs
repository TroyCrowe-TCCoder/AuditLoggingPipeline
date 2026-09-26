namespace AuditLoggingPipeline;

/// <summary>
/// Non-blocking, plugin-owned channel abstraction connecting an entry producer (a
/// DispatchProxy-based interceptor) to a single background consumer. Each plugin
/// (e.g. Audit, Tracker) provides its own concrete implementation and its own bounded
/// channel instance; this interface only defines the shape so producers and consumers
/// within a plugin can be composed and tested consistently, and so future plugins follow
/// the same non-blocking contract. Implementations must never block the caller of
/// <see cref="TryWrite"/>.
/// </summary>
/// <typeparam name="TEntry">The entry type carried by this plugin (e.g. AuditEntry, StateTrailEntry).</typeparam>
public interface IEntryChannel<TEntry>
{
    /// <summary>
    /// Non-blocking enqueue. Returns false (silently dropping the entry) if the channel is
    /// closed or full per the implementation's chosen full-mode; callers must never await or
    /// block the calling request on this.
    /// </summary>
    bool TryWrite(TEntry entry);

    /// <summary>
    /// Asynchronously drains all entries as they become available. Intended to be consumed by
    /// exactly one background dispatch loop per channel instance.
    /// </summary>
    IAsyncEnumerable<TEntry> ReadAllAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Pluggable persistence target for a drained entry of type <typeparamref name="TEntry"/>. A
/// consuming application registers zero, one, or multiple implementations per plugin; each
/// registered sink is invoked independently and non-blocking of the others by the plugin's
/// background dispatch loop. A failure in one sink's <see cref="WriteAsync(TEntry, CancellationToken)"/> must never
/// prevent other sinks from receiving the same entry.
/// </summary>
/// <typeparam name="TEntry">The entry type this sink persists (e.g. AuditEntry, StateTrailEntry).</typeparam>
public interface IEntrySink<TEntry>
{
    /// <summary>
    /// Persists a single drained entry. Implementations should handle their own failures
    /// internally where possible; any exception thrown here is caught and logged by the
    /// plugin's background dispatch loop and does not affect other registered sinks.
    /// </summary>
    Task WriteAsync(TEntry entry, CancellationToken cancellationToken);
}
