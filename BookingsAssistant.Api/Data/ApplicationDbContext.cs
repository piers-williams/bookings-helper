using BookingsAssistant.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ApplicationUser> ApplicationUsers { get; set; }
    public DbSet<OsmBooking> OsmBookings { get; set; }
    public DbSet<OsmComment> OsmComments { get; set; }
    public DbSet<SiteDuty> SiteDuties { get; set; }
    public DbSet<ProposedPlan> ProposedPlans { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Indexes for performance
        modelBuilder.Entity<OsmBooking>()
            .HasIndex(b => b.OsmBookingId)
            .IsUnique();

        modelBuilder.Entity<OsmComment>()
            .HasIndex(c => c.OsmCommentId)
            .IsUnique();

        // Configure relationships
        modelBuilder.Entity<OsmComment>()
            .HasOne(c => c.Booking)
            .WithMany(b => b.Comments)
            .HasForeignKey(c => c.OsmBookingId)
            .HasPrincipalKey(b => b.OsmBookingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Store PlanStatus enum as its string name for readability in the DB
        modelBuilder.Entity<ProposedPlan>()
            .Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        modelBuilder.Entity<ProposedPlan>()
            .HasOne(p => p.Booking)
            .WithMany()
            .HasForeignKey(p => p.OsmBookingId)
            .HasPrincipalKey(b => b.OsmBookingId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
