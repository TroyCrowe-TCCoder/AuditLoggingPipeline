# AuditLoggingPipeline

A modular, non-blocking audit and state-trail logging product for .NET APIs. One repository, one
solution, multiple independently installable packages — pick the shared contracts alone, one
plugin, or both.

## Components

| Package | Purpose | Depends on |
|---|---|---|
| `AuditLoggingPipeline.Models` | Shared contracts only (entry types, channel/sink interfaces). Always installed, transitively or directly. | — |
| `AuditLoggingPipeline` (Audit) | "What happened" — method-level action log: who called what, succeeded/failed, timestamp, correlation id. | `AuditLoggingPipeline.Models` |
| `AuditLoggingPipeline.Tracker` (Tracker) | "What changed" — state-trail: before/after snapshot of the entity a method mutated. | `AuditLoggingPipeline.Models` |

Both Audit and Tracker are optional sibling plugins. Install either, both, or (rarely) neither
if you only need the shared contracts to build your own sink implementations.

## Why this shape

- **Choose your components.** A consuming team can adopt only the Audit action log, only the
  Tracker state trail, or both — without pulling in the plugin they don't need.
- **Fully decoupled.** Audit and Tracker never reference each other. Each owns its own bounded
  channel, background hosted service, `DispatchProxy` interceptor, and DI registration helpers.
  They only share the contracts defined in `AuditLoggingPipeline.Models`.
- **Fully non-blocking, for the host and for each other.** Every plugin enqueues onto its own
  bounded, non-blocking channel (`TryWrite`, drop-oldest under pressure) and drains it on its own
  independent `BackgroundService`. A slow or failing sink in one plugin can never delay or fault
  the caller's request, the other plugin's pipeline, or another sink registered in the same
  plugin.
- **Bring your own sinks.** Neither plugin ships a concrete persistence implementation. Each
  consuming application implements `IAuditSink` and/or `ITrackerSink` for whatever destination it
  needs (a database table, a log sink, a queue, etc.) and registers zero, one, or several per
  plugin.

## Quick start

Install only what you need, then wire up the corresponding extension methods and your own sink
implementations at startup.

```csharp
// Models comes in transitively with either plugin below.

// Audit ("what happened")
services.AddAuditLogging();
services.AddScoped<IAuditSink, MyActionLogSink>();
services.AddAuditedScoped<IClientService, ClientService>();

// Tracker ("what changed")
services.AddStateTrailTracking();
services.AddScoped<ITrackerSink, MyStateTrailSink>();
services.AddTrackedScoped<IClientService, ClientService>();
```

Both plugins use the same repository/service mutation naming convention: any wrapped interface
method whose name starts with `Save`, `Add`, `Update`, `Archive`, or `Delete` is observed. All
other calls pass through untouched.

See the package-level READMEs for full details:

- [`AuditLoggingPipeline.Models/README.md`](./AuditLoggingPipeline.Models/README.md)
- [`AuditLoggingPipeline/README.md`](./AuditLoggingPipeline/README.md)
- [`AuditLoggingPipeline.Tracker/README.md`](./AuditLoggingPipeline.Tracker/README.md)

## Repository layout

```
AuditLoggingPipeline.slnx
AuditLoggingPipeline.Models/          Shared contracts (default/base package)
AuditLoggingPipeline/                 Audit plugin ("what happened")
AuditLoggingPipeline.Tracker/         Tracker plugin ("what changed")
AuditLoggingPipeline.Tests/           Audit plugin tests
AuditLoggingPipeline.Tracker.Tests/   Tracker plugin tests
```
