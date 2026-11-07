namespace AuditLoggingPipeline;

/// <summary>Convenience methods for <see cref="ITelemetryRecorder"/>.</summary>
public static class TelemetryRecorderExtensions
{
    /// <summary>
    /// Starts timing an operation. Dispose the returned <see cref="UsageTimer"/> when the operation ends
    /// and one event, with its duration, is recorded.
    /// </summary>
    /// <param name="recorder">The recorder to write to.</param>
    /// <param name="name">What is being measured, for example <c>Report.Export</c>.</param>
    /// <param name="category">Optional coarse grouping.</param>
    /// <param name="target">Optional feature, endpoint or service name.</param>
    /// <param name="properties">Optional low-cardinality dimensions.</param>
    public static UsageTimer StartTimer(
        this ITelemetryRecorder recorder,
        string name,
        string? category = null,
        string? target = null,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new UsageTimer(recorder, name, category, target, properties);
    }
}
