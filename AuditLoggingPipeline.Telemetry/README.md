# AuditLoggingPipeline.Telemetry

Non-blocking usage telemetry for .NET apps: what happened, how long it took and how often. It is
informational and about the application, not about who used it. An independent sibling of the Audit
and Tracker plugins with its own event shape (`UsageEvent`); it depends only on
`AuditLoggingPipeline.Models` and `AuditLoggingPipeline.Core`.

## How events are produced

- **Proxy.** `TelemetryDispatchProxy<TInterface>` times every call to a wrapped interface (or the
  methods chosen by `TelemetryOptions.MethodFilter`) and records `Interface.Method`, the duration,
  and whether it succeeded, failed or was canceled.
- **Timer.** `recorder.StartTimer("Report.Export", ...)` returns a `UsageTimer`; dispose it to
  record one event with the measured duration. Call `Fail()` or set `Outcome` when it did not succeed.
- **Recorder.** Inject `ITelemetryRecorder` and call `Record` or `RecordAsync` with a `UsageRecord`
  for anything else, such as a feature being used.

Events are stamped with the application name, version, environment and a UTC timestamp, queued on a
bounded channel, and delivered **in batches** (`BatchSize`, `FlushInterval`) to every registered
`ITelemetrySink`. Each sink gets a timeout and retries, a failing sink never stops the others, and
buffered events are delivered on shutdown.

## Usage

```csharp
services.AddTelemetry(options =>
{
	options.ApplicationName = "CaptiveIdentityAPI";
	options.ApplicationVersion = "1.0.0";
	options.EnvironmentName = builder.Environment.EnvironmentName;
	options.SampleRate = 0.25; // keep a quarter of successful calls
});
services.AddScoped<ITelemetrySink, MyTelemetrySink>();
services.AddTelemetryScoped<IClientService, ClientService>();
```

## Configuration

Every option, its default, and whether it is required or secret is in the [configuration reference](CONFIGURATION.md). `UserKeySalt` is a secret. It comes from Key Vault when the app is deployed and from user secrets locally, never from a configuration file.

## Sampling

With `SampleRate` below 1,
carries `SampleWeight` (the reciprocal), so counts can be reconstructed by summing weights. Failed
and canceled events are always kept with weight 1.

## Privacy

`UserKey` is empty unless `IncludeUserKey` is on and an `IUsageUserKeyProvider` is registered. Only an
HMAC-SHA256 hash of the identifier, keyed with `UserKeySalt`, is stored, so distinct users can be
counted without storing who they are. The salt is required when the option is on (validated at
start-up). Do not put personal data in `Properties`; keep them low-cardinality.

## Notes

- Telemetry never blocks or throws into the caller, and an exception from a wrapped call reaches the
  caller unchanged.
- This package ships no store. Implement `ITelemetrySink` (it receives an `IReadOnlyList<UsageEvent>`),
  or use a sink package such as `AuditLoggingPipeline.Telemetry.Sql` when available.
