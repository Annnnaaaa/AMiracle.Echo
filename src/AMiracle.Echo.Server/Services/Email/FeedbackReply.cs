using System.Net;
using System.Net.Mail;
using AMiracle.Echo.Abstractions.Email;
using AMiracle.Echo.Abstractions.Models;

namespace AMiracle.Echo.Server.Services.Email;

/// <summary>Builds the email that answers a piece of feedback.</summary>
internal static class FeedbackReply
{
    public const int MaxMessageChars = 10_000;
    private const int QuoteChars = 500;

    /// <summary>
    /// A single, plain address — no display name, no list, no header injection.
    /// The address is whatever the browser claimed, so it must not be able to smuggle
    /// extra recipients or headers into the message.
    /// </summary>
    public static string? SafeRecipient(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var trimmed = email.Trim();
        if (trimmed.Length > 254 || trimmed.IndexOfAny(['\r', '\n', ',', ';', '<', '>', '"']) >= 0) return null;
        try
        {
            var parsed = new MailAddress(trimmed);
            return parsed.Address == trimmed && string.IsNullOrEmpty(parsed.DisplayName) ? trimmed : null;
        }
        catch (FormatException) { return null; }
    }

    // How a sent reply is recorded in the triage thread. The submitter-facing history
    // reads replies back through TryReadRecord, so the two must stay in sync.
    private const string RecordPrefix = "Emailed ";
    private const string RecordSeparator = ":\n\n";

    public static string RecordBody(string to, string message) => RecordPrefix + to + RecordSeparator + message;

    /// <summary>The message of a recorded reply, without the recipient address.</summary>
    public static string? TryReadRecord(string body)
    {
        if (!body.StartsWith(RecordPrefix, StringComparison.Ordinal)) return null;
        var sep = body.IndexOf(RecordSeparator, StringComparison.Ordinal);
        return sep < 0 ? null : body[(sep + RecordSeparator.Length)..];
    }

    public static EchoEmailMessage Build(string to, string projectName, Feedback feedback, string message)
    {
        var subject = "Re: your feedback on " + OneLine(projectName, 120);
        var greeting = string.IsNullOrWhiteSpace(feedback.Submitter?.Name) ? "Hi," : $"Hi {OneLine(feedback.Submitter!.Name!, 80)},";
        var quote = Quote(feedback);

        var text = $"{greeting}\n\n{message.Trim()}\n" + (quote is null ? "" : $"\n— You wrote on {feedback.CreatedAt:yyyy-MM-dd}:\n> {quote.Replace("\n", "\n> ")}\n");

        var html = $"""
            <div style="font-family:system-ui,-apple-system,Segoe UI,sans-serif;font-size:15px;line-height:1.5;color:#111">
            <p>{Enc(greeting)}</p>
            <p style="white-space:pre-wrap">{Enc(message.Trim())}</p>
            {(quote is null ? "" : $"<p style=\"color:#666;font-size:13px;margin-top:24px\">You wrote on {feedback.CreatedAt:yyyy-MM-dd}:</p><blockquote style=\"margin:0;padding-left:12px;border-left:3px solid #ddd;color:#555;white-space:pre-wrap\">{Enc(quote)}</blockquote>")}
            </div>
            """;
        return new EchoEmailMessage(to, subject, text, html);
    }

    private static string? Quote(Feedback f)
    {
        var src = !string.IsNullOrWhiteSpace(f.Text) ? f.Text : f.Summary;
        if (string.IsNullOrWhiteSpace(src)) return null;
        src = src.Trim().Replace("\r\n", "\n");
        return src.Length <= QuoteChars ? src : src[..QuoteChars] + "…";
    }

    // Subjects and greetings are single-line: strip anything that could start a new header.
    private static string OneLine(string s, int max)
    {
        var clean = new string(s.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return clean.Length <= max ? clean : clean[..max];
    }

    private static string Enc(string s) => WebUtility.HtmlEncode(s);
}
