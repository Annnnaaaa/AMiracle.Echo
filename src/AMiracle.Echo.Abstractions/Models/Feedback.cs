using System.Text.Json;

namespace AMiracle.Echo.Abstractions.Models;

public sealed class Feedback
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Type { get; set; } = FeedbackType.Text;
    public string? Text { get; set; }
    public string? AudioBlobKey { get; set; }
    public string? ScreenshotKey { get; set; }
    public string? PageUrl { get; set; }
    public string? UserAgent { get; set; }
    public Submitter? Submitter { get; set; }
    public Dictionary<string, JsonElement>? CustomMetadata { get; set; }
    public string? Category { get; set; }
    public string Status { get; set; } = FeedbackStatus.New;
    public short? Priority { get; set; }
    public string? ConsentText { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class Submitter
{
    public string? Id { get; set; }
    public string? Email { get; set; }
    public string? Name { get; set; }
}
