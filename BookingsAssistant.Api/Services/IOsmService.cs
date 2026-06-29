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
    /// Fetches the line-items (sites and activities) for a booking.
    /// NOTE: OSM item parsing is a deferred seam — the implementation throws
    /// NotImplementedException until real response data is available to wire it up.
    /// </summary>
    Task<List<BookingItemDto>> GetBookingItemsAsync(string osmBookingId);

    /// <summary>
    /// Creates a new booking item by cloning the provided JSON payload.
    /// Returns the new item id assigned by OSM.
    /// NOTE: DEFERRED SEAM — real implementation pending example OSM request/response data.
    /// The real OsmService throws NotImplementedException("... pending example data").
    /// </summary>
    Task<string> CreateBookingItemAsync(string osmBookingId, string cloneJson);

    /// <summary>
    /// Deletes the specified booking item.
    /// Returns true on success, false on failure.
    /// NOTE: DEFERRED SEAM — real implementation pending example OSM request/response data.
    /// The real OsmService throws NotImplementedException("... pending example data").
    /// </summary>
    Task<bool> DeleteBookingItemAsync(string osmBookingId, string itemId);

    // Auth
    string GetAuthorizationUrl(string redirectUri);
    Task<bool> HandleOAuthCallbackAsync(string code, int userId, string redirectUri);
    Task<bool> IsAuthenticatedAsync(int userId);
}
