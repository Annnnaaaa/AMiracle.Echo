using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using AMiracle.Echo.Abstractions.Configuration;
using AMiracle.Echo.Abstractions.Email;
using Microsoft.Extensions.Options;

namespace AMiracle.Echo.Server.Services.Email;

/// <summary>Used when <c>Email:Provider</c> is empty: replies are switched off.</summary>
internal sealed class DisabledEmailSender : IEchoEmailSender
{
    public bool IsConfigured => false;
    public Task SendAsync(EchoEmailMessage message, CancellationToken ct)
        => throw new EchoEmailException("Email is not configured on this server.");
}

/// <summary>Resend's HTTP API (https://resend.com/docs/api-reference/emails/send-email).</summary>
internal sealed class ResendEmailSender : IEchoEmailSender
{
    public const string HttpClientName = "AMiracle.Echo.Resend";
    private readonly IHttpClientFactory _http;
    private readonly EmailOptions _options;

    public ResendEmailSender(IHttpClientFactory http, IOptions<EchoOptions> options)
    {
        _http = http;
        _options = options.Value.Email;
    }

    public bool IsConfigured => _options.Resend.ApiKey.Length > 0 && _options.From.Length > 0;

    public async Task SendAsync(EchoEmailMessage message, CancellationToken ct)
    {
        var client = _http.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new
            {
                from = _options.From,
                to = new[] { message.To },
                subject = message.Subject,
                text = message.TextBody,
                html = message.HtmlBody,
                reply_to = string.IsNullOrWhiteSpace(_options.ReplyTo) ? null : _options.ReplyTo,
            }),
        };
        req.Headers.Authorization = new("Bearer", _options.Resend.ApiKey);

        HttpResponseMessage res;
        try { res = await client.SendAsync(req, ct); }
        catch (HttpRequestException ex) { throw new EchoEmailException("Couldn't reach Resend.", ex); }

        using (res)
        {
            if (res.IsSuccessStatusCode) return;
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new EchoEmailException($"Resend refused the message ({(int)res.StatusCode}): {Truncate(body, 300)}");
        }
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
}

internal sealed class SmtpEmailSender : IEchoEmailSender
{
    private readonly EmailOptions _options;
    public SmtpEmailSender(IOptions<EchoOptions> options) { _options = options.Value.Email; }

    public bool IsConfigured => _options.Smtp.Host.Length > 0 && _options.From.Length > 0;

    public async Task SendAsync(EchoEmailMessage message, CancellationToken ct)
    {
        var smtp = _options.Smtp;
        using var mail = new MailMessage
        {
            From = new MailAddress(_options.From),
            Subject = message.Subject,
            Body = message.TextBody,
            IsBodyHtml = false,
        };
        mail.To.Add(new MailAddress(message.To));
        if (!string.IsNullOrWhiteSpace(_options.ReplyTo)) mail.ReplyToList.Add(new MailAddress(_options.ReplyTo));
        if (message.HtmlBody is { } html)
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, null, "text/html"));

        using var client = new SmtpClient(smtp.Host, smtp.Port) { EnableSsl = smtp.EnableSsl };
        if (!string.IsNullOrEmpty(smtp.Username))
            client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);

        try { await client.SendMailAsync(mail, ct); }
        catch (SmtpException ex) { throw new EchoEmailException("The SMTP server refused the message: " + ex.Message, ex); }
    }
}
