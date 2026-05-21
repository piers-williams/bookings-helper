using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;

namespace BookingsAssistant.Tests.Fakes;

public class FakeOsmService : IOsmService
{
    public List<BookingDto> BookingsToReturn { get; set; } = new();
    public List<BookingDto>? ConfirmedBookingsToReturn { get; set; }
    public Dictionary<string, List<CommentDto>> CommentsByBookingId { get; } = new();
    public CommentDto? CommentToReturn { get; set; }
    public bool ShouldFailSend { get; set; }

    public List<string> EmailsSent { get; } = new();
    public List<(string bookingId, string comment)> CommentsPosted { get; } = new();

    public Task<List<BookingDto>> GetBookingsAsync(string status)
    {
        if (ConfirmedBookingsToReturn != null &&
            status.Equals("confirmed", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(ConfirmedBookingsToReturn);
        return Task.FromResult(BookingsToReturn);
    }

    public Task<(string FullDetails, List<CommentDto> Comments)> GetBookingDetailsAsync(string osmBookingId)
    {
        var comments = CommentsByBookingId.TryGetValue(osmBookingId, out var list)
            ? list
            : new List<CommentDto>();
        return Task.FromResult((string.Empty, comments));
    }

    public Task<CommentDto?> PostCommentAsync(string osmBookingId, string comment)
    {
        CommentsPosted.Add((osmBookingId, comment));
        return Task.FromResult(CommentToReturn);
    }

    public Task<bool> SendBookingTemplateEmailAsync(string osmBookingId)
    {
        if (ShouldFailSend) return Task.FromResult(false);
        EmailsSent.Add(osmBookingId);
        return Task.FromResult(true);
    }

    public string GetAuthorizationUrl(string redirectUri)
    {
        return string.Empty;
    }

    public Task<bool> HandleOAuthCallbackAsync(string code, int userId, string redirectUri)
    {
        return Task.FromResult(true);
    }

    public Task<bool> IsAuthenticatedAsync(int userId)
    {
        return Task.FromResult(false);
    }
}
