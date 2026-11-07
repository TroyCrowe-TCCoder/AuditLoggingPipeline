using System.Threading.Channels;

namespace AuditLoggingPipeline;

/// <summary>
/// Bounded in-memory channel shared by every plugin. Counts every entry it loses, supports a
/// non-blocking write and an awaitable write, and can be completed so the consumer drains what is
/// left at shutdown.
/// </summary>
/// <typeparam name="TEntry">The entry type carried by the plugin.</typeparam>
public abstract class BoundedEntryChannel<TEntry> : IEntryChannel<TEntry>
    where TEntry : class
{
    private readonly Channel<TEntry> _channel;
    private readonly BoundedChannelFullMode _fullMode;
    private long _droppedCount;

    /// <summary>Creates the channel from the given delivery options.</summary>
    protected BoundedEntryChannel(EntryPipelineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _fullMode = options.FullMode;
        _channel = Channel.CreateBounded<TEntry>(
            new BoundedChannelOptions(options.ChannelCapacity)
            {
                FullMode = options.FullMode,
                SingleReader = true,
                SingleWriter = false,
            },
            _ => Interlocked.Increment(ref _droppedCount));
    }

    /// <summary>The underlying channel reader, exposed for tests and diagnostics.</summary>
    public ChannelReader<TEntry> Reader => _channel.Reader;

    /// <inheritdoc />
    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    /// <inheritdoc />
    public bool TryWrite(TEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (_channel.Writer.TryWrite(entry))
        {
            return true;
        }

        Interlocked.Increment(ref _droppedCount);
        return false;
    }

    /// <inheritdoc />
    public async ValueTask<bool> WriteAsync(TEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (_fullMode != BoundedChannelFullMode.Wait)
        {
            return TryWrite(entry);
        }

        while (await _channel.Writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_channel.Writer.TryWrite(entry))
            {
                return true;
            }
        }

        Interlocked.Increment(ref _droppedCount);
        return false;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TEntry> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    /// <summary>Stops accepting entries so the consumer can drain what is buffered and finish.</summary>
    public void Complete() => _channel.Writer.TryComplete();
}
