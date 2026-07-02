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

    // Auth
    string GetAuthorizationUrl(string redirectUri);
    Task<bool> HandleOAuthCallbackAsync(string code, int userId, string redirectUri);
    Task<bool> IsAuthenticatedAsync(int userId);
}
