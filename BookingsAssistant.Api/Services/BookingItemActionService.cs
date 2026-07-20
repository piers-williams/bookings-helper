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

        Models.CommentDto? posted;
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
