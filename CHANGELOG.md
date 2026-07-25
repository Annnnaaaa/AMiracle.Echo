# Changelog

All notable changes to AMiracle.Echo are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project follows [SemVer](https://semver.org/).

## [Unreleased]

## [0.1.0] - 2026-05-21

Initial public release.

### Phase 1 — core

- Vanilla-JS feedback widget: text + voice recording (`MediaRecorder`/Opus) + screenshot capture.
- Web Component internally, drops into any web page via a single `<script>` tag.
- Floating bubble + inline (`<amiracle-echo-form>`) modes.
- Multi-project backend on ASP.NET Core (minimal APIs).
- Pluggable storage:
  - `IFeedbackStore` — EF Core adapter (Postgres / SQL Server / SQLite / MySQL).
  - `IBlobStore` — local filesystem adapter (S3 / Azure Blob in v1.1).
- Project key + Origin allowlist auth on ingestion; admin bearer token for management.
- CORS reflection on every ingestion response (success *and* error) so browsers surface real error bodies.
- Privacy: per-project retention sweeper, hard-delete, GDPR submitter erasure, DNT-aware, consent gate.
- `IFeedbackProcessor` pipeline (length truncation + redaction stub ship by default).
- Admin web page at `/echo/admin` (no SPA build needed).
- `amiracle-echo` global CLI (`dotnet tool`).

### Phase 2 — analysis (interfaces shipped; OpenAI provider experimental, not on NuGet)

- `IFeedbackAnalyzer` abstraction for transcription + summary + categorization + priority.
- `AnalysisProcessor` background service (opt-in via config).
- Reference OpenAI provider implemented in the repo (Whisper + GPT-4o-mini) but **not published** in v0.1.0 — the cloud/BYOK story is still being designed.

### Phase 3 — triage

- Per-feedback `summary`, `assignee`, comments thread.
- Search box (server-side text + summary contains).
- Filters: status, type, category, priority, assignee, date range.
- Stats endpoint + charts in admin (counts by status/type/category/priority + last 30 days).
- CSV export endpoint (formula-injection-safe).
- Per-feedback **Re-analyze** button.

### Packages published in v0.1.0

- `AMiracle.Echo.Abstractions` — interfaces + DTOs.
- `AMiracle.Echo.Analysis.Abstractions` — analyzer interface.
- `AMiracle.Echo.Server` — ASP.NET Core endpoints + embedded admin + embedded widget.
- `AMiracle.Echo.Storage.EFCore` — EF Core `IFeedbackStore`.
- `AMiracle.Echo.Storage.LocalFS` — local filesystem `IBlobStore`.
- `AMiracle.Echo.Cli` — `dotnet tool install --global AMiracle.Echo.Cli`.

### Known gaps / not yet shipped

- No schema migrations (only `EnsureCreated`). Real EF Core migrations in v1.1.
- No S3 / Azure Blob adapters yet (v1.1).
- No webhook destinations yet (v1.1).
- Multi-user RBAC deferred (v3.x). Single admin bearer token model only.
- `AMiracle.Echo.Analysis.OpenAI` not published to NuGet (source available in the repo).
- `AMiracle.Echo.Host` is an example host, not a NuGet package.

[Unreleased]: https://github.com/Annnnaaaa/AMiracle.Echo/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/Annnnaaaa/AMiracle.Echo/releases/tag/v0.1.0
