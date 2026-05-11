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

    // Phase 2.
    public string? Summary { get; set; }
    public int AnalysisVersion { get; set; }
    public DateTimeOffset? AnalyzedAt { get; set; }
    public string? AnalysisError { get; set; }

    // Phase 3.
    public string? Assignee { get; set; }
}

internal sealed class FeedbackCommentEntity
{
    public Guid Id { get; set; }
    public Guid FeedbackId { get; set; }
    public string Body { get; set; } = "";
    public string Author { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
