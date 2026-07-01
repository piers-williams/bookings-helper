namespace BookingsAssistant.Api.Models;

public class MoveDatesRequest
{
    public int DayShift { get; set; }

    /// <summary>Optional free-text note appended to the auto-generated audit comment.</summary>
    public string? Note { get; set; }
}
