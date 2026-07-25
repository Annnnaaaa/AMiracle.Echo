# Samples

Two minimal, copy-paste-able examples.

## `html-page/`

A static HTML page that embeds the Echo widget via one `<script>` tag. Use it to verify:
- the widget JS loads from `/echo/widget.js`,
- the bubble appears,
- `AMiracleEcho.identify()` / `setMetadata()` / `open()` work from page code,
- the `submitted` event fires after a successful send.

Replace the placeholder `data-project-id` and `data-public-key` with values from your Echo admin page (`/echo/admin` → your project → "Show widget snippet").

Serve it on a port that you've added to the project's Allowed Origins:
```bash
cd samples/html-page
python -m http.server 8000
# or: npx serve -l 8000 .
```

## `postgres-host/`

A minimal `Program.cs` that wires AMiracle.Echo to Postgres. Targets `net8.0` and works against:
- Neon (`Host=ep-*.neon.tech;...`)
- Self-hosted Postgres
- RDS / Aiven / Supabase Postgres / Crunchy Data — any vanilla Postgres ≥ 13

In your own project, replace the `<ProjectReference>` entries with NuGet `<PackageReference>` entries:

```xml
<PackageReference Include="AMiracle.Echo.Server" Version="0.1.*" />
<PackageReference Include="AMiracle.Echo.Storage.EFCore" Version="0.1.*" />
<PackageReference Include="AMiracle.Echo.Storage.LocalFS" Version="0.1.*" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />
```

Run it:
```powershell
$env:AMiracle__Echo__AdminToken = "your-32-byte-token"
$env:AMiracle__Echo__Database__ConnectionString = "Host=...;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"
dotnet run --project samples/postgres-host --no-launch-profile
```

Open <http://localhost:5000/echo/admin>.
