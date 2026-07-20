using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingsAssistant.Api.Data.Entities;

public enum PlanStatus
{
    AwaitingApproval,
    Executed,
    Rejected,
    Failed
}

[Table("ProposedPlans")]
public class ProposedPlan
{
    [Key]
    public int Id { get; set; }

    [Required]
    public PlanStatus Status { get; set; } = PlanStatus.AwaitingApproval;

    public string? SourceEmailText { get; set; }

    [MaxLength(50)]
    public string? OsmBookingId { get; set; }

    public string? ActionsJson { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    // Navigation property
    [ForeignKey(nameof(OsmBookingId))]
    public OsmBooking? Booking { get; set; }
}
