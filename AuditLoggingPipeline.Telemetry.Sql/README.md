# AuditLoggingPipeline.Telemetry.Sql

SQL Server sink for [`AuditLoggingPipeline.Telemetry`](../AuditLoggingPipeline.Telemetry/README.md). It stores usage events in batches by calling a stored procedure with a table-valued parameter. The sink contains no SQL text and never creates objects: the table, the table type and the procedure belong to the consuming application's database project and are deployed with its DACPAC.

Each application has its own telemetry database, so connection and object names are options set per application.

## Install

```
dotnet add package AuditLoggingPipeline.Telemetry.Sql
```

## Register

```csharp
builder.Services.AddTelemetry(options => options.ApplicationName = "CaptiveIdentityAPI");
builder.Services.AddSqlTelemetrySink(options =>
{
	options.ConnectionString = builder.Configuration.GetConnectionString("Telemetry")!;
});
```

Options are validated at start-up, so a missing connection string stops the host instead of silently dropping events.

| Option | Default | Meaning |
|---|---|---|
| `ConnectionString` | none, required | Database that holds the telemetry tables |
| `ProcedureName` | `dbo.UsageEvent_Insert` | Procedure that inserts a batch |
| `ParameterName` | `@Events` | Its table-valued parameter |
| `TableTypeName` | `dbo.UsageEventList` | Table type of that parameter |
| `ChunkSize` | 100 | Most rows per procedure call; larger batches are split and commit together |
| `CommandTimeoutSeconds` | 30 | Timeout per procedure call |

## Database contract

The package ships reference scripts in its `Sql` folder. Add them to the database project of every application that uses the sink:

1. `1_UsageEvent_Table.sql` - `dbo.UsageEvent`
2. `2_UsageEventList_Type.sql` - `dbo.UsageEventList`
3. `3_UsageEvent_Insert_Procedure.sql` - `dbo.UsageEvent_Insert`

The procedure script's `CATCH` block calls `dbo.SqlErrorTracking_Insert`, which is the CaptiveInnovations error-tracking procedure. Adjust or remove that call if your database does not have it. The application account needs only `EXECUTE` on the procedure and on the table type. Nothing here touches any audit table.

## Behaviour

- A batch is written in one transaction, all-or-nothing, so the pipeline's retry is safe. If the write fails after the retries, the pipeline logs it and moves on; it never blocks the application.
- Text longer than its column (for example a very long `Name`) is truncated rather than failing the whole batch.
- `Duration` is stored as milliseconds with three decimals. `Properties` is stored as a JSON object. `UserKey` is null unless the application turned it on in `TelemetryOptions`.
