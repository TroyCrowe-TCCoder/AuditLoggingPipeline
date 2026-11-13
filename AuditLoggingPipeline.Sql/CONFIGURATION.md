# AuditLoggingPipeline.Sql configuration reference

Every option of this package, its default, and whether it is required or secret. The host app supplies the
values in code through `AddSqlAuditSink` and `AddSqlStateTrailSink`. The package reads no configuration file
and no environment variable, so where the host keeps the values is the host's choice. A test keeps this file in
step with the code, so a default listed here is the default in the package.

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
builder.Services.AddSqlAuditSink(options =>
{
	options.ConnectionString = builder.Configuration.GetConnectionString("Audit")!;
	options.CommandTimeoutSeconds = 60;
});

builder.Services.AddSqlStateTrailSink(options =>
{
	options.ConnectionString = builder.Configuration.GetConnectionString("Audit")!;
	options.Targets["Users"] = "dbo.UsersAudit_Insert";
	options.Targets["Roles"] = "dbo.RolesAudit_Insert";
});
```

## `SqlAuditSinkOptions`

The security-audit sink. A consuming solution has one security audit table, so there is one procedure and no
routing.

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `ConnectionString` | `none` | Yes | Yes | Connection string of the database that holds the security audit table |
| `ProcedureName` | `dbo.SecurityAudit_Insert` | No | No | The stored procedure that inserts an audit row |
| `ParameterName` | `@Entries` | No | No | The procedure's table-valued parameter |
| `TableTypeName` | `dbo.AuditList` | No | No | The standard audit table type of that parameter |
| `CommandTimeoutSeconds` | `30` | No | No | Timeout in seconds for one procedure call. Must be above 0 |

## `SqlStateTrailSinkOptions`

The state-trail sink. State tracking has one table per tracked object, so `Targets` routes each object to the
procedure of its table.

| Option | Default | Required | Secret | Meaning |
|---|---|---|---|---|
| `ConnectionString` | `none` | Yes | Yes | Connection string of the database that holds the state-trail tables |
| `Targets` | `none` | Yes | No | The insert procedure for each tracked object, keyed by entity name and matched without regard to case. At least one entry is required, and no procedure name may be blank. An entry whose object has no target is not written, and the pipeline logs the failure |
| `ParameterName` | `@Entries` | No | No | The table-valued parameter of every procedure |
| `TableTypeName` | `dbo.AuditList` | No | No | The standard audit table type of that parameter |
| `CommandTimeoutSeconds` | `30` | No | No | Timeout in seconds for one procedure call. Must be above 0 |
