using System.Security.Cryptography;
using System.Text;
using AMiracle.Echo.Abstractions.Configuration;
using Microsoft.Extensions.Options;

namespace AMiracle.Echo.Server.Services;

/// <summary>
/// Receipt tokens: proof that a client sent a given feedback, so an anonymous submitter
/// can see its status later. The token is an HMAC of the feedback id, recomputed on
/// lookup — nothing is stored, so this needs no schema change.
/// </summary>
public sealed class ReceiptSigner
{
    private const string Purpose = "amiracle-echo-receipt:v1:";
    private readonly byte[]? _key;

    public ReceiptSigner(IOptions<EchoOptions> options)
    {
        var o = options.Value;
        var master = !string.IsNullOrWhiteSpace(o.ReceiptSigningKey) ? o.ReceiptSigningKey
            : !string.IsNullOrWhiteSpace(o.AdminToken) ? "admin:" + o.AdminToken
            : null;
        _key = master is null ? null : SHA256.HashData(Encoding.UTF8.GetBytes(master));
    }

    /// <summary>The receipt for a feedback, or null when no key is configured.</summary>
    public string? Issue(Guid feedbackId)
        => _key is null ? null : Base64Url(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(Purpose + feedbackId.ToString("D"))));

    public bool IsValid(Guid feedbackId, string? token)
    {
        var expected = Issue(feedbackId);
        if (expected is null || string.IsNullOrEmpty(token)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(token.Trim()));
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
