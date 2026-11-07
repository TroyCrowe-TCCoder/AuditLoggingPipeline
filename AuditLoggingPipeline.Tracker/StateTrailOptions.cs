namespace AuditLoggingPipeline;

/// <summary>Options for the Tracker plugin. Delivery and method-matching settings are inherited.</summary>
public sealed class StateTrailOptions : InterceptionOptions
{
    /// <summary>
    /// Optional resolver that returns the identifier of the entity a call is changing. When it is
    /// null or returns null, the proxy looks for an <c>Id</c> or <c>{EntityName}Id</c> property on the entity.
    /// </summary>
    public Func<object, string?>? EntityIdResolver { get; set; }
}
