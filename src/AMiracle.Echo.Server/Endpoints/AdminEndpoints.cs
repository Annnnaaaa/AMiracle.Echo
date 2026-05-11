using AMiracle.Echo.Abstractions.Configuration;
using AMiracle.Echo.Abstractions.Models;
using AMiracle.Echo.Abstractions.Stores;
using AMiracle.Echo.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace AMiracle.Echo.Server.Endpoints;

internal static class AdminEndpoints
{
    public static void MapAdmin(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/admin").AddEndpointFilter<AdminTokenFilter>();

        group.MapGet("/projects", ListProjects);
        group.MapPost("/projects", CreateProject);
        group.MapGet("/projects/{id:guid}", GetProject);
        group.MapPatch("/projects/{id:guid}", UpdateProject);
        group.MapPost("/projects/{id:guid}/rotate-key", RotateKey);
        group.MapDelete("/projects/{id:guid}", DeleteProject);

        group.MapGet("/feedbacks", ListFeedbacks);
        group.MapGet("/feedbacks/{id:guid}", GetFeedback);
        group.MapPatch("/feedbacks/{id:guid}", UpdateFeedback);
        group.MapDelete("/feedbacks/{id:guid}", DeleteFeedback);
        group.MapGet("/feedbacks/{id:guid}/audio", GetAudio);
        group.MapGet("/feedbacks/{id:guid}/screenshot", GetScreenshot);

        group.MapDelete("/submitters/{submitterId}", DeleteBySubmitter);

        // Phase 3 — comments, stats, export.
        group.MapGet("/feedbacks/{id:guid}/comments", ListComments);
        group.MapPost("/feedbacks/{id:guid}/comments", AddComment);
        group.MapDelete("/feedbacks/{id:guid}/comments/{commentId:guid}", DeleteComment);

        group.MapGet("/stats", GetStats);
        group.MapGet("/feedbacks/export", ExportFeedbacks);

        // Force re-analysis of a single feedback.
        group.MapPost("/feedbacks/{id:guid}/reanalyze", ReanalyzeFeedback);
    }

    private static async Task<IResult> ReanalyzeFeedback(Guid id, IFeedbackStore store, CancellationToken ct)
    {
        var fb = await store.GetFeedbackAsync(id, ct);
        if (fb is null) return Results.NotFound();
        fb.AnalysisVersion = 0;
        fb.AnalysisError = null;
        fb.AnalyzedAt = null;
        await store.UpdateFeedbackAsync(fb, ct);
        return Results.Ok(new { id, queued = true });
    }

    // ---- Projects ----

    private static async Task<IResult> ListProjects(IFeedbackStore store, CancellationToken ct)
        => Results.Ok(await store.ListProjectsAsync(ct));

    private static async Task<IResult> CreateProject(CreateProjectRequest req, IFeedbackStore store, TimeProvider clock, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.Problem(title: "Name required.", statusCode: 400);
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = req.Name.Trim(),
            PublicKey = PublicKeyGenerator.Generate(),
            AllowedOrigins = req.AllowedOrigins ?? new(),
            RetentionDays = req.RetentionDays,
            CreatedAt = clock.GetUtcNow(),
        };
        var saved = await store.CreateProjectAsync(project, ct);
        return Results.Created($"/api/v1/admin/projects/{saved.Id}", saved);
    }

    private static async Task<IResult> GetProject(Guid id, IFeedbackStore store, CancellationToken ct)
    {
        var p = await store.GetProjectAsync(id, ct);
        return p is null ? Results.NotFound() : Results.Ok(p);
    }

    private static async Task<IResult> UpdateProject(Guid id, UpdateProjectRequest req, IFeedbackStore store, TimeProvider clock, CancellationToken ct)
    {
        var p = await store.GetProjectAsync(id, ct);
        if (p is null) return Results.NotFound();
        if (req.Name is not null) p.Name = req.Name.Trim();
        if (req.AllowedOrigins is not null) p.AllowedOrigins = req.AllowedOrigins;
        if (req.RetentionDays is { } rd) p.RetentionDays = rd;
        if (req.Archived == true && p.ArchivedAt is null) p.ArchivedAt = clock.GetUtcNow();
        if (req.Archived == false) p.ArchivedAt = null;
        var saved = await store.UpdateProjectAsync(p, ct);
        return Results.Ok(saved);
    }

    private static async Task<IResult> RotateKey(Guid id, IFeedbackStore store, CancellationToken ct)
    {
        var p = await store.GetProjectAsync(id, ct);
        if (p is null) return Results.NotFound();
        p.PublicKey = PublicKeyGenerator.Generate();
        var saved = await store.UpdateProjectAsync(p, ct);
        return Results.Ok(saved);
    }

    private static async Task<IResult> DeleteProject(Guid id, IFeedbackStore store, IBlobStore blobs, CancellationToken ct)
    {
        var p = await store.GetProjectAsync(id, ct);
        if (p is null) return Results.NotFound();
        await blobs.DeleteByPrefixAsync(p.Id.ToString(), ct);
        await store.DeleteProjectAsync(id, ct);
        return Results.NoContent();
    }

    // ---- Feedbacks ----

    private static async Task<IResult> ListFeedbacks(
        IFeedbackStore store,
        Guid? projectId, string? status, string? type,
        DateTimeOffset? since, DateTimeOffset? until,
        string? cursor, int? limit,
        string? search, string? assignee, string? category, short? priority,
        CancellationToken ct)
    {
        var page = await store.ListFeedbacksAsync(
            new FeedbackQuery(projectId, status, type, since, cursor, limit ?? 50,
                SearchText: search, Assignee: assignee, Category: category,
                Priority: priority, Until: until), ct);
        return Results.Ok(page);
    }

    private static async Task<IResult> GetFeedback(Guid id, IFeedbackStore store, CancellationToken ct)
    {
        var fb = await store.GetFeedbackAsync(id, ct);
        return fb is null ? Results.NotFound() : Results.Ok(fb);
    }

    private static async Task<IResult> UpdateFeedback(Guid id, UpdateFeedbackRequest req, IFeedbackStore store, CancellationToken ct)
    {
        var fb = await store.GetFeedbackAsync(id, ct);
        if (fb is null) return Results.NotFound();
        if (req.Status is { Length: > 0 } s)
        {
            if (!FeedbackStatus.IsValid(s)) return Results.Problem(title: "Invalid status.", statusCode: 400);
            fb.Status = s;
        }
        if (req.Priority is { } pr)
        {
            if (pr < 1 || pr > 5) return Results.Problem(title: "Priority must be 1..5.", statusCode: 400);
            fb.Priority = pr;
        }
        if (req.Category is not null)
        {
            if (req.Category.Length > 0 && !FeedbackCategory.IsValid(req.Category)) return Results.Problem(title: "Invalid category.", statusCode: 400);
            fb.Category = string.IsNullOrEmpty(req.Category) ? null : req.Category;
        }
        if (req.Assignee is not null)
        {
            fb.Assignee = string.IsNullOrWhiteSpace(req.Assignee) ? null : req.Assignee.Trim();
        }
        if (req.Summary is not null)
        {
            fb.Summary = string.IsNullOrWhiteSpace(req.Summary) ? null : req.Summary;
        }
        var saved = await store.UpdateFeedbackAsync(fb, ct);
        return Results.Ok(saved);
    }

    private static async Task<IResult> DeleteFeedback(Guid id, IFeedbackStore store, IBlobStore blobs, CancellationToken ct)
    {
        var fb = await store.GetFeedbackAsync(id, ct);
        if (fb is null) return Results.NotFound();
        if (fb.AudioBlobKey is { Length: > 0 } a && !a.StartsWith("pending:")) await blobs.DeleteAsync(a, ct);
        if (fb.ScreenshotKey is { Length: > 0 } s && !s.StartsWith("pending:")) await blobs.DeleteAsync(s, ct);
        await store.DeleteFeedbackAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetAudio(Guid id, IFeedbackStore store, IBlobStore blobs, CancellationToken ct)
    {
        var fb = await store.GetFeedbackAsync(id, ct);
        if (fb?.AudioBlobKey is null || fb.AudioBlobKey.StartsWith("pending:")) return Results.NotFound();
        var handle = await blobs.ReadAsync(fb.AudioBlobKey, ct);
        if (handle is null) return Results.NotFound();
        return Results.Stream(handle.Content, handle.ContentType);
    }

    private static async Task<IResult> GetScreenshot(Guid id, IFeedbackStore store, IBlobStore blobs, CancellationToken ct)
    {
        var fb = await store.GetFeedbackAsync(id, ct);
        if (fb?.ScreenshotKey is null || fb.ScreenshotKey.StartsWith("pending:")) return Results.NotFound();
        var handle = await blobs.ReadAsync(fb.ScreenshotKey, ct);
        if (handle is null) return Results.NotFound();
        return Results.Stream(handle.Content, handle.ContentType);
    }

    private static async Task<IResult> DeleteBySubmitter(string submitterId, IFeedbackStore store, IBlobStore blobs, CancellationToken ct)
    {
        var feedbacks = await store.FindBySubmitterIdAsync(submitterId, ct);
        foreach (var fb in feedbacks)
        {
            if (fb.AudioBlobKey is { Length: > 0 } a && !a.StartsWith("pending:")) await blobs.DeleteAsync(a, ct);
            if (fb.ScreenshotKey is { Length: > 0 } s && !s.StartsWith("pending:")) await blobs.DeleteAsync(s, ct);
            await store.DeleteFeedbackAsync(fb.Id, ct);
        }
        return Results.Ok(new { deleted = feedbacks.Count });
    }

    // ---- Phase 3: Comments ----

    private static async Task<IResult> ListComments(Guid id, IFeedbackStore store, CancellationToken ct)
    {
        var fb = await store.GetFeedbackAsync(id, ct);
        if (fb is null) return Results.NotFound();
        var comments = await store.ListCommentsAsync(id, ct);
        return Results.Ok(comments);
    }

    private static async Task<IResult> AddComment(Guid id, CreateCommentRequest req, IFeedbackStore store, TimeProvider clock, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Body)) return Results.Problem(title: "Body required.", statusCode: 400);
        if (string.IsNullOrWhiteSpace(req.Author)) return Results.Problem(title: "Author required.", statusCode: 400);
        var fb = await store.GetFeedbackAsync(id, ct);
        if (fb is null) return Results.NotFound();
        var c = await store.AddCommentAsync(new FeedbackComment
        {
            FeedbackId = id,
            Body = req.Body.Trim(),
            Author = req.Author.Trim(),
            CreatedAt = clock.GetUtcNow(),
        }, ct);
        return Results.Created($"/api/v1/admin/feedbacks/{id}/comments/{c.Id}", c);
    }

    private static async Task<IResult> DeleteComment(Guid id, Guid commentId, IFeedbackStore store, CancellationToken ct)
    {
        await store.DeleteCommentAsync(commentId, ct);
        return Results.NoContent();
    }

    // ---- Phase 3: Stats ----

    private static async Task<IResult> GetStats(IFeedbackStore store, Guid? projectId, DateTimeOffset? since, CancellationToken ct)
    {
        var stats = await store.GetStatsAsync(projectId, since, ct);
        return Results.Ok(stats);
    }

    // ---- Phase 3: CSV export ----

    private static async Task<IResult> ExportFeedbacks(
        IFeedbackStore store,
        HttpContext ctx,
        Guid? projectId, string? status, string? type, string? search,
        DateTimeOffset? since, DateTimeOffset? until,
        CancellationToken ct)
    {
        // Stream up to 10k rows; this keeps memory bounded without paging UI complexity.
        const int chunk = 500;
        const int max = 10_000;
        ctx.Response.ContentType = "text/csv; charset=utf-8";
        ctx.Response.Headers["Content-Disposition"] = "attachment; filename=feedbacks.csv";
        await using var sw = new StreamWriter(ctx.Response.Body, new System.Text.UTF8Encoding(true));
        await sw.WriteLineAsync("id,projectId,createdAt,type,status,category,priority,assignee,text,summary,pageUrl,submitterEmail,submitterId");

        string? cursor = null;
        int written = 0;
        while (written < max)
        {
            var page = await store.ListFeedbacksAsync(new FeedbackQuery(
                projectId, status, type, since, cursor, chunk,
                SearchText: search, Until: until), ct);
            if (page.Items.Count == 0) break;
            foreach (var f in page.Items)
            {
                await sw.WriteLineAsync(string.Join(",", new[]
                {
                    Csv(f.Id.ToString()),
                    Csv(f.ProjectId.ToString()),
                    Csv(f.CreatedAt.ToString("o")),
                    Csv(f.Type),
                    Csv(f.Status),
                    Csv(f.Category),
                    Csv(f.Priority?.ToString()),
                    Csv(f.Assignee),
                    Csv(f.Text),
                    Csv(f.Summary),
                    Csv(f.PageUrl),
                    Csv(f.Submitter?.Email),
                    Csv(f.Submitter?.Id),
                }));
                written++;
                if (written >= max) break;
            }
            if (page.NextCursor is null) break;
            cursor = page.NextCursor;
        }
        await sw.FlushAsync(ct);
        return Results.Empty;
    }

    private static string Csv(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        // Spreadsheet-safe quoting + neutralize formula-injection vectors.
        var needsQuote = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
        var safe = s.Replace("\"", "\"\"");
        if (safe.Length > 0 && "=+-@".IndexOf(safe[0]) >= 0) safe = "'" + safe;
        return needsQuote ? $"\"{safe}\"" : safe;
    }
}

public sealed record CreateProjectRequest(string Name, List<string>? AllowedOrigins, int? RetentionDays);
public sealed record UpdateProjectRequest(string? Name, List<string>? AllowedOrigins, int? RetentionDays, bool? Archived);
public sealed record UpdateFeedbackRequest(string? Status, short? Priority, string? Category, string? Assignee, string? Summary);
public sealed record CreateCommentRequest(string Body, string Author);

internal sealed class AdminTokenFilter : IEndpointFilter
{
    private readonly EchoOptions _options;
    public AdminTokenFilter(IOptions<EchoOptions> options) { _options = options.Value; }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var http = ctx.HttpContext;
        if (string.IsNullOrEmpty(_options.AdminToken))
            return Results.Problem(title: "Admin token not configured on server.", statusCode: 503);

        var auth = http.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!auth.StartsWith(prefix, StringComparison.Ordinal))
            return Results.Problem(title: "Missing bearer token.", statusCode: 401);
        var presented = auth[prefix.Length..];
        if (!CryptoEquals(presented, _options.AdminToken))
            return Results.Problem(title: "Invalid admin token.", statusCode: 401);

        return await next(ctx);
    }

    private static bool CryptoEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
