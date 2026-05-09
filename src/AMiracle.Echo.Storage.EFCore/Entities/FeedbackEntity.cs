namespace AMiracle.Echo.Storage.EFCore.Entities;

internal sealed class FeedbackEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Type { get; set; } = "text";
    public string? Text { get; set; }
    public string? AudioBlobKey { get; set; }
    public string? ScreenshotKey { get; set; }
    public string? PageUrl { get; set; }
    public string? UserAgent { get; set; }
    public string? SubmitterJson { get; set; }
    public string? SubmitterId { get; set; } // Indexed for GDPR lookup.
    public string? CustomMetadataJson { get; set; }
    public string? Category { get; set; }
    public string Status { get; set; } = "new";
    public short? Priority { get; set; }
    public string? ConsentText { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
