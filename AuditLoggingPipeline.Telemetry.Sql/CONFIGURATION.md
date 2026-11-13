# AuditLoggingPipeline.Telemetry.Sql configuration reference

Every option of this package, its default, and whether it is required or secret. The host app supplies the
values in code through `AddSqlTelemetrySink`. The package reads no configuration file and no environment
variable, so where the host keeps the values is the host's choice. A test keeps this file in step with the code,
so a default listed here is the default in the package.

The procedure, parameter and table type names are the conventions of the SQL Server adapter. A different
datastore gets its own adapter package with its own options.

| Column | Meaning |
|---|---|
| Default | The value used when the host sets nothing. `none` means the option has no value until the host sets one. |
| Required | `Yes` means the host must supply a value. `Conditional` means it is required under a stated condition. `No` means it can be left alone. A required option is checked when the host starts, and a missing value stops the host with a message that names the option. |
| Secret | `Yes` means the value is a secret. It comes from Key Vault when the app is deployed and from user secrets in local development. It never goes in a configuration file. |

## Supplying the connection string

The connection string is a secret. The host reads it from its configuration, which holds the value from Key
Vault when deployed and from user secrets locally, and passes it to the sink.

```csharp
builder.Services.AddSqlTelemetrySink(options =>
{
	options.ConnectionString = builder.Configuration.GetConnectionString("Telemetry")!;
	options.ChunkSize = 250;
});
```

## `SqlTelemetrySinkOptions`

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `ConnectionString` | `none` | Yes | Yes | Connection string of the database that holds the telemetry tables |
| `ProcedureName` | `dbo.UsageEvent_Insert` | No | No | The stored procedure that inserts a batch |
| `ParameterName` | `@Events` | No | No | The procedure's table-valued parameter |
| `TableTypeName` | `dbo.UsageEventList` | No | No | The table type of that parameter |
| `ChunkSize` | `100` | No | No | Most rows sent in one procedure call. A larger batch is split into chunks that commit together in one transaction. Must be above 0 |
| `CommandTimeoutSeconds` | `30` | No | No | Timeout in seconds for one procedure call. Must be above 0 |
