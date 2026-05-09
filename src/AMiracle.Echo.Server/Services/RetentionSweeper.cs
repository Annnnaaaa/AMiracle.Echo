using AMiracle.Echo.Abstractions.Configuration;
using AMiracle.Echo.Abstractions.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AMiracle.Echo.Server.Services;

public sealed class RetentionSweeper : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly EchoOptions _options;
    private readonly ILogger<RetentionSweeper> _logger;
    private readonly TimeProvider _clock;

    public RetentionSweeper(
        IServiceProvider services,
        IOptions<EchoOptions> options,
        ILogger<RetentionSweeper> logger,
        TimeProvider clock)
    {
        _services = services;
        _options = options.Value;
        _logger = logger;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.RetentionSweepIntervalMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retention sweep failed");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task SweepOnceAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IFeedbackStore>();
        var blob = scope.ServiceProvider.GetRequiredService<IBlobStore>();

        var now = _clock.GetUtcNow();

        // 1. Expired by retention policy
        var expired = await store.FindExpiredAsync(now, ct);
        foreach (var fb in expired)
        {
            await DeleteFeedbackAndBlobs(store, blob, fb.Id, fb.AudioBlobKey, fb.ScreenshotKey, fb.ProjectId, ct);
        }

        // 2. Abandoned uploads (promised blobs that never arrived)
        var abandonCutoff = now.AddSeconds(-_options.BlobUploadWindowSeconds);
        var abandoned = await store.FindAbandonedAsync(abandonCutoff, ct);
        foreach (var fb in abandoned)
        {
            await DeleteFeedbackAndBlobs(store, blob, fb.Id, fb.AudioBlobKey, fb.ScreenshotKey, fb.ProjectId, ct);
        }

        if (expired.Count > 0 || abandoned.Count > 0)
            _logger.LogInformation("Retention sweep: expired={Expired}, abandoned={Abandoned}", expired.Count, abandoned.Count);
    }

    private static async Task DeleteFeedbackAndBlobs(
        IFeedbackStore store, IBlobStore blob,
        Guid feedbackId, string? audioKey, string? screenshotKey, Guid projectId,
        CancellationToken ct)
    {
        if (audioKey is { Length: > 0 } && !audioKey.StartsWith("pending:", StringComparison.Ordinal))
            await blob.DeleteAsync(audioKey, ct);
        if (screenshotKey is { Length: > 0 } && !screenshotKey.StartsWith("pending:", StringComparison.Ordinal))
            await blob.DeleteAsync(screenshotKey, ct);
        await store.DeleteFeedbackAsync(feedbackId, ct);
    }
}
