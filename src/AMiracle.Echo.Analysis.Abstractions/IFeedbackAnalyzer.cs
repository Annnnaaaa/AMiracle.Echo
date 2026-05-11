using AMiracle.Echo.Abstractions.Models;
using AMiracle.Echo.Abstractions.Stores;

namespace AMiracle.Echo.Analysis.Abstractions;

/// <summary>
/// Single end-to-end analyzer per feedback. Phase 2 ships one OpenAI-backed implementation.
/// Other providers (Anthropic, Azure, local) can implement this same interface.
/// </summary>
public interface IFeedbackAnalyzer
{
    /// <summary>
    /// Run the full analysis pipeline on a single feedback. Returns the suggested updates;
    /// the caller persists them. Implementations should be idempotent and safe to retry.
    /// </summary>
    Task<AnalysisResult> AnalyzeAsync(Feedback feedback, AnalysisContext context, CancellationToken ct = default);

    /// <summary>
    /// A monotonically increasing version this analyzer emits. Bumped when prompts/models
    /// change so old feedbacks can be re-analyzed by the queue processor.
    /// </summary>
    int Version { get; }
}

public sealed class AnalysisContext
{
    public Func<string, CancellationToken, Task<Stream?>>? OpenAudioBlob { get; init; }
}

public sealed class AnalysisResult
{
    public string? Transcript { get; set; }   // fills feedback.Text for voice feedbacks
    public string? Summary { get; set; }
    public string? Category { get; set; }     // bug | idea | praise | question
    public short? Priority { get; set; }      // 1..5
    public string? Error { get; set; }        // non-fatal — recorded on the row, prevents re-tries
}
