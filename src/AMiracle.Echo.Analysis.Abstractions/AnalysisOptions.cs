namespace AMiracle.Echo.Analysis.Abstractions;

public sealed class AnalysisOptions
{
    /// <summary>Master kill-switch. If false, the background processor doesn't run.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>How often the background processor looks for unanalyzed feedbacks.</summary>
    public int PollIntervalSeconds { get; set; } = 30;

    /// <summary>Max feedbacks to analyze per tick.</summary>
    public int BatchSize { get; set; } = 5;

    /// <summary>Max seconds to spend on a single feedback before giving up (transcribe + analyze).</summary>
    public int PerFeedbackTimeoutSeconds { get; set; } = 90;
}
