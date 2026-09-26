namespace AuditLoggingPipeline;

/// <summary>
/// Pluggable persistence target for a drained <see cref="StateTrailEntry"/>. A consuming
/// application registers zero, one, or multiple implementations. Each registered sink is
/// invoked independently and non-blocking of the others by
/// <see cref="TrackerLogBackgroundService"/>; a failure in one sink's <see cref="IEntrySink{TEntry}.WriteAsync(TEntry, CancellationToken)"/>
/// must never prevent other sinks from receiving the same entry.
/// </summary>
public interface ITrackerSink : IEntrySink<StateTrailEntry>
{
}
