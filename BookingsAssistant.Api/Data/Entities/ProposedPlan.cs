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

    /// <summary>
    /// Per-action execution outcomes, set once the plan has been approved and executed
    /// (or the attempt failed partway through). JSON array of objects shaped like
    /// { type, status, reason? } — one entry per action in ActionsJson, in the same order.
    /// Null until the plan has been approved.
    /// </summary>
    public string? ExecutionResultJson { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    // Navigation property
    [ForeignKey(nameof(OsmBookingId))]
    public OsmBooking? Booking { get; set; }
}
