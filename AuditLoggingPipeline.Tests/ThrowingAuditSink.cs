namespace AuditLoggingPipeline.Tests;

public sealed class ThrowingAuditSink : IAuditSink
{
    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("sink failure");
}
