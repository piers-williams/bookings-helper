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
            Name = "Admin User",
            Office365Email = "admin@example.com"
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
                OsmBookingId = "12345",
                CustomerName = "John Smith",
                CustomerEmail = "john@scouts.org.uk",
                StartDate = DateTime.UtcNow.AddDays(30),
                EndDate = DateTime.UtcNow.AddDays(32),
                Status = "Provisional"
            },
            new OsmBooking
            {
                Id = 2,
                OsmBookingId = "12346",
                CustomerName = "Sarah Johnson",
                CustomerEmail = "sarah@guides.org.uk",
                StartDate = DateTime.UtcNow.AddDays(45),
                EndDate = DateTime.UtcNow.AddDays(47),
                Status = "Confirmed"
            },
            new OsmBooking
            {
                Id = 3,
                OsmBookingId = "12347",
                CustomerName = "Mike Brown",
                CustomerEmail = "mike@school.ac.uk",
                StartDate = DateTime.UtcNow.AddDays(60),
                EndDate = DateTime.UtcNow.AddDays(63),
                Status = "Provisional"
            }
        };
        context.OsmBookings.AddRange(bookings);

        // Create mock OSM comments
        var comments = new List<OsmComment>
        {
            new OsmComment
            {
                Id = 1,
                OsmCommentId = "101",
                OsmBookingId = "12345",
                AuthorName = "System",
                TextPreview = "Booking created",
                CreatedDate = DateTime.UtcNow.AddDays(-10),
                IsNew = false
            },
            new OsmComment
            {
                Id = 2,
                OsmCommentId = "102",
                OsmBookingId = "12345",
                AuthorName = "Admin",
                TextPreview = "Customer requested early arrival",
                CreatedDate = DateTime.UtcNow.AddHours(-3),
                IsNew = true
            },
            new OsmComment
            {
                Id = 3,
                OsmCommentId = "103",
                OsmBookingId = "12346",
                AuthorName = "System",
                TextPreview = "Payment received",
                CreatedDate = DateTime.UtcNow.AddDays(-1),
                IsNew = true
            }
        };
        context.OsmComments.AddRange(comments);

        await context.SaveChangesAsync();
    }
}
