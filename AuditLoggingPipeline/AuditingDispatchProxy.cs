namespace AuditLoggingPipeline;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Runtime.ExceptionServices;

/// <summary>
/// <see cref="DispatchProxy"/> that wraps an interface implementation and records an
/// <see cref="AuditEntry"/> for each call to a method selected by
/// <see cref="InterceptionMatcher.IsMatch(InterceptionOptions, MethodInfo)"/> (by default the Save, Add, Update, Upsert,
/// Insert, Archive, Restore, Delete and Remove verbs, as prefix or suffix). Other calls pass through untouched.
/// </summary>
/// <remarks>
/// The acting user, IP address and correlation id are read from the <see cref="IAuditContextProvider"/>
/// synchronously at call time, while the request is still alive. The wrapped call's result is returned
/// unchanged; its outcome is observed with a continuation and never awaited by the caller, and an
/// exception from the wrapped call propagates unchanged. This type cannot be sealed because
/// <see cref="DispatchProxy"/> generates a subclass of it.
/// </remarks>
public class AuditingDispatchProxy<TInterface> : DispatchProxy where TInterface : class
{
	private TInterface _decorated = null!;
	private AuditChannel _channel = null!;
	private IAuditContextProvider _contextProvider = null!;
	private AuditPipelineOptions _options = null!;
	private ILogger? _logger;

	/// <summary>
	/// Creates an auditing proxy that reads the actor from the current <see cref="HttpContext"/> and
	/// uses default <see cref="AuditPipelineOptions"/>.
	/// </summary>
	/// <param name="decorated">The real implementation to wrap.</param>
	/// <param name="channel">The channel audit entries are enqueued to.</param>
	/// <param name="httpContextAccessor">Optional accessor used to capture the current user and correlation id.</param>
	/// <param name="logger">Optional logger for failures observed while tracking.</param>
	public static TInterface Create(
		TInterface decorated,
		AuditChannel channel,
		IHttpContextAccessor? httpContextAccessor,
		ILogger<AuditingDispatchProxy<TInterface>>? logger = null) =>
		Create(decorated, channel, new HttpContextAuditContextProvider(httpContextAccessor), new AuditPipelineOptions(), logger);

	/// <summary>Creates an auditing proxy with an explicit context provider and options.</summary>
	/// <param name="decorated">The real implementation to wrap.</param>
	/// <param name="channel">The channel audit entries are enqueued to.</param>
	/// <param name="contextProvider">Supplies the actor, IP address and correlation id at call time.</param>
	/// <param name="options">Decides which methods are recorded and whether arguments are captured.</param>
	/// <param name="logger">Optional logger for failures observed while tracking.</param>
	public static TInterface Create(
		TInterface decorated,
		AuditChannel channel,
		IAuditContextProvider contextProvider,
		AuditPipelineOptions options,
		ILogger? logger = null)
	{
		ArgumentNullException.ThrowIfNull(decorated);
		ArgumentNullException.ThrowIfNull(channel);
		ArgumentNullException.ThrowIfNull(contextProvider);
		ArgumentNullException.ThrowIfNull(options);

		var proxy = (AuditingDispatchProxy<TInterface>)(object)Create<TInterface, AuditingDispatchProxy<TInterface>>()!;

		proxy._decorated = decorated;
		proxy._channel = channel;
		proxy._contextProvider = contextProvider;
		proxy._options = options;
		proxy._logger = logger;

		return (TInterface)(object)proxy;
	}

	/// <summary>
	/// Invokes the wrapped method, records an entry when the method is selected by the options, and
	/// returns the original result (or rethrows the original exception) unchanged.
	/// </summary>
	protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
	{
		ArgumentNullException.ThrowIfNull(targetMethod);

		if (!InterceptionMatcher.IsMatch(_options, targetMethod))
		{
			return Forward(targetMethod, args);
		}

		var call = CaptureCall(targetMethod, args);

		object? result;
		try
		{
			result = Forward(targetMethod, args);
		}
		catch (Exception ex)
		{
			Enqueue(call, succeeded: false, ex.Message);
			throw;
		}

		if (result is Task task)
		{
			task.ContinueWith(
				completed => Enqueue(call, completed.IsCompletedSuccessfully, DescribeFailure(completed)),
				CancellationToken.None,
				TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Default);
		}
		else
		{
			Enqueue(call, succeeded: true, errorMessage: null);
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

	private CapturedCall CaptureCall(MethodInfo method, object?[]? args)
	{
		AuditContext context;
		try
		{
			context = _contextProvider.GetContext();
		}
		catch (Exception ex)
		{
			_logger?.LogWarning(ex, "Audit context provider failed for {ServiceName}.{MethodName}; recording without actor details.", typeof(TInterface).Name, method.Name);
			context = AuditContext.Empty;
		}

		var arguments = _options.IncludeArguments ? SerializeArguments(args) : null;

		return new CapturedCall(method.Name, context, arguments, DateTime.UtcNow);
	}

	private string? SerializeArguments(object?[]? args)
	{
		// A CancellationToken is not data and cannot be serialized, so it must not sink the whole list.
		var data = args?.Where(arg => arg is not CancellationToken).ToArray();

		return data is { Length: > 0 } ? JsonPayload.Serialize(data, _logger) : null;
	}

	private static string? DescribeFailure(Task task)
	{
		if (task.IsCanceled)
		{
			return "The operation was canceled.";
		}

		return task.IsFaulted ? task.Exception?.GetBaseException().Message : null;
	}

	private void Enqueue(CapturedCall call, bool succeeded, string? errorMessage)
	{
		var entry = new AuditEntry
		{
			TimestampUtc = call.TimestampUtc,
			Action = MethodNames.TrimAsync(call.MethodName),
			Succeeded = succeeded,
			UserId = call.Context.ActorId,
			ActorDisplayName = call.Context.ActorDisplayName,
			IpAddress = call.Context.IpAddress,
			CorrelationId = call.Context.CorrelationId,
			ServiceName = typeof(TInterface).Name,
			MethodName = call.MethodName,
			Arguments = call.Arguments,
			ErrorMessage = errorMessage,
		};

		_channel.TryWrite(entry);
	}

	private sealed record CapturedCall(string MethodName, AuditContext Context, string? Arguments, DateTime TimestampUtc);
}
