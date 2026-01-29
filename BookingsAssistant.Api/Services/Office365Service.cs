using System.Text.RegularExpressions;
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Models;

namespace BookingsAssistant.Api.Services;

public class Office365Service : IOffice365Service
{
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly ILogger<Office365Service> _logger;

    public Office365Service(
        ApplicationDbContext context,
        ITokenService tokenService,
        ILogger<Office365Service> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<List<EmailDto>> GetUnreadEmailsAsync()
    {
        _logger.LogWarning("GetUnreadEmailsAsync is not yet implemented. Graph API integration pending OAuth flow completion.");

        // Return empty list as stub implementation
        return await Task.FromResult(new List<EmailDto>());
    }

    public async Task<(string Body, List<string> BookingRefs)> GetEmailDetailsAsync(string messageId)
    {
        _logger.LogWarning("GetEmailDetailsAsync is not yet implemented. Graph API integration pending OAuth flow completion. MessageId: {MessageId}", messageId);

        // Mock body text for testing the booking reference extraction
        var mockBody = "Thank you for your booking. Your reference is #12345. Please quote this reference for any queries.";

        // Extract booking references from the mock body
        var bookingRefs = ExtractBookingReferences(mockBody);

        return await Task.FromResult((mockBody, bookingRefs));
    }

    private List<string> ExtractBookingReferences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        // Pattern matches:
        // - #12345
        // - Booking #12345
        // - Ref: 12345
        // - REF:12345
        // - Reference 12345
        // - OSM #12345
        var pattern = @"(?:#|Ref:|REF:|Reference|Booking\s*#|OSM\s*#)\s*(\d{4,6})";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);

        var matches = regex.Matches(text);
        var bookingRefs = new List<string>();

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                bookingRefs.Add(match.Groups[1].Value);
            }
        }

        // Return distinct booking references only (no duplicates)
        return bookingRefs.Distinct().ToList();
    }
}
