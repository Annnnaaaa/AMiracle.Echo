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
}

public sealed record FeedbackQuery(
    Guid? ProjectId = null,
    string? Status = null,
    string? Type = null,
    DateTimeOffset? Since = null,
    string? Cursor = null,
    int Limit = 50);

public sealed record FeedbackPage(IReadOnlyList<Feedback> Items, string? NextCursor);
