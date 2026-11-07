namespace AuditLoggingPipeline;

/// <summary>
/// Pluggable persistence target for a drained <see cref="StateTrailEntry"/>. A consuming
/// application registers zero, one, or multiple implementations. Each registered sink is
/// called in turn by <see cref="TrackerLogBackgroundService"/> with retry and a timeout; a sink that
/// still fails is logged and skipped, so it never prevents other sinks from receiving the same entry.
/// </summary>
public interface ITrackerSink : IEntrySink<StateTrailEntry>
{
}
