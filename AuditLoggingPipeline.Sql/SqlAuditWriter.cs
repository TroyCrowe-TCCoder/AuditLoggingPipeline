using System.Data;
using Microsoft.Data.SqlClient;

namespace AuditLoggingPipeline;

/// <summary>
/// Writes one audit row by calling a stored procedure with the standard table-valued parameter, on a
/// connection and transaction of its own. Shared by both SQL sinks. It contains no SQL text: the table, the
/// table type and the procedure belong to the consuming application's database project.
/// </summary>
internal static class SqlAuditWriter
{
    public static async Task WriteAsync(
        string connectionString,
        string procedureName,
        string parameterName,
        string tableTypeName,
        int commandTimeoutSeconds,
        AuditRow row,
        CancellationToken cancellationToken)
    {
        using var rows = AuditRowTable.Create(row);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (var command = new SqlCommand(procedureName, connection, transaction)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = commandTimeoutSeconds,
        })
        {
            command.Parameters.Add(new SqlParameter(parameterName, SqlDbType.Structured)
            {
                TypeName = tableTypeName,
                Value = rows,
            });

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
