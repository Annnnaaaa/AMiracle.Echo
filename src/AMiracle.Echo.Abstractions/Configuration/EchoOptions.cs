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

    /// <summary>
    /// Master key for signed user identity. When set, each project gets an identity
    /// secret derived from it (shown in the admin), and submissions that carry a valid
    /// <c>submitter.hash</c> are marked verified. Rotating this key rotates every
    /// project's secret at once. Leave empty to disable identity signing.
    /// </summary>
    public string IdentitySigningKey { get; set; } = "";

    /// <summary>
    /// Key for the receipt tokens returned when feedback is sent, which let an anonymous
    /// submitter look their feedback up again. Defaults to a key derived from
    /// <see cref="AdminToken"/>; set this so rotating the admin token doesn't invalidate
    /// receipts users already hold. Changing it invalidates existing receipts.
    /// </summary>
    public string ReceiptSigningKey { get; set; } = "";

    /// <summary>Outgoing email, used to reply to submitters from the admin.</summary>
    public EmailOptions Email { get; set; } = new();
}

public sealed class EmailOptions
{
    /// <summary>"resend", "smtp", or empty to disable replies.</summary>
    public string Provider { get; set; } = "";

    /// <summary>Sender, e.g. "Echo &lt;feedback@example.com&gt;". Must be a domain your provider lets you send from.</summary>
    public string From { get; set; } = "";

    /// <summary>Optional Reply-To, so the submitter's answer reaches a monitored inbox.</summary>
    public string? ReplyTo { get; set; }

    public ResendOptions Resend { get; set; } = new();
    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class ResendOptions
{
    public string ApiKey { get; set; } = "";
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool EnableSsl { get; set; } = true;
}

public sealed class RateLimitOptions
{
    public int IngestionPerMinute { get; set; } = 30;
    public int AdminPerMinute { get; set; } = 600;
}
