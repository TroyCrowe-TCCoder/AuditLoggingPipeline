namespace AuditLoggingPipeline;

using System.Threading.Channels;

/// <summary>
/// Singleton bounded channel connecting state-trail producers (the
/// <see cref="TrackingDispatchProxy{TInterface}"/> wrapping each service) to the single consumer
/// (<see cref="TrackerLogBackgroundService"/>). Bounded with a drop-oldest full-mode so a burst
/// of tracked calls can never cause request-path backpressure or unbounded memory growth.
/// Independent of <c>AuditChannel</c>: the Tracker plugin never depends on the Audit plugin, so
/// this type provides its own implementation of <see cref="IEntryChannel{TEntry}"/>.
/// </summary>
public sealed class TrackerChannel : IEntryChannel<StateTrailEntry>
{
    private const int Capacity = 2048;

    private readonly Channel<StateTrailEntry> _channel = Channel.CreateBounded<StateTrailEntry>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

    /// <summary>
    /// Non-blocking enqueue. Returns false (silently dropping the entry) if the channel is
    /// closed; callers must never await or block the calling request on this.
    /// </summary>
    public bool TryWrite(StateTrailEntry entry) => _channel.Writer.TryWrite(entry);

    /// <summary>
    /// Asynchronously drains all state-trail entries as they become available. Intended to be
    /// consumed by exactly one background dispatch loop (<see cref="TrackerLogBackgroundService"/>).
    /// </summary>
    public IAsyncEnumerable<StateTrailEntry> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
