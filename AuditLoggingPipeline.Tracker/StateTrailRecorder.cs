namespace AuditLoggingPipeline;

/// <summary>
/// Default <see cref="IStateTrailRecorder"/>. Captures the <see cref="AuditContext"/> synchronously on
/// the calling thread, serializes the supplied states to JSON and enqueues a <see cref="StateTrailEntry"/>.
/// </summary>
public sealed class StateTrailRecorder : IStateTrailRecorder
{
    private readonly TrackerChannel _channel;
    private readonly IAuditContextProvider _contextProvider;

    /// <summary>Creates the recorder.</summary>
    public StateTrailRecorder(TrackerChannel channel, IAuditContextProvider contextProvider)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(contextProvider);

        _channel = channel;
        _contextProvider = contextProvider;
    }

    /// <inheritdoc />
    public bool Record(StateTrailRecord record) => _channel.TryWrite(Build(record));

    /// <inheritdoc />
    public ValueTask<bool> RecordAsync(StateTrailRecord record, CancellationToken cancellationToken = default) =>
        _channel.WriteAsync(Build(record), cancellationToken);

    private StateTrailEntry Build(StateTrailRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var context = _contextProvider.GetContext();

        return new StateTrailEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Action = record.Action,
            EntityName = record.EntityName,
            EntityId = record.EntityId ?? JoinKeys(record.EntityKeys),
            EntityKeys = record.EntityKeys,
            UserId = context.ActorId,
            ActorDisplayName = context.ActorDisplayName,
            IpAddress = context.IpAddress,
            CorrelationId = context.CorrelationId,
            BeforeState = JsonPayload.Serialize(record.BeforeState),
            AfterState = JsonPayload.Serialize(record.AfterState),
            Succeeded = record.Succeeded,
            Reason = record.Reason,
        };
    }

    private static string? JoinKeys(IReadOnlyDictionary<string, string>? keys) =>
        keys is { Count: > 0 } ? string.Join('|', keys.Values) : null;
}
