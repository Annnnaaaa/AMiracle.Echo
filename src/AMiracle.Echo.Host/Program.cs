using System.Threading.RateLimiting;
using AMiracle.Echo.Analysis.OpenAI;
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
            // Serverless Postgres (Neon) suspends when idle and drops pooled connections;
            // retry so the first query after a wake-up doesn't surface as a 500.
            opts.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure());
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

// Phase 2 — optional OpenAI analyzer. Only registered if Analysis.Enabled=true AND ApiKey set.
var analysisEnabled = builder.Configuration.GetValue<bool>("AMiracle:Echo:Analysis:Enabled");
var openAIKey = builder.Configuration["AMiracle:Echo:Analysis:OpenAI:ApiKey"];
if (analysisEnabled && !string.IsNullOrWhiteSpace(openAIKey))
{
    builder.Services.AddEchoOpenAIAnalyzer(builder.Configuration.GetSection("AMiracle:Echo:Analysis:OpenAI"));
}

// Per-client-IP limits. Behind a reverse proxy (Azure Container Apps, App Service, k8s ingress) the real
// client IP only reaches us if ASPNETCORE_FORWARDEDHEADERS_ENABLED=true (set in docker/Dockerfile).
var ingestionPerMinute = builder.Configuration.GetValue("AMiracle:Echo:RateLimit:IngestionPerMinute", 30);
var adminPerMinute = builder.Configuration.GetValue("AMiracle:Echo:RateLimit:AdminPerMinute", 600);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (ctx.Request.Path.StartsWithSegments("/api/v1/feedbacks"))
            return RateLimitPartition.GetFixedWindowLimiter("ingest:" + ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = ingestionPerMinute,
                Window = TimeSpan.FromMinutes(1),
            });
        if (ctx.Request.Path.StartsWithSegments("/api/v1/admin"))
            return RateLimitPartition.GetFixedWindowLimiter("admin:" + ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = adminPerMinute,
                Window = TimeSpan.FromMinutes(1),
            });
        return RateLimitPartition.GetNoLimiter("unlimited");
    });
});

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

app.UseRateLimiter();

app.MapAmiracleEcho();
app.MapGet("/", () => Results.Redirect("/echo/admin"));

app.Run();
