namespace BookingsAssistant.Api.Models;

public class CaptureEmailRequest
{
    public string Subject { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public List<string> CandidateNames { get; set; } = new();
}
