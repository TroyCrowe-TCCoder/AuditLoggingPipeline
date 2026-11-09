namespace AuditLoggingPipeline.Sql.Tests;

using System.Data;
using AuditLoggingPipeline;

public class AuditRowTests
{
    private static readonly DateTime When = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    [Fact]
    public void From_AuditEntry_MapsToStandardColumns()
    {
        var entry = new AuditEntry
        {
            TimestampUtc = When,
            Action = "UserRoleInsert",
            Succeeded = false,
            UserId = "oid-1",
            ActorDisplayName = "Pat Doe",
            IpAddress = "10.0.0.1",
            CorrelationId = "corr-1",
            EntityType = "UserRole",
            EntityId = "u1|r1",
            EntityKeys = new Dictionary<string, string> { ["UserId"] = "u1", ["RoleId"] = "r1" },
            Reason = "denied",
        };

        var row = AuditRow.From(entry);

        Assert.Equal(When, row.OccurredOn);
        Assert.Equal("oid-1", row.Who);
        Assert.Equal("Pat Doe", row.WhoName);
        Assert.Equal("UserRoleInsert", row.What);
        Assert.Equal("Failure", row.Outcome);
        Assert.Equal("corr-1", row.CorrelationId);
        Assert.Equal("UserRole", row.EntityType);
        Assert.Equal("u1|r1", row.EntityId);
        Assert.Equal("{\"UserId\":\"u1\",\"RoleId\":\"r1\"}", row.EntityKeys);
        Assert.Null(row.State);
        Assert.Equal("denied", row.Reason);
        Assert.Equal("10.0.0.1", row.IpAddress);
    }

    [Fact]
    public void From_StateTrailEntry_StoresAfterStateAsState()
    {
        var entry = new StateTrailEntry
        {
            TimestampUtc = When,
            Action = "Archived",
            EntityName = "Users",
            EntityId = "u1",
            UserId = "oid-1",
            BeforeState = "{\"old\":1}",
            AfterState = "{\"new\":2}",
            Succeeded = true,
        };

        var row = AuditRow.From(entry);

        Assert.Equal("Users", row.EntityType);
        Assert.Equal("Archived", row.What);
        Assert.Equal("Success", row.Outcome);
        Assert.Equal("{\"new\":2}", row.State);
        Assert.Null(row.EntityKeys);
    }

    [Fact]
    public void From_MissingActorCorrelationAndAddress_StoresUnknown()
    {
        var entry = new AuditEntry { TimestampUtc = When, Succeeded = true };

        var row = AuditRow.From(entry);

        Assert.Equal("unknown", row.Who);
        Assert.Equal("unknown", row.What);
        Assert.Equal("unknown", row.CorrelationId);
        Assert.Equal("unknown", row.EntityType);
        Assert.Equal("unknown", row.IpAddress);
        Assert.Null(row.WhoName);
        Assert.Null(row.EntityId);
    }

    [Fact]
    public void From_NullEntries_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => AuditRow.From((AuditEntry)null!));
        Assert.Throws<ArgumentNullException>(() => AuditRow.From((StateTrailEntry)null!));
    }

    [Fact]
    public void Table_HasColumnsInTableTypeOrder()
    {
        using var table = AuditRowTable.Create(AuditRow.From(new AuditEntry { TimestampUtc = When, Succeeded = true }));

        var names = table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray();

        Assert.Equal(
            ["OccurredOn", "Who", "WhoName", "What", "Outcome", "CorrelationId", "EntityType", "EntityId", "EntityKeys", "State", "Reason", "IpAddress"],
            names);
        Assert.Single(table.Rows);
    }

    [Fact]
    public void Table_NullOptionalValues_AreDbNull()
    {
        using var table = AuditRowTable.Create(AuditRow.From(new AuditEntry { TimestampUtc = When, Succeeded = true }));
        var row = table.Rows[0];

        Assert.Equal(DBNull.Value, row["WhoName"]);
        Assert.Equal(DBNull.Value, row["EntityId"]);
        Assert.Equal(DBNull.Value, row["EntityKeys"]);
        Assert.Equal(DBNull.Value, row["State"]);
        Assert.Equal(DBNull.Value, row["Reason"]);
    }

    [Fact]
    public void Table_TextLongerThanColumn_IsTruncated()
    {
        var entry = new AuditEntry
        {
            TimestampUtc = When,
            Succeeded = true,
            UserId = new string('u', 500),
            Action = new string('a', 500),
            CorrelationId = new string('c', 500),
            EntityType = new string('t', 500),
            EntityId = new string('e', 500),
            Reason = new string('r', 900),
            IpAddress = new string('i', 100),
        };

        using var table = AuditRowTable.Create(AuditRow.From(entry));
        var row = table.Rows[0];

        Assert.Equal(200, ((string)row["Who"]).Length);
        Assert.Equal(100, ((string)row["What"]).Length);
        Assert.Equal(64, ((string)row["CorrelationId"]).Length);
        Assert.Equal(100, ((string)row["EntityType"]).Length);
        Assert.Equal(200, ((string)row["EntityId"]).Length);
        Assert.Equal(500, ((string)row["Reason"]).Length);
        Assert.Equal(45, ((string)row["IpAddress"]).Length);
    }

    [Fact]
    public void Table_LongState_IsNotTruncated()
    {
        var state = new string('s', 50_000);
        var entry = new StateTrailEntry { TimestampUtc = When, EntityName = "Users", Succeeded = true, AfterState = state };

        using var table = AuditRowTable.Create(AuditRow.From(entry));

        Assert.Equal(50_000, ((string)table.Rows[0]["State"]).Length);
    }
}
