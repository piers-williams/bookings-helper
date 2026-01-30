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
}
