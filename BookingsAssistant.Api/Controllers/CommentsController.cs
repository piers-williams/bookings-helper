using BookingsAssistant.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommentsController : ControllerBase
{
    [HttpGet]
    public ActionResult<List<CommentDto>> GetNew([FromQuery] bool? newOnly = true)
    {
        // Mock data for now
        var comments = new List<CommentDto>
        {
            new CommentDto
            {
                Id = 1,
                OsmBookingId = "12345",
                OsmCommentId = "c1",
                AuthorName = "Tammy",
                TextPreview = "Called customer to confirm arrival time",
                CreatedDate = DateTime.UtcNow.AddHours(-3),
                IsNew = true,
                Booking = new BookingDto
                {
                    Id = 1,
                    OsmBookingId = "12345",
                    CustomerName = "John Smith",
                    Status = "Provisional"
                }
            },
            new CommentDto
            {
                Id = 2,
                OsmBookingId = "12340",
                OsmCommentId = "c2",
                AuthorName = "Piers",
                TextPreview = "Deposit received via BACS",
                CreatedDate = DateTime.UtcNow.AddHours(-6),
                IsNew = true,
                Booking = new BookingDto
                {
                    Id = 2,
                    OsmBookingId = "12340",
                    CustomerName = "Sarah Johnson",
                    Status = "Confirmed"
                }
            }
        };

        return Ok(comments);
    }
}
