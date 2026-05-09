namespace AMiracle.Echo.Server.Services;

internal static class OriginValidator
{
    public static bool IsAllowed(string? origin, IReadOnlyList<string> allowed)
    {
        if (allowed.Count == 0) return true; // No allowlist configured = accept any (consumer's choice).
        if (string.IsNullOrEmpty(origin)) return false;
        foreach (var entry in allowed)
        {
            if (string.Equals(entry, origin, StringComparison.OrdinalIgnoreCase)) return true;
            if (entry == "*") return true;
            // Wildcard subdomain support: "*.example.com"
            if (entry.StartsWith("*.", StringComparison.Ordinal))
            {
                var suffix = entry[1..]; // ".example.com"
                if (origin.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }
}
