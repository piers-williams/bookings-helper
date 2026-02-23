namespace BookingsAssistant.Api.Models;

public class EmailDto
{
    public int Id { get; set; }
    public string? SenderName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public bool IsRead { get; set; }
    public string? ExtractedBookingRef { get; set; }
}
