# Changelog

All notable changes to AMiracle.Echo are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project follows [SemVer](https://semver.org/).

## [Unreleased]

### Added

- **Reply to submitters by email** from the admin page (Resend or SMTP, `AMiracle:Echo:Email`). The reply is recorded in the comment thread and can resolve the feedback. Unsigned addresses get a warning; malformed or multi-recipient addresses are refused. `POST /api/v1/admin/feedbacks/{id}/reply`, `GET /api/v1/admin/capabilities`, `IEchoEmailSender` for custom providers.
- **Tests.** `tests/AMiracle.Echo.Tests` (xUnit, the real endpoints on an in-memory server with SQLite) and `tests/widget-e2e` (Playwright: real tab capture, file attach, upload retry). Both run in CI.
- **Signed identity.** Set `IdentitySigningKey` on the server; each project gets a derived identity secret (shown under **Show snippet**). Host apps sign the user id server-side and pass `hash` to `identify()`; feedback is badged *verified*, *unverified* or *signature mismatch*. No schema change — the flag lives in the existing submitter JSON. `GET /api/v1/admin/projects/{id}/identity-secret`.
- Widget: Armenian (`hy`) and Russian (`ru`) translations. `data-locale` / `setLocale()` now work (they were a no-op), re-rendering an open widget in place. Defaults to `<html lang>`.
- Widget: `data-theme` / `theme({ mode })` for `light`, `dark` or `auto`, for apps with their own theme toggle.
- Widget: **Add screenshot** captures the current tab via the Screen Capture API instead of opening a file picker, and is hidden where the browser can't capture. **Attach image** is a separate button that always lets the user pick an existing file.
- Widget: `closed` event; `submitted` reports `attachmentsComplete`.
- Widget: the bubble and panel are exposed as CSS parts (`amiracle-echo::part(bubble)`, `::part(panel)`), so a host page can, for example, hide the bubble on phones with a media query.

- Widget: **Highlight** button on an attached screenshot — drag to draw boxes, Undo, Done bakes them into the uploaded image.
- Admin: submitter shown in a **From** column and at the top of the feedback detail (email is a `mailto:` link); says so explicitly when no submitter was sent.
- `deploy/azure/deploy.ps1`: `-MinReplicas` (0 or 1) to keep one replica warm and avoid the cold start after idle.

### Fixed

- Widget: **Add screenshot** could hang forever on a static page, leaving the panel hidden: tab capture only sends a frame when the page repaints. The widget now triggers a repaint until the first frame arrives, and after 5 seconds shows "Couldn't capture the screen." and brings the panel back.
- Security: EF Core 9.0.0 → 9.0.21 (bundled SQLite had a high-severity advisory, GHSA-2m69-gcr7-jv3q), Npgsql provider 9.0.4, SourceLink 8.0.0 → 10.0.402 (GHSA-23fw-v26w-5fgq).
- Widget: `AMiracleEcho.open()` did nothing with `data-bubble="off"` — the element was never mounted. It's now always mounted, and opens from your own button.
- Widget: voice and screenshot uploads were never checked, so a failed attachment still showed "Thanks!". They're retried once; on failure the user is told exactly which attachment was lost, and an `error` event fires with `stage: "upload"`.
- Widget: "Send another" didn't cancel the previous submission's auto-close, so the panel could close while the user was typing a second message.
- Widget: re-rendering reset a colour set via `theme()` back to the default.
- Widget: an `<amiracle-echo-form>` already in the page's HTML crashed on load (the element was registered before the API existed).
- Widget: every message is translated, including errors that were hard-coded in English; emoji icons replaced with inline SVG; the mic button no longer says "Hold to record" for a tap-to-toggle control.

- Host: Postgres connections retry on transient failure, so the first query after a serverless database (Neon) wakes up no longer fails.
- Audio uploads: content types with parameters (`audio/webm; codecs=opus`) and phone formats (`audio/x-m4a`, `audio/wav`, `audio/flac`) get the right file extension.
- OpenAI analyzer: transcription is sent with the recording's real format instead of always claiming WebM.

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
