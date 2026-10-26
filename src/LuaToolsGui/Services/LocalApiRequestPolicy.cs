namespace LuaToolsGui.Services;

/// <summary>
/// Browser-origin check for the Steam plugin bridge. Native loopback clients (no Origin and no
/// cross-site Fetch Metadata) remain compatible. This mitigates website-initiated cross-origin
/// calls but does NOT authenticate malicious local processes, which can spoof HTTP headers.
/// </summary>
internal static class LocalApiRequestPolicy
{
    private static readonly HashSet<string> AllowedOrigins = new(StringComparer.OrdinalIgnoreCase)
    {
        "https://store.steampowered.com",
        "https://steamcommunity.com"
    };

    internal static bool IsAllowed(string? origin, string? fetchSite)
    {
        if (!string.IsNullOrWhiteSpace(origin))
            return AllowedOrigins.Contains(origin.Trim());

        // Browser fetches may omit Origin on simple GET requests. Do not allow arbitrary
        // external websites to read a local endpoint through no-CORS or navigation tricks.
        return !string.Equals(fetchSite?.Trim(), "cross-site", StringComparison.OrdinalIgnoreCase);
    }
}
