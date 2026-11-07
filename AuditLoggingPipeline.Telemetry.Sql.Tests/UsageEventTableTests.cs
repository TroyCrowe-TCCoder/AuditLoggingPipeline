namespace AuditLoggingPipeline.Telemetry.Sql.Tests;

using System.Data;
using AuditLoggingPipeline;

public class UsageEventTableTests
{
    private static UsageEvent Event(string name, Action<UsageEventBuilder>? configure = null)
    {
        var builder = new UsageEventBuilder { Name = name };
        configure?.Invoke(builder);
        return builder.Build();
    }

    [Fact]
    public void Create_WithFullEvent_MapsEveryColumnInTableTypeOrder()
    {
        var timestamp = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
        var events = new[]
        {
            new UsageEvent
            {
                TimestampUtc = timestamp,
                Application = "Identity",
                Version = "1.2.3",
                Environment = "Test",
                Name = "IReportService.ExportAsync",
                Category = "Method",
                Target = "IReportService",
                Duration = TimeSpan.FromMilliseconds(12.3456),
                Outcome = UsageOutcomes.Failure,
                UserKey = new string('a', 64),
                SampleWeight = 10,
                Properties = new Dictionary<string, string> { ["format"] = "csv" },
            },
        };

        using var table = UsageEventTable.Create(events, 0, 1);

        Assert.Equal(
            new[] { "OccurredOn", "Application", "Version", "Environment", "Name", "Category", "Target", "DurationMs", "Outcome", "UserKey", "SampleWeight", "Properties" },
            table.Columns.Cast<DataColumn>().Select(column => column.ColumnName));
        var row = Assert.Single(table.Rows.Cast<DataRow>());
        Assert.Equal(timestamp, row["OccurredOn"]);
        Assert.Equal("Identity", row["Application"]);
        Assert.Equal("1.2.3", row["Version"]);
        Assert.Equal("Test", row["Environment"]);
        Assert.Equal("IReportService.ExportAsync", row["Name"]);
        Assert.Equal("Method", row["Category"]);
        Assert.Equal("IReportService", row["Target"]);
        Assert.Equal(12.346m, row["DurationMs"]);
        Assert.Equal(UsageOutcomes.Failure, row["Outcome"]);
        Assert.Equal(new string('a', 64), row["UserKey"]);
        Assert.Equal(10, row["SampleWeight"]);
        Assert.Equal("{\"format\":\"csv\"}", row["Properties"]);
    }

    [Fact]
    public void Create_WithOptionalValuesMissing_WritesDbNull()
    {
        var events = new[] { Event("Minimal") };

        using var table = UsageEventTable.Create(events, 0, 1);

        var row = Assert.Single(table.Rows.Cast<DataRow>());
        Assert.Equal("Minimal", row["Name"]);
        foreach (var column in new[] { "Application", "Version", "Environment", "Category", "Target", "DurationMs", "UserKey", "Properties" })
        {
            Assert.True(row.IsNull(column), column);
        }
    }

    [Fact]
    public void Create_WithEmptyProperties_WritesDbNull()
    {
        var events = new[] { Event("Empty", builder => builder.Properties = new Dictionary<string, string>()) };

        using var table = UsageEventTable.Create(events, 0, 1);

        Assert.True(table.Rows[0].IsNull("Properties"));
    }

    [Fact]
    public void Create_WithTextLongerThanColumn_TruncatesToColumnWidth()
    {
        var events = new[]
        {
            Event(new string('n', 500), builder =>
            {
                builder.Application = new string('a', 500);
                builder.Version = new string('v', 500);
                builder.Environment = new string('e', 500);
                builder.Category = new string('c', 500);
                builder.Target = new string('t', 500);
                builder.Outcome = new string('o', 500);
                builder.UserKey = new string('u', 500);
            }),
        };

        using var table = UsageEventTable.Create(events, 0, 1);

        var row = table.Rows[0];
        Assert.Equal(200, ((string)row["Name"]).Length);
        Assert.Equal(100, ((string)row["Application"]).Length);
        Assert.Equal(50, ((string)row["Version"]).Length);
        Assert.Equal(50, ((string)row["Environment"]).Length);
        Assert.Equal(100, ((string)row["Category"]).Length);
        Assert.Equal(200, ((string)row["Target"]).Length);
        Assert.Equal(20, ((string)row["Outcome"]).Length);
        Assert.Equal(64, ((string)row["UserKey"]).Length);
    }

    [Fact]
    public void Create_WithOffsetAndCount_ReturnsOnlyThatSlice()
    {
        var events = Enumerable.Range(0, 5).Select(index => Event("E" + index)).ToList();

        using var table = UsageEventTable.Create(events, 2, 2);

        Assert.Equal(new[] { "E2", "E3" }, table.Rows.Cast<DataRow>().Select(row => (string)row["Name"]));
    }

    private sealed class UsageEventBuilder
    {
        public required string Name { get; init; }

        public string? Application { get; set; }

        public string? Version { get; set; }

        public string? Environment { get; set; }

        public string? Category { get; set; }

        public string? Target { get; set; }

        public string Outcome { get; set; } = UsageOutcomes.Success;

        public string? UserKey { get; set; }

        public IReadOnlyDictionary<string, string>? Properties { get; set; }

        public UsageEvent Build() => new()
        {
            TimestampUtc = DateTime.UtcNow,
            Name = Name,
            Application = Application,
            Version = Version,
            Environment = Environment,
            Category = Category,
            Target = Target,
            Outcome = Outcome,
            UserKey = UserKey,
            Properties = Properties,
        };
    }
}
