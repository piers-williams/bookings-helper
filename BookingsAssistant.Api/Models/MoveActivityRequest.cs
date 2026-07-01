namespace BookingsAssistant.Api.Models;

public class MoveActivityRequest
{
    public string ItemId { get; set; } = string.Empty;
    public DateTime? NewStartDate { get; set; }
    public string? NewStartTime { get; set; }
    public string? NewEndTime { get; set; }

    /// <summary>Optional free-text note appended to the auto-generated audit comment.</summary>
    public string? Note { get; set; }
}
