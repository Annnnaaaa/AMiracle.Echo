using AMiracle.Echo.Abstractions.Models;

namespace AMiracle.Echo.Abstractions.Processing;

public interface IFeedbackProcessor
{
    Task<ProcessorResult> ProcessAsync(FeedbackDraft draft, CancellationToken ct = default);
}

public sealed class FeedbackDraft
{
    public required Feedback Feedback { get; set; }
}

public sealed record ProcessorResult(FeedbackDraft Draft, bool Reject = false, string? RejectReason = null)
{
    public static ProcessorResult Continue(FeedbackDraft draft) => new(draft);
    public static ProcessorResult Rejected(FeedbackDraft draft, string reason) => new(draft, true, reason);
}
