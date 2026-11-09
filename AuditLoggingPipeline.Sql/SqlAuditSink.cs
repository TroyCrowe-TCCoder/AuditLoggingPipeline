using Microsoft.Extensions.Options;

namespace AuditLoggingPipeline;

/// <summary>
/// An <see cref="IAuditSink"/> that stores security-audit entries in the standard audit shape through a
/// stored procedure. See <see cref="SqlAuditSinkOptions"/>.
/// </summary>
public sealed class SqlAuditSink : IAuditSink
{
    private readonly IOptionsMonitor<SqlAuditSinkOptions> _options;

    /// <summary>Creates the sink. Options are read on every write, so changes apply without a restart.</summary>
    public SqlAuditSink(IOptionsMonitor<SqlAuditSinkOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var options = _options.CurrentValue;

        return SqlAuditWriter.WriteAsync(
            options.ConnectionString,
            options.ProcedureName,
            options.ParameterName,
            options.TableTypeName,
            options.CommandTimeoutSeconds,
            AuditRow.From(entry),
            cancellationToken);
    }
}
