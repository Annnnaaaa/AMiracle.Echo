namespace AMiracle.Echo.Abstractions.Configuration;

public sealed class EchoOptions
{
    public string AdminToken { get; set; } = "";
    public string BasePath { get; set; } = "/";
    public RateLimitOptions RateLimit { get; set; } = new();
    public long MaxAudioBytes { get; set; } = 25_000_000;
    public long MaxScreenshotBytes { get; set; } = 5_000_000;
    public int BlobUploadWindowSeconds { get; set; } = 300;
    public int RetentionSweepIntervalMinutes { get; set; } = 60;
    public int MaxFeedbackTextChars { get; set; } = 10_000;
}

public sealed class RateLimitOptions
{
    public int IngestionPerMinute { get; set; } = 30;
    public int AdminPerMinute { get; set; } = 600;
}
