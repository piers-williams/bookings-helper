namespace BookingsAssistant.Api.Models;

public class CaptureEmailResponse
{
    public int EmailId { get; set; }
    public bool AutoLinked { get; set; }
    public List<BookingDto> LinkedBookings { get; set; } = new();
    public List<BookingDto> SuggestedBookings { get; set; } = new();
}
