namespace AuditLoggingPipeline;

/// <summary>
/// Plugin-owned channel connecting entry producers to a single background consumer. Each plugin
/// (Audit, Tracker, Telemetry) owns its own channel instance so one stream can never delay another.
/// </summary>
/// <typeparam name="TEntry">The entry type carried by this plugin.</typeparam>
public interface IEntryChannel<TEntry>
{
    /// <summary>
    /// Number of entries that were lost because the buffer was full or closed. A non-zero value
    /// means the stream is incomplete and should be investigated.
    /// </summary>
    long DroppedCount { get; }

    /// <summary>
    /// Non-blocking enqueue. Returns false when the entry was not accepted (channel closed, or full
    /// while configured to wait); the loss is counted in <see cref="DroppedCount"/>. Never blocks the caller.
    /// </summary>
    bool TryWrite(TEntry entry);

    /// <summary>
    /// Enqueues an entry, waiting for room when the buffer is full and configured to wait. Use when
    /// an entry must not be lost and brief back-pressure on the caller is acceptable.
    /// </summary>
    ValueTask<bool> WriteAsync(TEntry entry, CancellationToken cancellationToken);

    /// <summary>Drains entries as they become available, until the channel is completed or cancelled.</summary>
    IAsyncEnumerable<TEntry> ReadAllAsync(CancellationToken cancellationToken);
}
