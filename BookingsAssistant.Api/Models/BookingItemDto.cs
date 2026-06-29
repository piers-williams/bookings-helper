using System.Text.Json.Serialization;

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

    /// <summary>
    /// Number of people on the booked item. Needed to rebuild the create payload
    /// when cloning (OSM's addItem requires number_people).
    /// </summary>
    public int? NumberPeople { get; set; }

    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// The item's question answers (from the booking-detail response). Carried so a
    /// clone can replay them; not part of the public items contract (it could contain
    /// free-text), so excluded from JSON serialisation.
    /// </summary>
    [JsonIgnore]
    public List<BookingItemQuestion> Questions { get; set; } = new();
}
