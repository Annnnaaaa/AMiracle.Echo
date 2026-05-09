using AMiracle.Echo.Server;
using AMiracle.Echo.Storage.EFCore;
using AMiracle.Echo.Storage.LocalFS;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Echo configuration: bind from "AMiracle:Echo" section, with env vars overriding (AMiracle__Echo__AdminToken=...).
builder.Services.AddAmiracleEcho(builder.Configuration.GetSection("AMiracle:Echo"));

// Pick a metadata store provider via config (default sqlite for zero-friction dev).
var dbProvider = (builder.Configuration["AMiracle:Echo:Database:Provider"] ?? "sqlite").ToLowerInvariant();
var connectionString = builder.Configuration["AMiracle:Echo:Database:ConnectionString"]
    ?? builder.Configuration.GetConnectionString("Echo")
    ?? "Data Source=echo.db";

builder.Services.AddEchoEfCoreStorage(opts =>
{
    switch (dbProvider)
    {
        case "postgres":
        case "postgresql":
        case "npgsql":
            opts.UseNpgsql(connectionString);
            break;
        case "sqlite":
        default:
            opts.UseSqlite(connectionString);
            break;
    }
});

// Local filesystem blob store.
var blobRoot = builder.Configuration["AMiracle:Echo:BlobStore:RootPath"] ?? "./echo-blobs";
builder.Services.AddEchoLocalFileBlobStore(opts => opts.RootPath = blobRoot);

var app = builder.Build();

// Apply migrations / ensure created on startup (good enough for v1; we don't ship migrations yet).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EchoDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// CORS preflight needs to succeed before our origin-validating ingestion endpoints reflect on the actual request.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Method == "OPTIONS" && ctx.Request.Path.StartsWithSegments("/api/v1/feedbacks"))
    {
        var origin = ctx.Request.Headers["Origin"].ToString();
        if (!string.IsNullOrEmpty(origin))
        {
            ctx.Response.Headers["Access-Control-Allow-Origin"] = origin;
            ctx.Response.Headers["Access-Control-Allow-Methods"] = "POST, OPTIONS";
            ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-Echo-Project-Key";
            ctx.Response.Headers["Access-Control-Max-Age"] = "600";
            ctx.Response.Headers["Vary"] = "Origin";
            ctx.Response.StatusCode = 204;
            return;
        }
    }
    await next();
});

app.MapAmiracleEcho();
app.MapGet("/", () => Results.Redirect("/echo/admin"));

app.Run();
