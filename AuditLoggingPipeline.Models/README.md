# AuditLoggingPipeline.Models

Shared contracts for the AuditLoggingPipeline product. This package has no runtime behavior of
its own; it defines the types that let the `AuditLoggingPipeline` (Audit) and
`AuditLoggingPipeline.Tracker` (Tracker) plugins interoperate with a consuming application
without depending on each other.

## What it contains

- `IEntryChannel<TEntry>` / `IEntrySink<TEntry>` — the generic, non-blocking channel and sink
  contracts every plugin implements.
- `AuditEntry` — the "what happened" action-log record (who, what method, succeeded/failed,
  timestamp, correlation id).
- `StateTrailEntry` — the "what changed" record (before/after entity snapshot).
- `IAuditSink` / `ITrackerSink` — named, discoverable aliases of `IEntrySink<TEntry>` for each
  plugin's entry type.

## Who needs this package

Always. `AuditLoggingPipeline.Models` is the default/base package. Install it alone only if
you're authoring your own sink implementations against these contracts ahead of adding a plugin;
otherwise it comes in transitively with `AuditLoggingPipeline` and/or
`AuditLoggingPipeline.Tracker`.

## Design notes

- Contains contracts only — no channels, background services, or DispatchProxy logic. That
  keeps this package trivially safe to reference from anywhere (including a consumer's own sink
  implementations) without pulling in ASP.NET Core or plugin-specific runtime dependencies.
- Plugins never reference each other, only this package.
