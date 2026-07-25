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

    /// <summary>
    /// Adds a brand-new activity item to the booking. Unlike the other actions here, there is
    /// no original item to resolve or clone from — the create spec is built entirely from
    /// <paramref name="request"/> and created directly via IOsmService.CreateBookingItemAsync.
    /// </summary>
    Task<BookingActionResult> AddActivityAsync(string osmBookingId, AddActivityRequest request);

    /// <summary>
    /// Removes (hard-deletes) an existing item — activity or site — from the booking. Unlike
    /// the other actions here, there is no replacement created: the item is resolved via
    /// ItemId and deleted directly via IOsmService.DeleteBookingItemAsync. Throws
    /// <see cref="BookingItemNotFoundException"/> if ItemId isn't in the booking.
    /// </summary>
    Task<BookingActionResult> RemoveActivityAsync(string osmBookingId, RemoveActivityRequest request);
}
