# AuditLoggingPipeline

Non-blocking, multi-sink audit and state-trail logging for .NET APIs, built around a
`DispatchProxy`-based interception pattern.

## What it does

`AuditingDispatchProxy<TInterface>` transparently wraps a service interface implementation.
Any invoked method whose name starts with `Save`, `Add`, `Update`, `Archive`, or `Delete` is
observed asynchronously and turned into an `AuditEntry` — without ever delaying, blocking, or
faulting the caller's original request. Entries are queued onto a bounded `AuditChannel` and
drained by a background hosted service, `AuditLogBackgroundService`, which fans each entry out
to every `IAuditSink` registered by the consuming application.

This library ships the interception mechanism and plumbing only. It does not ship any concrete
sink implementation — each consuming application defines its own `IAuditSink` implementations
for whatever purpose it needs (e.g. an action log persisted to an audit table, or a
security/state-trail append-only history table), and registers zero, one, or several of them.
Each registered sink is invoked and isolated independently: a slow or failing sink can never
affect another sink, the channel drain loop, or the caller's original request.

## Usage

```csharp
// Startup
services.AddAuditLogging();
services.AddScoped<IAuditSink, MyActionLogSink>();
services.AddAuditedScoped<IClientService, ClientService>();
```

## Design notes

- One shared interception mechanism serves any number of logging purposes; adding a new purpose
  means writing a new `IAuditSink` implementation, not changing this library.
- No purpose is hardcoded (no "action log" vs. "state trail" split baked into the proxy).
- Sinks needing "previous state" for a state-trail use case are expected to rely on their own
  append-only table's most recent row for that entity — the proxy and this library do not fetch
  or supply prior state.
