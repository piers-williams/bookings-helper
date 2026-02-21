namespace BookingsAssistant.Api.Models;

public class BookingDetailDto
{
    public int Id { get; set; }
    public string OsmBookingId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FullDetails { get; set; } = string.Empty; // JSON from OSM
    public List<CommentDto> Comments { get; set; } = new();
    public List<EmailDto> LinkedEmails { get; set; } = new();
}
