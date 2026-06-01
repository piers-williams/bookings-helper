using System.Globalization;

namespace BookingsAssistant.Api.Services;

/// <summary>
/// Pure decision logic for honouring OSM's rate-limit headers. Kept separate
/// from <see cref="OsmService"/> so the parsing and back-off rules can be
/// unit-tested without an HTTP round-trip.
///
/// OSM returns <c>X-RateLimit-Limit/Remaining/Reset</c> on every response and a
/// <c>Retry-After</c> on a 429. <c>X-RateLimit-Reset</c> is normally seconds
/// until the window refreshes, but we also tolerate an absolute Unix timestamp.
/// </summary>
internal static class OsmRateLimit
{
    /// When remaining requests drop to this or below, pause until the window resets.
    public const int LowRemainingThreshold = 5;

    /// Used when a 429 arrives with no usable Retry-After / Reset hint.
    public static readonly TimeSpan FallbackBackoff = TimeSpan.FromSeconds(10);

    /// Never wait longer than this for a single back-off.
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How long to wait before retrying after a 429, derived from Retry-After
    /// (delta-seconds or HTTP-date) or, failing that, X-RateLimit-Reset. Always
    /// returns a positive, capped delay.
    /// </summary>
    public static TimeSpan GetRetryAfterDelay(string? retryAfter, string? rateLimitReset, DateTimeOffset now)
    {
        var delay = ParseRetryAfter(retryAfter, now) ?? ParseReset(rateLimitReset, now);
        if (delay is null || delay.Value <= TimeSpan.Zero) return FallbackBackoff;
        return Cap(delay.Value);
    }

    /// <summary>
    /// If the remaining-request count is at or below the threshold, returns how
    /// long to pause (until the window resets) to avoid hitting a 429 at all.
    /// Returns null when there's headroom or no usable reset hint.
    /// </summary>
    public static TimeSpan? GetProactiveDelay(string? remaining, string? rateLimitReset, DateTimeOffset now)
    {
        if (!int.TryParse(remaining, out var rem) || rem > LowRemainingThreshold) return null;
        var reset = ParseReset(rateLimitReset, now);
        if (reset is null || reset.Value <= TimeSpan.Zero) return null;
        return Cap(reset.Value);
    }

    private static TimeSpan Cap(TimeSpan d) => d > MaxBackoff ? MaxBackoff : d;

    private static TimeSpan? ParseRetryAfter(string? value, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            return TimeSpan.FromSeconds(seconds);
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when))
            return when - now;
        return null;
    }

    private static TimeSpan? ParseReset(string? value, DateTimeOffset now)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n <= 0)
            return null;
        // Values that large can only be an absolute Unix timestamp (seconds);
        // anything smaller is a delta in seconds until the window resets.
        if (n > 1_000_000_000L)
            return DateTimeOffset.FromUnixTimeSeconds(n) - now;
        return TimeSpan.FromSeconds(n);
    }
}
