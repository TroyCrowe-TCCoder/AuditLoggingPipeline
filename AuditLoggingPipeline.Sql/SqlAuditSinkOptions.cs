namespace AuditLoggingPipeline;

/// <summary>
/// Settings for <see cref="SqlAuditSink"/>, the security-audit sink. A consuming solution has one security
/// audit table, so there is one procedure and no routing. Each consuming app sets its own values. Only
/// <see cref="ConnectionString"/> has no default.
/// </summary>
public sealed class SqlAuditSinkOptions
{
    /// <summary>Connection string of the database that holds the security audit table. Required.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>The stored procedure that inserts an audit row. Defaults to <c>dbo.SecurityAudit_Insert</c>.</summary>
    public string ProcedureName { get; set; } = "dbo.SecurityAudit_Insert";

    /// <summary>The procedure's table-valued parameter. Defaults to <c>@Entries</c>.</summary>
    public string ParameterName { get; set; } = "@Entries";

    /// <summary>The standard audit table type of that parameter. Defaults to <c>dbo.AuditList</c>.</summary>
    public string TableTypeName { get; set; } = "dbo.AuditList";

    /// <summary>Timeout in seconds for the procedure call. Defaults to 30.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
}
