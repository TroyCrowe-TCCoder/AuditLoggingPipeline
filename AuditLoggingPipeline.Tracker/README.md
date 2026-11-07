# AuditLoggingPipeline.Tracker

Non-blocking state-trail tracking for .NET APIs: what a record looked like when it changed. An
independent sibling of `AuditLoggingPipeline`; it depends only on `AuditLoggingPipeline.Models`
and `AuditLoggingPipeline.Core`.

## How entries are produced

- **Proxy.** `TrackingDispatchProxy<TInterface>` wraps a service. For a matching method (same
  prefix and suffix rules as the Audit plugin) it takes the first class-typed argument as the
  entity and records its state at call time as the after state. The entity id comes from
  `StateTrailOptions.EntityIdResolver`, or from an `Id` or `<TypeName>Id` property when none is set.
- **Recorder.** Inject `IStateTrailRecorder` and call `Record` or `RecordAsync` with a
  `StateTrailRecord` when you have both the before and after state, for example after loading the
  record first. Both states are serialized to JSON, and `EntityKeys` carries composite keys.

The proxy leaves `BeforeState` empty because it cannot load the prior record.

Entries are queued on a bounded channel and delivered to every registered `ITrackerSink`, with a
timeout and retries per sink and a drain on shutdown.

## Usage

```csharp
services.AddStateTrailTracking(options =>
{
	options.EntityIdResolver = entity => (entity as IHasKey)?.Key;
});
services.AddScoped<IAuditContextProvider, MyContextProvider>();
services.AddScoped<ITrackerSink, MyStateTrailSink>();
services.AddTrackedScoped<IClientService, ClientService>();
```

## Notes

- Audit and Tracker are independent; install either or both. A failure in one never affects the
  other.
- Any exception from the wrapped call reaches the caller unchanged; tracking never rethrows.
