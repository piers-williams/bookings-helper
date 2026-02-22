namespace BookingsAssistant.Api.Models;

public class BookingStatsDto
{
    public int OnSiteNow { get; set; }
    public int ArrivingThisWeek { get; set; }
    public int ArrivingNext30Days { get; set; }
    public int Provisional { get; set; }
    public DateTime? LastSynced { get; set; }
}
