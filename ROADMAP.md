# Roadmap

## v1.1 (after Phase 1 stabilizes)

- **`AMiracle.Echo.Storage.S3`** — S3-compatible `IBlobStore` (covers AWS S3, MinIO, Cloudflare R2, Supabase Storage, Backblaze B2, DigitalOcean Spaces).
- **`AMiracle.Echo.Storage.AzureBlob`** — Azure Blob Storage `IBlobStore`.
- **`AMiracle.Echo.Destinations.Webhook`** — outbound webhook destination on feedback create (Slack/Discord/Linear/custom URL).
- Optional JWT-signed widget auth (host's backend mints a short-lived token; widget includes it in ingestion calls).
- "Retention not set" warning UX in admin page project-create dialog.

## v2 — Phase 2 (analysis)

- Voice transcription pipeline; pluggable `ITranscriptionService` (Whisper, Azure Speech, Deepgram).
- LLM summarization + auto-categorization of feedbacks.
- Priority scoring (semantic + rule-based hybrid; writes `feedbacks.priority`).
- LLM-based PII redaction `IFeedbackProcessor` (replacing the v1 stub).

## v3 — Phase 3 (full dashboard)

- Polished SPA admin UI (Blazor or React — TBD).
- Multi-user auth + RBAC (admin / triage / viewer).
- Triage workflow (assign, comment, link to GitHub/Linear issues).
- Search, charts, exports.

## Echo Cloud (managed offering — private)

Paid hosted option we run for customers. **Not part of this OSS repo** (control-plane code stays private).

**Direction:**
- Signup / billing → provision managed database + admin access for paying plans.
- Customers embed the widget against our shared Host URL (or later per-tenant Hosts).
- Self-host remains free via this MIT repo + NuGet / Docker.

**Still needed before launch:** Stripe checkout + verified webhooks, customer auth, marketing site, ToS/privacy, durable secrets, support.

## Future / community

- Mongo `IFeedbackStore` adapter.
- Native mobile SDKs (iOS / Android) if demand emerges. (Flutter already supported via WebView or REST.)
- Translation packs (es, fr, de, ru, ...).
