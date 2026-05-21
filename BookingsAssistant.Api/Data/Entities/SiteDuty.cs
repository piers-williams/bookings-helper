using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingsAssistant.Api.Data.Entities;

[Table("SiteDuties")]
public class SiteDuty
{
    [Key]
    public int Id { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [MaxLength(255)]
    public string? TeamName { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
