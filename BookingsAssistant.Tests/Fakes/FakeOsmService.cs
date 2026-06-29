using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;

namespace BookingsAssistant.Tests.Fakes;

public class FakeOsmService : IOsmService
{
    public List<BookingDto> BookingsToReturn { get; set; } = new();
    public List<BookingDto>? ConfirmedBookingsToReturn { get; set; }
    public Dictionary<string, List<CommentDto>> CommentsByBookingId { get; } = new();
    public Dictionary<string, string> DetailsJsonByBookingId { get; } = new();
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
        var details = DetailsJsonByBookingId.TryGetValue(osmBookingId, out var json)
            ? json
            : string.Empty;
        return Task.FromResult((details, comments));
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

    public Dictionary<string, string?> ContactEmailByBookingId { get; } = new();

    public Task<string?> GetBookingContactEmailAsync(string osmBookingId)
        => Task.FromResult(ContactEmailByBookingId.TryGetValue(osmBookingId, out var email) ? email : null);

    // Items
    public List<BookingItemDto> ItemsToReturn { get; set; } = new();
    public bool ThrowNotImplementedForItems { get; set; }

    public Task<List<BookingItemDto>> GetBookingItemsAsync(string osmBookingId)
    {
        if (ThrowNotImplementedForItems)
            throw new NotImplementedException("OSM item parsing not yet wired — pending example data");
        return Task.FromResult(ItemsToReturn);
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
