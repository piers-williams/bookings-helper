using System.Security.Cryptography;
using System.Text;

namespace BookingsAssistant.Api.Services;

/// <summary>
/// Decides whether a request may proceed given the configured shared API token.
///
/// Rules:
/// - No token configured  → guard is OFF (the addon keeps working until the
///   user sets one in the addon options).
/// - Non-/api paths (the SPA's HTML/JS/CSS)  → always allowed, so the app can
///   load and prompt for the token.
/// - /api/auth/*  → allowed, because the OSM OAuth login/callback are top-level
///   browser redirects that cannot carry a custom header.
/// - Everything else under /api  → requires a matching token (constant-time
///   comparison to avoid leaking it via timing).
/// </summary>
internal static class ApiTokenPolicy
{
    public static bool IsAllowed(string path, string? configuredToken, string? providedToken)
    {
        if (string.IsNullOrEmpty(configuredToken)) return true;
        if (!IsApiPath(path)) return true;
        if (IsAuthPath(path)) return true;
        if (string.IsNullOrEmpty(providedToken)) return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedToken),
            Encoding.UTF8.GetBytes(configuredToken));
    }

    private static bool IsApiPath(string p) =>
        p.Equals("/api", StringComparison.OrdinalIgnoreCase) ||
        p.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);

    private static bool IsAuthPath(string p) =>
        p.Equals("/api/auth", StringComparison.OrdinalIgnoreCase) ||
        p.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase);
}
