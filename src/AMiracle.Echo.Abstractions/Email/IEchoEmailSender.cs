namespace AMiracle.Echo.Abstractions.Email;

/// <summary>
/// Sends a reply from the admin to the person who left feedback. Echo ships Resend
/// and SMTP senders (picked by <c>AMiracle:Echo:Email:Provider</c>); register your own
/// implementation before <c>AddAmiracleEcho()</c> to use anything else.
/// </summary>
public interface IEchoEmailSender
{
    /// <summary>False when no provider is configured; the reply endpoint then answers 501.</summary>
    bool IsConfigured { get; }

    /// <summary>Throws <see cref="EchoEmailException"/> when the provider refuses the message.</summary>
    Task SendAsync(EchoEmailMessage message, CancellationToken ct);
}

public sealed record EchoEmailMessage(string To, string Subject, string TextBody, string? HtmlBody = null);

public sealed class EchoEmailException : Exception
{
    public EchoEmailException(string message, Exception? inner = null) : base(message, inner) { }
}
