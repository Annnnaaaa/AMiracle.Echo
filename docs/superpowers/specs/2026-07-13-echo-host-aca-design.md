# Echo Host on Azure Container Apps — Design

**Date:** 2026-07-13  
**Status:** Approved for deploy (shared Host v1)  
**Goal:** Publish one shared `AMiracle.Echo.Host` so widgets load from a public HTTPS URL; keep a clear path to Echo Cloud later.

---

## 1. Product decisions

| Decision | Choice |
|---|---|
| Audience now | Own projects first; early customers as **projects** on the same Host |
| Later SaaS | Echo Cloud for signup/billing/provisioning; still this shared Host until isolation is needed |
| Compute | Azure Container Apps, **Consumption** plan (Students-friendly free grant) |
| Image registry | GitHub Container Registry (`ghcr.io`), not Azure Container Registry |
| Database | Existing Neon Postgres (one DB, many Echo projects) |
| Public URL | Custom domain + ACA managed TLS from day one |
| Blob storage | LocalFS under `/data` for early text-first use; durable blobs before relying on audio/screenshots |

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
GitHub repo / local machine
        │ docker build + push
        ▼
 ghcr.io/<owner>/amiracle-echo:<tag>
        │ ACA pulls (registry secret)
        ▼
 Azure Container Apps (Consumption)
   AMiracle.Echo.Host :8080
   https://echo.<your-domain>
        │
        ├─ Neon Postgres (metadata)
        └─ /data/blobs (ephemeral unless Azure Files mounted)
```

### Runtime config (ACA secrets / env)

| Env var | Value |
|---|---|
| `ASPNETCORE_URLS` | `http://+:8080` (Dockerfile default) |
| `AMiracle__Echo__AdminToken` | Long random secret (never commit) |
| `AMiracle__Echo__Database__Provider` | `postgres` |
| `AMiracle__Echo__Database__ConnectionString` | Neon .NET connection string |
| `AMiracle__Echo__BlobStore__RootPath` | `/data/blobs` |

### Auth reminder

- **Admin token** — server-wide; protects `/api/v1/admin/*` and `/echo/admin`. Never embed in customer pages.
- **Project public key** — safe in `<script>`; abuse control = **origin allowlist + rate limits**. Never leave allowlist empty in production.

---

## 3. OSS / security posture

### Expected to be public (MIT)

- Host/Server/widget source, API design, Dockerfile.
- Anyone can self-host. Compete on hosted uptime, support, brand, and (later) Cloud billing — not on hiding the code.

### Never commit / never bake into the image

| Secret | Impact if leaked |
|---|---|
| `AMiracle__Echo__AdminToken` | Full control of all projects/feedback |
| Neon connection string | Full DB access |
| Cloud `InternalApiKey`, Stripe, OpenAI keys | Provisioning, billing, quota abuse |
| GHCR deploy PAT / ACA registry password | Image pull/push abuse |

### Operational pitfalls

- Empty origin allowlist on a public Host → spam.
- LocalFS on ACA without a volume → audio/screenshots lost on restart/scale-out.
- Large uploads can burn Neon + ACA free grant.
- Echo Cloud control-plane source is kept **out of this public repo** (gitignored locally); compete on hosted ops, not by publishing that code.

---

## 4. Out of scope for this deploy

- Per-tenant containers
- Echo Cloud signup / Stripe live webhooks
- Azure Blob / S3 adapters
- Automating ACA deploy from CI (optional follow-up; manual/GHCR push is enough for v1)

---

## 5. Deployment runbook

Use this to get a working Host for your own projects. PowerShell-oriented (Windows). Adjust `<owner>`, domain, and names as needed.

### Prerequisites

- [ ] Azure for Students (or any) subscription with permission to create resources
- [ ] [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed (`az`)
- [ ] Docker Desktop installed and running
- [ ] Neon project + connection string (Direct / .NET style)
- [ ] DNS control for your domain (to create a CNAME)
- [ ] GitHub account (for GHCR)

Suggested names (change freely):

```text
Resource group:     rg-echo
ACA environment:    echo-env
Container app:      echo-host
Image:              ghcr.io/<github-username>/amiracle-echo:v0.1.0
Hostname:           echo.<your-domain>
```

---

### Step 1 — Generate an admin token

```powershell
$adminToken = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
Write-Host $adminToken
# Save this somewhere safe (password manager). You will paste it into ACA and into /echo/admin.
```

---

### Step 2 — Build and push the image to GHCR

From the repo root:

```powershell
# Login to GHCR (use a GitHub PAT with write:packages, read:packages, and repo if private)
$env:GITHUB_TOKEN = "ghp_..."   # or paste when docker asks
echo $env:GITHUB_TOKEN | docker login ghcr.io -u <github-username> --password-stdin

docker build -t ghcr.io/<github-username>/amiracle-echo:v0.1.0 -f docker/Dockerfile .
docker push ghcr.io/<github-username>/amiracle-echo:v0.1.0
```

If the package is **private**, ACA needs a PAT with `read:packages` (Step 4).  
If you make the package **public** (GHCR package settings → Change visibility), ACA can pull without a registry secret — simpler for a public OSS image, but anyone can pull the same bits (source is already public).

---

### Step 3 — Azure login and resource group

```powershell
az login
az account show   # confirm the Students subscription is selected
# az account set --subscription "<subscription-id>"   # if needed

az group create --name rg-echo --location westeurope
```

Pick a region close to your Neon region when possible.

---

### Step 4 — Container Apps environment + app

```powershell
# Environment (Consumption-capable)
az containerapp env create `
  --name echo-env `
  --resource-group rg-echo `
  --location westeurope

# Registry credentials (skip --registry-* if the GHCR package is public)
az containerapp create `
  --name echo-host `
  --resource-group rg-echo `
  --environment echo-env `
  --image ghcr.io/<github-username>/amiracle-echo:v0.1.0 `
  --target-port 8080 `
  --ingress external `
  --cpu 0.25 `
  --memory 0.5Gi `
  --min-replicas 0 `
  --max-replicas 2 `
  --registry-server ghcr.io `
  --registry-username <github-username> `
  --registry-password "<PAT-with-read:packages>" `
  --secrets `
    admin-token="<paste-admin-token>" `
    neon-conn="Host=ep-....neon.tech;Database=neondb;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true" `
  --env-vars `
    AMiracle__Echo__AdminToken=secretref:admin-token `
    AMiracle__Echo__Database__Provider=postgres `
    AMiracle__Echo__Database__ConnectionString=secretref:neon-conn `
    AMiracle__Echo__BlobStore__RootPath=/data/blobs
```

Notes:

- `--min-replicas 0` saves quota (cold start on first request). Use `--min-replicas 1` if you want the widget always warm.
- Secrets referenced as `secretref:...` are not visible in plain env listings the same way plaintext would be.

Get the default FQDN:

```powershell
az containerapp show -n echo-host -g rg-echo --query properties.configuration.ingress.fqdn -o tsv
```

Smoke-test: open `https://<fqdn>/echo/admin` — you should see the admin UI.

---

### Step 5 — Custom domain + managed certificate

1. In Azure Portal: **Container App** → **Custom domains** → **Add custom domain**.
2. Follow the DNS instructions Azure shows (usually a **CNAME** from `echo` → `<fqdn>.azurecontainerapps.io`, plus a validation record if requested).
3. Enable **managed certificate** for HTTPS.

Or with CLI (domain binding details vary slightly by API version; Portal is fine for DNS validation):

```powershell
# After DNS CNAME is in place and validated in Portal, traffic goes to:
# https://echo.<your-domain>/echo/admin
# https://echo.<your-domain>/echo/widget.js
```

Wait for DNS propagation (often minutes; sometimes up to an hour).

---

### Step 6 — Create your first project (own apps)

1. Open `https://echo.<your-domain>/echo/admin`.
2. Paste the **admin token** → Save.
3. **+ New** project.
4. Set **allowed origins** to the exact origins of your app(s), e.g. `https://myapp.com`, `http://localhost:5173`. Do **not** leave empty in production.
5. Copy **Show widget snippet** and point `src` at your custom domain if the snippet still shows an internal FQDN:

```html
<script src="https://echo.<your-domain>/echo/widget.js"
        data-project-id="..."
        data-public-key="ekp_..."
        defer></script>
```

6. Load a page with the snippet, submit feedback, confirm it appears in admin.

---

### Step 7 — Updating the app later

```powershell
docker build -t ghcr.io/<github-username>/amiracle-echo:v0.1.1 -f docker/Dockerfile .
docker push ghcr.io/<github-username>/amiracle-echo:v0.1.1

az containerapp update `
  --name echo-host `
  --resource-group rg-echo `
  --image ghcr.io/<github-username>/amiracle-echo:v0.1.1
```

Rotate admin token by updating the ACA secret and restarting/creating a new revision.

---

## 6. Post-deploy checklist

- [ ] `https://echo.<your-domain>/echo/admin` loads over HTTPS
- [ ] Admin token works; random guess fails (401 on admin API)
- [ ] Project has non-empty allowed origins
- [ ] Widget submits from an allowed origin; blocked from a random origin
- [ ] Neon shows `projects` / `feedbacks` tables after first run
- [ ] Budget alert on the Azure subscription (Students $100 credit)
- [ ] Admin token + Neon password stored only in password manager / ACA secrets

---

## 7. Path to Echo Cloud (later)

1. Keep this shared Host as the widget endpoint.
2. Run Cloud control plane separately when signup/billing is ready.
3. Cloud may create Neon projects / tokens; for a long time you can still map paying customers to **projects** on this Host.
4. Only introduce per-tenant containers if isolation or noisy-neighbor becomes a real problem.

---

## 8. Known limitations (accept for v1)

- Audio/screenshot blobs on container-local disk are **not durable** across revisions without Azure Files (or a future Azure Blob adapter).
- No CI publish-to-GHCR yet (release workflow today only publishes NuGet). Manual docker push is intentional for first deploy.
- Scale-out above 1 replica without shared blob storage will break blob consistency.

---

## 9. Success criteria

You can embed the widget on your own site(s) using only:

`https://echo.<your-domain>/echo/widget.js` + project id + public key  

…and feedback lands in Neon and shows in `/echo/admin`.
