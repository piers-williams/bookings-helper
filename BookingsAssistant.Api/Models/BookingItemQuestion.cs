namespace BookingsAssistant.Api.Models;

/// <summary>
/// A booked item's question answer, as carried on the booking-detail response.
/// Keyed by the stable question-definition id so answers can be replayed onto a
/// cloned item (whose per-item answer rows have different ids).
/// </summary>
public class BookingItemQuestion
{
    public int QuestionDefId { get; set; }
    public string Answer { get; set; } = string.Empty;
}
