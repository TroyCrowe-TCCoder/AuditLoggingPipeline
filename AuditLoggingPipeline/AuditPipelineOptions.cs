namespace AuditLoggingPipeline;

/// <summary>Options for the Audit plugin. Delivery and method-matching settings are inherited.</summary>
public sealed class AuditPipelineOptions : InterceptionOptions
{
    /// <summary>
    /// When true the dispatch proxy serializes each recorded call's arguments into
    /// <see cref="AuditEntry.Arguments"/>. Off by default because arguments can contain sensitive data.
    /// </summary>
    public bool IncludeArguments { get; set; }
}
