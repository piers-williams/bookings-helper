namespace BookingsAssistant.Api.Models;

public class CommentDto
{
    public int Id { get; set; }
    public string OsmBookingId { get; set; } = string.Empty;
    public string OsmCommentId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string TextPreview { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public bool IsNew { get; set; }
    public BookingDto? Booking { get; set; }
}
