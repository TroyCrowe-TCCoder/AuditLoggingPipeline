namespace AuditLoggingPipeline;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Runtime.ExceptionServices;

/// <summary>
/// <see cref="DispatchProxy"/> that wraps an interface implementation and records a
/// <see cref="StateTrailEntry"/> for each call to a method selected by
/// <see cref="InterceptionMatcher.IsMatch(InterceptionOptions, MethodInfo)"/>. The first reference-type argument (the
/// entity being written) is serialized at call time as <see cref="StateTrailEntry.AfterState"/>;
/// the proxy cannot know the previous state, so <see cref="StateTrailEntry.BeforeState"/> stays null.
/// Use <see cref="IStateTrailRecorder"/> where the previous state is available.
/// </summary>
/// <remarks>
/// The actor is read from the <see cref="IAuditContextProvider"/> synchronously at call time. The
/// wrapped call's result is returned unchanged; its outcome is observed with a continuation, and an
/// exception from the wrapped call propagates unchanged. This type cannot be sealed because
/// <see cref="DispatchProxy"/> generates a subclass of it.
/// </remarks>
public class TrackingDispatchProxy<TInterface> : DispatchProxy where TInterface : class
{
	private TInterface _decorated = null!;
	private TrackerChannel _channel = null!;
	private IAuditContextProvider _contextProvider = null!;
	private StateTrailOptions _options = null!;
	private ILogger? _logger;

	/// <summary>
	/// Creates a tracking proxy that reads the actor from the current <see cref="HttpContext"/> and
	/// uses default <see cref="StateTrailOptions"/>.
	/// </summary>
	/// <param name="decorated">The real implementation to wrap.</param>
	/// <param name="channel">The channel state-trail entries are enqueued to.</param>
	/// <param name="httpContextAccessor">Optional accessor used to capture the current user and correlation id.</param>
	/// <param name="logger">Optional logger for failures observed while tracking.</param>
	public static TInterface Create(
		TInterface decorated,
		TrackerChannel channel,
		IHttpContextAccessor? httpContextAccessor,
		ILogger<TrackingDispatchProxy<TInterface>>? logger = null) =>
		Create(decorated, channel, new HttpContextAuditContextProvider(httpContextAccessor), new StateTrailOptions(), logger);

	/// <summary>Creates a tracking proxy with an explicit context provider and options.</summary>
	/// <param name="decorated">The real implementation to wrap.</param>
	/// <param name="channel">The channel state-trail entries are enqueued to.</param>
	/// <param name="contextProvider">Supplies the actor, IP address and correlation id at call time.</param>
	/// <param name="options">Decides which methods are recorded and how entity ids are found.</param>
	/// <param name="logger">Optional logger for failures observed while tracking.</param>
	public static TInterface Create(
		TInterface decorated,
		TrackerChannel channel,
		IAuditContextProvider contextProvider,
		StateTrailOptions options,
		ILogger? logger = null)
	{
		ArgumentNullException.ThrowIfNull(decorated);
		ArgumentNullException.ThrowIfNull(channel);
		ArgumentNullException.ThrowIfNull(contextProvider);
		ArgumentNullException.ThrowIfNull(options);

		var proxy = (TrackingDispatchProxy<TInterface>)(object)Create<TInterface, TrackingDispatchProxy<TInterface>>()!;

		proxy._decorated = decorated;
		proxy._channel = channel;
		proxy._contextProvider = contextProvider;
		proxy._options = options;
		proxy._logger = logger;

		return (TInterface)(object)proxy;
	}

	/// <summary>
	/// Invokes the wrapped method, records a state-trail entry when the method is selected by the
	/// options, and returns the original result (or rethrows the original exception) unchanged.
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

		var entity = FindEntityArgument(args);

		return new CapturedCall(
			method.Name,
			context,
			entity?.GetType().Name ?? typeof(TInterface).Name,
			entity is null ? null : ResolveEntityId(entity),
			JsonPayload.Serialize(entity, _logger),
			DateTime.UtcNow);
	}

	private string? ResolveEntityId(object entity)
	{
		try
		{
			var resolved = _options.EntityIdResolver?.Invoke(entity);
			if (resolved is not null)
			{
				return resolved;
			}

			var type = entity.GetType();
			var property = type.GetProperty("Id") ?? type.GetProperty(type.Name + "Id");

			return property?.GetValue(entity)?.ToString();
		}
		catch (Exception ex) when (ex is not OutOfMemoryException)
		{
			_logger?.LogWarning(ex, "Could not resolve the entity id for {ServiceName}; the entry will omit it.", typeof(TInterface).Name);
			return null;
		}
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
		var entry = new StateTrailEntry
		{
			TimestampUtc = call.TimestampUtc,
			Action = MethodNames.TrimAsync(call.MethodName),
			EntityName = call.EntityName,
			EntityId = call.EntityId,
			UserId = call.Context.ActorId,
			ActorDisplayName = call.Context.ActorDisplayName,
			IpAddress = call.Context.IpAddress,
			CorrelationId = call.Context.CorrelationId,
			AfterState = call.AfterState,
			ServiceName = typeof(TInterface).Name,
			MethodName = call.MethodName,
			Succeeded = succeeded,
			ErrorMessage = errorMessage,
		};

		_channel.TryWrite(entry);
	}

	private sealed record CapturedCall(
		string MethodName,
		AuditContext Context,
		string EntityName,
		string? EntityId,
		string? AfterState,
		DateTime TimestampUtc);
}
