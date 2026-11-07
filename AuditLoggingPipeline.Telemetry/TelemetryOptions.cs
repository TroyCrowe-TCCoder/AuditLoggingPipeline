using System.Reflection;

namespace AuditLoggingPipeline;

/// <summary>Options for the Telemetry plugin. Delivery and batching settings are inherited.</summary>
public sealed class TelemetryOptions : BatchPipelineOptions
{
    /// <summary>The application name stamped on every event.</summary>
    public string? ApplicationName { get; set; }

    /// <summary>The application version stamped on every event.</summary>
    public string? ApplicationVersion { get; set; }

    /// <summary>The environment name stamped on every event, for example <c>Production</c>.</summary>
    public string? EnvironmentName { get; set; }

    /// <summary>
    /// Fraction of successful events to keep, from above 0 up to 1. Below 1, each kept event carries a
    /// <see cref="UsageEvent.SampleWeight"/> of the reciprocal so counts can be reconstructed. Failed
    /// and cancelled events are always kept. Defaults to 1, which keeps everything.
    /// </summary>
    public double SampleRate { get; set; } = 1.0;

    /// <summary>
    /// When true, the recorder asks the registered <see cref="IUsageUserKeyProvider"/> for a user
    /// identifier and stores only its salted hash in <see cref="UsageEvent.UserKey"/>. Off by default:
    /// telemetry is about what is used, not who uses it.
    /// </summary>
    public bool IncludeUserKey { get; set; }

    /// <summary>
    /// The secret the user identifier is hashed with. Required when <see cref="IncludeUserKey"/> is true.
    /// Keep it out of source control; rotating it breaks continuity of distinct-user counts.
    /// </summary>
    public string? UserKeySalt { get; set; }

    /// <summary>
    /// Decides which methods the dispatch proxy measures. By default every method on the proxied
    /// interface is measured.
    /// </summary>
    public Func<MethodInfo, bool>? MethodFilter { get; set; }

    /// <summary>The <see cref="UsageEvent.Category"/> given to events measured by the dispatch proxy.</summary>
    public string ProxyCategory { get; set; } = "Method";
}
