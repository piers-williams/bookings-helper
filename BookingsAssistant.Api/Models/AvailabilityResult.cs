namespace BookingsAssistant.Api.Models;

/// <summary>
/// The outcome of a read-only availability check for an item-type (site or activity) over a
/// date range (the "checkAvailability" action). Unlike every other action in this feature set,
/// checking availability never creates/modifies/deletes anything in OSM — it only reports what
/// OSM's availability endpoint says. A false <see cref="Available"/> is a normal, successful
/// result (see <see cref="Reason"/>), not an error.
/// </summary>
public class AvailabilityResult
{
    /// <summary>True when a slot covers the requested start/end date window.</summary>
    public bool Available { get; set; }

    /// <summary>Present when <see cref="Available"/> is false — a human-readable explanation.</summary>
    public string? Reason { get; set; }
}
