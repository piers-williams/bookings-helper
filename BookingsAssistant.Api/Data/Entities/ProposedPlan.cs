using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingsAssistant.Api.Data.Entities;

public enum PlanStatus
{
    AwaitingApproval,

    /// <summary>
    /// Transient state used only as an atomic "claim" marker: Approve/Reject conditionally
    /// transition AwaitingApproval → Processing in a single ExecuteUpdateAsync round-trip
    /// before doing any work, so two concurrent requests for the same plan can't both pass
    /// the status check and double-execute. Always immediately followed by a terminal status
    /// (Executed/Failed/Rejected) within the same request. Stored as a string column
    /// (see ApplicationDbContext), so adding this value needs no schema migration.
    /// </summary>
    Processing,

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
