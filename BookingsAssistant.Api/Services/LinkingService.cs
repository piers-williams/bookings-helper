using System.Text.RegularExpressions;
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Services;

public class LinkingService : ILinkingService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LinkingService> _logger;

    public LinkingService(ApplicationDbContext context, ILogger<LinkingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task CreateAutoLinksForEmailAsync(int emailId, string subject, string body)
    {
        _logger.LogInformation("Creating auto-links for email {EmailId}", emailId);

        // Combine subject and body for extraction
        var text = $"{subject} {body}";
        var bookingReferences = ExtractBookingReferences(text);

        _logger.LogInformation("Extracted {Count} booking references from email {EmailId}: {References}",
            bookingReferences.Count, emailId, string.Join(", ", bookingReferences));

        foreach (var bookingRef in bookingReferences)
        {
            // Find the booking by OsmBookingId
            var booking = await _context.OsmBookings
                .FirstOrDefaultAsync(b => b.OsmBookingId == bookingRef);

            if (booking == null)
            {
                _logger.LogWarning("Booking reference {BookingRef} found in email {EmailId} but booking does not exist in database",
                    bookingRef, emailId);
                continue;
            }

            // Check if link already exists
            var existingLink = await _context.ApplicationLinks
                .FirstOrDefaultAsync(l => l.EmailMessageId == emailId && l.OsmBookingId == booking.Id);

            if (existingLink != null)
            {
                _logger.LogDebug("Link between email {EmailId} and booking {BookingId} already exists, skipping",
                    emailId, booking.Id);
                continue;
            }

            // Create auto-link (CreatedByUserId = null)
            var link = new ApplicationLink
            {
                EmailMessageId = emailId,
                OsmBookingId = booking.Id,
                CreatedByUserId = null, // null indicates auto-linked
                CreatedDate = DateTime.UtcNow
            };

            _context.ApplicationLinks.Add(link);
            _logger.LogInformation("Created auto-link between email {EmailId} and booking {BookingId} (ref: {BookingRef})",
                emailId, booking.Id, bookingRef);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<int>> GetLinkedBookingIdsAsync(int emailId)
    {
        var bookingIds = await _context.ApplicationLinks
            .Where(l => l.EmailMessageId == emailId)
            .Select(l => l.OsmBookingId)
            .ToListAsync();

        _logger.LogDebug("Found {Count} linked bookings for email {EmailId}", bookingIds.Count, emailId);
        return bookingIds;
    }

    public async Task<List<int>> GetLinkedEmailIdsAsync(int bookingId)
    {
        var emailIds = await _context.ApplicationLinks
            .Where(l => l.OsmBookingId == bookingId)
            .Select(l => l.EmailMessageId)
            .ToListAsync();

        _logger.LogDebug("Found {Count} linked emails for booking {BookingId}", emailIds.Count, bookingId);
        return emailIds;
    }

    public async Task<List<int>> FindSuggestedBookingIdsAsync(
        string senderEmailHash, List<string> candidateNameHashes)
    {
        var byEmail = await _context.OsmBookings
            .Where(b => b.CustomerEmailHash == senderEmailHash
                     && b.CustomerEmailHash != "no-email")
            .Select(b => b.Id)
            .ToListAsync();

        var byName = candidateNameHashes.Count > 0
            ? await _context.OsmBookings
                .Where(b => b.CustomerNameHash != null
                         && candidateNameHashes.Contains(b.CustomerNameHash))
                .Select(b => b.Id)
                .ToListAsync()
            : new List<int>();

        return byEmail.Concat(byName).Distinct().ToList();
    }

    private List<string> ExtractBookingReferences(string text)
    {
        // Regex pattern to match booking references
        // Matches patterns like: #12345, Ref: 12345, REF: 12345, Reference 12345, Booking #12345, OSM #12345
        var pattern = @"(?:#|Ref:|REF:|Reference|Booking\s*#|OSM\s*#)\s*(\d{4,6})";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);

        var matches = regex.Matches(text);
        var references = new HashSet<string>(); // Use HashSet to ensure distinct values

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                var reference = match.Groups[1].Value;
                references.Add(reference);
            }
        }

        return references.ToList();
    }
}
