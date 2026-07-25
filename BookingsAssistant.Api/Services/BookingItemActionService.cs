using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Models;

namespace BookingsAssistant.Api.Services;

/// <inheritdoc cref="IBookingItemActionService"/>
public class BookingItemActionService : IBookingItemActionService
{
    private readonly ApplicationDbContext _context;
    private readonly IOsmService _osmService;
    private readonly IBookingMutationService _mutationService;
    private readonly ILogger<BookingItemActionService> _logger;

    public BookingItemActionService(
        ApplicationDbContext context,
        IOsmService osmService,
        IBookingMutationService mutationService,
        ILogger<BookingItemActionService> logger)
    {
        _context = context;
        _osmService = osmService;
        _mutationService = mutationService;
        _logger = logger;
    }

    public async Task<BookingActionResult> MoveActivityAsync(string osmBookingId, MoveActivityRequest request)
    {
        var items = await _osmService.GetBookingItemsAsync(osmBookingId);
        var item = items.FirstOrDefault(i => i.ItemId == request.ItemId)
            ?? throw new BookingItemNotFoundException(request.ItemId, osmBookingId);

        var replacement = new ItemReplacement
        {
            Original = item,
            NewStartDate = request.NewStartDate,
            NewStartTime = request.NewStartTime,
            NewEndTime = request.NewEndTime
        };

        var result = await _mutationService.ReplaceItemsAsync(osmBookingId, new[] { replacement });
        var summary = BookingActionCommentComposer.ComposeMoveActivitySummary(item, request);
        await PostAuditCommentAsync(osmBookingId, result, summary);
        return result;
    }

    public async Task<BookingActionResult> ChangeSiteAsync(string osmBookingId, ChangeSiteRequest request)
    {
        var items = await _osmService.GetBookingItemsAsync(osmBookingId);
        var item = items.FirstOrDefault(i => i.ItemId == request.ItemId)
            ?? throw new BookingItemNotFoundException(request.ItemId, osmBookingId);

        var replacement = new ItemReplacement
        {
            Original = item,
            NewSiteId = request.NewSiteId
        };

        var result = await _mutationService.ReplaceItemsAsync(osmBookingId, new[] { replacement });
        var summary = BookingActionCommentComposer.ComposeChangeSiteSummary(item, request);
        await PostAuditCommentAsync(osmBookingId, result, summary);
        return result;
    }

    public async Task<BookingActionResult> MoveDatesAsync(string osmBookingId, MoveDatesRequest request)
    {
        var items = await _osmService.GetBookingItemsAsync(osmBookingId);

        var replacements = items.Select(item => new ItemReplacement
        {
            Original = item,
            NewStartDate = item.StartDate.HasValue
                ? item.StartDate.Value.AddDays(request.DayShift)
                : null,
            NewEndDate = item.EndDate.HasValue
                ? item.EndDate.Value.AddDays(request.DayShift)
                : null
            // StartTime and EndTime are preserved (not overridden)
        }).ToList();

        var result = await _mutationService.ReplaceItemsAsync(osmBookingId, replacements);
        var summary = BookingActionCommentComposer.ComposeMoveDatesSummary(request);
        await PostAuditCommentAsync(osmBookingId, result, summary);
        return result;
    }

    public async Task<BookingActionResult> AddActivityAsync(string osmBookingId, AddActivityRequest request)
    {
        // No original item here (unlike MoveActivity/ChangeSite/MoveDates, which clone one via
        // IBookingMutationService) — build the create spec straight from the request and create
        // it directly. A create failure propagates as-is; there's nothing created yet to roll back.
        var spec = new BookingItemCreateSpec
        {
            CampsiteItemId = request.ActivityId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            NumberPeople = request.NumberPeople
        };

        var newItemId = await _osmService.CreateBookingItemAsync(osmBookingId, spec);

        var result = new BookingActionResult
        {
            Status = BookingActionStatus.Completed,
            Created = new List<string> { newItemId },
            Deleted = new List<string>(),
            Message = $"Added new activity item {newItemId}.",
            Items = await GetItemsSafeAsync(osmBookingId)
        };

        var summary = BookingActionCommentComposer.ComposeAddActivitySummary(request);
        await PostAuditCommentAsync(osmBookingId, result, summary);
        return result;
    }

    public async Task<BookingActionResult> RemoveActivityAsync(string osmBookingId, RemoveActivityRequest request)
    {
        var items = await _osmService.GetBookingItemsAsync(osmBookingId);
        var item = items.FirstOrDefault(i => i.ItemId == request.ItemId)
            ?? throw new BookingItemNotFoundException(request.ItemId, osmBookingId);

        // No replacement created here (unlike MoveActivity/ChangeSite) — a straight delete of an
        // existing item. A thrown OSM exception (e.g. auth failure) propagates as-is; a `false`
        // return means OSM declined the delete without erroring, which we surface as a Failed
        // result rather than reporting success.
        var deleted = await _osmService.DeleteBookingItemAsync(osmBookingId, item.ItemId);

        var result = deleted
            ? new BookingActionResult
            {
                Status = BookingActionStatus.Completed,
                Created = new List<string>(),
                Deleted = new List<string> { item.ItemId },
                Message = $"Removed '{item.Label}'.",
                Items = await GetItemsSafeAsync(osmBookingId)
            }
            : new BookingActionResult
            {
                Status = BookingActionStatus.Failed,
                Created = new List<string>(),
                Deleted = new List<string>(),
                Message = $"Failed to remove '{item.Label}': OSM declined the delete.",
                Items = await GetItemsSafeAsync(osmBookingId)
            };

        var summary = BookingActionCommentComposer.ComposeRemoveActivitySummary(item, request);
        await PostAuditCommentAsync(osmBookingId, result, summary);
        return result;
    }

    /// <summary>Best-effort fetch of the booking's current items; returns empty on failure rather than throwing.</summary>
    private async Task<List<BookingItemDto>> GetItemsSafeAsync(string osmBookingId)
    {
        try
        {
            return await _osmService.GetBookingItemsAsync(osmBookingId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AddActivityAsync: could not fetch items after operation for booking {BookingId}",
                osmBookingId);
            return new List<BookingItemDto>();
        }
    }

    // ── Audit-trail comment posting ───────────────────────────────────────────
    // Runs after a mutation completes. Posts the given summary as an OSM comment and
    // persists it locally (same shape as BookingsController.PostComment) so it shows up
    // immediately. Only Completed/CompletedWithWarnings results get a comment — a rolled
    // back or failed move has nothing to summarize. A failed comment post never fails the
    // request; it downgrades the result to CompletedWithWarnings instead.
    private async Task PostAuditCommentAsync(string osmBookingId, BookingActionResult result, string summary)
    {
        if (result.Status != BookingActionStatus.Completed && result.Status != BookingActionStatus.CompletedWithWarnings)
            return;

        CommentDto? posted;
        try
        {
            posted = await _osmService.PostCommentAsync(osmBookingId, summary);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "PostAuditCommentAsync: failed to post audit comment for booking {BookingId}",
                osmBookingId);
            posted = null;
        }

        if (posted == null)
        {
            result.Status = BookingActionStatus.CompletedWithWarnings;
            result.Message += "; audit comment failed to post";
            return;
        }

        _context.OsmComments.Add(new Data.Entities.OsmComment
        {
            OsmBookingId = osmBookingId,
            OsmCommentId = posted.OsmCommentId,
            AuthorName = posted.AuthorName,
            TextPreview = posted.TextPreview,
            CreatedDate = posted.CreatedDate,
            IsNew = false,
            LastFetched = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }
}
