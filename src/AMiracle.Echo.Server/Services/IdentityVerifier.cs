using System.Security.Cryptography;
using System.Text;
using AMiracle.Echo.Abstractions.Configuration;
using Microsoft.Extensions.Options;

namespace AMiracle.Echo.Server.Services;

/// <summary>
/// Signed user identity. <c>AMiracleEcho.identify()</c> runs in the browser, so on its
/// own the submitter is just a claim anyone holding the public key can make. The host
/// app's server signs the user id with the project's identity secret; Echo checks it.
/// </summary>
/// <remarks>
/// Each project's secret is derived from <see cref="EchoOptions.IdentitySigningKey"/>
/// rather than stored, so enabling this needs no schema change.
/// </remarks>
public sealed class IdentityVerifier
{
    private const string Purpose = "amiracle-echo-identity:v1:";
    private readonly byte[]? _masterKey;

    public IdentityVerifier(IOptions<EchoOptions> options)
    {
        var key = options.Value.IdentitySigningKey;
        _masterKey = string.IsNullOrWhiteSpace(key) ? null : Encoding.UTF8.GetBytes(key);
    }

    public bool Enabled => _masterKey is not null;

    /// <summary>The secret the host app's server uses to sign user ids, or null if disabled.</summary>
    public string? ProjectSecret(Guid projectId)
        => _masterKey is null ? null : Hex(HMACSHA256.HashData(_masterKey, Encoding.UTF8.GetBytes(Purpose + projectId.ToString("D"))));

    /// <summary>
    /// null — unsigned (no hash sent, or signing disabled); true — valid; false — a hash
    /// was sent but does not match the submitter id.
    /// </summary>
    public bool? Evaluate(Guid projectId, string? submitterId, string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) return null;
        var secret = ProjectSecret(projectId);
        if (secret is null) return null;
        if (string.IsNullOrEmpty(submitterId)) return false;

        var expected = Encoding.ASCII.GetBytes(Sign(secret, submitterId));
        var actual = Encoding.ASCII.GetBytes(hash.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    /// <summary>What the host app computes: lowercase hex HMAC-SHA256(secret, userId).</summary>
    public static string Sign(string projectSecret, string submitterId)
        => Hex(HMACSHA256.HashData(Encoding.UTF8.GetBytes(projectSecret), Encoding.UTF8.GetBytes(submitterId)));

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
