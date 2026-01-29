using BookingsAssistant.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    [HttpGet]
    public ActionResult<List<BookingDto>> GetProvisional([FromQuery] string? status = "Provisional")
    {
        // Mock data for now
        var bookings = new List<BookingDto>
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
            },
            new BookingDto
            {
                Id = 2,
                OsmBookingId = "12346",
                CustomerName = "Jane Doe",
                CustomerEmail = "jane@school.ac.uk",
                StartDate = new DateTime(2026, 4, 10),
                EndDate = new DateTime(2026, 4, 12),
                Status = "Provisional"
            }
        };

        return Ok(bookings);
    }

    [HttpGet("{id}")]
    public ActionResult<BookingDetailDto> GetById(int id)
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

        return Ok(booking);
    }
}
