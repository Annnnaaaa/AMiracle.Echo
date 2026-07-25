# Contributing to AMiracle.Echo

Thanks for considering a contribution! AMiracle.Echo is MIT-licensed OSS.

## Quick start (developer setup)

```bash
git clone https://github.com/Annnnaaaa/AMiracle.Echo
cd AMiracle.Echo
dotnet build
```

Run the example host:

```bash
# PowerShell
$env:AMiracle__Echo__AdminToken = "dev-token-please-change"
dotnet run --project src/AMiracle.Echo.Host --no-launch-profile
```

Open <http://localhost:5000/echo/admin>, paste the token, click Save.

## Filing issues

Use the bug / feature request templates. Helpful issues include:
- AMiracle.Echo version (NuGet version or Git commit).
- .NET SDK version (`dotnet --version`).
- The exact admin / widget config you're using (redact tokens).
- Minimal reproduction steps. A failing curl example is gold.

## Pull requests

- Open against `main`.
- One logical change per PR; split unrelated changes.
- Run `dotnet build` before pushing. CI will run on Linux + Windows.
- Add a `CHANGELOG.md` entry under `[Unreleased]` describing the change.
- If you touch the widget or admin page, briefly describe what you tested in a browser.

## Architecture pointers

- `src/AMiracle.Echo.Abstractions/` — interfaces and DTOs. Adding a new storage backend? Start here.
- `src/AMiracle.Echo.Server/Endpoints/` — routes. Adding a new admin endpoint goes here.
- `src/AMiracle.Echo.Server/Resources/widget.js` — the embeddable widget. Vanilla JS, Web Components, Shadow DOM. No build step.
- `src/AMiracle.Echo.Server/Resources/admin.html` — the admin page. Vanilla JS too.
- `src/AMiracle.Echo.Storage.EFCore/` — EF Core implementation. Schema lives in `EchoDbContext`.
- `src/AMiracle.Echo.Analysis.Abstractions/` — analyzer interface. Adding a Claude / Azure Speech / local-Whisper provider goes in a sibling project.

## Code style

- C# 12+ patterns OK. `LangVersion=latest` is set repo-wide.
- Nullable reference types are enabled everywhere; please don't disable.
- Public API surface should be commented; internals can speak for themselves.

## Releases (maintainers only)

Tag the commit you want to release as `vMAJOR.MINOR.PATCH`. The `release.yml` workflow:
1. Builds + packs every `IsPackable=true` project.
2. Pushes packages to NuGet.org using the `NUGET_API_KEY` secret.
3. Creates a GitHub Release with the artifacts attached.

`Directory.Build.props` holds the default `<Version>`; the tag overrides it at pack time via `-p:Version=...`.
