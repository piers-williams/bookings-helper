using BookingsAssistant.Api.Models;

namespace BookingsAssistant.Api.Services;

public interface IOsmService
{
    // Bookings
    Task<List<BookingDto>> GetBookingsAsync(string status);

    // Comments
    /// <summary>
    /// Fetches the current comments for a booking directly from OSM.
    /// Returns an empty list if the fetch fails (no way to distinguish
    /// "no comments" from "fetch failed" from the return value alone).
    /// </summary>
    Task<List<CommentDto>> GetBookingCommentsAsync(string osmBookingId);
    Task<CommentDto?> PostCommentAsync(string osmBookingId, string comment);

    // Email
    Task<bool> SendBookingTemplateEmailAsync(string osmBookingId);

    // Items
    /// <summary>
    /// Fetches the line-items (sites and activities) for a booking from the OSM
    /// booking-detail resource.
    /// </summary>
    Task<List<BookingItemDto>> GetBookingItemsAsync(string osmBookingId);

    /// <summary>
    /// Creates a new booking item from the given spec (an existing item with overrides
    /// applied). The adapter resolves the OSM availability slot, posts the addItem form,
    /// and replays the original item's question answers. Returns the new item id.
    /// </summary>
    Task<string> CreateBookingItemAsync(string osmBookingId, BookingItemCreateSpec spec);

    /// <summary>
    /// Deletes the specified booking item. Returns true on success, false on failure.
    /// </summary>
    Task<bool> DeleteBookingItemAsync(string osmBookingId, string itemId);

    /// <summary>
    /// Lists the bookable sites/pitches a booked item could be moved to (for change-site),
    /// sourced from the OSM item-type catalogue.
    /// </summary>
    Task<List<AvailableSiteDto>> GetAvailableSitesAsync(string osmBookingId);

    /// <summary>
    /// Lists the bookable activities that can be added to a booking (for add-activity),
    /// sourced from the same OSM item-type catalogue as <see cref="GetAvailableSitesAsync"/>.
    /// </summary>
    Task<List<AvailableSiteDto>> GetAvailableActivitiesAsync(string osmBookingId);

    /// <summary>
    /// Read-only check of whether an item-type (site or activity) has an available slot for
    /// the given date range. Reuses the same per-item availability endpoint and slot-resolution
    /// logic as <see cref="CreateBookingItemAsync"/>, but never creates anything. A "no slot"
    /// outcome is reported via <see cref="AvailabilityResult.Available"/> = false, not an
    /// exception — only OSM/auth failures throw.
    /// </summary>
    Task<AvailabilityResult> CheckAvailabilityAsync(string osmBookingId, string campsiteItemId, DateTime startDate, DateTime endDate);

    // Auth
    string GetAuthorizationUrl(string redirectUri);
    Task<bool> HandleOAuthCallbackAsync(string code, int userId, string redirectUri);
    Task<bool> IsAuthenticatedAsync(int userId);
}
