# AuditLoggingPipeline.Core configuration reference

Every option of this package, its default, and whether it is required or secret. The host app supplies the
values in code through the `Add...` extension of the plugin it uses (`AddAuditLogging`, `AddStateTrailTracking`,
`AddTelemetry`). The package reads no configuration file and no environment variable, so where the host keeps
the values is the host's choice. A test keeps this file in step with the code, so a default listed here is the
default in the package.

These options are inherited. A plugin's own options type adds its settings to the ones listed here, so the same
properties appear on `AuditPipelineOptions`, `StateTrailOptions` and `TelemetryOptions`.

| Column | Meaning |
|---|---|
| Default | The value used when the host sets nothing. `none` means the option has no value until the host sets one. |
| Required | `Yes` means the host must supply a value. `Conditional` means it is required under a stated condition. `No` means it can be left alone. |
| Secret | `Yes` means the value is a secret. It comes from Key Vault when the app is deployed and from user secrets in local development. It never goes in a configuration file. |

No option in this package is required or secret.

## Overriding a default

```csharp
services.AddAuditLogging(options =>
{
	options.ChannelCapacity = 4096;
	options.SinkWriteTimeout = TimeSpan.FromSeconds(60);
});
```

## `EntryPipelineOptions`

Delivery settings shared by every plugin.

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `ChannelCapacity` | `2048` | No | No | Most entries buffered between the code that records them and the background delivery |
| `FullMode` | `DropOldest` | No | No | What happens when the buffer is full. `DropOldest` never blocks a request, and every lost entry is counted and logged. `Wait` applies back-pressure to the caller |
| `SinkMaxRetries` | `2` | No | No | Extra attempts after a sink write fails. Zero turns retry off |
| `SinkRetryDelay` | `200 ms` | No | No | Delay before the first retry. Each later retry doubles the previous delay |
| `SinkWriteTimeout` | `30 s` | No | No | Longest one sink write attempt may take, so a hung sink cannot stall delivery |
| `ShutdownDrainTimeout` | `10 s` | No | No | How long shutdown waits for buffered entries to be delivered |

## `BatchPipelineOptions`

Added by plugins that deliver in batches, such as Telemetry. It includes everything in `EntryPipelineOptions`.

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `BatchSize` | `100` | No | No | Most entries delivered to a sink in one call. A value below 1 is treated as 1 |
| `FlushInterval` | `5 s` | No | No | How long a partly filled batch waits to fill before it is delivered anyway. Zero delivers whatever is available at once |

## `InterceptionOptions`

Added by plugins that observe method calls through a dispatch proxy. It includes everything in
`EntryPipelineOptions`.

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `MethodPrefixes` | `Save, Add, Update, Upsert, Insert, Archive, Restore, Delete, Remove` | No | No | A method whose name starts with one of these words is recorded. A prefix must end at a word boundary, so `Add` matches `AddUser` but not `AddressGet`. Add to the list, or clear it and add your own |
| `MethodSuffixes` | `Save, Add, Update, Upsert, Insert, Archive, Restore, Delete, Remove` | No | No | A method whose name ends with one of these words, ignoring a trailing `Async`, is recorded. Matches the `NounVerbAsync` convention |
| `MethodMatcher` | `none` | No | No | A full override. When set, it alone decides whether a method is recorded and the two lists above are ignored |
