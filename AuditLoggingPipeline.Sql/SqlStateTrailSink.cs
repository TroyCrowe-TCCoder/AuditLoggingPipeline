using Microsoft.Extensions.Options;

namespace AuditLoggingPipeline;

/// <summary>
/// An <see cref="ITrackerSink"/> that stores state-trail entries in the standard audit shape. It routes each
/// entry by <see cref="StateTrailEntry.EntityName"/> to the procedure of that object's table. See
/// <see cref="SqlStateTrailSinkOptions"/>.
/// </summary>
public sealed class SqlStateTrailSink : ITrackerSink
{
    private readonly IOptionsMonitor<SqlStateTrailSinkOptions> _options;

    /// <summary>Creates the sink. Options are read on every write, so changes apply without a restart.</summary>
    public SqlStateTrailSink(IOptionsMonitor<SqlStateTrailSinkOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public Task WriteAsync(StateTrailEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var options = _options.CurrentValue;

        if (!options.Targets.TryGetValue(entry.EntityName, out var procedureName) || string.IsNullOrWhiteSpace(procedureName))
        {
            throw new InvalidOperationException($"No state-trail target is configured for '{entry.EntityName}'.");
        }

        return SqlAuditWriter.WriteAsync(
            options.ConnectionString,
            procedureName,
            options.ParameterName,
            options.TableTypeName,
            options.CommandTimeoutSeconds,
            AuditRow.From(entry),
            cancellationToken);
    }
}
