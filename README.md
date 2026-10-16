# AMiracle.Echo

[![CI](https://github.com/Annnnaaaa/AMiracle.Echo/actions/workflows/ci.yml/badge.svg)](https://github.com/Annnnaaaa/AMiracle.Echo/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/AMiracle.Echo.Server.svg?label=AMiracle.Echo.Server)](https://www.nuget.org/packages/AMiracle.Echo.Server/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 8 + .NET 10](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-512BD4)](https://dotnet.microsoft.com/)

Drop-in feedback collection for any web app. Single-`<script>` widget that captures **text, voice, and screenshots**, plus an ASP.NET Core backend with pluggable storage. MIT licensed, OSS.

```
┌──────────────────┐     drop‑in widget       ┌──────────────────┐
│  Your web app    │ ───────────────────────► │  Echo backend    │
│ (React/Vue/HTML/ │   POST /api/v1/feedbacks │ (.NET 10, NuGet  │
│  Blazor/Flutter) │ ◄─────── 201 Created ──── │  or Docker)      │
└──────────────────┘                          └──────────────────┘
                                                       │
                                              ┌────────┴────────┐
                                              ▼                 ▼
                                         IFeedbackStore     IBlobStore
                                         (Postgres /        (local FS;
                                          SQL Server /       S3 / Azure
                                          SQLite /           coming v1.1)
                                          MySQL via EF Core)
```

> **Status:** Phase 1 (core widget + backend + storage + admin) under active development. Phase 2 (voice transcription, LLM summary, priority scoring) and Phase 3 (full dashboard) are planned — see [SPEC.md](SPEC.md) and [ROADMAP.md](ROADMAP.md).

---

## Contents

1. [Features](#features)
2. [Quickstart](#quickstart)
3. [How auth works (read this!)](#how-auth-works)
4. [Configuration reference](#configuration-reference)
5. [Widget integration](#widget-integration)
6. [REST API](#rest-api)
7. [CLI](#cli)
8. [.NET (NuGet) integration](#net-nuget-integration)
9. [Privacy, retention, GDPR](#privacy-retention-gdpr)
10. [Troubleshooting](#troubleshooting)
11. [Repo layout](#repo-layout)
12. [Roadmap](#roadmap)
13. [License](#license)

---

## Features

- **Embeddable widget** — vanilla JS Web Component in a Shadow DOM (no style bleed, works in any framework).
- **Text + voice + screenshot** capture out of the box. Voice via `MediaRecorder` (Opus). Screenshot via thumbnail with click-to-zoom.
- **Multi-project** — one backend can serve many consumer projects. Each project has its own public key and origin allowlist.
- **Pluggable storage** — split between `IFeedbackStore` (relational metadata, via EF Core — Postgres / SQL Server / SQLite / MySQL) and `IBlobStore` (binary audio/screenshots — local FS in v1, S3 + Azure Blob in v1.1).
- **Privacy by design** — per-project retention sweeper, hard-delete endpoint, GDPR right-to-erasure (`DELETE /admin/submitters/{id}`), DNT-aware, optional consent gate.
- **Minimal admin** at `/echo/admin` (no separate SPA build) + a `amiracle-echo` CLI.
- **Two delivery modes**: as a NuGet library (`app.MapAmiracleEcho()`) for .NET apps, or as a standalone Docker container for any stack.

## Quickstart

### Option A — Run from source (5 minutes, no Docker)

```bash
git clone <this-repo>
cd AMiracle.Echo
dotnet build
```

Open a terminal in `src/AMiracle.Echo.Host/` and run:

**Linux/macOS:**
```bash
export AMiracle__Echo__AdminToken="$(openssl rand -hex 32)"
echo "Your admin token: $AMiracle__Echo__AdminToken"
dotnet run --no-launch-profile
```

**Windows (PowerShell):**
```powershell
$env:AMiracle__Echo__AdminToken = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
Write-Host "Your admin token: $env:AMiracle__Echo__AdminToken"
dotnet run --no-launch-profile
```

**Windows (cmd):**
```bat
set AMiracle__Echo__AdminToken=replace-me-with-a-32+-char-random-string
dotnet run --no-launch-profile
```

The host listens on `http://localhost:5000` by default. **Save your admin token somewhere safe — you'll need it.**

> `--no-launch-profile` is important. Without it, Visual Studio's `launchSettings.json` overrides ports and env vars.

Then:

1. Open <http://localhost:5000/echo/admin> in a browser.
2. Paste your admin token into the field at the top right and click **Save**.
3. Click **+ New** in the left sidebar to create your first project. Add the origin(s) where you'll embed the widget (e.g. `http://localhost:8080`, `https://myapp.com`).
4. The new project's panel shows the generated **public key**. Click **Show widget snippet** for a ready-to-paste `<script>` tag.
5. Paste the snippet into your web app. Click the bubble that appears, send a feedback, watch it land in the admin page.

### Option B — Docker

```bash
docker build -t amiracle/echo:dev -f docker/Dockerfile .

docker run -d --name echo \
  -e AMiracle__Echo__AdminToken="$(openssl rand -hex 32)" \
  -p 8080:8080 -v echo-data:/data \
  amiracle/echo:dev
```

Same admin URL: <http://localhost:8080/echo/admin>.

### Option C — Postgres via Neon (free tier, ~10 minutes)

Want a real Postgres for production / shared dev without standing up your own server? [Neon](https://neon.tech) is serverless Postgres with a generous free tier and an HTTPS-only connection string. Echo runs against it out of the box.

1. **Sign up at <https://neon.tech>** and create a new project (any region close to your Echo host).
2. In the Neon dashboard, copy the **connection string** for the `.NET` view and append `Check Certificate Revocation=true`. It looks like
   `Host=ep-something.eu-central-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=...;SSL Mode=VerifyFull;Channel Binding=Require;Check Certificate Revocation=true`
   (In Linux containers, `SSL Mode=VerifyFull` without `Check Certificate Revocation=true` fails with "The remote certificate was rejected by the provided RemoteCertificateValidationCallback".)
3. Set it on the Echo host:
   ```powershell
   $env:AMiracle__Echo__Database__Provider = "postgres"
   $env:AMiracle__Echo__Database__ConnectionString = "Host=ep-...neon.tech;Database=neondb;Username=...;Password=...;SSL Mode=VerifyFull;Channel Binding=Require;Check Certificate Revocation=true"
   $env:AMiracle__Echo__AdminToken = "your-32-byte-token"
   dotnet run --project src/AMiracle.Echo.Host --no-launch-profile
   ```
4. The first run creates the `projects`, `feedbacks`, and `feedback_comments` tables in Neon. Visit `/echo/admin` and create a project as normal.

> Neon's free tier covers small-to-medium feedback volume. For high throughput, scale up Neon's plan or move to a self-hosted Postgres / RDS instance — the connection string is the only thing that changes.

### Option D — Echo Cloud (managed-by-us, coming soon)

We're working on a hosted offering where the database and Echo host are managed for you. Sign up for the waitlist at <https://amiracle.net/echo> (TBD). Until that lands, options A–C all work today.

---

## How auth works

Echo has **two separate authentication mechanisms**. Mixing them up is the #1 source of confusion.

### 1. Admin token (server-wide secret)

- A single long-random string set on the server via `AMiracle__Echo__AdminToken` (env var) or `appsettings.json` (`AMiracle:Echo:AdminToken`).
- **Required** for everything under `/api/v1/admin/*` (projects, feedbacks management, blob retrieval).
- Sent as a header: `Authorization: Bearer <token>`.
- Used by: the admin web page at `/echo/admin`, the `amiracle-echo` CLI, any custom integrations you build.
- **NEVER expose this token to a browser end-user.** It grants full read/write/delete over all projects.
- Rotate it by changing the env var and restarting the server. The new value is the only valid token after restart.

If you hit `/api/v1/admin/projects` directly in a browser tab, you'll get:
```json
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.2","title":"Missing bearer token.","status":401}
```
That's **expected**. The admin API isn't a public URL — use the `/echo/admin` page or the CLI.

### 2. Project public key (per-project, browser-safe)

- Auto-generated when you create a project. Looks like `ekp_abc123...`. Visible in the admin page's project detail card.
- **Safe to embed in browser JavaScript** — it's not a secret. Anyone reading your site's source can see it.
- Security against abuse comes from **origin allowlisting + rate limits**, not from hiding the key:
  - When you create a project, list the browser origins (`https://myapp.com`, `http://localhost:8080`, etc.) that may submit feedback.
  - The server rejects any `POST /api/v1/feedbacks` whose `Origin` request header isn't in the list.
  - Wildcard subdomains supported: `*.example.com` matches any subdomain.
  - Empty allowlist = accept any origin (handy for dev; **not recommended in production**).
- Rotate via the admin page's **Rotate public key** button (or `amiracle-echo projects rotate-key <id>`). Old key stops working immediately — update your `<script>` snippets.

| Use case | Token type | Header |
|---|---|---|
| Widget submitting feedback from a browser | Project public key | `X-Echo-Project-Key: ekp_...` |
| Admin page / CLI / your backend managing projects | Admin token | `Authorization: Bearer ...` |

---

## Configuration reference

The server reads config via .NET's `IConfiguration` — `appsettings.json` is the default source, but env vars override (using `__` as section separator).

| Key | Env var | Default | Notes |
|---|---|---|---|
| `AMiracle:Echo:AdminToken` | `AMiracle__Echo__AdminToken` | *(empty — must set)* | Bearer token for `/api/v1/admin/*`. |
| `AMiracle:Echo:Database:Provider` | `AMiracle__Echo__Database__Provider` | `sqlite` | `sqlite`, `postgres`. |
| `AMiracle:Echo:Database:ConnectionString` | `AMiracle__Echo__Database__ConnectionString` | `Data Source=echo.db` | Standard EF Core connection string for the provider. |
| `AMiracle:Echo:BlobStore:RootPath` | `AMiracle__Echo__BlobStore__RootPath` | `./echo-blobs` | Where audio/screenshot blobs are written on disk. |
| `AMiracle:Echo:RateLimit:IngestionPerMinute` | `AMiracle__Echo__RateLimit__IngestionPerMinute` | `30` | Per-IP, per-project rate limit on feedback submission. |
| `AMiracle:Echo:RateLimit:AdminPerMinute` | `AMiracle__Echo__RateLimit__AdminPerMinute` | `600` | Per-IP rate limit on admin endpoints. |
| `AMiracle:Echo:MaxAudioBytes` | `AMiracle__Echo__MaxAudioBytes` | `25000000` | Max upload size for audio (25 MB). |
| `AMiracle:Echo:MaxScreenshotBytes` | `AMiracle__Echo__MaxScreenshotBytes` | `5000000` | Max upload size for screenshots (5 MB). |
| `AMiracle:Echo:BlobUploadWindowSeconds` | `AMiracle__Echo__BlobUploadWindowSeconds` | `300` | Promised blobs must arrive within this window or the row is reaped. |
| `AMiracle:Echo:RetentionSweepIntervalMinutes` | `AMiracle__Echo__RetentionSweepIntervalMinutes` | `60` | How often the retention sweeper runs. |
| `AMiracle:Echo:MaxFeedbackTextChars` | `AMiracle__Echo__MaxFeedbackTextChars` | `10000` | Text feedback over this is truncated. |
| `AMiracle:Echo:IdentitySigningKey` | `AMiracle__Echo__IdentitySigningKey` | *(empty — off)* | Enables [signed identity](#signed-identity). Each project's identity secret is derived from it; rotating it rotates every project's secret. |
| `AMiracle:Echo:Email:Provider` | `AMiracle__Echo__Email__Provider` | *(empty — off)* | `resend` or `smtp`. Turns on [replying to submitters](#replying-to-submitters). |
| `AMiracle:Echo:Email:From` | `AMiracle__Echo__Email__From` | | Sender, e.g. `Echo <feedback@yourdomain.com>`. Must be a domain your provider lets you send from. |
| `AMiracle:Echo:Email:ReplyTo` | `AMiracle__Echo__Email__ReplyTo` | | Optional. Where the submitter's answer goes. |
| `AMiracle:Echo:Email:Resend:ApiKey` | `AMiracle__Echo__Email__Resend__ApiKey` | | Resend API key (keep it in a secret). |
| `AMiracle:Echo:Email:Smtp:Host` / `Port` / `Username` / `Password` / `EnableSsl` | `AMiracle__Echo__Email__Smtp__Host` … | port `587`, SSL on | SMTP relay. |

---

## Widget integration

The simplest case — one `<script>` tag:

```html
<script src="http://localhost:5000/echo/widget.js"
        data-project-id="01HX..."
        data-public-key="ekp_abc123..."
        data-position="bottom-right"
        defer></script>
```

### Script-tag attributes

| Attribute | Required | Default | What it does |
|---|---|---|---|
| `data-project-id` | ✓ | — | Project GUID from `POST /admin/projects`. |
| `data-public-key` | ✓ | — | Public key from same response. |
| `data-server-url` | | (inferred from script `src` origin) | Backend base URL if different. |
| `data-position` | | `bottom-right` | `bottom-right`, `bottom-left`, `top-right`, `top-left`. |
| `data-bubble` | | `on` | `off` to hide the floating bubble. Open the widget from your own button with `AMiracleEcho.open()`, or use inline `<amiracle-echo-form>`. |
| `data-locale` | | `<html lang>`, then `en` | `en`, `hy` (Armenian), `ru`. Region tags fall back to the base language (`hy-AM` → `hy`); unknown ones to English. |
| `data-theme` | | `auto` | `light`, `dark`, or `auto` (follow the OS). Set it when your app has its own theme toggle. |
| `data-collect-contact` | | `off` | `optional` or `required` shows an email field when host hasn't called `identify()`. |
| `data-categories` | | (off) | Set to `bug,idea,praise,question` to add a category selector. |
| `data-consent-text` | | (off) | Adds a consent checkbox with this label; recorded with the feedback. |
| `data-primary` | | `#4f46e5` | Theme color. |

### Your own button (no bubble)

```html
<script src="/echo/widget.js" data-project-id="..." data-public-key="..." data-bubble="off" defer></script>
<button onclick="AMiracleEcho.open()">Send feedback</button>
```

### Inline mode (no bubble)

```html
<script src="/echo/widget.js" data-project-id="..." data-public-key="..." data-bubble="off" defer></script>

<!-- somewhere in your DOM -->
<amiracle-echo-form></amiracle-echo-form>
```

### JS API

```js
AMiracleEcho.init({ projectId, publicKey, serverUrl, bubble, position, locale, theme });
AMiracleEcho.open();   // works with the bubble off
AMiracleEcho.close();

// Pass through the logged-in user from your app. Widget hides name/email fields.
// Add `hash` to prove the identity — see Signed identity below.
AMiracleEcho.identify({ id: "user-123", email: "a@b.com", name: "Aram", hash: "<from your server>" });
AMiracleEcho.clearIdentity();

// Attach arbitrary context to every submission.
AMiracleEcho.setMetadata({ buildVersion: "1.2.3", tier: "pro", env: "production" });

// Show the user what they sent before, with status and replies (also reachable from
// "My feedback" in the panel once there is something to show).
AMiracleEcho.openHistory();

// Language at runtime (re-renders an open widget in place).
AMiracleEcho.setLocale("hy");

// Theming at runtime. `mode` follows your app's own light/dark toggle.
AMiracleEcho.theme({ mode: "dark", primary: "#10b981", radius: "16px", font: "Inter, sans-serif" });

// Events: "opened", "closed", "submitted", "error".
AMiracleEcho.on("submitted", ({ id, attachmentsComplete }) => console.log("Submitted feedback", id, attachmentsComplete));
// "error" carries { stage: "create" } when nothing was saved, or
// { stage: "upload", id, audio, screenshot } when the feedback was saved but an attachment wasn't.
AMiracleEcho.on("error", (e) => console.warn("Echo", e));
```

### Screenshots

Two separate choices:

- **Add screenshot** captures the current tab with the browser's Screen Capture API
  (the widget hides itself for the shot); the browser asks which tab to share each
  time. Where that API is missing it uses `html2canvas` if your page loaded it. If
  neither exists — most mobile browsers — the button isn't shown.
- **Attach image** always opens the file picker, for a screenshot or photo the user
  already has.

### Signed identity

`identify()` runs in the browser, and the public key is in your page source, so on
its own a submitter is only a *claim* — anyone could send feedback as
`ceo@yourcompany.com`. Signing makes it provable:

1. Set `AMiracle__Echo__IdentitySigningKey` on the Echo server (any long random string).
2. In the admin, open the project's **Show snippet**: it shows the project's identity
   secret. Store it on **your** server — never in the page.
3. On your server, sign the user's id and hand the hash to the page:

```js
// Node — same in any language: lowercase hex HMAC-SHA256(secret, user.id)
const hash = crypto.createHmac("sha256", ECHO_IDENTITY_SECRET).update(user.id).digest("hex");
```

```js
AMiracleEcho.identify({ id: user.id, email: user.email, hash });
```

The admin then badges each submitter **verified**, **unverified** (no hash sent), or
**signature mismatch** (a hash was sent but doesn't match — treat as forged). The
hash itself is never stored, and a client can't mark itself verified — the server
always sets that flag.

### Feedback history

Users can look back at what they sent, its status (**Received** → **In review** →
**Resolved**) and any reply you emailed from the admin. Internal comments are never shown.

- **Signed-in users** (a signed identity, above): everything they sent to the project,
  on any device.
- **Anonymous users:** each send returns a `receiptToken`. The widget keeps receipts in
  the browser, so users see what they sent from that browser. Receipts are signed with
  `AMiracle__Echo__ReceiptSigningKey` — set it, or they're derived from the admin token
  and stop working if you rotate it.

### Mobile / Flutter

The widget itself is web-only. For mobile apps:

- **Easiest:** embed `http://your-echo-server/echo/widget.js` inside a WebView.
- **Native-feel:** call the REST API directly from Dart/Swift/Kotlin (`POST /api/v1/feedbacks`, then optional blob uploads). See [REST API](#rest-api) below — the contract is a stable public interface.

---

## REST API

All endpoints are versioned `/api/v1/`. JSON is `camelCase`. Errors follow RFC 7807 (`application/problem+json`).

### Public ingestion (widget calls these)

Auth: `X-Echo-Project-Key: ekp_...` header + `Origin` header validated against project allowlist.

| Method | Path | Body | Returns |
|---|---|---|---|
| `POST` | `/api/v1/feedbacks` | `{type, text?, pageUrl?, userAgent?, submitter?, customMetadata?, category?, consentText?, willUploadAudio?, willUploadScreenshot?}` | `201 {id, uploadAudioUrl?, uploadScreenshotUrl?, receiptToken}` |
| `POST` | `/api/v1/feedbacks/{id}/audio` | binary (audio/*) | `204` |
| `POST` | `/api/v1/feedbacks/{id}/screenshot` | binary (image/*) | `204` |
| `POST` | `/api/v1/feedbacks/history` | `{submitter?: {id, hash}, receipts?: [{id, token}]}` (max 100 receipts) | `200 {identity: "verified"\|"invalid"\|"none", items: [{id, createdAt, type, status, category, text, hasAudio, hasScreenshot, replies: [{sentAt, message}]}]}` |

`history` is a `POST` so receipts and identity hashes stay out of URLs and logs. A
signed `submitter` returns everything that user sent to the project; each valid receipt
returns that one feedback. Anything else — an unsigned id, a wrong token, another
project's feedback — returns nothing.

### Admin (CLI, admin page, your backend)

Auth: `Authorization: Bearer <admin-token>`.

| Method | Path |
|---|---|
| `GET\|POST` | `/api/v1/admin/projects` |
| `GET\|PATCH\|DELETE` | `/api/v1/admin/projects/{id}` |
| `POST` | `/api/v1/admin/projects/{id}/rotate-key` |
| `GET` | `/api/v1/admin/feedbacks?projectId=&status=&type=&since=&cursor=&limit=` |
| `GET\|PATCH\|DELETE` | `/api/v1/admin/feedbacks/{id}` |
| `GET` | `/api/v1/admin/feedbacks/{id}/audio` |
| `GET` | `/api/v1/admin/feedbacks/{id}/screenshot` |
| `DELETE` | `/api/v1/admin/submitters/{submitterId}` (GDPR erasure) |
| `POST` | `/api/v1/admin/feedbacks/{id}/reply` — `{ "message": "…", "resolve": true }` |
| `GET` | `/api/v1/admin/capabilities` — `{ emailReplies, identitySigning }` |

### Replying to submitters

With `Email:Provider` set, the feedback detail in the admin page has a **Reply** box. Echo emails the submitter's address with your message and a short quote of what they wrote, records exactly what was sent in the comment thread (author *Reply to submitter*), and can mark the feedback resolved in the same step.

- **Who it goes to.** The address is whatever the widget sent. Unless the project uses [signed identity](#signed-identity), anyone holding the public key could have typed someone else's address, so the admin page warns before you reply to an unsigned one. Write accordingly — don't put anything private in a reply to an unverified address.
- **Errors.** `501` when email isn't configured, `409` when the feedback has no single plain address (lists, display names and header-injection attempts are refused), `502` when the provider rejects the message — nothing is recorded and the status is left alone.
- **Other providers.** Register your own `IEchoEmailSender` before `AddAmiracleEcho()` and Echo uses it instead.

---

## CLI

The `amiracle-echo` CLI wraps the admin API. Install it as a global tool:

```bash
dotnet pack src/AMiracle.Echo.Cli/AMiracle.Echo.Cli.csproj
dotnet tool install --global --add-source ./src/AMiracle.Echo.Cli/bin/Release AMiracle.Echo.Cli
```

Or just `dotnet run --project src/AMiracle.Echo.Cli -- <args>` from this repo.

Configure connection:
```bash
export ECHO_SERVER_URL=http://localhost:5000
export ECHO_ADMIN_TOKEN=...your-token...
```

Common commands:
```
amiracle-echo projects list
amiracle-echo projects create --name "My App" --origins https://myapp.com,http://localhost:8080
amiracle-echo projects show <id>
amiracle-echo projects rotate-key <id>
amiracle-echo projects delete <id>

amiracle-echo feedbacks list --project <id> [--status new] [--type voice] [--limit 50]
amiracle-echo feedbacks show <id>
amiracle-echo feedbacks set-status <id> resolved
amiracle-echo feedbacks set-priority <id> 3
amiracle-echo feedbacks delete <id>
amiracle-echo feedbacks download-audio <id> --out ./out.opus
amiracle-echo feedbacks download-screenshot <id> --out ./out.png

amiracle-echo submitters delete <submitterId>     # GDPR erasure
```

---

## .NET (NuGet) integration

For .NET hosts that want to embed the backend into their existing ASP.NET Core app:

```csharp
// Program.cs
builder.Services.AddAmiracleEcho(builder.Configuration.GetSection("AMiracle:Echo"));
builder.Services.AddEchoEfCoreStorage(opts => opts.UseNpgsql(connStr));
builder.Services.AddEchoLocalFileBlobStore(opts => opts.RootPath = "/var/echo/blobs");

var app = builder.Build();

// Mounts /api/v1/*, /echo/admin, /echo/widget.js
app.MapAmiracleEcho();

app.Run();
```

`appsettings.json`:
```json
{
  "AMiracle": {
    "Echo": {
      "AdminToken": "set-via-user-secrets-or-env",
      "Database": { "Provider": "postgres", "ConnectionString": "Host=..." }
    }
  }
}
```

> Don't commit the admin token to source control. Use `dotnet user-secrets` in dev and env vars / a secrets manager in prod.

---

## Privacy, retention, GDPR

Built into v1, no extra work needed:

- **Per-project retention.** Set `retentionDays` when creating a project. A background sweeper deletes feedbacks (and their blobs) older than the threshold every `RetentionSweepIntervalMinutes`. Leave blank to keep forever.
- **Hard delete.** `DELETE /api/v1/admin/feedbacks/{id}` removes the row *and* the blobs immediately — no soft-delete flag.
- **Right to erasure.** `DELETE /api/v1/admin/submitters/{submitterId}` removes every feedback tied to a submitter ID (the one your host app passed via `AMiracleEcho.identify({ id: ... })`).
- **DNT.** If the browser sends `DNT: 1`, the widget will not send `pageUrl` or `userAgent`.
- **Consent gate.** Set `data-consent-text` and the widget shows a one-time checkbox; the consent string is stored with the feedback.
- **PII redaction hook.** Implement `IFeedbackProcessor` and register it via DI to run server-side before persistence (e.g. regex-scrub emails/phones). v1 ships a no-op stub.
- **Encryption at rest** is the storage layer's responsibility. Recommend disk encryption / encrypted Postgres / encrypted S3 buckets in production.

---

## Troubleshooting

### "Missing bearer token. (401)" when I open `/api/v1/admin/projects`
Expected. That endpoint is auth-protected — use `/echo/admin` (a browser page) or the CLI. See [How auth works](#how-auth-works).

### "Access to fetch ... has been blocked by CORS policy"
The browser blocks the request because the project's **allowed origins** list doesn't include the origin where the widget is running. Fix:
1. Open `/echo/admin`.
2. Click the project on the left, then **Edit** in the detail panel.
3. Add the origin (e.g. `http://localhost:8080`) to the comma-separated list. Save.
4. Refresh your page and try again.

(As of the latest version, CORS headers are now set on *all* ingestion responses, so even auth errors should be readable in the browser console.)

### Project creation silently fails in the admin page
You likely didn't save an admin token first (or pasted a wrong one). The admin page now shows a red error banner — re-check the token at the top.

### "There is not enough space on the disk" during `dotnet build`
NuGet needs a few hundred MB to unpack packages. Free up disk space; if needed, `dotnet nuget locals all --clear` (forces re-download next time, but reclaims cache).

### Audio doesn't record / "Microphone access denied"
Browser-level mic permission was denied. Click the address-bar 🎤 icon to grant access, then try again. Voice recording is also unavailable on insecure origins (non-HTTPS, non-localhost).

### Where are the blobs stored?
By default in `./echo-blobs/<projectId>/<feedbackId>/audio.webm` (or `.png`). Configurable via `AMiracle__Echo__BlobStore__RootPath`.

### How do I migrate from SQLite to Postgres?
Stop the server. Use [pgloader](https://pgloader.io) or your favorite ETL to move `projects` and `feedbacks` tables. Update `AMiracle__Echo__Database__Provider=postgres` and `ConnectionString=...`. Restart. The schema is identical across providers (EF Core handles dialect differences).

---

## Repo layout

```
src/
  AMiracle.Echo.Abstractions/      interfaces, DTOs, processor pipeline
  AMiracle.Echo.Server/            ASP.NET Core endpoints + admin HTML + widget JS (embedded)
  AMiracle.Echo.Storage.EFCore/    IFeedbackStore (Postgres / SQL Server / SQLite / MySQL)
  AMiracle.Echo.Storage.LocalFS/   IBlobStore (local filesystem)
  AMiracle.Echo.Cli/               `amiracle-echo` global tool
  AMiracle.Echo.Host/              ASP.NET Core host wrapping Server (used by Docker image)
tests/
  AMiracle.Echo.Tests/             xUnit: the endpoints on an in-memory server (`dotnet test`)
  widget-e2e/                      Playwright: the widget in a real browser (`npm test`)
docker/                            Dockerfile
SPEC.md                            High-level design
ROADMAP.md                         What's coming next
```

## Roadmap

**v1.1**: S3-compatible blob adapter, Azure Blob adapter, webhook destinations, optional JWT widget auth.
**v2 (Phase 2)**: voice transcription, LLM-based summarization, priority scoring, real PII redaction.
**v3 (Phase 3)**: polished SPA dashboard, multi-user RBAC, triage workflow, search, charts, exports.

See [ROADMAP.md](ROADMAP.md) for the full list.

## License

MIT — see [LICENSE](LICENSE).
