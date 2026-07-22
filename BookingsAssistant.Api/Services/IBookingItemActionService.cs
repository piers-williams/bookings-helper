using BookingsAssistant.Api.Models;

namespace BookingsAssistant.Api.Services;

/// <summary>
/// Reusable dispatch for the three OSM item-mutation actions (move-activity, change-site,
/// move-dates): resolves the target item(s) from the booking's current item list, delegates
/// the actual mutation to <see cref="IBookingMutationService"/>, and posts an audit-trail
/// comment summarising what changed.
///
/// Shared by <c>BookingActionsController</c> (human-triggered, via the REST endpoints) and
/// the plan-execution path (LLM-drafted, human-approved). Both callers own their own
/// request validation and error-to-HTTP-status mapping — this service only throws
/// <see cref="BookingItemNotFoundException"/> for an unresolvable ItemId; any other failure
/// propagates as-is (typically from the underlying OSM calls).
/// </summary>
public interface IBookingItemActionService
{
    /// <summary>Reschedules a single activity item. Throws <see cref="BookingItemNotFoundException"/> if ItemId isn't in the booking.</summary>
    Task<BookingActionResult> MoveActivityAsync(string osmBookingId, MoveActivityRequest request);

    /// <summary>Moves a single site item to a different site. Throws <see cref="BookingItemNotFoundException"/> if ItemId isn't in the booking.</summary>
    Task<BookingActionResult> ChangeSiteAsync(string osmBookingId, ChangeSiteRequest request);

    /// <summary>Shifts every item in the booking by the given number of days.</summary>
    Task<BookingActionResult> MoveDatesAsync(string osmBookingId, MoveDatesRequest request);
}
