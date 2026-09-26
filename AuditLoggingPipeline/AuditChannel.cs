namespace AuditLoggingPipeline;

using System.Threading.Channels;

/// <summary>
/// Singleton bounded channel connecting audit producers (the <see cref="AuditingDispatchProxy{TInterface}"/>
/// wrapping each service) to the single consumer (<see cref="AuditLogBackgroundService"/>).
/// Bounded with a drop-oldest full-mode so a burst of audited calls can never cause request-path
/// backpressure or unbounded memory growth.
/// </summary>
public sealed class AuditChannel : IEntryChannel<AuditEntry>
{
    private const int Capacity = 2048;

    private readonly Channel<AuditEntry> _channel = Channel.CreateBounded<AuditEntry>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

    /// <summary>The underlying channel reader, exposed for test/diagnostic purposes.</summary>
    public ChannelReader<AuditEntry> Reader => _channel.Reader;

    /// <summary>
    /// Non-blocking enqueue. Returns false (silently dropping the entry) if the channel is
    /// closed; callers must never await or block the calling request on this.
    /// </summary>
    public bool TryWrite(AuditEntry entry) => _channel.Writer.TryWrite(entry);

    /// <summary>
    /// Asynchronously drains all audit entries as they become available. Intended to be
    /// consumed by exactly one background dispatch loop (<see cref="AuditLogBackgroundService"/>).
    /// </summary>
    public IAsyncEnumerable<AuditEntry> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
