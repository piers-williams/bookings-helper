namespace BookingsAssistant.Api.Models;

public class ChangeSiteRequest
{
    public string ItemId { get; set; } = string.Empty;
    public string NewSiteId { get; set; } = string.Empty;
}
