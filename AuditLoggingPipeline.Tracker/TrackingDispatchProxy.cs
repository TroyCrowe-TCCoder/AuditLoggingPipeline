namespace AuditLoggingPipeline;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

/// <summary>
/// <see cref="DispatchProxy"/> that transparently wraps an interface implementation and
/// records a <see cref="StateTrailEntry"/> for any invoked method whose name starts with Save,
/// Add, Update, Archive, or Delete (the same repository/service mutation naming convention
/// <c>AuditingDispatchProxy</c> uses). Unlike the Audit plugin, Tracker captures a before/after
/// snapshot of the first reference-type argument (the entity being mutated), rather than the
/// raw call arguments.
///
/// Tracking is fully non-blocking: the underlying call's result (Task/Task{T}/value) is
/// returned to the caller immediately and completely untouched. Tracking is spun off
/// independently via <see cref="Task.Run(Func{Task})"/>, which observes the original task's
/// outcome and enqueues a <see cref="StateTrailEntry"/> to <see cref="TrackerChannel"/> for
/// later fan-out to every registered <see cref="ITrackerSink"/> by
/// <see cref="TrackerLogBackgroundService"/>. Any exception thrown by the wrapped call
/// propagates to the caller unchanged; the tracking task never rethrows.
/// </summary>
public class TrackingDispatchProxy<TInterface> : DispatchProxy where TInterface : class
{
    private static readonly string[] TrackedPrefixes = ["Save", "Add", "Update", "Archive", "Delete"];
    private const int MaxStateLength = 4000;

    private TInterface _decorated = null!;
    private TrackerChannel _channel = null!;
    private IHttpContextAccessor? _httpContextAccessor;
    private ILogger? _logger;

    /// <summary>
    /// Creates a transparent tracking proxy wrapping <paramref name="decorated"/>. Calls to
    /// tracked methods (see <see cref="TrackedPrefixes"/>) capture a before/after entity
    /// snapshot recorded to <paramref name="channel"/>; all other calls pass through untouched.
    /// </summary>
    /// <param name="decorated">The real implementation to wrap.</param>
    /// <param name="channel">The channel state-trail entries are enqueued to.</param>
    /// <param name="httpContextAccessor">Optional accessor used to capture the current user and correlation id.</param>
    /// <param name="logger">Optional logger used to record failures observed by the detached tracking task.</param>
    public static TInterface Create(
        TInterface decorated,
        TrackerChannel channel,
        IHttpContextAccessor? httpContextAccessor,
        ILogger<TrackingDispatchProxy<TInterface>>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(decorated);
        ArgumentNullException.ThrowIfNull(channel);

        object proxy = Create<TInterface, TrackingDispatchProxy<TInterface>>()!;
        var typedProxy = (TrackingDispatchProxy<TInterface>)proxy;

        typedProxy._decorated = decorated;
        typedProxy._channel = channel;
        typedProxy._httpContextAccessor = httpContextAccessor;
        typedProxy._logger = logger;

        return (TInterface)proxy;
    }

    /// <summary>
    /// Invokes the wrapped method, records a before/after state-trail entry for tracked method
    /// names, and returns the original result (or rethrows the original exception) unchanged.
    /// </summary>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        var beforeState = IsTracked(targetMethod.Name) ? SerializeEntityArgument(args) : null;

        object? result;
        try
        {
            result = targetMethod.Invoke(_decorated, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            if (IsTracked(targetMethod.Name))
            {
                Enqueue(targetMethod, args, beforeState, succeeded: false, errorMessage: ex.InnerException.Message);
            }

            // Unwrap so the caller sees the original exception type, not the reflection wrapper.
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // unreachable
        }

        if (IsTracked(targetMethod.Name))
        {
            TrackAsync(result, targetMethod, args, beforeState);
        }

        // The original result (Task/Task<T>/value) is returned to the caller completely
        // untouched; tracking runs independently on the thread pool and never affects it.
        return result;
    }

    private static bool IsTracked(string methodName)
    {
        foreach (var prefix in TrackedPrefixes)
        {
            if (methodName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Fires off state-trail tracking on the thread pool via <see cref="Task.Run(Func{Task})"/>
    /// so it runs fully independently of the caller's awaited task. The tracked task is only
    /// observed (never awaited by the caller's own continuation chain), so tracking can never
    /// delay or fault the original call.
    /// </summary>
    private void TrackAsync(object? result, MethodInfo targetMethod, object?[]? args, string? beforeState)
    {
        if (result is not Task task)
        {
            // Synchronous tracked method (not expected in typical usage, but handled safely).
            Enqueue(targetMethod, args, beforeState, succeeded: true, errorMessage: null);
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                await task.ConfigureAwait(false);
                Enqueue(targetMethod, args, beforeState, succeeded: true, errorMessage: null);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Tracked method {MethodName} on {ServiceName} failed; state-trail entry recorded with failure details.", targetMethod.Name, typeof(TInterface).Name);
                Enqueue(targetMethod, args, beforeState, succeeded: false, errorMessage: ex.Message);
            }
        });
    }

    private void Enqueue(MethodInfo targetMethod, object?[]? args, string? beforeState, bool succeeded, string? errorMessage)
    {
        var httpContext = _httpContextAccessor?.HttpContext;
        var entityArgument = FindEntityArgument(args);

        var entry = new StateTrailEntry
        {
            TimestampUtc = DateTime.UtcNow,
            ServiceName = typeof(TInterface).Name,
            MethodName = targetMethod.Name,
            EntityName = entityArgument?.GetType().Name ?? typeof(TInterface).Name,
            EntityId = null,
            UserId = httpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext?.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value,
            CorrelationId = httpContext?.TraceIdentifier,
            BeforeState = beforeState,
            AfterState = SerializeEntityArgument(args),
            Succeeded = succeeded,
            ErrorMessage = errorMessage,
        };

        _channel.TryWrite(entry);
    }

    private static object? FindEntityArgument(object?[]? args)
    {
        if (args is null)
        {
            return null;
        }

        foreach (var arg in args)
        {
            if (arg is not null && arg.GetType().IsClass && arg is not string)
            {
                return arg;
            }
        }

        return null;
    }

    private string? SerializeEntityArgument(object?[]? args)
    {
        var entityArgument = FindEntityArgument(args);
        if (entityArgument is null)
        {
            return null;
        }

        try
        {
            var json = JsonSerializer.Serialize(entityArgument);
            return json.Length > MaxStateLength ? json[..MaxStateLength] : json;
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            _logger?.LogWarning(ex, "Failed to serialize entity argument for tracked method on {ServiceName}; state-trail entry will omit entity state.", typeof(TInterface).Name);
            return null;
        }
    }
}
