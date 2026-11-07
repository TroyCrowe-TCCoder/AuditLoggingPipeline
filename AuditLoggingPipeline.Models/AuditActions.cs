namespace AuditLoggingPipeline;

/// <summary>Common action verbs for <see cref="AuditEntry.Action"/> and <see cref="StateTrailEntry.Action"/>. Callers may use any string.</summary>
public static class AuditActions
{
    /// <summary>A new record was created.</summary>
    public const string Insert = "Insert";

    /// <summary>An existing record was changed.</summary>
    public const string Update = "Update";

    /// <summary>A record was archived (soft deleted).</summary>
    public const string Archive = "Archive";

    /// <summary>An archived record was restored.</summary>
    public const string Restore = "Restore";

    /// <summary>A record was permanently deleted.</summary>
    public const string Delete = "Delete";
}
