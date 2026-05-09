using System.Text.Json;
using AMiracle.Echo.Abstractions.Configuration;
using AMiracle.Echo.Abstractions.Models;
using AMiracle.Echo.Abstractions.Processing;
using AMiracle.Echo.Abstractions.Stores;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AMiracle.Echo.Server.Services;

public sealed class CreateFeedbackInput
{
    public string Type { get; set; } = FeedbackType.Text;
    public string? Text { get; set; }
    public string? PageUrl { get; set; }
    public string? UserAgent { get; set; }
    public Submitter? Submitter { get; set; }
    public Dictionary<string, JsonElement>? CustomMetadata { get; set; }
    public string? Category { get; set; }
    public string? ConsentText { get; set; }
    public bool WillUploadAudio { get; set; }
    public bool WillUploadScreenshot { get; set; }
}

public sealed class FeedbackIngestionService
{
    private readonly IFeedbackStore _store;
    private readonly IEnumerable<IFeedbackProcessor> _processors;
    private readonly EchoOptions _options;
    private readonly ILogger<FeedbackIngestionService> _logger;
    private readonly TimeProvider _clock;

    public FeedbackIngestionService(
        IFeedbackStore store,
        IEnumerable<IFeedbackProcessor> processors,
        IOptions<EchoOptions> options,
        ILogger<FeedbackIngestionService> logger,
        TimeProvider clock)
    {
        _store = store;
        _processors = processors;
        _options = options.Value;
        _logger = logger;
        _clock = clock;
    }

    public async Task<IngestResult> IngestAsync(Guid projectId, CreateFeedbackInput input, CancellationToken ct)
    {
        if (!FeedbackType.IsValid(input.Type))
            return IngestResult.Rejected("type-invalid", $"type must be 'text' or 'voice'");
        if (!FeedbackCategory.IsValid(input.Category))
            return IngestResult.Rejected("category-invalid", "category must be one of: bug, idea, praise, question");

        var feedback = new Feedback
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Type = input.Type,
            Text = input.Text,
            PageUrl = input.PageUrl,
            UserAgent = input.UserAgent,
            Submitter = input.Submitter,
            CustomMetadata = input.CustomMetadata,
            Category = input.Category,
            Status = FeedbackStatus.New,
            ConsentText = input.ConsentText,
            CreatedAt = _clock.GetUtcNow(),
            AudioBlobKey = input.WillUploadAudio ? "pending:audio" : null,
            ScreenshotKey = input.WillUploadScreenshot ? "pending:screenshot" : null,
        };

        var draft = new FeedbackDraft { Feedback = feedback };
        foreach (var p in _processors)
        {
            var result = await p.ProcessAsync(draft, ct);
            if (result.Reject)
            {
                _logger.LogInformation("Feedback rejected by {Processor}: {Reason}", p.GetType().Name, result.RejectReason);
                return IngestResult.Rejected("processor-rejected", result.RejectReason ?? "rejected");
            }
            draft = result.Draft;
        }

        var saved = await _store.CreateFeedbackAsync(draft.Feedback, ct);
        return IngestResult.Created(saved);
    }
}

public sealed record IngestResult(bool Success, Feedback? Feedback, string? ErrorCode, string? ErrorMessage)
{
    public static IngestResult Created(Feedback fb) => new(true, fb, null, null);
    public static IngestResult Rejected(string code, string msg) => new(false, null, code, msg);
}
