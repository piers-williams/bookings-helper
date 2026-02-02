using BookingsAssistant.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Only seed if database is empty
        if (await context.ApplicationUsers.AnyAsync())
            return;

        // Create default user
        var user = new ApplicationUser
        {
            Id = 1,
            Email = "admin@example.com",
            Name = "Admin User"
        };
        context.ApplicationUsers.Add(user);

        // Create mock email messages matching EmailsController mock data
        var emails = new List<EmailMessage>
        {
            new EmailMessage
            {
                Id = 1,
                MessageId = "msg-1",
                SenderEmail = "john@scouts.org.uk",
                SenderName = "John Smith",
                Subject = "Query about booking #12345",
                Body = "Hi, I'd like to confirm the details for booking #12345...",
                ReceivedDate = DateTime.UtcNow.AddHours(-2),
                IsRead = false,
                ExtractedBookingRef = "12345"
            },
            new EmailMessage
            {
                Id = 2,
                MessageId = "msg-2",
                SenderEmail = "jane@school.ac.uk",
                SenderName = "Jane Doe",
                Subject = "Availability for March?",
                Body = "Hello, I'm interested in booking your campsite for March...",
                ReceivedDate = DateTime.UtcNow.AddHours(-5),
                IsRead = false,
                ExtractedBookingRef = null
            },
            new EmailMessage
            {
                Id = 3,
                MessageId = "msg-3",
                SenderEmail = "john@scouts.org.uk",
                SenderName = "John Smith",
                Subject = "Re: Booking confirmation",
                Body = "Thanks for the confirmation. This is regarding REF: 12345",
                ReceivedDate = DateTime.UtcNow.AddDays(-3),
                IsRead = true,
                ExtractedBookingRef = "12345"
            }
        };
        context.EmailMessages.AddRange(emails);

        // Create mock OSM bookings matching BookingsController mock data
        var bookings = new List<OsmBooking>
        {
            new OsmBooking
            {
                Id = 1,
                OsmBookingId = 12345,
                CustomerName = "John Smith",
                CustomerEmail = "john@scouts.org.uk",
                StartDate = DateTime.UtcNow.AddDays(30),
                EndDate = DateTime.UtcNow.AddDays(32),
                Status = "Provisional",
                TotalCost = 450.00m,
                LastUpdated = DateTime.UtcNow.AddHours(-1)
            },
            new OsmBooking
            {
                Id = 2,
                OsmBookingId = 12346,
                CustomerName = "Sarah Johnson",
                CustomerEmail = "sarah@guides.org.uk",
                StartDate = DateTime.UtcNow.AddDays(45),
                EndDate = DateTime.UtcNow.AddDays(47),
                Status = "Confirmed",
                TotalCost = 520.00m,
                LastUpdated = DateTime.UtcNow.AddDays(-2)
            },
            new OsmBooking
            {
                Id = 3,
                OsmBookingId = 12347,
                CustomerName = "Mike Brown",
                CustomerEmail = "mike@school.ac.uk",
                StartDate = DateTime.UtcNow.AddDays(60),
                EndDate = DateTime.UtcNow.AddDays(63),
                Status = "Provisional",
                TotalCost = 780.00m,
                LastUpdated = DateTime.UtcNow.AddHours(-6)
            }
        };
        context.OsmBookings.AddRange(bookings);

        // Create mock OSM comments
        var comments = new List<OsmComment>
        {
            new OsmComment
            {
                Id = 1,
                OsmCommentId = 101,
                OsmBookingId = 12345,
                AuthorName = "System",
                CommentText = "Booking created",
                CreatedDate = DateTime.UtcNow.AddDays(-10),
                IsNew = false
            },
            new OsmComment
            {
                Id = 2,
                OsmCommentId = 102,
                OsmBookingId = 12345,
                AuthorName = "Admin",
                CommentText = "Customer requested early arrival",
                CreatedDate = DateTime.UtcNow.AddHours(-3),
                IsNew = true
            },
            new OsmComment
            {
                Id = 3,
                OsmCommentId = 103,
                OsmBookingId = 12346,
                AuthorName = "System",
                CommentText = "Payment received",
                CreatedDate = DateTime.UtcNow.AddDays(-1),
                IsNew = true
            }
        };
        context.OsmComments.AddRange(comments);

        await context.SaveChangesAsync();
    }
}
