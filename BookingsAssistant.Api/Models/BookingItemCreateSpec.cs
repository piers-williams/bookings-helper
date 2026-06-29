namespace BookingsAssistant.Api.Models;

/// <summary>
/// Everything OSM needs to create (clone) a booked item. Built by the mutation engine
/// from an existing item plus any field overrides. The OSM adapter resolves the
/// availability slot and posts the addItem form from these values.
/// </summary>
public class BookingItemCreateSpec
{
    /// <summary>The item-TYPE id (OSM campsite_item_id) used in the addItem URL.</summary>
    public string CampsiteItemId { get; set; } = string.Empty;

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public int? NumberPeople { get; set; }

    /// <summary>
    /// The original item's question answers, keyed by stable question-definition id
    /// (OSM campsite_booking_question_id). Replayed onto the clone after creation.
    /// </summary>
    public Dictionary<int, string> QuestionAnswers { get; set; } = new();
}
