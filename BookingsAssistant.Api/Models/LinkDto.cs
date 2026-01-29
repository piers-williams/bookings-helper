namespace BookingsAssistant.Api.Models;

public class LinkDto
{
    public int Id { get; set; }
    public int EmailMessageId { get; set; }
    public int OsmBookingId { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsAutoLinked => CreatedByUserId == null;
}
