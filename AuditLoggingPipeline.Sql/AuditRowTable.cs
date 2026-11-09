using System.Data;

namespace AuditLoggingPipeline;

/// <summary>
/// Builds the table-valued parameter value for the standard <c>dbo.AuditList</c> table type. Columns are in
/// the table type's order. Text longer than its column is truncated, because one over-long value would
/// otherwise fail the write on every retry and lose the entry.
/// </summary>
internal static class AuditRowTable
{
    private const int WhoLength = 200;
    private const int WhoNameLength = 200;
    private const int WhatLength = 100;
    private const int OutcomeLength = 10;
    private const int CorrelationIdLength = 64;
    private const int EntityTypeLength = 100;
    private const int EntityIdLength = 200;
    private const int ReasonLength = 500;
    private const int IpAddressLength = 45;

    /// <summary>Creates a one-row table for <paramref name="row"/>.</summary>
    public static DataTable Create(AuditRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var table = new DataTable();
        table.Columns.Add("OccurredOn", typeof(DateTime));
        table.Columns.Add("Who", typeof(string));
        table.Columns.Add("WhoName", typeof(string));
        table.Columns.Add("What", typeof(string));
        table.Columns.Add("Outcome", typeof(string));
        table.Columns.Add("CorrelationId", typeof(string));
        table.Columns.Add("EntityType", typeof(string));
        table.Columns.Add("EntityId", typeof(string));
        table.Columns.Add("EntityKeys", typeof(string));
        table.Columns.Add("State", typeof(string));
        table.Columns.Add("Reason", typeof(string));
        table.Columns.Add("IpAddress", typeof(string));

        table.Rows.Add(
            row.OccurredOn,
            Fit(row.Who, WhoLength),
            Fit(row.WhoName, WhoNameLength),
            Fit(row.What, WhatLength),
            Fit(row.Outcome, OutcomeLength),
            Fit(row.CorrelationId, CorrelationIdLength),
            Fit(row.EntityType, EntityTypeLength),
            Fit(row.EntityId, EntityIdLength),
            row.EntityKeys is null ? DBNull.Value : row.EntityKeys,
            row.State is null ? DBNull.Value : row.State,
            Fit(row.Reason, ReasonLength),
            Fit(row.IpAddress, IpAddressLength));

        return table;
    }

    private static object Fit(string? value, int maxLength)
    {
        if (value is null)
        {
            return DBNull.Value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
