# AuditLoggingPipeline

A modular, non-blocking audit and state-trail logging product for .NET APIs. One repository, one
solution, multiple independently installable packages — pick the shared contracts alone, one
plugin, or both.

## Components

| Package | Purpose | Depends on |
|---|---|---|
| `AuditLoggingPipeline.Models` | Shared contracts and data shapes (entry types, recorder, context, channel and sink interfaces). No dependencies. | — |
| `AuditLoggingPipeline.Core` | Shared engine: bounded channel, dispatch service with retry, timeout and shutdown drain, interception options. Comes in transitively. | `AuditLoggingPipeline.Models` |
| `AuditLoggingPipeline` (Audit) | "What happened" — who did what to which record, and whether it worked. | Models, Core |
| `AuditLoggingPipeline.Tracker` (Tracker) | "What changed" — state trail of the record a method mutated. | Models, Core |

Both Audit and Tracker are optional sibling plugins. Install either, both, or (rarely) neither
if you only need the shared contracts to build your own sink implementations.

## Why this shape

- **Choose your components.** A consuming team can adopt only the Audit action log, only the
  Tracker state trail, or both — without pulling in the plugin they don't need.
- **Fully decoupled.** Audit and Tracker never reference each other. Each owns its own channel,
  hosted service, `DispatchProxy` interceptor, and DI registration helpers, built on the shared
  Core engine. They share only the contracts in `AuditLoggingPipeline.Models`.
- **Non-blocking, and no silent loss.** Entries are captured on the caller's thread, enqueued on a
  bounded channel, and delivered by a background service. Each sink gets a timeout and retries,
  and a failing sink never stops the others. Dropped entries and sink failures are logged, and
  buffered entries are delivered on shutdown.
- **Bring your own sinks.** Neither plugin ships a concrete store. Each consuming application
  implements `IAuditSink` and/or `ITrackerSink` and maps the entry onto its own tables.

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

Both plugins use the same method-name rule: a wrapped interface method is observed when its name
starts or ends with `Save`, `Add`, `Update`, `Upsert`, `Insert`, `Archive`, `Restore`, `Delete`,
or `Remove`, and you can add your own words or supply a full matcher through the options. All
other calls pass through untouched. For events a proxy cannot see, inject `IAuditRecorder` or
`IStateTrailRecorder`.

See the package-level READMEs for full details:

- [`AuditLoggingPipeline.Models/README.md`](./AuditLoggingPipeline.Models/README.md)
- [`AuditLoggingPipeline.Core/README.md`](./AuditLoggingPipeline.Core/README.md)
- [`AuditLoggingPipeline/README.md`](./AuditLoggingPipeline/README.md)
- [`AuditLoggingPipeline.Tracker/README.md`](./AuditLoggingPipeline.Tracker/README.md)

## Repository layout

```
AuditLoggingPipeline.slnx
AuditLoggingPipeline.Models/          Shared contracts and shapes
AuditLoggingPipeline.Core/            Shared engine
AuditLoggingPipeline/                 Audit plugin ("what happened")
AuditLoggingPipeline.Tracker/         Tracker plugin ("what changed")
AuditLoggingPipeline.Tests/           Audit plugin and Core tests
AuditLoggingPipeline.Tracker.Tests/   Tracker plugin tests
```
