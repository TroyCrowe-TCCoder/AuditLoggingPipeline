# AuditLoggingPipeline.Models

Shared contracts and data shapes for the AuditLoggingPipeline product. This package has no
runtime behavior and no dependencies; it defines the types that let the plugins and a consuming
application's own code and sinks interoperate without depending on each other.

## What it contains

- `IEntryChannel<TEntry>` / `IEntrySink<TEntry>` - the generic, non-blocking channel and sink
  contracts every plugin implements.
- `AuditEntry` - the "what happened" security record: actor, action, outcome, entity, reason,
  timestamp, correlation id, and optional serialized arguments.
- `StateTrailEntry` - the "what changed" record: entity name, id, named entity keys, and the
  before and after state as JSON strings.
- `IAuditSink` / `ITrackerSink` - named aliases of `IEntrySink<TEntry>` for each entry type.
- Recorder contracts for explicit recording from code that is not behind a proxy:
  `IAuditRecorder` with `AuditRecord`, and `IStateTrailRecorder` with `StateTrailRecord`.
- `IAuditContextProvider` and `AuditContext` - how a consuming app supplies the acting user's
  id, display name, IP address, and correlation id.

## Design notes

- Contracts and plain shapes only, so this package is safe to reference from anywhere.
- The entry shapes describe the data to audit. Mapping an entry to a specific table or store is
  the job of the consuming application's sinks.
- Plugins never reference each other, only this package (and `AuditLoggingPipeline.Core`).
