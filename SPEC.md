# AMiracle.Echo — Phase 1 Spec (high-level)

Drop-in OSS feedback collection for any web app. .NET backend with pluggable storage; vanilla-JS widget that embeds anywhere; minimal admin web page + CLI.

## Phase 1 scope

- Vanilla-JS widget (text + voice recording + screenshot)
- ASP.NET Core backend (NuGet library + Docker image)
- Pluggable `IFeedbackStore` (EF Core: Postgres / SQL Server / SQLite / MySQL)
- Pluggable `IBlobStore` (local filesystem only in v1)
- Minimal admin HTML page at `/echo/admin` + bearer-token-protected admin REST API
- `dotnet amiracle-echo` CLI for projects + feedbacks
- Retention sweeper (per-project `retentionDays`, default `null` = keep forever)
- Multi-project support (one backend / DB serves N consumer projects)

## Non-goals (deferred — see ROADMAP.md)

v1.1: S3-compatible blob adapter, Azure Blob adapter, webhook destinations, optional JWT widget auth.
v2 (Phase 2): voice transcription, LLM summarization, priority scoring, real PII redaction.
v3 (Phase 3): polished SPA dashboard, multi-user RBAC, triage workflow.

## Architecture

```
host page  ─►  /echo/widget.js  ─►  POST /api/v1/feedbacks
                                    POST /api/v1/feedbacks/{id}/audio
                                    POST /api/v1/feedbacks/{id}/screenshot

admin page ─►  /api/v1/admin/*    (bearer token)
CLI        ─►  /api/v1/admin/*    (bearer token)

server: FeedbackIngestionService → IFeedbackProcessor[] → IFeedbackStore + IBlobStore
        RetentionSweeper (IHostedService)
```

## Repo layout

```
src/
  AMiracle.Echo.Abstractions/       interfaces, DTOs, IFeedbackProcessor
  AMiracle.Echo.Server/             ASP.NET Core endpoints, services, admin HTML
  AMiracle.Echo.Storage.EFCore/     IFeedbackStore impl + migrations
  AMiracle.Echo.Storage.LocalFS/    IBlobStore impl
  AMiracle.Echo.Cli/                `dotnet amiracle-echo` global tool
  AMiracle.Echo.Host/               thin Program.cs for Docker image
  widget/                           TypeScript → bundled JS, served by Server
docker/
tests/
```

## Data model (logical)

**projects**: `id, name, publicKey (unique), allowedOrigins[], retentionDays?, createdAt, archivedAt?`
**feedbacks**: `id, projectId, type ('text'|'voice'), text?, audioBlobKey?, screenshotKey?, pageUrl?, userAgent?, submitter (jsonb)?, customMetadata (jsonb)?, category? ('bug'|'idea'|'praise'|'question'), status ('new'|'triaged'|'resolved'), priority? (1-5), consentText?, createdAt, deletedAt?`

Plural in routes / table names / JSON arrays. Singular in C# class names. JSON is `camelCase`.

## REST API (versioned `/api/v1/`)

Public ingestion (auth: `X-Echo-Project-Key` + Origin allowlist + rate limit):
- `POST /feedbacks` → 201 `{id}`
- `POST /feedbacks/{id}/audio` (binary upload)
- `POST /feedbacks/{id}/screenshot` (binary upload)

Admin (auth: `Authorization: Bearer <admin-token>`):
- `GET|POST /admin/projects`, `GET|PATCH|DELETE /admin/projects/{id}`, `POST /admin/projects/{id}/rotate-key`
- `GET /admin/feedbacks` (filters), `GET|PATCH|DELETE /admin/feedbacks/{id}`
- `GET /admin/feedbacks/{id}/audio`, `GET /admin/feedbacks/{id}/screenshot`
- `DELETE /admin/submitters/{submitterId}` (GDPR)

Errors: RFC 7807 (`application/problem+json`).

## Widget

- Single self-contained JS, internally a Web Component (`<amiracle-echo>`), Shadow DOM (no style bleed).
- Auto-mount via `<script src="/echo/widget.js" data-project-id=... data-public-key=... defer>`.
- JS API: `init`, `open`, `close`, `identify`, `clearIdentity`, `setMetadata`, `setLocale`, `theme`, event hooks.
- Bubble (default) + inline (`<amiracle-echo-form>`) + bubble-off mode.
- Voice via `MediaRecorder` (Opus, 5 min hard cap).
- Screenshot via `html-to-image`.
- Theming via CSS custom properties; `prefers-color-scheme`-aware.
- WCAG 2.1 AA target, `axe-core`-tested.
- v1 ships `en` only; locale infrastructure ready.
- If `identify({...})` is called by host, name/email never asked. Host context wins; never re-prompt for what host already knows.

## Privacy / GDPR (v1 baked-in)

- Per-project `retentionDays` + retention sweeper (default `null` = forever).
- Hard `DELETE /admin/feedbacks/{id}` removes row + blobs.
- `DELETE /admin/submitters/{submitterId}` for right-to-erasure across all feedbacks.
- Optional consent gate (`data-consent-text`).
- DNT-aware: don't auto-collect `userAgent`/`pageUrl` if `DNT: 1` unless explicitly opted in.
- `IFeedbackProcessor` pipeline; ships with `NoOpRedactionProcessor` (off by default) and `MaxLengthProcessor`.

## First-run experience

1. `docker run -e ECHO_DB=... -e ECHO_BLOB_PATH=... -e ECHO_ADMIN_TOKEN=... -p 8080:8080 amiracle/echo`
2. `dotnet amiracle-echo projects create --name "My App" --origins https://myapp.com` → prints `projectId` + `publicKey`
3. Paste `<script>` snippet into host page
4. Visit `/echo/admin`, paste admin token, see feedback land

## Tech

- .NET 10 (LTS). ASP.NET Core minimal APIs. EF Core 10 (Npgsql, SqlServer, Sqlite, Pomelo.MySql).
- Widget: TypeScript + esbuild. Tests: xUnit + WebApplicationFactory + Testcontainers; Vitest + Playwright + axe-core for widget.
- License: MIT.

## Naming rules (hard)

- Product / package: `AMiracle.Echo`. Never use "Echo" as a synonym for a feedback record.
- Entity: `Feedback` (C# singular), `feedbacks` (plural in routes / tables / JSON arrays).
- JSON: `camelCase`. C#: `PascalCase`.
