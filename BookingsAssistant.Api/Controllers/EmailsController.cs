using BookingsAssistant.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailsController : ControllerBase
{
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
    public ActionResult<EmailDetailDto> GetById(int id)
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
            LinkedBookings = new List<BookingDto>
            {
                new BookingDto
                {
                    Id = 1,
                    OsmBookingId = "12345",
                    CustomerName = "John Smith",
                    CustomerEmail = "john@scouts.org.uk",
                    StartDate = new DateTime(2026, 3, 15),
                    EndDate = new DateTime(2026, 3, 17),
                    Status = "Provisional"
                }
            },
            RelatedEmails = new List<EmailDto>()
        };

        return Ok(email);
    }
}
