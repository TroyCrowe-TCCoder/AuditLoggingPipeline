namespace AuditLoggingPipeline;

/// <summary>
/// Default <see cref="IAuditRecorder"/>. Captures the <see cref="AuditContext"/> synchronously on the
/// calling thread, builds the <see cref="AuditEntry"/> and enqueues it.
/// </summary>
public sealed class AuditRecorder : IAuditRecorder
{
    private readonly AuditChannel _channel;
    private readonly IAuditContextProvider _contextProvider;

    /// <summary>Creates the recorder.</summary>
    public AuditRecorder(AuditChannel channel, IAuditContextProvider contextProvider)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(contextProvider);

        _channel = channel;
        _contextProvider = contextProvider;
    }

    /// <inheritdoc />
    public bool Record(AuditRecord record) => _channel.TryWrite(Build(record));

    /// <inheritdoc />
    public ValueTask<bool> RecordAsync(AuditRecord record, CancellationToken cancellationToken = default) =>
        _channel.WriteAsync(Build(record), cancellationToken);

    private AuditEntry Build(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var context = _contextProvider.GetContext();

        return new AuditEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Action = record.Action,
            Succeeded = record.Succeeded,
            UserId = context.ActorId,
            ActorDisplayName = context.ActorDisplayName,
            IpAddress = context.IpAddress,
            CorrelationId = context.CorrelationId,
            EntityType = record.EntityType,
            EntityId = record.EntityId,
            Reason = record.Reason,
        };
    }
}
