# AuditLoggingPipeline.Core

The shared engine behind the AuditLoggingPipeline plugins. You normally get it transitively from
`AuditLoggingPipeline` or `AuditLoggingPipeline.Tracker`; reference it directly only when
building your own plugin.

## What it contains

- `BoundedEntryChannel<TEntry>` - a bounded, non-blocking channel that counts dropped entries.
- `EntryDispatchService<TEntry, TSink>` - the hosted service that drains the channel. For each
  entry it creates one DI scope and calls every registered sink in turn. Each attempt has a
  timeout, failures are retried with exponential backoff, and a sink that still fails is logged
  and skipped so the others run. On shutdown it delivers what is buffered, up to
  `ShutdownDrainTimeout`.
- `EntryPipelineOptions` - channel capacity, full mode, retries, retry delay, sink timeout, and
  shutdown drain timeout.
- `InterceptionOptions` - which methods a proxy observes. `MethodPrefixes` and `MethodSuffixes`
  default to Save, Add, Update, Upsert, Insert, Archive, Restore, Delete, and Remove, and a prefix
  must be followed by a non-lowercase character (`SaveWidget` matches, `Saved` does not). Add your
  own with a collection initializer, or replace the rule entirely with `MethodMatcher`.
- `HttpContextAuditContextProvider` - the default `IAuditContextProvider`, reading the actor from
  the current HTTP request. Register your own to take the actor from another source, such as an
  active-user cache.
- `JsonPayload` - serialization that never truncates and passes valid JSON through unchanged.

## Design notes

- Entries are captured on the caller's thread, so request-scoped data is never read from a
  background task.
- Entries are never lost silently: drops and sink failures are logged at Error.
