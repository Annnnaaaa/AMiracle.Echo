# AMiracle.Echo

Drop-in feedback collection for any web app. Vanilla-JS widget (text + voice + screenshot) plus an ASP.NET Core backend with pluggable storage. MIT licensed.

> **Status:** Phase 1 in development. See [SPEC.md](SPEC.md) and [ROADMAP.md](ROADMAP.md).

## What you get

- **Embeddable widget** — single `<script>` tag, works on React/Vue/Angular/Blazor/plain HTML, Web Component internally.
- **Text + voice + screenshot** capture out of the box.
- **Multi-project** — one backend can serve many consumer projects.
- **Pluggable storage** — `IFeedbackStore` (EF Core: Postgres / SQL Server / SQLite / MySQL) and `IBlobStore` (local FS in v1; S3 + Azure Blob coming in v1.1).
- **Privacy built in** — per-project retention, hard-delete, GDPR right-to-erasure, DNT-aware, optional consent gate.
- **Minimal admin** at `/echo/admin` plus an `amiracle-echo` CLI.

## 5-minute quickstart (Docker)

```bash
docker run -d --name echo \
  -e AMiracle__Echo__AdminToken="$(openssl rand -hex 32)" \
  -e AMiracle__Echo__Database__ConnectionString="Data Source=/data/echo.db" \
  -p 8080:8080 -v echo-data:/data \
  amiracle/echo:latest

# Create a project
ECHO_SERVER_URL=http://localhost:8080 \
ECHO_ADMIN_TOKEN=...your-token... \
amiracle-echo projects create --name "My App" --origins https://myapp.com
# → prints { id, publicKey }

# Drop into your page
<script src="http://localhost:8080/echo/widget.js"
        data-project-id="..." data-public-key="..." defer></script>

# Visit http://localhost:8080/echo/admin to see feedback land.
```

## NuGet integration (.NET hosts)

```csharp
builder.Services.AddAmiracleEcho(builder.Configuration.GetSection("AMiracle:Echo"))
    .AddEchoEfCoreStorage(opts => opts.UseNpgsql(connStr))
    .AddEchoLocalFileBlobStore(opts => opts.RootPath = "/var/echo/blobs");

app.MapAmiracleEcho();
```

## Widget JS API

```js
AMiracleEcho.init({ projectId, publicKey, serverUrl, bubble: true, position: "bottom-right" });
AMiracleEcho.identify({ id, email, name });   // hides name/email fields from UI
AMiracleEcho.setMetadata({ buildVersion: "1.2.3", tier: "pro" });
AMiracleEcho.theme({ primary: "#4f46e5" });
AMiracleEcho.on("submitted", ({ id }) => { /* ... */ });
AMiracleEcho.open(); AMiracleEcho.close();
```

Script-tag attributes: `data-project-id`, `data-public-key`, `data-server-url`, `data-position`, `data-bubble` (`on`/`off`), `data-collect-contact` (`off`/`optional`/`required`), `data-categories`, `data-consent-text`, `data-primary`.

## Repo layout

```
src/
  AMiracle.Echo.Abstractions/      interfaces, DTOs, processor pipeline
  AMiracle.Echo.Server/            ASP.NET Core endpoints + admin HTML + widget JS (embedded)
  AMiracle.Echo.Storage.EFCore/    IFeedbackStore (Postgres / SQL Server / SQLite / MySQL)
  AMiracle.Echo.Storage.LocalFS/   IBlobStore (local filesystem)
  AMiracle.Echo.Cli/               `amiracle-echo` global tool
  AMiracle.Echo.Host/              ASP.NET Core host wrapping Server (used by Docker image)
docker/                            Dockerfile
SPEC.md                            High-level design
ROADMAP.md                         What's coming next
```

## Mobile / non-web (Flutter etc.)

The widget is web-only. Mobile apps either embed `/echo/widget.js` in a WebView or call the REST API directly (`POST /api/v1/feedbacks`, then upload audio/screenshot blobs). The REST contract is a stable public interface.

## License

MIT.
