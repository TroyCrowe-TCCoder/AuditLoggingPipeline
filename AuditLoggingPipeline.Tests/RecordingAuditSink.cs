namespace AuditLoggingPipeline.Tests;

public sealed class RecordingAuditSink : IAuditSink
{
    private readonly List<AuditEntry> _received = [];
    private readonly object _lock = new();

    public IReadOnlyList<AuditEntry> Received
    {
        get
        {
            lock (_lock)
            {
                return _received.ToArray();
            }
        }
    }

    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _received.Add(entry);
        }

        return Task.CompletedTask;
    }
}

public sealed class ThrowingAuditSink : IAuditSink
{
    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("sink failure");
}
