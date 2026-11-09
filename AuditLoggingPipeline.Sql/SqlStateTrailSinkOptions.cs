namespace AuditLoggingPipeline;

/// <summary>
/// Settings for <see cref="SqlStateTrailSink"/>, the state-tracking sink. State tracking has one table per
/// tracked object, so <see cref="Targets"/> routes each object to the procedure of its table. Each consuming
/// app sets its own values. <see cref="ConnectionString"/> and at least one target are required.
/// </summary>
public sealed class SqlStateTrailSinkOptions
{
    /// <summary>Connection string of the database that holds the state-trail tables. Required.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// The insert procedure for each tracked object, keyed by <see cref="StateTrailEntry.EntityName"/>
    /// (case-insensitive). For example <c>Users</c> maps to <c>dbo.UsersAudit_Insert</c>. An entry whose
    /// object has no target is not written, and the failure is logged by the pipeline.
    /// </summary>
    public Dictionary<string, string> Targets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The procedures' table-valued parameter. Defaults to <c>@Entries</c>.</summary>
    public string ParameterName { get; set; } = "@Entries";

    /// <summary>The standard audit table type of that parameter. Defaults to <c>dbo.AuditList</c>.</summary>
    public string TableTypeName { get; set; } = "dbo.AuditList";

    /// <summary>Timeout in seconds for the procedure call. Defaults to 30.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
}
