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

    public Task<List<BookingItemDto>> GetBookingItemsAsync(string osmBookingId)
        => Task.FromResult(ItemsToReturn);

    // Create / Delete — configurable for mutation service tests
    public List<string> CreatedItemIds { get; set; } = new();
    private int _createCallCount;

    /// <summary>
    /// Captures each spec passed to CreateBookingItemAsync (in call order).
    /// </summary>
    public List<BookingItemCreateSpec> CapturedSpecs { get; } = new();

    /// <summary>
    /// List of (osmBookingId, itemId) calls to DeleteBookingItemAsync.
    /// </summary>
    public List<(string OsmBookingId, string ItemId)> DeletedItems { get; } = new();

    /// <summary>
    /// Combined ordered call log — entries are either ("create", newItemId) or ("delete", itemId).
    /// Lets tests assert that all creates happen before any deletes.
    /// </summary>
    public List<(string Op, string ItemId)> CallLog { get; } = new();

    /// <summary>
    /// If set, the Nth create call (1-based) will throw this exception.
    /// </summary>
    public (int CallNumber, Exception Error)? FailCreateOnCall { get; set; }

    /// <summary>
    /// Item ids whose delete should return false instead of true.
    /// </summary>
    public HashSet<string> DeleteReturnFalseForIds { get; } = new();

    /// <summary>
    /// Item ids whose delete should throw.
    /// </summary>
    public HashSet<string> DeleteThrowForIds { get; } = new();

    public Task<string> CreateBookingItemAsync(string osmBookingId, BookingItemCreateSpec spec)
    {
        _createCallCount++;
        if (FailCreateOnCall is { } fail && fail.CallNumber == _createCallCount)
            throw fail.Error;

        CapturedSpecs.Add(spec);
        var newId = _createCallCount <= CreatedItemIds.Count
            ? CreatedItemIds[_createCallCount - 1]
            : $"new-item-{_createCallCount}";
        CallLog.Add(("create", newId));
        return Task.FromResult(newId);
    }

    public Task<bool> DeleteBookingItemAsync(string osmBookingId, string itemId)
    {
        if (DeleteThrowForIds.Contains(itemId))
            throw new InvalidOperationException($"Fake: delete forced to throw for item {itemId}");
        CallLog.Add(("delete", itemId));
        DeletedItems.Add((osmBookingId, itemId));
        return Task.FromResult(!DeleteReturnFalseForIds.Contains(itemId));
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
