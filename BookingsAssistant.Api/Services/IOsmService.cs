using BookingsAssistant.Api.Models;

namespace BookingsAssistant.Api.Services;

public interface IOsmService
{
    Task<List<BookingDto>> GetBookingsAsync(string status);
    Task<(string FullDetails, List<CommentDto> Comments)> GetBookingDetailsAsync(string osmBookingId);
}
