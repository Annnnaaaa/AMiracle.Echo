using System.Security.Cryptography;

namespace AMiracle.Echo.Server.Services;

internal static class PublicKeyGenerator
{
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        var base64 = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return $"ekp_{base64}";
    }
}
