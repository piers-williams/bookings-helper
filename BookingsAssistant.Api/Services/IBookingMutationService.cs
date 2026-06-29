using BookingsAssistant.Api.Models;

namespace BookingsAssistant.Api.Services;

public interface IBookingMutationService
{
    /// <summary>
    /// Replaces a set of booking items using clone → create-all → delete-all with rollback.
    /// Phase 1: creates all clones. If any create fails, rolls back by deleting the created items.
    /// Phase 2 (only if all creates succeeded): deletes the originals.
    /// Returns a BookingActionResult describing what happened.
    /// </summary>
    Task<BookingActionResult> ReplaceItemsAsync(string osmBookingId, IReadOnlyList<ItemReplacement> replacements);
}
