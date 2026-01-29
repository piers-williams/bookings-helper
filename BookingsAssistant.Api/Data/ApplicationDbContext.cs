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
    public DbSet<EmailMessage> EmailMessages { get; set; }
    public DbSet<OsmBooking> OsmBookings { get; set; }
    public DbSet<OsmComment> OsmComments { get; set; }
    public DbSet<ApplicationLink> ApplicationLinks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Indexes for performance
        modelBuilder.Entity<EmailMessage>()
            .HasIndex(e => e.MessageId)
            .IsUnique();

        modelBuilder.Entity<OsmBooking>()
            .HasIndex(b => b.OsmBookingId)
            .IsUnique();

        modelBuilder.Entity<OsmComment>()
            .HasIndex(c => c.OsmCommentId)
            .IsUnique();

        modelBuilder.Entity<EmailMessage>()
            .HasIndex(e => e.ExtractedBookingRef);

        modelBuilder.Entity<OsmBooking>()
            .HasIndex(b => b.CustomerEmail);

        // Configure relationships
        modelBuilder.Entity<ApplicationLink>()
            .HasOne(l => l.EmailMessage)
            .WithMany(e => e.Links)
            .HasForeignKey(l => l.EmailMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ApplicationLink>()
            .HasOne(l => l.OsmBooking)
            .WithMany(b => b.Links)
            .HasForeignKey(l => l.OsmBookingId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OsmComment>()
            .HasOne(c => c.Booking)
            .WithMany(b => b.Comments)
            .HasForeignKey(c => c.OsmBookingId)
            .HasPrincipalKey(b => b.OsmBookingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
