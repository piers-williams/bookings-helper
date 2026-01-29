namespace BookingsAssistant.Api.Models;

public class EmailDetailDto
{
    public int Id { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string? SenderName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public bool IsRead { get; set; }
    public string Body { get; set; } = string.Empty; // Fetched from Office 365 on-demand
    public string? ExtractedBookingRef { get; set; }
    public List<BookingDto> LinkedBookings { get; set; } = new();
    public List<EmailDto> RelatedEmails { get; set; } = new(); // Same sender
}
