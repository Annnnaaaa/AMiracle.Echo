using AMiracle.Echo.Abstractions.Models;

namespace AMiracle.Echo.Abstractions.Stores;

public interface IFeedbackStore
{
    // Projects
    Task<Project> CreateProjectAsync(Project project, CancellationToken ct = default);
    Task<Project?> GetProjectAsync(Guid id, CancellationToken ct = default);
    Task<Project?> GetProjectByPublicKeyAsync(string publicKey, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListProjectsAsync(CancellationToken ct = default);
    Task<Project> UpdateProjectAsync(Project project, CancellationToken ct = default);
    Task DeleteProjectAsync(Guid id, CancellationToken ct = default);

    // Feedbacks
    Task<Feedback> CreateFeedbackAsync(Feedback feedback, CancellationToken ct = default);
    Task<Feedback?> GetFeedbackAsync(Guid id, CancellationToken ct = default);
    Task<FeedbackPage> ListFeedbacksAsync(FeedbackQuery query, CancellationToken ct = default);
    Task<Feedback> UpdateFeedbackAsync(Feedback feedback, CancellationToken ct = default);
    Task DeleteFeedbackAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Feedback>> FindBySubmitterIdAsync(string submitterId, CancellationToken ct = default);

    // Retention sweep helpers
    Task<IReadOnlyList<Feedback>> FindExpiredAsync(DateTimeOffset now, CancellationToken ct = default);
    Task<IReadOnlyList<Feedback>> FindAbandonedAsync(DateTimeOffset cutoff, CancellationToken ct = default);

    // Phase 2 — analysis queue.
    Task<IReadOnlyList<Feedback>> FindPendingAnalysisAsync(int currentVersion, int limit, CancellationToken ct = default);

    // Phase 3 — comments.
    Task<FeedbackComment> AddCommentAsync(FeedbackComment comment, CancellationToken ct = default);
    Task<IReadOnlyList<FeedbackComment>> ListCommentsAsync(Guid feedbackId, CancellationToken ct = default);
    Task DeleteCommentAsync(Guid id, CancellationToken ct = default);

    // Phase 3 — stats.
    Task<FeedbackStats> GetStatsAsync(Guid? projectId, DateTimeOffset? since, CancellationToken ct = default);
}

public sealed record FeedbackQuery(
    Guid? ProjectId = null,
    string? Status = null,
    string? Type = null,
    DateTimeOffset? Since = null,
    string? Cursor = null,
    int Limit = 50,
    // Phase 3 filters.
    string? SearchText = null,
    string? Assignee = null,
    string? Category = null,
    short? Priority = null,
    DateTimeOffset? Until = null);

public sealed record FeedbackPage(IReadOnlyList<Feedback> Items, string? NextCursor);

public sealed record FeedbackStats(
    int Total,
    Dictionary<string, int> ByStatus,
    Dictionary<string, int> ByType,
    Dictionary<string, int> ByCategory,
    Dictionary<string, int> ByPriority,
    Dictionary<string, int> ByDay);   // "yyyy-MM-dd" → count, last 30 days.
