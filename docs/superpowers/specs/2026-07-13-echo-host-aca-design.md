# Echo Host on Azure Container Apps — Design

**Date:** 2026-07-13 (runbook updated 2026-10-06)  
**Status:** Approved for deploy (shared Host v1)  
**Goal:** Publish one shared `AMiracle.Echo.Host` so widgets load from a public HTTPS URL; keep a clear path to Echo Cloud later.

---

## 1. Product decisions

| Decision | Choice |
|---|---|
| Audience now | Own projects first; early customers as **projects** on the same Host |
| Later SaaS | Echo Cloud for signup/billing/provisioning; still this shared Host until isolation is needed |
| Compute | Azure Container Apps, **Consumption** plan (Students-friendly free grant) |
| Image registry | GitHub Container Registry (`ghcr.io`), built by GitHub Actions (`.github/workflows/docker.yml`) |
| Database | Existing Neon Postgres (one DB, many Echo projects) |
| Public URL | Custom domain + ACA managed TLS from day one |
| Blob storage | Azure Files share mounted at `/data/blobs` (or container-local disk for text-only use) |

### What customers use

Browsers only talk to **Echo Host** (`/echo/widget.js`, `/api/v1/...`).  
**Echo Cloud** is an internal control plane (Neon provisioning, tokens). It is not required for this deploy.

### Shared Host model

```
Customer sites ──widget──► https://echo.<your-domain>  (ONE Container App)
                                    │
                                    ▼
                              Neon (shared)
                         many rows in `projects`
```

Each customer/app = one Echo **project** (public key + allowed origins). They do not run their own Host.

---

## 2. Architecture

```
git push to main
        │ GitHub Actions (docker.yml)
        ▼
 ghcr.io/<owner>/amiracle-echo:latest   (public package)
        │ ACA pulls on each new revision
        ▼
 Azure Container Apps (Consumption, max 1 replica)
   AMiracle.Echo.Host :8080
   https://echo.<your-domain>
        │
        ├─ Neon Postgres (metadata)
        └─ /data/blobs → Azure Files share (audio/screenshots)
```

### Runtime config (ACA secrets / env)

| Env var | Value |
|---|---|
| `ASPNETCORE_URLS` | `http://+:8080` (Dockerfile default) |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` (Dockerfile default; real client IP for rate limits behind ACA ingress) |
| `AMiracle__Echo__AdminToken` | secret `admin-token` |
| `AMiracle__Echo__Database__Provider` | `postgres` |
| `AMiracle__Echo__Database__ConnectionString` | secret `neon-conn` |
| `AMiracle__Echo__BlobStore__RootPath` | `/data/blobs` (Dockerfile default) |

### Auth and abuse controls

- **Admin token** — server-wide; protects `/api/v1/admin/*`. The `/echo/admin` page itself is public HTML; it is useless without the token. Never embed the token in customer pages.
- **Project public key** — safe in `<script>`; abuse control = **origin allowlist + per-IP rate limits** (`RateLimit:IngestionPerMinute`, default 30; `RateLimit:AdminPerMinute`, default 600 — enforced in `AMiracle.Echo.Host`). Never leave the allowlist empty in production.
- Origin allowlist stops other *websites* from using your key; it does not stop scripts that fake the `Origin` header. Rate limits + upload size caps bound that abuse.

---

## 3. OSS / security posture

### Expected to be public (MIT)

- Host/Server/widget source, API design, Dockerfile, the Docker image itself.
- Anyone can self-host. Compete on hosted uptime, support, brand, and (later) Cloud billing — not on hiding the code.

### Never commit / never bake into the image

| Secret | Impact if leaked |
|---|---|
| `AMiracle__Echo__AdminToken` | Full control of all projects/feedback |
| Neon connection string | Full DB access |
| Cloud `InternalApiKey`, Stripe, OpenAI keys | Provisioning, billing, quota abuse |

The image contains no secrets: they live only in ACA secrets. `.dockerignore` keeps local DBs, blobs, `appsettings.local.json`, and the private Cloud folders out of the build.

### Operational pitfalls

- Empty origin allowlist on a public Host → spam.
- Container-local disk without the Azure Files mount → audio/screenshots lost on every restart/new revision.
- More than 1 replica → blobs and in-memory rate limits are not shared between replicas. Keep **max replicas = 1** until a shared blob store exists.
- Echo Cloud control-plane source is kept **out of this public repo** (gitignored locally).

---

## 4. Out of scope for this deploy

- Per-tenant containers
- Echo Cloud signup / Stripe live webhooks
- Azure Blob / S3 adapters
- Auto-deploying new images to ACA from CI (new revision is one click in Portal)

---

## 5. Deployment runbook (Azure Portal)

Everything below is clicks in a browser, except generating the admin token (one PowerShell line).

### What you need before starting

- Azure for Students subscription ([portal.azure.com](https://portal.azure.com))
- Neon connection string in .NET format:  
  `Host=ep-xxx.<region>.aws.neon.tech;Database=neondb;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true`  
  (Neon dashboard → **Connect** → choose **.NET**.)
- Access to your domain's DNS settings (where you bought the domain, or Cloudflare)
- Note the Neon region (e.g. `aws-eu-central-1` = Frankfurt) — pick the closest Azure region (Frankfurt → **Germany West Central**, or **West Europe**)

---

### Step 1 — Make sure the image exists on GHCR

The workflow `.github/workflows/docker.yml` builds and pushes the image on every push to `main` (and on `v*.*.*` tags).

1. GitHub → repo → **Actions** → **Docker image** → latest run is green.  
   (No run yet? Click **Run workflow** → **Run workflow**.)
2. GitHub → your profile → **Packages** → `amiracle-echo` → **Package settings** → **Danger Zone** → **Change visibility** → **Public**.  
   Public is fine: the source is already MIT, and the image contains no secrets. It lets Azure pull without a password.

Image name to use in Azure: `ghcr.io/<github-owner-lowercase>/amiracle-echo:latest`

---

### Step 2 — Generate the admin token

PowerShell:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Save the output in a password manager. You'll paste it into Azure (Step 4) and into `/echo/admin` (Step 8).

---

### Step 3 — Create the Container App

Portal → search **Container Apps** → **+ Create** → **Container App**.

**Basics tab**

| Field | Value |
|---|---|
| Subscription | Azure for Students |
| Resource group | **Create new** → `rg-echo` |
| Container app name | `echo-host` |
| Deployment source | **Container image** |
| Region | closest to Neon |
| Container Apps environment | **Create new** → name `echo-env`, plan **Consumption only** (no zone redundancy) |

**Container tab**

| Field | Value |
|---|---|
| Use quickstart image | **unchecked** |
| Name | `echo-host` |
| Image source | **Docker Hub or other registries** |
| Image type | **Public** |
| Registry login server | `ghcr.io` |
| Image and tag | `<github-owner-lowercase>/amiracle-echo:latest` |
| CPU and Memory | **0.25 CPU cores, 0.5 Gi memory** |
| Environment variables | leave empty for now |

**Ingress tab**

| Field | Value |
|---|---|
| Ingress | **Enabled** |
| Ingress traffic | **Accepting traffic from anywhere** |
| Ingress type | **HTTP** |
| Target port | `8080` |
| Allow insecure connections | **unchecked** (HTTP is redirected to HTTPS) |

**Review + create** → **Create**. Wait for "Your deployment is complete" → **Go to resource**.

On **Overview**, open the **Application Url** and add `/echo/admin`. The admin page should load. (It's using temporary SQLite inside the container at this point — fine, Step 4 switches it to Neon.)

---

### Step 4 — Add secrets and point the app at Neon

1. Container App → **Settings → Secrets** → **+ Add**:
   - Key `admin-token`, Type **Container Apps Secret**, Value = token from Step 2 → **Add**
   - Key `neon-conn`, Type **Container Apps Secret**, Value = Neon .NET connection string → **Add**
2. Container App → **Application → Containers** → **Edit and deploy** → click the container `echo-host` → **Environment variables** tab → add:

| Name | Source | Value |
|---|---|---|
| `AMiracle__Echo__AdminToken` | **Reference a secret** | `admin-token` |
| `AMiracle__Echo__Database__Provider` | **Manual entry** | `postgres` |
| `AMiracle__Echo__Database__ConnectionString` | **Reference a secret** | `neon-conn` |

3. **Save** → **Create** (this creates a new revision).

Check: Container App → **Monitoring → Log stream** shows the app starting without errors. In Neon → **Tables**, you should now see `projects`, `feedbacks`, `feedback_comments`.

---

### Step 5 — Scale settings

Container App → **Application → Scale** → **Edit and deploy** (or the Scale tab inside Edit and deploy):

| Setting | Value | Why |
|---|---|---|
| Min replicas | `0` | Free when idle; first request after idle takes a few seconds. Set `1` if that delay bothers you (~$3–10/month of student credit). |
| Max replicas | `1` | Blobs and rate limits are per-replica; don't scale out yet. |

**Create** to apply.

---

### Step 6 — Persistent storage for audio/screenshots (recommended)

Skip only if you'll use text feedback only. Without this, audio/screenshot files vanish on every restart.

1. Portal → **Storage accounts** → **+ Create**: resource group `rg-echo`, name e.g. `echostorage<random>`, same region, **Standard**, **LRS** → **Review + create** → **Create**.
2. Storage account → **Data storage → File shares** → **+ File share** → name `echo-blobs` → **Create**.
3. Storage account → **Security + networking → Access keys** → copy **key1**.
4. Portal → **Container Apps Environments** → `echo-env` → **Settings → Azure Files** → **+ Add**:
   - Name `echo-blobs`, storage account name, account key = key1, file share `echo-blobs`, access mode **Read/Write** → **Save**.
5. Container App → **Application → Containers** → **Edit and deploy**:
   - **Volumes** tab → **+ Add** → type **Azure file volume**, name `blobs`, file share `echo-blobs` → **Add**.
   - Click the container → **Volume mounts** tab → **+ Add** → volume `blobs`, mount path `/data/blobs` → **Save**.
   - **Create**.

---

### Step 7 — Custom domain + free HTTPS certificate

1. Container App → **Settings → Custom domains** → **+ Add custom domain** → **Managed certificate**.
2. Domain: `echo.<your-domain>`. Azure shows two DNS records to create:
   - **CNAME**: host `echo` → value `<app>.<random>.<region>.azurecontainerapps.io`
   - **TXT**: host `asuid.echo` → value (a long verification string)
3. At your DNS provider, add both records exactly as shown. If you use Cloudflare, set the CNAME to **DNS only** (grey cloud), not proxied.
4. Back in Azure → **Validate** → **Add**. The certificate is issued automatically (5–20 minutes).
5. When status is **Secured**, open `https://echo.<your-domain>/echo/admin`.

---

### Step 8 — Create a project and embed the widget

1. Open `https://echo.<your-domain>/echo/admin` → paste the admin token → **Save**.
2. **+ New** → name your app → **allowed origins** = exact origins of the sites that will embed the widget, e.g. `https://myapp.com` (add `http://localhost:5173` only while developing). Never leave it empty.
3. **Show widget snippet** → paste into your app's HTML:

```html
<script src="https://echo.<your-domain>/echo/widget.js"
        data-project-id="..."
        data-public-key="ekp_..."
        defer></script>
```

4. Load your app, send a feedback, confirm it appears in `/echo/admin`.

Repeat Step 8 for each app (one project per app).

---

### Step 9 — Budget alert (protects your student credit)

Portal → **Cost Management + Billing** → **Budgets** → **+ Add**: scope = your subscription, amount e.g. `$10/month`, alert at 80% to your email.

---

### Updating to a new version later

1. Push to `main` on GitHub → wait for **Docker image** workflow to go green.
2. Container App → **Application → Revisions and replicas** → **Create new revision** → **Create** (re-pulls `:latest`).

Rotating the admin token: **Secrets** → edit `admin-token` → then **Revisions and replicas** → restart the active revision.

---

## 6. Post-deploy checklist

- [ ] `https://echo.<your-domain>/echo/admin` loads over HTTPS
- [ ] Admin token works; a wrong token shows "Token rejected (401)"
- [ ] Every project has non-empty allowed origins
- [ ] Widget submits from an allowed origin; blocked from a random origin
- [ ] Neon shows `projects` / `feedbacks` tables
- [ ] Azure Files mounted at `/data/blobs` (if using audio/screenshots); a voice note still plays after **Restart revision**
- [ ] Max replicas = 1
- [ ] Budget alert set
- [ ] Admin token + Neon password stored only in password manager / ACA secrets

---

## 7. Path to Echo Cloud (later)

1. Keep this shared Host as the widget endpoint.
2. Run Cloud control plane separately when signup/billing is ready.
3. Cloud may create Neon projects / tokens; for a long time you can still map paying customers to **projects** on this Host.
4. Only introduce per-tenant containers if isolation or noisy-neighbor becomes a real problem.

---

## 8. Known limitations (accept for v1)

- Single replica only (blobs on a file share + in-memory rate limits).
- Scale-to-zero means the retention sweeper only runs while a replica is awake.
- Rate limiting lives in `AMiracle.Echo.Host`, not in the `AMiracle.Echo.Server` NuGet package — apps embedding the package must add their own limiter.

---

## 9. Success criteria

You can embed the widget on your own site(s) using only:

`https://echo.<your-domain>/echo/widget.js` + project id + public key  

…and feedback lands in Neon and shows in `/echo/admin`.
