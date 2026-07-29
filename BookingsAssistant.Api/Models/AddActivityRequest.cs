namespace BookingsAssistant.Api.Models;

/// <summary>
/// Adds a brand-new activity item to a booking. Unlike MoveActivityRequest/ChangeSiteRequest
/// (which clone an existing item), there is no original item here — every field needed to
/// build the OSM create spec from scratch must be supplied.
/// </summary>
public class AddActivityRequest
{
    /// <summary>The activity item-TYPE id (OSM campsite_item_id) to add, e.g. from GetAvailableActivitiesAsync.</summary>
    public string ActivityId { get; set; } = string.Empty;

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public int? NumberPeople { get; set; }

    /// <summary>Optional free-text note appended to the auto-generated audit comment.</summary>
    public string? Note { get; set; }
}
