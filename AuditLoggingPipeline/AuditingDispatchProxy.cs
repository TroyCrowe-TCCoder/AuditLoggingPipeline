namespace AuditLoggingPipeline;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

/// <summary>
/// <see cref="DispatchProxy"/> that transparently wraps an interface implementation and
/// records an <see cref="AuditEntry"/> for any invoked method whose name starts with Save, Add,
/// Update, Archive, or Delete (the repository/service mutation naming convention this library
/// assumes). All other calls pass through untouched.
///
/// Auditing is fully non-blocking: the underlying call's result (Task/Task{T}/value) is
/// returned to the caller immediately and completely untouched. Audit tracking is spun off
/// independently via <see cref="Task.Run(Func{Task})"/>, which observes the original task's
/// outcome and enqueues an <see cref="AuditEntry"/> to <see cref="AuditChannel"/> for later
/// fan-out to every registered <see cref="IAuditSink"/> by <see cref="AuditLogBackgroundService"/>.
/// Any exception thrown by the wrapped call propagates to the caller unchanged; the
/// audit-tracking task never rethrows.
/// </summary>
public class AuditingDispatchProxy<TInterface> : DispatchProxy where TInterface : class
{
    private static readonly string[] AuditedPrefixes = ["Save", "Add", "Update", "Archive", "Delete"];
    private const int MaxArgumentsLength = 2000;

    private TInterface _decorated = null!;
    private AuditChannel _channel = null!;
    private IHttpContextAccessor? _httpContextAccessor;
    private ILogger? _logger;

    /// <summary>
    /// Creates a transparent auditing proxy wrapping <paramref name="decorated"/>. Calls to
    /// audited methods (see <see cref="AuditedPrefixes"/>) are recorded to <paramref name="channel"/>;
    /// all other calls pass through untouched.
    /// </summary>
    /// <param name="decorated">The real implementation to wrap.</param>
    /// <param name="channel">The channel audit entries are enqueued to.</param>
    /// <param name="httpContextAccessor">Optional accessor used to capture the current user and correlation id.</param>
    /// <param name="logger">Optional logger used to record failures observed by the detached audit-tracking task.</param>
    public static TInterface Create(
        TInterface decorated,
        AuditChannel channel,
        IHttpContextAccessor? httpContextAccessor,
        ILogger<AuditingDispatchProxy<TInterface>>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(decorated);
        ArgumentNullException.ThrowIfNull(channel);

        object proxy = Create<TInterface, AuditingDispatchProxy<TInterface>>()!;
        var typedProxy = (AuditingDispatchProxy<TInterface>)proxy;

        typedProxy._decorated = decorated;
        typedProxy._channel = channel;
        typedProxy._httpContextAccessor = httpContextAccessor;
        typedProxy._logger = logger;

        return (TInterface)proxy;
    }

    /// <summary>
    /// Invokes the wrapped method, records an audit entry for audited method names, and
    /// returns the original result (or rethrows the original exception) unchanged.
    /// </summary>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        object? result;
        try
        {
            result = targetMethod.Invoke(_decorated, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            if (IsAudited(targetMethod.Name))
            {
                Enqueue(targetMethod, args, succeeded: false, errorMessage: ex.InnerException.Message);
            }

            // Unwrap so the caller sees the original exception type, not the reflection wrapper.
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // unreachable
        }

        if (IsAudited(targetMethod.Name))
        {
            TrackAsync(result, targetMethod, args);
        }

        // The original result (Task/Task<T>/value) is returned to the caller completely
        // untouched; auditing runs independently on the thread pool and never affects it.
        return result;
    }

    private static bool IsAudited(string methodName)
    {
        foreach (var prefix in AuditedPrefixes)
        {
            if (methodName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Fires off audit tracking on the thread pool via <see cref="Task.Run(Func{Task})"/> so it
    /// runs fully independently of the caller's awaited task. The audited task is only observed
    /// (never awaited by the caller's own continuation chain), so audit tracking can never delay
    /// or fault the original call.
    /// </summary>
    private void TrackAsync(object? result, MethodInfo targetMethod, object?[]? args)
    {
        if (result is not Task task)
        {
            // Synchronous audited method (not expected in typical usage, but handled safely).
            Enqueue(targetMethod, args, succeeded: true, errorMessage: null);
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                await task.ConfigureAwait(false);
                Enqueue(targetMethod, args, succeeded: true, errorMessage: null);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Audited method {MethodName} on {ServiceName} failed; audit entry recorded with failure details.", targetMethod.Name, typeof(TInterface).Name);
                Enqueue(targetMethod, args, succeeded: false, errorMessage: ex.Message);
            }
        });
    }

    private void Enqueue(MethodInfo targetMethod, object?[]? args, bool succeeded, string? errorMessage)
    {
        var httpContext = _httpContextAccessor?.HttpContext;

        var entry = new AuditEntry
        {
            TimestampUtc = DateTime.UtcNow,
            ServiceName = typeof(TInterface).Name,
            MethodName = targetMethod.Name,
            UserId = httpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext?.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value,
            CorrelationId = httpContext?.TraceIdentifier,
            Arguments = SerializeArguments(args),
            Succeeded = succeeded,
            ErrorMessage = errorMessage,
        };

        _channel.TryWrite(entry);
    }

    private string? SerializeArguments(object?[]? args)
    {
        if (args is null || args.Length == 0)
        {
            return null;
        }

        try
        {
            var json = JsonSerializer.Serialize(args);
            return json.Length > MaxArgumentsLength ? json[..MaxArgumentsLength] : json;
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            _logger?.LogWarning(ex, "Failed to serialize arguments for audited method on {ServiceName}; audit entry will omit argument details.", typeof(TInterface).Name);
            return null;
        }
    }
}
