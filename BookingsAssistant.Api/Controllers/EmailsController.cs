using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailsController : ControllerBase
{
    private readonly ILinkingService _linkingService;
    private readonly ApplicationDbContext _context;
    private readonly IHashingService _hashingService;

    public EmailsController(ILinkingService linkingService, ApplicationDbContext context, IHashingService hashingService)
    {
        _linkingService = linkingService;
        _context = context;
        _hashingService = hashingService;
    }

    [HttpPost("capture")]
    [Microsoft.AspNetCore.Cors.EnableCors("ExtensionCapture")]
    public async Task<ActionResult<CaptureEmailResponse>> Capture([FromBody] CaptureEmailRequest request)
    {
        var senderEmailHash = _hashingService.HashValue(request.SenderEmail);

        // Duplicate detection: same subject + sender + date already captured
        var existing = await _context.EmailMessages.FirstOrDefaultAsync(e =>
            e.Subject == request.Subject &&
            e.SenderEmailHash == senderEmailHash &&
            e.ReceivedDate == request.ReceivedDate);

        int emailId;
        if (existing != null)
        {
            emailId = existing.Id;
        }
        else
        {
            var email = new BookingsAssistant.Api.Data.Entities.EmailMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                SenderEmailHash = senderEmailHash,
                SenderName = request.SenderName,
                Subject = request.Subject,
                ReceivedDate = request.ReceivedDate,
                IsRead = false,
                LastFetched = DateTime.UtcNow
            };
            _context.EmailMessages.Add(email);
            await _context.SaveChangesAsync();
            emailId = email.Id;

            await _linkingService.CreateAutoLinksForEmailAsync(emailId, request.Subject, request.BodyText);
        }

        // Fetch linked bookings
        var linkedBookingIds = await _linkingService.GetLinkedBookingIdsAsync(emailId);
        var linkedBookings = await _context.OsmBookings
            .Where(b => linkedBookingIds.Contains(b.Id))
            .Select(b => new BookingDto
            {
                Id = b.Id,
                OsmBookingId = b.OsmBookingId,
                CustomerName = b.CustomerName,
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                Status = b.Status
            })
            .ToListAsync();

        // Hash candidate names from the extension
        var candidateNameHashes = request.CandidateNames
            .Select(n => _hashingService.HashValue(n))
            .ToList();

        // Only compute suggestions when there are no confirmed auto-links
        var suggestedIds = linkedBookings.Any()
            ? new List<int>()
            : await _linkingService.FindSuggestedBookingIdsAsync(senderEmailHash, candidateNameHashes);

        var suggestedBookings = suggestedIds.Any()
            ? await _context.OsmBookings
                .Where(b => suggestedIds.Contains(b.Id))
                .Select(b => new BookingDto
                {
                    Id = b.Id,
                    OsmBookingId = b.OsmBookingId,
                    CustomerName = b.CustomerName,
                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    Status = b.Status
                })
                .ToListAsync()
            : new List<BookingDto>();

        return Ok(new CaptureEmailResponse
        {
            EmailId = emailId,
            AutoLinked = linkedBookings.Any(),
            LinkedBookings = linkedBookings,
            SuggestedBookings = suggestedBookings
        });
    }
}
