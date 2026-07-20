namespace BookingsAssistant.Api.Models;

public class CreatePlanRequest
{
    public string SourceEmailText { get; set; } = string.Empty;
    public string? OsmBookingId { get; set; }
}
