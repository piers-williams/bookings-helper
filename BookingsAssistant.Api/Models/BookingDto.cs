namespace BookingsAssistant.Api.Models;

public class BookingDto
{
    public int Id { get; set; }
    public string OsmBookingId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}
