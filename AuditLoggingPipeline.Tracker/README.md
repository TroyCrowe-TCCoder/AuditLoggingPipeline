# AuditLoggingPipeline.Tracker

Non-blocking state-trail (before/after entity value) tracking for .NET APIs, built around a
`DispatchProxy`-based interception pattern. Independent sibling of `AuditLoggingPipeline`
(the Audit plugin); depends only on `AuditLoggingPipeline.Models`.

## What it does

`TrackingDispatchProxy<TInterface>` transparently wraps a service interface implementation. Any
invoked method whose name starts with `Save`, `Add`, `Update`, `Archive`, or `Delete` has its
first reference-type argument (the entity being mutated) snapshotted before the call, and the
call's outcome observed asynchronously afterward — without ever delaying, blocking, or faulting
the caller's original request. A `StateTrailEntry` (before/after snapshot, success/failure) is
queued onto a bounded `TrackerChannel` and drained by a background hosted service,
`TrackerLogBackgroundService`, which fans each entry out to every `ITrackerSink` registered by
the consuming application.

This library ships the interception mechanism and plumbing only. It does not ship any concrete
sink implementation — each consuming application defines its own `ITrackerSink` implementations
(e.g. a state-trail table keyed by entity id) and registers zero, one, or several of them. Each
registered sink is invoked and isolated independently: a slow or failing sink can never affect
another sink, the channel drain loop, or the caller's original request.

## Usage

```csharp
// Startup
services.AddStateTrailTracking();
services.AddScoped<ITrackerSink, MyStateTrailSink>();
services.AddTrackedScoped<IClientService, ClientService>();
```

## Relationship to AuditLoggingPipeline (Audit)

Tracker and Audit are fully independent sibling plugins:

- Each owns its own bounded channel, background service, DispatchProxy, and DI registration
  helpers (`AddStateTrailTracking`/`AddTrackedScoped` vs. `AddAuditLogging`/`AddAuditedScoped`).
- Neither references the other; both depend only on `AuditLoggingPipeline.Models`.
- A consumer can install `Models` + `Tracker` only, `Models` + Audit only, or both — a failure or
  slowdown in one plugin's pipeline never affects the other.

## Design notes

- Tracking is fully non-blocking: results are returned to the caller untouched, and tracking
  work is spun off independently via `Task.Run`.
- Any exception thrown by the wrapped call propagates to the caller unchanged (unwrapped from
  the `DispatchProxy` reflection wrapper); the tracking task never rethrows.
