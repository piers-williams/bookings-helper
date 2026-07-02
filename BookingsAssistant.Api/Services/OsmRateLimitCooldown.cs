namespace BookingsAssistant.Api.Services;

/// <summary>
/// Cross-request OSM rate-limit cooldown state, registered as a singleton so
/// it is shared across every <see cref="OsmService"/> instance. OsmService is
/// registered via <c>AddHttpClient</c>, which — per ASP.NET Core's typed-client
/// pattern — creates a new OsmService per resolution, i.e. effectively per HTTP
/// request. Without this shared singleton, a proactive pause learned from one
/// request (e.g. a low X-RateLimit-Remaining on one call) would be forgotten as
/// soon as that request finished, leaving no cross-request throttling between
/// the two current callers of rate-limited OSM calls:
/// <see cref="BookingsController"/>'s per-booking-open live comment fetch and
/// <see cref="BookingDetailBackfillService"/>'s periodic batches.
/// Thread-safe via a simple lock; this is a low-frequency check, not a hot path.
/// </summary>
public class OsmRateLimitCooldown
{
    private readonly object _gate = new();
    private DateTimeOffset _cooldownUntil = DateTimeOffset.MinValue;

    /// <summary>How long the caller should wait before its next OSM request, or null if none.</summary>
    public TimeSpan? TimeUntilReady()
    {
        lock (_gate)
        {
            var wait = _cooldownUntil - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : null;
        }
    }

    /// <summary>Pauses every caller (across all OsmService instances) until the given time.</summary>
    public void PauseUntil(DateTimeOffset until)
    {
        lock (_gate)
        {
            _cooldownUntil = until;
        }
    }
}
