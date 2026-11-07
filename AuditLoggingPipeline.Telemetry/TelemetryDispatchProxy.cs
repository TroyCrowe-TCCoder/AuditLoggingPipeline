using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace AuditLoggingPipeline;

/// <summary>
/// <see cref="DispatchProxy"/> that wraps an interface implementation and records one
/// <see cref="UsageEvent"/>, with its duration and outcome, for each call to a method selected by
/// <see cref="TelemetryOptions.MethodFilter"/> (every method by default).
/// </summary>
/// <remarks>
/// The wrapped call's result is returned unchanged; for a <see cref="Task"/> the outcome and duration
/// are observed with a continuation and never awaited by the caller, and an exception from the wrapped
/// call propagates unchanged. This type cannot be sealed because <see cref="DispatchProxy"/> generates
/// a subclass of it.
/// </remarks>
public class TelemetryDispatchProxy<TInterface> : DispatchProxy where TInterface : class
{
    private TInterface _decorated = null!;
    private ITelemetryRecorder _recorder = null!;
    private TelemetryOptions _options = null!;

    /// <summary>Creates a measuring proxy around <paramref name="decorated"/>.</summary>
    /// <param name="decorated">The real implementation to wrap.</param>
    /// <param name="recorder">Receives one observation per measured call.</param>
    /// <param name="options">Decides which methods are measured and the category used.</param>
    public static TInterface Create(TInterface decorated, ITelemetryRecorder recorder, TelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(decorated);
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(options);

        var proxy = (TelemetryDispatchProxy<TInterface>)(object)Create<TInterface, TelemetryDispatchProxy<TInterface>>()!;

        proxy._decorated = decorated;
        proxy._recorder = recorder;
        proxy._options = options;

        return (TInterface)(object)proxy;
    }

    /// <summary>
    /// Invokes the wrapped method, records an observation when the method is selected, and returns
    /// the original result (or rethrows the original exception) unchanged.
    /// </summary>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        if (_options.MethodFilter is { } filter && !filter(targetMethod))
        {
            return Forward(targetMethod, args);
        }

        var start = Stopwatch.GetTimestamp();

        object? result;
        try
        {
            result = Forward(targetMethod, args);
        }
        catch (Exception ex)
        {
            Record(targetMethod, start, ex is OperationCanceledException ? UsageOutcomes.Canceled : UsageOutcomes.Failure);
            throw;
        }

        if (result is Task task)
        {
            task.ContinueWith(
                completed => Record(targetMethod, start, OutcomeOf(completed)),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        else
        {
            Record(targetMethod, start, UsageOutcomes.Success);
        }

        return result;
    }

    private object? Forward(MethodInfo targetMethod, object?[]? args)
    {
        try
        {
            return targetMethod.Invoke(_decorated, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static string OutcomeOf(Task task)
    {
        if (task.IsCanceled)
        {
            return UsageOutcomes.Canceled;
        }

        return task.IsFaulted ? UsageOutcomes.Failure : UsageOutcomes.Success;
    }

    private void Record(MethodInfo method, long startTimestamp, string outcome) =>
        _recorder.Record(new UsageRecord
        {
            Name = $"{typeof(TInterface).Name}.{MethodNames.TrimAsync(method.Name)}",
            Category = _options.ProxyCategory,
            Target = typeof(TInterface).Name,
            Duration = Stopwatch.GetElapsedTime(startTimestamp),
            Outcome = outcome,
        });
}
