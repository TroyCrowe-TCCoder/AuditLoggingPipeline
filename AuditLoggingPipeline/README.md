# AuditLoggingPipeline

Non-blocking, multi-sink audit logging for .NET APIs. It records who did what, to which record,
and whether it worked, without ever delaying or failing the caller's request.

## How entries are produced

Two ways, which can be mixed:

- **Proxy.** `AuditingDispatchProxy<TInterface>` wraps a service. Any method that matches the
  configured prefixes or suffixes (Save, Add, Update, Upsert, Insert, Archive, Restore, Delete,
  Remove by default) produces an `AuditEntry`. The actor, IP address, and correlation id are
  captured at call time from `IAuditContextProvider`.
- **Recorder.** Inject `IAuditRecorder` and call `Record` or `RecordAsync` with an `AuditRecord`
  for events no proxy can see, such as a sign-in or an authorization failure.

Entries go to a bounded channel and are drained by a hosted service that delivers each one to
every registered `IAuditSink`, with a timeout and retries per sink. A slow or failing sink does
not stop the others. On shutdown, buffered entries are delivered first.

This package ships the plumbing only. Each application writes its own `IAuditSink`, which maps
the entry onto its own audit tables.

## Usage

```csharp
services.AddAuditLogging(options =>
{
	options.MethodSuffixes.Add("Onboarding");
	options.IncludeArguments = false;
});
services.AddScoped<IAuditContextProvider, MyContextProvider>();
services.AddScoped<IAuditSink, MyAuditSink>();
services.AddAuditedScoped<IClientService, ClientService>();
```

`IncludeArguments` is off by default because arguments can contain sensitive data.

## Notes

- Depends on `AuditLoggingPipeline.Models` and `AuditLoggingPipeline.Core`.
- The proxy cannot know the record's state; use `AuditLoggingPipeline.Tracker` for before and
  after state.
