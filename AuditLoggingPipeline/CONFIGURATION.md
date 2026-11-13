# AuditLoggingPipeline configuration reference

Every option of this package, its default, and whether it is required or secret. The host app supplies the
values in code through `AddAuditLogging`. The package reads no configuration file and no environment variable.
A test keeps this file in step with the code, so a default listed here is the default in the package.

`AuditPipelineOptions` also has every option of `InterceptionOptions` and `EntryPipelineOptions`. Those are
listed in the [AuditLoggingPipeline.Core configuration reference](../AuditLoggingPipeline.Core/CONFIGURATION.md).

No option in this package is required or secret. The columns are explained in the Core reference.

## Overriding a default

```csharp
services.AddAuditLogging(options =>
{
	options.IncludeArguments = true;
});
```

## `AuditPipelineOptions`

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `IncludeArguments` | `false` | No | No | When true, the proxy stores each recorded call's arguments in the audit entry. Off by default because arguments can contain sensitive data |
