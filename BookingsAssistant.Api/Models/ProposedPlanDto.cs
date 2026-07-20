namespace BookingsAssistant.Api.Models;

public class ProposedPlanDto
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? SourceEmailText { get; set; }
    public string? OsmBookingId { get; set; }
    public string? ActionsJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
