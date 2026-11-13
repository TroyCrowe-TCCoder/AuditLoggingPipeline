# AuditLoggingPipeline.Sql

SQL Server sinks for [`AuditLoggingPipeline`](../AuditLoggingPipeline/README.md) and [`AuditLoggingPipeline.Tracker`](../AuditLoggingPipeline.Tracker/README.md). They store security-audit entries and state-trail entries in one **standard audit shape** by calling a stored procedure with a table-valued parameter. The sinks contain no SQL text and never create objects: the tables, the table type and the procedures belong to the consuming application's database project and are deployed with its DACPAC.

The package defines the shape and its delivery. The consuming application owns the configuration: it supplies the connection string and procedure names from its own settings, so each app (or microservice) can use its own database.

## Install

```
dotnet add package AuditLoggingPipeline.Sql
```

## Register

```csharp
builder.Services.AddAuditLogging();
builder.Services.AddStateTrailTracking();

builder.Services.AddSqlAuditSink(options =>
{
	options.ConnectionString = builder.Configuration.GetConnectionString("Audit")!;
});

builder.Services.AddSqlStateTrailSink(options =>
{
	options.ConnectionString = builder.Configuration.GetConnectionString("Audit")!;
	options.Targets["Users"] = "dbo.UsersAudit_Insert";
	options.Targets["Roles"] = "dbo.RolesAudit_Insert";
});
```

Options are validated at start-up, so a missing connection string or an empty target map stops the host instead of silently dropping entries.

## Configuration

Every option, its default, and whether it is required or secret is in the [configuration reference](CONFIGURATION.md). The connection string is a secret. It comes from Key Vault when the app is deployed and from user secrets locally, never from a configuration file.

### Security audit: `SqlAuditSinkOptions`

One security audit table per consuming solution, so there is one procedure and no routing.

### State tracking: `SqlStateTrailSinkOptions`

One table per tracked object. `Targets` maps each entry's `EntityName` (case-insensitive) to the insert procedure of that object's table. An entry whose object has no target throws, so the pipeline retries, logs and drops it. It is never written to the wrong table.

## The standard shape

Every audit table has the same columns, whatever it audits.

| Column | Meaning |
|---|---|
| `AuditId` | Identity, primary key |
| `OccurredOn` | UTC time of the entry |
| `Who` / `WhoName` | Acting principal's id and display name |
| `What` | The operation or action |
| `Outcome` | `Success` or `Failure` |
| `CorrelationId` | Ties the entry to the request |
| `EntityType` / `EntityId` | The target and its key; a composite key is its parts joined with `\|` |
| `EntityKeys` | JSON of the key parts by name |
| `State` | JSON of the record after the change; null when there is no record state (security audit, denials) |
| `Reason` | Why, for example a denial reason |
| `IpAddress` | Caller address, `unknown` when not known |

There is no before-state column. An entry is stored on every change, so the previous row for the same entity holds the previous state.

## Database contract

The package ships reference scripts in its `Sql` folder:

1. `1_AuditList_Type.sql` - `dbo.AuditList`, created once per database
2. `2_Audit_Table_Template.sql` - copy once per audit table and rename
3. `3_Audit_Insert_Procedure_Template.sql` - copy once per audit table and rename

The procedure script's `CATCH` block calls `dbo.SqlErrorTracking_Insert`, the CaptiveInnovations error-tracking procedure. Adjust or remove that call if your database does not have it. The application account needs only `EXECUTE` on the procedures and on the table type.

## Behaviour

- Each entry is written in its own transaction, so the pipeline's retry is safe. After the retries the pipeline logs the failure and moves on; it never blocks the application.
- Text longer than its column is truncated rather than failing the write. `EntityKeys` and `State` are not truncated.
- A missing actor, correlation id or address is stored as `unknown`.
