namespace AuditLoggingPipeline;

/// <summary>
/// Settings for <see cref="SqlTelemetrySink"/>. Each consuming application sets its own values, because
/// each application has its own telemetry database. Only <see cref="ConnectionString"/> has no default.
/// </summary>
public sealed class SqlTelemetrySinkOptions
{
    /// <summary>Connection string of the database that holds the telemetry tables. Required.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>The stored procedure that inserts a batch. Defaults to <c>dbo.UsageEvent_Insert</c>.</summary>
    public string ProcedureName { get; set; } = "dbo.UsageEvent_Insert";

    /// <summary>The procedure's table-valued parameter. Defaults to <c>@Events</c>.</summary>
    public string ParameterName { get; set; } = "@Events";

    /// <summary>The table type of that parameter. Defaults to <c>dbo.UsageEventList</c>.</summary>
    public string TableTypeName { get; set; } = "dbo.UsageEventList";

    /// <summary>
    /// The most rows sent in one procedure call. Larger batches are split into chunks that all commit
    /// together in one transaction. Defaults to 100.
    /// </summary>
    public int ChunkSize { get; set; } = 100;

    /// <summary>Timeout in seconds for each procedure call. Defaults to 30.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
}
