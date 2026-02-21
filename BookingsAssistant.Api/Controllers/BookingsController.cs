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
    private readonly ILinkingService _linkingService;
    private readonly ApplicationDbContext _context;
    private readonly IOsmService _osmService;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(ILinkingService linkingService, ApplicationDbContext context,
        IOsmService osmService, ILogger<BookingsController> logger)
    {
        _linkingService = linkingService;
        _context = context;
        _osmService = osmService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<BookingDto>>> GetProvisional([FromQuery] string? status = "Provisional")
    {
        try
        {
            var bookings = await _osmService.GetBookingsAsync(status ?? "Provisional");

            // Write-through: persist to DB as a side effect so email linking works
            if (bookings.Any())
            {
                try { await UpsertBookingsAsync(bookings); }
                catch (Exception ex) { _logger.LogError(ex, "Failed to cache bookings from OSM"); }
            }

            return Ok(bookings);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("OSM"))
        {
            return Unauthorized(new { message = "OSM authentication required", detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error fetching bookings from OSM", detail = ex.Message });
        }
    }

    [HttpPost("sync")]
    [Microsoft.AspNetCore.Cors.EnableCors("ExtensionCapture")]
    public async Task<ActionResult<SyncResult>> Sync()
    {
        try
        {
            // Fetch provisional and confirmed in parallel
            var provisionalTask = _osmService.GetBookingsAsync("provisional");
            var confirmedTask = _osmService.GetBookingsAsync("confirmed");
            await Task.WhenAll(provisionalTask, confirmedTask);

            // Merge, deduplicating by OsmBookingId (provisional wins if duplicated)
            var allBookings = provisionalTask.Result
                .Concat(confirmedTask.Result)
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
        // Mock data for now
        var booking = new BookingDetailDto
        {
            Id = id,
            OsmBookingId = "12345",
            CustomerName = "John Smith",
            CustomerEmail = "john@scouts.org.uk",
            StartDate = new DateTime(2026, 3, 15),
            EndDate = new DateTime(2026, 3, 17),
            Status = "Provisional",
            FullDetails = "{\"site\": \"Main Field\", \"attendees\": 25}",
            Comments = new List<CommentDto>
            {
                new CommentDto
                {
                    Id = 1,
                    OsmBookingId = "12345",
                    OsmCommentId = "c1",
                    AuthorName = "Tammy",
                    TextPreview = "Called customer to confirm arrival time",
                    CreatedDate = DateTime.UtcNow.AddDays(-1),
                    IsNew = false
                }
            },
            LinkedEmails = new List<EmailDto>()
        };

        var linkedEmailIds = await _linkingService.GetLinkedEmailIdsAsync(id);
        if (linkedEmailIds.Any())
        {
            var linkedEmails = await _context.EmailMessages
                .Where(e => linkedEmailIds.Contains(e.Id))
                .OrderByDescending(e => e.ReceivedDate)
                .Select(e => new EmailDto
                {
                    Id = e.Id,
                    SenderEmail = e.SenderEmail,
                    SenderName = e.SenderName,
                    Subject = e.Subject,
                    ReceivedDate = e.ReceivedDate,
                    IsRead = e.IsRead,
                    ExtractedBookingRef = e.ExtractedBookingRef
                })
                .ToListAsync();

            booking.LinkedEmails = linkedEmails;
        }

        return Ok(booking);
    }

    [HttpGet("{id}/links")]
    [Microsoft.AspNetCore.Cors.EnableCors("ExtensionCapture")]
    public async Task<ActionResult<List<EmailDto>>> GetLinks(int id)
    {
        var linkedEmailIds = await _linkingService.GetLinkedEmailIdsAsync(id);
        if (!linkedEmailIds.Any())
            return Ok(new List<EmailDto>());

        var emails = await _context.EmailMessages
            .Where(e => linkedEmailIds.Contains(e.Id))
            .OrderByDescending(e => e.ReceivedDate)
            .Select(e => new EmailDto
            {
                Id = e.Id,
                SenderEmail = e.SenderEmail,
                SenderName = e.SenderName,
                Subject = e.Subject,
                ReceivedDate = e.ReceivedDate,
                IsRead = e.IsRead,
                ExtractedBookingRef = e.ExtractedBookingRef
            })
            .ToListAsync();

        return Ok(emails);
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
                // CustomerEmail not updated here — populated only via per-booking detail fetch (future phase)
                entity.LastFetched = DateTime.UtcNow;
                updated++;
            }
            else
            {
                // CustomerEmail is intentionally null here: the OSM list API returns group_name but
                // not email. It will be populated in a future phase via per-booking detail fetching.
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
