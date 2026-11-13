# AuditLoggingPipeline.Tracker configuration reference

Every option of this package, its default, and whether it is required or secret. The host app supplies the
values in code through `AddStateTrailTracking`. The package reads no configuration file and no environment
variable. A test keeps this file in step with the code, so a default listed here is the default in the package.

`StateTrailOptions` also has every option of `InterceptionOptions` and `EntryPipelineOptions`. Those are
listed in the [AuditLoggingPipeline.Core configuration reference](../AuditLoggingPipeline.Core/CONFIGURATION.md).

No option in this package is required or secret. The columns are explained in the Core reference.

## Overriding a default

```csharp
services.AddStateTrailTracking(options =>
{
	options.EntityIdResolver = entity => (entity as IHasKey)?.Key;
});
```

## `StateTrailOptions`

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `EntityIdResolver` | `none` | No | No | Returns the identifier of the entity a call is changing. When it is not set or returns null, the proxy looks for an `Id` or `<EntityName>Id` property on the entity |
