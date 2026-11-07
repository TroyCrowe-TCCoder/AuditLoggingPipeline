namespace AuditLoggingPipeline;

/// <summary>Well-known values for <see cref="UsageEvent.Outcome"/>.</summary>
public static class UsageOutcomes
{
    /// <summary>The measured operation completed normally.</summary>
    public const string Success = "Success";

    /// <summary>The measured operation failed or threw.</summary>
    public const string Failure = "Failure";

    /// <summary>The measured operation was cancelled before it finished. Usually not an error.</summary>
    public const string Canceled = "Canceled";
}
