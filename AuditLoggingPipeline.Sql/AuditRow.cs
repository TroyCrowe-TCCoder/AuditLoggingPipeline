using System.Text.Json;

namespace AuditLoggingPipeline;

/// <summary>
/// One row in the standard audit shape. Both the security-audit entry and the state-trail entry are
/// converted to this, so every audit table in every app has the same columns.
/// </summary>
internal sealed class AuditRow
{
    private const string Unknown = "unknown";

    public DateTime OccurredOn { get; init; }

    public string Who { get; init; } = Unknown;

    public string? WhoName { get; init; }

    public string What { get; init; } = Unknown;

    public string Outcome { get; init; } = AuditEntry.SuccessOutcome;

    public string CorrelationId { get; init; } = Unknown;

    public string EntityType { get; init; } = Unknown;

    public string? EntityId { get; init; }

    public string? EntityKeys { get; init; }

    public string? State { get; init; }

    public string? Reason { get; init; }

    public string IpAddress { get; init; } = Unknown;

    /// <summary>Converts a security-audit entry. It carries no record state.</summary>
    public static AuditRow From(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new AuditRow
        {
            OccurredOn = entry.TimestampUtc,
            Who = Required(entry.UserId),
            WhoName = entry.ActorDisplayName,
            What = Required(entry.Action),
            Outcome = entry.Outcome,
            CorrelationId = Required(entry.CorrelationId),
            EntityType = Required(entry.EntityType),
            EntityId = entry.EntityId,
            EntityKeys = Serialize(entry.EntityKeys),
            Reason = entry.Reason,
            IpAddress = Required(entry.IpAddress),
        };
    }

    /// <summary>Converts a state-trail entry. The after-state is the record state.</summary>
    public static AuditRow From(StateTrailEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new AuditRow
        {
            OccurredOn = entry.TimestampUtc,
            Who = Required(entry.UserId),
            WhoName = entry.ActorDisplayName,
            What = Required(entry.Action),
            Outcome = entry.Succeeded ? AuditEntry.SuccessOutcome : AuditEntry.FailureOutcome,
            CorrelationId = Required(entry.CorrelationId),
            EntityType = Required(entry.EntityName),
            EntityId = entry.EntityId,
            EntityKeys = Serialize(entry.EntityKeys),
            State = entry.AfterState,
            Reason = entry.Reason,
            IpAddress = Required(entry.IpAddress),
        };
    }

    private static string Required(string? value) => string.IsNullOrWhiteSpace(value) ? Unknown : value;

    private static string? Serialize(IReadOnlyDictionary<string, string>? keys) =>
        keys is { Count: > 0 } ? JsonSerializer.Serialize(keys) : null;
}
