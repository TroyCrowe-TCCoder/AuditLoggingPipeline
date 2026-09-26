# Copilot Instructions

Before doing any work in this repository, read the global governance standard first:

**[GlobalGovernanceStandards.md](../../ValidationEngine/ValidationEngine/Docs/Standards/GlobalGovernanceStandards.md)**

That document is the mandatory entry point into the full standards corpus (coding, security,
database, testing, repository, and pipeline standards) and inner-links to every other applicable
standards file. Follow it before making assumptions about repository structure, coding
conventions, or process.

## Repository-Specific Notes

- This repository (`AuditLoggingPipeline`) is a modular, non-blocking audit/state-tracking
  library suite: `AuditLoggingPipeline.Models` (shared contracts), `AuditLoggingPipeline` (Audit
  plugin), and `AuditLoggingPipeline.Tracker` (Tracker plugin).
- Standards compliance for this repository is enforced via:
  - `ValidationEngine.Analyzers` (Roslyn analyzer NuGet package) for compile-time enforcement.
  - `validation-engine` (.NET tool) for full repository validation runs, invoked via a git
	pre-commit hook and CI.
- Default branch is `main`.
