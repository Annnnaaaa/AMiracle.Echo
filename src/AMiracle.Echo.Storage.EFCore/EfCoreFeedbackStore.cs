using System.Text.Json;
using AMiracle.Echo.Abstractions.Models;
using AMiracle.Echo.Abstractions.Stores;
using AMiracle.Echo.Storage.EFCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace AMiracle.Echo.Storage.EFCore;

public sealed class EfCoreFeedbackStore : IFeedbackStore
{
    private readonly EchoDbContext _db;

    public EfCoreFeedbackStore(EchoDbContext db)
    {
        _db = db;
    }

    // ----- Projects -----

    public async Task<Project> CreateProjectAsync(Project project, CancellationToken ct = default)
    {
        var entity = ToEntity(project);
        _db.Projects.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToModel(entity);
    }

    public async Task<Project?> GetProjectAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<Project?> GetProjectByPublicKeyAsync(string publicKey, CancellationToken ct = default)
    {
        var entity = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.PublicKey == publicKey, ct);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<Project>> ListProjectsAsync(CancellationToken ct = default)
    {
        var entities = await _db.Projects.AsNoTracking().OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        return entities.Select(ToModel).ToList();
    }

    public async Task<Project> UpdateProjectAsync(Project project, CancellationToken ct = default)
    {
        var entity = await _db.Projects.FirstOrDefaultAsync(p => p.Id == project.Id, ct)
            ?? throw new InvalidOperationException($"Project {project.Id} not found");
        entity.Name = project.Name;
        entity.PublicKey = project.PublicKey;
        entity.AllowedOriginsJson = JsonSerializer.Serialize(project.AllowedOrigins);
        entity.RetentionDays = project.RetentionDays;
        entity.ArchivedAt = project.ArchivedAt;
        await _db.SaveChangesAsync(ct);
        return ToModel(entity);
    }

    public async Task DeleteProjectAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return;
        var feedbacks = _db.Feedbacks.Where(f => f.ProjectId == id);
        _db.Feedbacks.RemoveRange(feedbacks);
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(ct);
    }

    // ----- Feedbacks -----

    public async Task<Feedback> CreateFeedbackAsync(Feedback feedback, CancellationToken ct = default)
    {
        var entity = ToEntity(feedback);
        _db.Feedbacks.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToModel(entity);
    }

    public async Task<Feedback?> GetFeedbackAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Feedbacks.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<FeedbackPage> ListFeedbacksAsync(FeedbackQuery query, CancellationToken ct = default)
    {
        var q = _db.Feedbacks.AsNoTracking().AsQueryable();
        if (query.ProjectId is { } pid) q = q.Where(f => f.ProjectId == pid);
        if (query.Status is { Length: > 0 } s) q = q.Where(f => f.Status == s);
        if (query.Type is { Length: > 0 } t) q = q.Where(f => f.Type == t);
        if (query.Since is { } since) q = q.Where(f => f.CreatedAt >= since);
        if (query.Until is { } until) q = q.Where(f => f.CreatedAt <= until);
        if (query.Assignee is { Length: > 0 } a) q = q.Where(f => f.Assignee == a);
        if (query.Category is { Length: > 0 } c) q = q.Where(f => f.Category == c);
        if (query.Priority is { } pr) q = q.Where(f => f.Priority == pr);
        if (query.SearchText is { Length: > 0 } st)
        {
            // Case-insensitive contains across text + summary. EF Like is portable.
            var pat = "%" + st.Replace("%", "[%]").Replace("_", "[_]") + "%";
            q = q.Where(f => EF.Functions.Like(f.Text ?? "", pat)
                          || EF.Functions.Like(f.Summary ?? "", pat));
        }

        if (TryDecodeCursor(query.Cursor, out var cursorTs, out var cursorId))
        {
            q = q.Where(f => f.CreatedAt < cursorTs || (f.CreatedAt == cursorTs && f.Id.CompareTo(cursorId) < 0));
        }

        q = q.OrderByDescending(f => f.CreatedAt).ThenByDescending(f => f.Id);
        var limit = Math.Clamp(query.Limit, 1, 200);
        var items = await q.Take(limit + 1).ToListAsync(ct);
        string? nextCursor = null;
        if (items.Count > limit)
        {
            var last = items[limit - 1];
            nextCursor = EncodeCursor(last.CreatedAt, last.Id);
            items = items.Take(limit).ToList();
        }
        return new FeedbackPage(items.Select(ToModel).ToList(), nextCursor);
    }

    public async Task<Feedback> UpdateFeedbackAsync(Feedback feedback, CancellationToken ct = default)
    {
        var entity = await _db.Feedbacks.FirstOrDefaultAsync(f => f.Id == feedback.Id, ct)
            ?? throw new InvalidOperationException($"Feedback {feedback.Id} not found");
        entity.Status = feedback.Status;
        entity.Priority = feedback.Priority;
        entity.Category = feedback.Category;
        entity.Text = feedback.Text;
        entity.AudioBlobKey = feedback.AudioBlobKey;
        entity.ScreenshotKey = feedback.ScreenshotKey;
        entity.DeletedAt = feedback.DeletedAt;
        // Phase 2.
        entity.Summary = feedback.Summary;
        entity.AnalysisVersion = feedback.AnalysisVersion;
        entity.AnalyzedAt = feedback.AnalyzedAt;
        entity.AnalysisError = feedback.AnalysisError;
        // Phase 3.
        entity.Assignee = feedback.Assignee;
        await _db.SaveChangesAsync(ct);
        return ToModel(entity);
    }

    public async Task DeleteFeedbackAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Feedbacks.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (entity is null) return;
        _db.Feedbacks.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Feedback>> FindBySubmitterIdAsync(string submitterId, CancellationToken ct = default)
    {
        var entities = await _db.Feedbacks.AsNoTracking()
            .Where(f => f.SubmitterId == submitterId)
            .ToListAsync(ct);
        return entities.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<Feedback>> FindExpiredAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var projects = await _db.Projects.AsNoTracking()
            .Where(p => p.RetentionDays != null)
            .ToListAsync(ct);

        var expired = new List<FeedbackEntity>();
        foreach (var p in projects)
        {
            var cutoff = now.AddDays(-(p.RetentionDays!.Value));
            var rows = await _db.Feedbacks.AsNoTracking()
                .Where(f => f.ProjectId == p.Id && f.CreatedAt < cutoff)
                .ToListAsync(ct);
            expired.AddRange(rows);
        }
        return expired.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<Feedback>> FindAbandonedAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        // Rows whose blob promises (audio/screenshot keys) were set but their objects never arrived.
        // A blob key starting with "pending:" means the placeholder was reserved but no upload yet.
        // SQLite's LINQ translator chokes on mixed AND/OR with StartsWith on multiple columns; use Union of two queries.
        var byAudio = _db.Feedbacks.AsNoTracking()
            .Where(f => f.CreatedAt < cutoff && f.AudioBlobKey != null && f.AudioBlobKey.StartsWith("pending:"));
        var byShot = _db.Feedbacks.AsNoTracking()
            .Where(f => f.CreatedAt < cutoff && f.ScreenshotKey != null && f.ScreenshotKey.StartsWith("pending:"));
        var entities = await byAudio.Union(byShot).ToListAsync(ct);
        return entities.Select(ToModel).ToList();
    }

    // ----- Mapping -----

    private static FeedbackEntity ToEntity(Feedback m) => new()
    {
        Id = m.Id,
        ProjectId = m.ProjectId,
        Type = m.Type,
        Text = m.Text,
        AudioBlobKey = m.AudioBlobKey,
        ScreenshotKey = m.ScreenshotKey,
        PageUrl = m.PageUrl,
        UserAgent = m.UserAgent,
        SubmitterJson = m.Submitter is null ? null : JsonSerializer.Serialize(m.Submitter),
        SubmitterId = m.Submitter?.Id,
        CustomMetadataJson = m.CustomMetadata is null ? null : JsonSerializer.Serialize(m.CustomMetadata),
        Category = m.Category,
        Status = m.Status,
        Priority = m.Priority,
        ConsentText = m.ConsentText,
        CreatedAt = m.CreatedAt,
        DeletedAt = m.DeletedAt,
        Summary = m.Summary,
        AnalysisVersion = m.AnalysisVersion,
        AnalyzedAt = m.AnalyzedAt,
        AnalysisError = m.AnalysisError,
        Assignee = m.Assignee,
    };

    private static Feedback ToModel(FeedbackEntity e) => new()
    {
        Id = e.Id,
        ProjectId = e.ProjectId,
        Type = e.Type,
        Text = e.Text,
        AudioBlobKey = e.AudioBlobKey,
        ScreenshotKey = e.ScreenshotKey,
        PageUrl = e.PageUrl,
        UserAgent = e.UserAgent,
        Submitter = e.SubmitterJson is null ? null : JsonSerializer.Deserialize<Submitter>(e.SubmitterJson),
        CustomMetadata = e.CustomMetadataJson is null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(e.CustomMetadataJson),
        Category = e.Category,
        Status = e.Status,
        Priority = e.Priority,
        ConsentText = e.ConsentText,
        CreatedAt = e.CreatedAt,
        DeletedAt = e.DeletedAt,
        Summary = e.Summary,
        AnalysisVersion = e.AnalysisVersion,
        AnalyzedAt = e.AnalyzedAt,
        AnalysisError = e.AnalysisError,
        Assignee = e.Assignee,
    };

    private static ProjectEntity ToEntity(Project m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        PublicKey = m.PublicKey,
        AllowedOriginsJson = JsonSerializer.Serialize(m.AllowedOrigins),
        RetentionDays = m.RetentionDays,
        CreatedAt = m.CreatedAt,
        ArchivedAt = m.ArchivedAt,
    };

    private static Project ToModel(ProjectEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        PublicKey = e.PublicKey,
        AllowedOrigins = JsonSerializer.Deserialize<List<string>>(e.AllowedOriginsJson) ?? new(),
        RetentionDays = e.RetentionDays,
        CreatedAt = e.CreatedAt,
        ArchivedAt = e.ArchivedAt,
    };

    private static string EncodeCursor(DateTimeOffset ts, Guid id)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{ts.UtcTicks}:{id}"));

    private static bool TryDecodeCursor(string? cursor, out DateTimeOffset ts, out Guid id)
    {
        ts = default; id = default;
        if (string.IsNullOrEmpty(cursor)) return false;
        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split(':', 2);
            ts = new DateTimeOffset(long.Parse(parts[0]), TimeSpan.Zero);
            id = Guid.Parse(parts[1]);
            return true;
        }
        catch { return false; }
    }

    // ----- Phase 2: pending analysis queue -----

    public async Task<IReadOnlyList<Feedback>> FindPendingAnalysisAsync(int currentVersion, int limit, CancellationToken ct = default)
    {
        var entities = await _db.Feedbacks.AsNoTracking()
            .Where(f => f.AnalysisVersion < currentVersion
                     && f.DeletedAt == null
                     && f.AnalysisError == null)
            .OrderBy(f => f.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(ct);
        return entities.Select(ToModel).ToList();
    }

    // ----- Phase 3: comments -----

    public async Task<FeedbackComment> AddCommentAsync(FeedbackComment comment, CancellationToken ct = default)
    {
        var entity = new FeedbackCommentEntity
        {
            Id = comment.Id == Guid.Empty ? Guid.NewGuid() : comment.Id,
            FeedbackId = comment.FeedbackId,
            Body = comment.Body,
            Author = comment.Author,
            CreatedAt = comment.CreatedAt == default ? DateTimeOffset.UtcNow : comment.CreatedAt,
        };
        _db.FeedbackComments.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new FeedbackComment
        {
            Id = entity.Id, FeedbackId = entity.FeedbackId,
            Body = entity.Body, Author = entity.Author, CreatedAt = entity.CreatedAt,
        };
    }

    public async Task<IReadOnlyList<FeedbackComment>> ListCommentsAsync(Guid feedbackId, CancellationToken ct = default)
    {
        var entities = await _db.FeedbackComments.AsNoTracking()
            .Where(c => c.FeedbackId == feedbackId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);
        return entities.Select(c => new FeedbackComment
        {
            Id = c.Id, FeedbackId = c.FeedbackId,
            Body = c.Body, Author = c.Author, CreatedAt = c.CreatedAt,
        }).ToList();
    }

    public async Task DeleteCommentAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.FeedbackComments.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return;
        _db.FeedbackComments.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ----- Phase 3: stats -----

    public async Task<FeedbackStats> GetStatsAsync(Guid? projectId, DateTimeOffset? since, CancellationToken ct = default)
    {
        var q = _db.Feedbacks.AsNoTracking().Where(f => f.DeletedAt == null);
        if (projectId is { } pid) q = q.Where(f => f.ProjectId == pid);
        var sinceCutoff = since ?? DateTimeOffset.UtcNow.AddDays(-30);
        var qRecent = q.Where(f => f.CreatedAt >= sinceCutoff);

        // Pull lightweight projections; group in memory to avoid provider quirks.
        var rows = await q
            .Select(f => new { f.Status, f.Type, f.Category, f.Priority, f.CreatedAt })
            .ToListAsync(ct);

        int total = rows.Count;
        var byStatus = rows.GroupBy(r => r.Status ?? "").ToDictionary(g => g.Key, g => g.Count());
        var byType = rows.GroupBy(r => r.Type ?? "").ToDictionary(g => g.Key, g => g.Count());
        var byCategory = rows.GroupBy(r => r.Category ?? "uncategorized").ToDictionary(g => g.Key, g => g.Count());
        var byPriority = rows.GroupBy(r => r.Priority?.ToString() ?? "unset").ToDictionary(g => g.Key, g => g.Count());

        var byDay = rows
            .Where(r => r.CreatedAt >= sinceCutoff)
            .GroupBy(r => r.CreatedAt.UtcDateTime.Date.ToString("yyyy-MM-dd"))
            .ToDictionary(g => g.Key, g => g.Count());

        return new FeedbackStats(total, byStatus, byType, byCategory, byPriority, byDay);
    }
}
