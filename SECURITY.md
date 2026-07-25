# Security Policy

## Reporting a vulnerability

If you discover a security vulnerability in AMiracle.Echo, **please do not open a public GitHub issue.**

Instead, email a description of the issue and reproduction steps to:

**help@amiracle.net**

Include the version (Git commit or NuGet version) and your contact info if you'd like a response. We aim to acknowledge reports within 3 business days and to ship a fix or coordinated disclosure within 30 days.

## Scope

In-scope:
- Anything in this repository's `src/` tree.
- Behavior of the published NuGet packages under `AMiracle.Echo.*`.
- The widget JavaScript shipped at `/echo/widget.js`.
- The admin web page shipped at `/echo/admin`.

Out of scope:
- Vulnerabilities in dependencies (please report those to the upstream project).
- Configuration mistakes in your own deployment (e.g. running with no admin token, leaving allowed-origins empty in production). The README documents recommended hardening.
- Denial-of-service via flooding without authentication — the project ships per-IP and per-project rate limiting; tuning is a deployment concern.

## Supported versions

During the pre-1.0 series, only the latest published `0.x.y` release is supported with security fixes. After 1.0, this section will be replaced with a support matrix.

## Hardening checklist for operators

- Set a strong `AMiracle__Echo__AdminToken` (32+ random bytes). Never commit it to source control.
- Restrict each project's `allowedOrigins` to the exact domains that should be able to submit feedback.
- Set a `retentionDays` policy unless your legal/compliance posture requires keeping feedbacks forever.
- Run the backend behind TLS (the widget refuses voice recording on non-HTTPS origins anyway, per browser policy).
- Store screenshots/audio on an encrypted disk or use an encrypted blob backend.
- Review the `IFeedbackProcessor` pipeline — add PII redaction if your project captures sensitive content.
