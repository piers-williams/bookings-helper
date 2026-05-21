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

    // Auth
    string GetAuthorizationUrl(string redirectUri);
    Task<bool> HandleOAuthCallbackAsync(string code, int userId, string redirectUri);
    Task<bool> IsAuthenticatedAsync(int userId);
}
