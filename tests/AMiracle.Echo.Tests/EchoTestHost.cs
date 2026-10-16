using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AMiracle.Echo.Abstractions.Configuration;
using AMiracle.Echo.Abstractions.Email;
using AMiracle.Echo.Abstractions.Models;
using AMiracle.Echo.Server;
using AMiracle.Echo.Storage.EFCore;
using AMiracle.Echo.Storage.LocalFS;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AMiracle.Echo.Tests;

/// <summary>
/// Echo mounted the way a host app mounts it — AddAmiracleEcho + MapAmiracleEcho —
/// on an in-memory test server, with SQLite in memory and a temp blob folder.
/// </summary>
public sealed class EchoTestHost : IAsyncDisposable
{
    public const string AdminToken = "test-admin-token";
    public const string Origin = "https://app.example";

    private readonly WebApplication _app;
    private readonly SqliteConnection _db;
    private readonly string _blobRoot;
    public HttpClient Client { get; }
    public FakeEmailSender? Email { get; }

    private EchoTestHost(WebApplication app, SqliteConnection db, string blobRoot, FakeEmailSender? email)
    {
        _app = app; _db = db; _blobRoot = blobRoot; Email = email;
        Client = app.GetTestClient();
    }

    public static async Task<EchoTestHost> StartAsync(
        string identitySigningKey = "", FakeEmailSender? email = null, Action<EchoOptions>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        // The connection must stay open for the in-memory database to live.
        var db = new SqliteConnection("Data Source=:memory:");
        await db.OpenAsync();
        var blobRoot = Path.Combine(Path.GetTempPath(), "echo-tests-" + Guid.NewGuid().ToString("N"));

        if (email is not null) builder.Services.AddSingleton<IEchoEmailSender>(email);
        builder.Services.AddAmiracleEcho(o =>
        {
            o.AdminToken = AdminToken;
            o.IdentitySigningKey = identitySigningKey;
            configure?.Invoke(o);
        });
        builder.Services.AddEchoEfCoreStorage(o => o.UseSqlite(db));
        builder.Services.AddEchoLocalFileBlobStore(o => o.RootPath = blobRoot);
        // The background sweepers aren't under test and would race the assertions.
        builder.Services.RemoveAll<IHostedService>();

        var app = builder.Build();
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<EchoDbContext>().Database.EnsureCreatedAsync();
        app.MapAmiracleEcho();
        await app.StartAsync();
        return new EchoTestHost(app, db, blobRoot, email);
    }

    public T Service<T>() where T : notnull => _app.Services.GetRequiredService<T>();

    // ---- Admin API ----

    public HttpRequestMessage Admin(HttpMethod method, string path, object? body = null)
    {
        var req = new HttpRequestMessage(method, "/api/v1/admin" + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AdminToken);
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    public async Task<T> AdminAsync<T>(HttpMethod method, string path, object? body = null)
    {
        using var res = await Client.SendAsync(Admin(method, path, body));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    public Task<Project> CreateProjectAsync(string name = "Test app")
        => AdminAsync<Project>(HttpMethod.Post, "/projects", new { name, allowedOrigins = new[] { Origin } });

    public Task<Feedback> GetFeedbackAsync(Guid id) => AdminAsync<Feedback>(HttpMethod.Get, "/feedbacks/" + id);

    // ---- Ingestion, as the widget sends it ----

    public async Task<HttpResponseMessage> SubmitAsync(Project project, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/feedbacks/") { Content = JsonContent.Create(body) };
        req.Headers.Add("X-Echo-Project-Key", project.PublicKey);
        req.Headers.Add("Origin", Origin);
        return await Client.SendAsync(req);
    }

    public async Task<Guid> SubmitOkAsync(Project project, object body)
    {
        using var res = await SubmitAsync(project, body);
        Assert.Equal(System.Net.HttpStatusCode.Created, res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        await _db.DisposeAsync();
        try { if (Directory.Exists(_blobRoot)) Directory.Delete(_blobRoot, recursive: true); } catch (IOException) { }
    }
}

public sealed class FakeEmailSender : IEchoEmailSender
{
    public bool IsConfigured { get; set; } = true;
    public Exception? FailWith { get; set; }
    public List<EchoEmailMessage> Sent { get; } = new();

    public Task SendAsync(EchoEmailMessage message, CancellationToken ct)
    {
        if (FailWith is not null) throw FailWith;
        Sent.Add(message);
        return Task.CompletedTask;
    }
}
