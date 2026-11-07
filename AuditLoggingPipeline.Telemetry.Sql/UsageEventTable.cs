using System.Data;
using System.Text.Json;

namespace AuditLoggingPipeline;

/// <summary>
/// Builds the table-valued parameter value for the <c>dbo.UsageEventList</c> table type from
/// <see cref="UsageEvent"/> rows. Columns are in the table type's order. Text longer than its column is
/// truncated, because one over-long value would otherwise fail the whole batch on every retry and lose
/// every event in it.
/// </summary>
internal static class UsageEventTable
{
    private const int ApplicationLength = 100;
    private const int VersionLength = 50;
    private const int EnvironmentLength = 50;
    private const int NameLength = 200;
    private const int CategoryLength = 100;
    private const int TargetLength = 200;
    private const int OutcomeLength = 20;
    private const int UserKeyLength = 64;

    /// <summary>Creates the rows for <paramref name="count"/> events starting at <paramref name="offset"/>.</summary>
    public static DataTable Create(IReadOnlyList<UsageEvent> events, int offset, int count)
    {
        var table = new DataTable();
        table.Columns.Add("OccurredOn", typeof(DateTime));
        table.Columns.Add("Application", typeof(string));
        table.Columns.Add("Version", typeof(string));
        table.Columns.Add("Environment", typeof(string));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Category", typeof(string));
        table.Columns.Add("Target", typeof(string));
        table.Columns.Add("DurationMs", typeof(decimal));
        table.Columns.Add("Outcome", typeof(string));
        table.Columns.Add("UserKey", typeof(string));
        table.Columns.Add("SampleWeight", typeof(int));
        table.Columns.Add("Properties", typeof(string));

        for (var index = offset; index < offset + count; index++)
        {
            var usageEvent = events[index];
            table.Rows.Add(
                usageEvent.TimestampUtc,
                Fit(usageEvent.Application, ApplicationLength),
                Fit(usageEvent.Version, VersionLength),
                Fit(usageEvent.Environment, EnvironmentLength),
                Fit(usageEvent.Name, NameLength),
                Fit(usageEvent.Category, CategoryLength),
                Fit(usageEvent.Target, TargetLength),
                usageEvent.Duration is { } duration ? Math.Round((decimal)duration.TotalMilliseconds, 3) : DBNull.Value,
                Fit(usageEvent.Outcome, OutcomeLength),
                Fit(usageEvent.UserKey, UserKeyLength),
                usageEvent.SampleWeight,
                usageEvent.Properties is { Count: > 0 } properties ? JsonSerializer.Serialize(properties) : DBNull.Value);
        }

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
