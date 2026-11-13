# AuditLoggingPipeline.Telemetry configuration reference

Every option of this package, its default, and whether it is required or secret. The host app supplies the
values in code through `AddTelemetry`. The package reads no configuration file and no environment variable, so
where the host keeps the values is the host's choice. A test keeps this file in step with the code, so a default
listed here is the default in the package.

`TelemetryOptions` also has every option of `BatchPipelineOptions` and `EntryPipelineOptions`. Those are listed
in the [AuditLoggingPipeline.Core configuration reference](../AuditLoggingPipeline.Core/CONFIGURATION.md).

| Column | Meaning |
|---|---|
| Default | The value used when the host sets nothing. `none` means the option has no value until the host sets one. |
| Required | `Yes` means the host must supply a value. `Conditional` means it is required under a stated condition. `No` means it can be left alone. A required option is checked when the host starts, and a missing value stops the host with a message that names the option. |
| Secret | `Yes` means the value is a secret. It comes from Key Vault when the app is deployed and from user secrets in local development. It never goes in a configuration file. |

## Overriding a default

```csharp
builder.Services.AddTelemetry(options =>
{
	options.ApplicationName = "CaptiveIdentityAPI";
	options.SampleRate = 0.25;
	options.IncludeUserKey = true;
	options.UserKeySalt = builder.Configuration["Telemetry:UserKeySalt"];
});
```

`UserKeySalt` is a secret, so the value the host reads here comes from Key Vault or user secrets.

## `TelemetryOptions`

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `ApplicationName` | `none` | No | No | The application name stamped on every event |
| `ApplicationVersion` | `none` | No | No | The application version stamped on every event |
| `EnvironmentName` | `none` | No | No | The environment name stamped on every event, for example `Production` |
| `SampleRate` | `1` | No | No | Fraction of successful events to keep, above 0 and at most 1. Below 1, each kept event carries a sample weight of the reciprocal so counts can be rebuilt. Failed and canceled events are always kept. The default keeps everything |
| `IncludeUserKey` | `false` | No | No | When true, the recorder stores a salted hash of the user identifier in each event. Off by default: telemetry is about what is used, not who uses it |
| `UserKeySalt` | `none` | Conditional | Yes | The secret the user identifier is hashed with. Required when `IncludeUserKey` is true. Rotating it breaks continuity of distinct-user counts |
| `MethodFilter` | `none` | No | No | Decides which methods the dispatch proxy measures. When not set, every method on the proxied interface is measured |
| `ProxyCategory` | `Method` | No | No | The category given to events measured by the dispatch proxy |
