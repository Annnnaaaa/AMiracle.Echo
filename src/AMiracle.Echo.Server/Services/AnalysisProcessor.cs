using AMiracle.Echo.Abstractions.Stores;
using AMiracle.Echo.Analysis.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AMiracle.Echo.Server.Services;

/// <summary>
/// Background service that finds unanalyzed feedbacks and runs the configured IFeedbackAnalyzer.
/// Opt-in via AMiracle:Echo:Analysis:Enabled = true AND an IFeedbackAnalyzer registered.
/// </summary>
public sealed class AnalysisProcessor : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly AnalysisOptions _options;
    private readonly ILogger<AnalysisProcessor> _log;

    public AnalysisProcessor(
        IServiceProvider services,
        IOptions<AnalysisOptions> options,
        ILogger<AnalysisProcessor> log)
    {
        _services = services;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _log.LogInformation("Analysis processor disabled (AMiracle:Echo:Analysis:Enabled=false). Skipping.");
            return;
        }

        var poll = TimeSpan.FromSeconds(Math.Max(5, _options.PollIntervalSeconds));
        _log.LogInformation("Analysis processor started; polling every {Seconds}s, batch={Batch}.",
            poll.TotalSeconds, _options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { _log.LogError(ex, "Analysis tick failed."); }

            try { await Task.Delay(poll, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var analyzer = scope.ServiceProvider.GetService<IFeedbackAnalyzer>();
        if (analyzer is null) return;

        var store = scope.ServiceProvider.GetRequiredService<IFeedbackStore>();
        var blobs = scope.ServiceProvider.GetRequiredService<IBlobStore>();

        var pending = await store.FindPendingAnalysisAsync(analyzer.Version, _options.BatchSize, ct);
        if (pending.Count == 0) return;

        _log.LogInformation("Analyzing {Count} feedback(s) at version {Version}.", pending.Count, analyzer.Version);

        foreach (var feedback in pending)
        {
            using var perFeedbackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            perFeedbackCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(15, _options.PerFeedbackTimeoutSeconds)));

            try
            {
                var context = new AnalysisContext
                {
                    OpenAudioBlob = async (key, c) =>
                    {
                        var handle = await blobs.ReadAsync(key, c);
                        return handle?.Content;
                    }
                };

                var result = await analyzer.AnalyzeAsync(feedback, context, perFeedbackCts.Token);

                if (!string.IsNullOrWhiteSpace(result.Transcript)) feedback.Text = result.Transcript;
                if (!string.IsNullOrWhiteSpace(result.Summary)) feedback.Summary = result.Summary;
                if (result.Category is { Length: > 0 } && feedback.Category is null) feedback.Category = result.Category;
                if (result.Priority is { } pri && feedback.Priority is null) feedback.Priority = pri;

                feedback.AnalysisVersion = analyzer.Version;
                feedback.AnalyzedAt = DateTimeOffset.UtcNow;
                feedback.AnalysisError = result.Error;

                await store.UpdateFeedbackAsync(feedback, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Analysis errored for feedback {Id}", feedback.Id);
                feedback.AnalysisError = ex.Message;
                feedback.AnalysisVersion = analyzer.Version; // mark to avoid retry loop
                feedback.AnalyzedAt = DateTimeOffset.UtcNow;
                try { await store.UpdateFeedbackAsync(feedback, ct); } catch { /* swallow */ }
            }
        }
    }
}
