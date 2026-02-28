namespace BookingsAssistant.Api.Models;

public class SyncResult
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Total => Added + Updated;
    public int CommentsAdded { get; set; }
    public int CommentsUpdated { get; set; }
}
