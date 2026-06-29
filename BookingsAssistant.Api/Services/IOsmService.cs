using BookingsAssistant.Api.Models;

namespace BookingsAssistant.Api.Services;

public interface IOsmService
{
    // Bookings
    Task<List<BookingDto>> GetBookingsAsync(string status);
    Task<(string FullDetails, List<CommentDto> Comments)> GetBookingDetailsAsync(string osmBookingId);

    // Comments
    Task<CommentDto?> PostCommentAsync(string osmBookingId, string comment);

    // Email
    Task<bool> SendBookingTemplateEmailAsync(string osmBookingId);

    /// <summary>
    /// Resolves the booking's primary contact email (member_id → contacts),
    /// the same path used to send. Returns null when the booking has no
    /// resolvable email. The caller is responsible for not persisting the raw
    /// address (only its hash is stored).
    /// </summary>
    Task<string?> GetBookingContactEmailAsync(string osmBookingId);

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
