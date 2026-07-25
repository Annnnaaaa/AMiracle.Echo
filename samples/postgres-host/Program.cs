// Minimal AMiracle.Echo host wired to Postgres (Neon, RDS, self-hosted — any Postgres).
//
// Set env vars before `dotnet run`:
//   AMiracle__Echo__AdminToken              = "your-32-byte-random-token"
//   AMiracle__Echo__Database__ConnectionString = "Host=...;Database=...;Username=...;Password=...;SSL Mode=Require"
//   (optional) AMiracle__Echo__BlobStore__RootPath = "/var/echo/blobs"
//
// Then open http://localhost:5000/echo/admin.

using AMiracle.Echo.Server;
using AMiracle.Echo.Storage.EFCore;
using AMiracle.Echo.Storage.LocalFS;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAmiracleEcho(builder.Configuration.GetSection("AMiracle:Echo"));

var connectionString = builder.Configuration["AMiracle:Echo:Database:ConnectionString"]
    ?? throw new InvalidOperationException(
        "Set AMiracle__Echo__Database__ConnectionString to your Postgres connection string.");

builder.Services.AddEchoEfCoreStorage(opts => opts.UseNpgsql(connectionString));

var blobRoot = builder.Configuration["AMiracle:Echo:BlobStore:RootPath"] ?? "./echo-blobs";
builder.Services.AddEchoLocalFileBlobStore(opts => opts.RootPath = blobRoot);

var app = builder.Build();

// Create tables on first run. For real deployments, generate EF Core migrations instead.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EchoDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapAmiracleEcho();
app.MapGet("/", () => Results.Redirect("/echo/admin"));

app.Run();
