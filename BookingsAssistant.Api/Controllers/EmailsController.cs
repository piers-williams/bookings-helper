using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailsController : ControllerBase
{
    private readonly ILinkingService _linkingService;
    private readonly ApplicationDbContext _context;

    public EmailsController(ILinkingService linkingService, ApplicationDbContext context)
    {
        _linkingService = linkingService;
        _context = context;
    }

    [HttpGet]
    public ActionResult<List<EmailDto>> GetUnread()
    {
        // Mock data for now
        var emails = new List<EmailDto>
        {
            new EmailDto
            {
                Id = 1,
                SenderEmail = "john@scouts.org.uk",
                SenderName = "John Smith",
                Subject = "Query about booking #12345",
                ReceivedDate = DateTime.UtcNow.AddHours(-2),
                IsRead = false,
                ExtractedBookingRef = "12345"
            },
            new EmailDto
            {
                Id = 2,
                SenderEmail = "jane@school.ac.uk",
                SenderName = "Jane Doe",
                Subject = "Availability for March?",
                ReceivedDate = DateTime.UtcNow.AddHours(-5),
                IsRead = false,
                ExtractedBookingRef = null
            }
        };

        return Ok(emails);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<EmailDetailDto>> GetById(int id)
    {
        // Mock data for now
        var email = new EmailDetailDto
        {
            Id = id,
            MessageId = $"msg-{id}",
            SenderEmail = "john@scouts.org.uk",
            SenderName = "John Smith",
            Subject = "Query about booking #12345",
            ReceivedDate = DateTime.UtcNow.AddHours(-2),
            IsRead = false,
            Body = "Hi, I'd like to confirm the details for booking #12345...",
            ExtractedBookingRef = "12345",
            LinkedBookings = new List<BookingDto>(),
            RelatedEmails = new List<EmailDto>()
        };

        // Fetch linked bookings using the linking service
        var linkedBookingIds = await _linkingService.GetLinkedBookingIdsAsync(id);
        if (linkedBookingIds.Any())
        {
            var linkedBookings = await _context.OsmBookings
                .Where(b => linkedBookingIds.Contains(b.Id))
                .Select(b => new BookingDto
                {
                    Id = b.Id,
                    OsmBookingId = b.OsmBookingId,
                    CustomerName = b.CustomerName,
                    CustomerEmail = b.CustomerEmail,
                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    Status = b.Status
                })
                .ToListAsync();

            email.LinkedBookings = linkedBookings;
        }

        // Fetch related emails (same sender, different ID)
        var relatedEmails = await _context.EmailMessages
            .Where(e => e.SenderEmail == email.SenderEmail && e.Id != id)
            .OrderByDescending(e => e.ReceivedDate)
            .Take(10) // Limit to 10 most recent
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

        email.RelatedEmails = relatedEmails;

        return Ok(email);
    }

    [HttpPost("capture")]
    public async Task<ActionResult<CaptureEmailResponse>> Capture([FromBody] CaptureEmailRequest request)
    {
        // Duplicate detection: same subject + sender + date already captured
        var existing = await _context.EmailMessages.FirstOrDefaultAsync(e =>
            e.Subject == request.Subject &&
            e.SenderEmail == request.SenderEmail &&
            e.ReceivedDate == request.ReceivedDate);

        int emailId;
        if (existing != null)
        {
            emailId = existing.Id;
        }
        else
        {
            var email = new BookingsAssistant.Api.Data.Entities.EmailMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Subject = request.Subject,
                SenderEmail = request.SenderEmail,
                SenderName = request.SenderName,
                ReceivedDate = request.ReceivedDate,
                IsRead = false,
                LastFetched = DateTime.UtcNow
            };
            _context.EmailMessages.Add(email);
            await _context.SaveChangesAsync();
            emailId = email.Id;

            await _linkingService.CreateAutoLinksForEmailAsync(emailId, request.Subject, request.BodyText);
        }

        // Fetch linked bookings
        var linkedBookingIds = await _linkingService.GetLinkedBookingIdsAsync(emailId);
        var linkedBookings = await _context.OsmBookings
            .Where(b => linkedBookingIds.Contains(b.Id))
            .Select(b => new BookingDto
            {
                Id = b.Id,
                OsmBookingId = b.OsmBookingId,
                CustomerName = b.CustomerName,
                CustomerEmail = b.CustomerEmail,
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                Status = b.Status
            })
            .ToListAsync();

        // Suggested bookings: match by sender email if no auto-links found
        var suggestedBookings = new List<BookingDto>();
        if (!linkedBookings.Any())
        {
            suggestedBookings = await _context.OsmBookings
                .Where(b => b.CustomerEmail == request.SenderEmail)
                .Select(b => new BookingDto
                {
                    Id = b.Id,
                    OsmBookingId = b.OsmBookingId,
                    CustomerName = b.CustomerName,
                    CustomerEmail = b.CustomerEmail,
                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    Status = b.Status
                })
                .ToListAsync();
        }

        return Ok(new CaptureEmailResponse
        {
            EmailId = emailId,
            AutoLinked = linkedBookings.Any(),
            LinkedBookings = linkedBookings,
            SuggestedBookings = suggestedBookings
        });
    }
}
