using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AuditLoggingPipeline;

/// <summary>
/// An <see cref="ITelemetrySink"/> that stores usage events by calling a stored procedure with a
/// table-valued parameter. It contains no SQL text: the table, the table type and the procedure belong to
/// the consuming application's database project. A batch is written all-or-nothing, so the pipeline can
/// retry it safely.
/// </summary>
public sealed class SqlTelemetrySink : ITelemetrySink
{
    private readonly IOptionsMonitor<SqlTelemetrySinkOptions> _options;

    /// <summary>Creates the sink. Options are read on every write, so changes apply without a restart.</summary>
    public SqlTelemetrySink(IOptionsMonitor<SqlTelemetrySinkOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public async Task WriteBatchAsync(IReadOnlyList<UsageEvent> entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return;
        }

        var options = _options.CurrentValue;
        var chunkSize = Math.Max(1, options.ChunkSize);

        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        for (var offset = 0; offset < entries.Count; offset += chunkSize)
        {
            var count = Math.Min(chunkSize, entries.Count - offset);
            using var rows = UsageEventTable.Create(entries, offset, count);
            await using var command = CreateCommand(connection, transaction, options, rows);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static SqlCommand CreateCommand(SqlConnection connection, SqlTransaction transaction, SqlTelemetrySinkOptions options, DataTable rows)
    {
        var command = new SqlCommand(options.ProcedureName, connection, transaction)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = options.CommandTimeoutSeconds,
        };

        command.Parameters.Add(new SqlParameter(options.ParameterName, SqlDbType.Structured)
        {
            TypeName = options.TableTypeName,
            Value = rows,
        });

        return command;
    }
}
