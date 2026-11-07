using System.Diagnostics;

namespace AuditLoggingPipeline;

/// <summary>
/// Times a block of code and records one <see cref="UsageEvent"/> when disposed. Create it with
/// <see cref="TelemetryRecorderExtensions.StartTimer"/>. Set <see cref="Outcome"/> (or call
/// <see cref="Fail"/>) before disposing when the work did not succeed.
/// </summary>
public sealed class UsageTimer : IDisposable
{
    private readonly ITelemetryRecorder _recorder;
    private readonly string _name;
    private readonly string? _category;
    private readonly string? _target;
    private readonly IReadOnlyDictionary<string, string>? _properties;
    private readonly long _startTimestamp = Stopwatch.GetTimestamp();
    private int _recorded;

    internal UsageTimer(ITelemetryRecorder recorder, string name, string? category, string? target, IReadOnlyDictionary<string, string>? properties)
    {
        _recorder = recorder;
        _name = name;
        _category = category;
        _target = target;
        _properties = properties;
    }

    /// <summary>The outcome that will be recorded. Defaults to <see cref="UsageOutcomes.Success"/>.</summary>
    public string Outcome { get; set; } = UsageOutcomes.Success;

    /// <summary>Marks the work as failed.</summary>
    public void Fail() => Outcome = UsageOutcomes.Failure;

    /// <summary>Records the event once; later calls do nothing.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _recorded, 1) != 0)
        {
            return;
        }

        _recorder.Record(new UsageRecord
        {
            Name = _name,
            Category = _category,
            Target = _target,
            Duration = Stopwatch.GetElapsedTime(_startTimestamp),
            Outcome = Outcome,
            Properties = _properties,
        });
    }
}
