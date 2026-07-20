using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IOsmService _osmService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(ApplicationDbContext context,
        IOsmService osmService, IConfiguration configuration, ILogger<BookingsController> logger)
    {
        _context = context;
        _osmService = osmService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<BookingDto>>> GetAll([FromQuery] string? status = null)
    {
        var query = _context.OsmBookings.AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(b => b.Status.ToLower() == status.ToLower());

        var rows = await query
            .OrderBy(b => b.StartDate)
            .Select(b => new
            {
                b.Id,
                b.OsmBookingId,
                b.CustomerName,
                b.StartDate,
                b.EndDate,
                b.Status,
                b.GateCodeSentAt
            })
            .ToListAsync();

        var duties = await _context.SiteDuties.ToListAsync();
        var now = DateTime.UtcNow;
        var daysBefore = _configuration.GetValue("GateCode:DaysBefore", 2);

        var bookings = rows.Select(b =>
        {
            var coveredByDuty = duties.Any(d =>
                d.StartDate < b.StartDate.Date.AddDays(1) && d.EndDate > b.StartDate.Date);

            return new BookingDto
            {
                Id = b.Id,
                OsmBookingId = b.OsmBookingId,
                CustomerName = b.CustomerName,
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                Status = b.Status,
                GateCodeSentAt = b.GateCodeSentAt,
                GateCodeStatus = GateCodeStatusEvaluator.Evaluate(
                    b.Status, b.StartDate, b.GateCodeSentAt,
                    coveredByDuty, now, daysBefore)
            };
        }).ToList();

        return Ok(bookings);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<BookingStatsDto>> GetStats()
    {
        var today = DateTime.UtcNow.Date;

        var stats = new BookingStatsDto
        {
            OnSiteNow = await _context.OsmBookings
                .CountAsync(b => b.Status == "Confirmed"
                              && b.StartDate < today.AddDays(1)
                              && b.EndDate >= today),
            ArrivingThisWeek = await _context.OsmBookings
                .CountAsync(b => b.StartDate >= today
                              && b.StartDate < today.AddDays(8)
                              && b.Status != "Cancelled"
                              && b.Status != "Past"),
            ArrivingNext30Days = await _context.OsmBookings
                .CountAsync(b => b.StartDate >= today
                              && b.StartDate < today.AddDays(31)
                              && b.Status != "Cancelled"
                              && b.Status != "Past"),
            Provisional = await _context.OsmBookings
                .CountAsync(b => b.Status == "Provisional"),
            LastSynced = await _context.OsmBookings
                .MaxAsync(b => (DateTime?)b.LastFetched)
        };

        return Ok(stats);
    }

    [HttpPost("sync")]
    public async Task<ActionResult<SyncResult>> Sync()
    {
        try
        {
            // Fetch all booking statuses in parallel
            var provisionalTask = _osmService.GetBookingsAsync("provisional");
            var confirmedTask   = _osmService.GetBookingsAsync("confirmed");
            var futureTask      = _osmService.GetBookingsAsync("future");
            var pastTask        = _osmService.GetBookingsAsync("past");
            var cancelledTask   = _osmService.GetBookingsAsync("cancelled");
            await Task.WhenAll(provisionalTask, confirmedTask, futureTask, pastTask, cancelledTask);

            // Merge, deduplicating by OsmBookingId (provisional wins if duplicated)
            var allBookings = provisionalTask.Result
                .Concat(confirmedTask.Result)
                .Concat(futureTask.Result)
                .Concat(pastTask.Result)
                .Concat(cancelledTask.Result)
                .GroupBy(b => b.OsmBookingId)
                .Select(g => g.First())
                .ToList();

            var result = await UpsertBookingsAsync(allBookings);

            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("OSM"))
        {
            return Unauthorized(new { message = "OSM authentication required", detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { message = "Error syncing bookings from OSM", detail = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BookingDetailDto>> GetById(int id)
    {
        var booking = await _context.OsmBookings.FindAsync(id);
        if (booking == null)
            return NotFound();

        // Refresh from OSM; on failure/empty response, existing DB rows below are left untouched.
        var freshComments = await _osmService.GetBookingCommentsAsync(booking.OsmBookingId);
        if (freshComments.Count > 0)
        {
            var existingComments = await _context.OsmComments
                .Where(c => c.OsmBookingId == booking.OsmBookingId)
                .ToDictionaryAsync(c => c.OsmCommentId);

            foreach (var comment in freshComments)
            {
                if (existingComments.TryGetValue(comment.OsmCommentId, out var entity))
                {
                    entity.AuthorName = comment.AuthorName;
                    entity.TextPreview = comment.TextPreview;
                    entity.LastFetched = DateTime.UtcNow;
                }
                else
                {
                    _context.OsmComments.Add(new Data.Entities.OsmComment
                    {
                        OsmBookingId = booking.OsmBookingId,
                        OsmCommentId = comment.OsmCommentId,
                        AuthorName = comment.AuthorName,
                        TextPreview = comment.TextPreview,
                        CreatedDate = comment.CreatedDate,
                        IsNew = true,
                        LastFetched = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        // Get comments by OSM booking ID (string)
        var comments = await _context.OsmComments
            .Where(c => c.OsmBookingId == booking.OsmBookingId)
            .OrderByDescending(c => c.CreatedDate)
            .Select(c => new CommentDto
            {
                Id = c.Id,
                OsmBookingId = c.OsmBookingId,
                OsmCommentId = c.OsmCommentId,
                AuthorName = c.AuthorName,
                TextPreview = c.TextPreview ?? string.Empty,
                CreatedDate = c.CreatedDate,
                IsNew = c.IsNew
            })
            .ToListAsync();

        var detail = new BookingDetailDto
        {
            Id = booking.Id,
            OsmBookingId = booking.OsmBookingId,
            CustomerName = booking.CustomerName,
            StartDate = booking.StartDate,
            EndDate = booking.EndDate,
            Status = booking.Status,
            FullDetails = "{}",
            Comments = comments
        };

        return Ok(detail);
    }

    [HttpPost("{id}/comments")]
    public async Task<ActionResult<CommentDto>> PostComment(int id, [FromBody] PostCommentRequest request)
    {
        var booking = await _context.OsmBookings.FindAsync(id);
        if (booking == null) return NotFound();

        // Post to OSM
        var result = await _osmService.PostCommentAsync(booking.OsmBookingId, request.Comment);
        if (result == null) return StatusCode(502, new { message = "Failed to post comment to OSM" });

        // Persist locally
        var entity = new Data.Entities.OsmComment
        {
            OsmBookingId = booking.OsmBookingId,
            OsmCommentId = result.OsmCommentId,
            AuthorName = result.AuthorName,
            TextPreview = result.TextPreview,
            CreatedDate = result.CreatedDate,
            IsNew = false,
            LastFetched = DateTime.UtcNow
        };
        _context.OsmComments.Add(entity);
        await _context.SaveChangesAsync();

        result.Id = entity.Id;
        return Ok(result);
    }

    private async Task<SyncResult> UpsertBookingsAsync(List<BookingDto> bookings)
    {
        var osmIds = bookings.Select(b => b.OsmBookingId).ToList();
        var existing = await _context.OsmBookings
            .Where(b => osmIds.Contains(b.OsmBookingId))
            .ToDictionaryAsync(b => b.OsmBookingId);

        int added = 0, updated = 0;

        foreach (var booking in bookings)
        {
            if (existing.TryGetValue(booking.OsmBookingId, out var entity))
            {
                entity.CustomerName = booking.CustomerName;
                entity.StartDate = booking.StartDate;
                entity.EndDate = booking.EndDate;
                entity.Status = booking.Status;
                entity.LastFetched = DateTime.UtcNow;
                updated++;
            }
            else
            {
                _context.OsmBookings.Add(new Data.Entities.OsmBooking
                {
                    OsmBookingId = booking.OsmBookingId,
                    CustomerName = booking.CustomerName,
                    StartDate = booking.StartDate,
                    EndDate = booking.EndDate,
                    Status = booking.Status,
                    LastFetched = DateTime.UtcNow
                });
                added++;
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("OSM sync: {Added} added, {Updated} updated", added, updated);
        return new SyncResult { Added = added, Updated = updated };
    }
}
