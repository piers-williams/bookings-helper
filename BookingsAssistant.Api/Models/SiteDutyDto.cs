namespace BookingsAssistant.Api.Models;

public class SiteDutyDto
{
    public int Id { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? TeamName { get; set; }
    public string? Notes { get; set; }
}

public class CreateSiteDutyRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? TeamName { get; set; }
    public string? Notes { get; set; }
}

public class GenerateScheduleRequest
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}
