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

    public BookingsController(ILinkingService linkingService, ApplicationDbContext context, IOsmService osmService)
    {
        _linkingService = linkingService;
        _context = context;
        _osmService = osmService;
    }

    [HttpGet]
    public async Task<ActionResult<List<BookingDto>>> GetProvisional([FromQuery] string? status = "Provisional")
    {
        try
        {
            // Fetch real bookings from OSM API
            var bookings = await _osmService.GetBookingsAsync(status ?? "Provisional");
            return Ok(bookings);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("OSM"))
        {
            // OAuth not configured or token expired
            return Unauthorized(new { message = "OSM authentication required", detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error fetching bookings from OSM", detail = ex.Message });
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

        // Fetch linked emails using the linking service
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
}
