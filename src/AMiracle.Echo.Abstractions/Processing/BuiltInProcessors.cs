namespace AMiracle.Echo.Abstractions.Processing;

public sealed class NoOpRedactionProcessor : IFeedbackProcessor
{
    public Task<ProcessorResult> ProcessAsync(FeedbackDraft draft, CancellationToken ct = default)
        => Task.FromResult(ProcessorResult.Continue(draft));
}

public sealed class MaxLengthProcessor : IFeedbackProcessor
{
    private readonly int _maxChars;

    public MaxLengthProcessor(int maxChars = 10_000)
    {
        _maxChars = maxChars;
    }

    public Task<ProcessorResult> ProcessAsync(FeedbackDraft draft, CancellationToken ct = default)
    {
        if (draft.Feedback.Text is { Length: > 0 } text && text.Length > _maxChars)
        {
            draft.Feedback.Text = text[.._maxChars];
        }
        return Task.FromResult(ProcessorResult.Continue(draft));
    }
}
