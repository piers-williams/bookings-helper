namespace BookingsAssistant.Api.Models;

public class BookingItemDto
{
    public string ItemId { get; set; } = string.Empty;

    /// <summary>
    /// "site" or "activity"
    /// </summary>
    public string Type { get; set; } = string.Empty;

    public string? SiteId { get; set; }
    public string? ActivityId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string Label { get; set; } = string.Empty;
}
