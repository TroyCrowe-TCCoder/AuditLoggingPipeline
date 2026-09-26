namespace AuditLoggingPipeline.Tracker.Tests;

using AuditLoggingPipeline;

public sealed class RecordingTrackerSink : ITrackerSink
{
    private readonly List<StateTrailEntry> _received = [];
    private readonly object _lock = new();

    public IReadOnlyList<StateTrailEntry> Received
    {
        get
        {
            lock (_lock)
            {
                return _received.ToArray();
            }
        }
    }

    public Task WriteAsync(StateTrailEntry entry, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _received.Add(entry);
        }

        return Task.CompletedTask;
    }
}
