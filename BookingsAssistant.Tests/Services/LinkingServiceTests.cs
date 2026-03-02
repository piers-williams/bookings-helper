using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookingsAssistant.Tests.Services;

public class LinkingServiceTests
{
    private (ApplicationDbContext context, LinkingService service) CreateService()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);
        var logger = new LoggerFactory().CreateLogger<LinkingService>();
        var service = new LinkingService(context, logger);
        return (context, service);
    }

    private static OsmBooking MakeBooking(string osmBookingId) => new()
    {
        OsmBookingId = osmBookingId,
        CustomerName = "Test Customer",
        StartDate = DateTime.UtcNow.AddDays(10),
        EndDate = DateTime.UtcNow.AddDays(13),
        Status = "Provisional",
        LastFetched = DateTime.UtcNow
    };

    private static EmailMessage MakeEmail(string messageId = "msg-1") => new()
    {
        MessageId = messageId,
        Subject = "Test Subject",
        ReceivedDate = DateTime.UtcNow
    };

    // ── Regex extraction ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAutoLinks_MatchesHashRef()
    {
        var (ctx, svc) = CreateService();
        ctx.OsmBookings.Add(MakeBooking("12345"));
        ctx.EmailMessages.Add(MakeEmail());
        await ctx.SaveChangesAsync();
        var email = await ctx.EmailMessages.FirstAsync();

        await svc.CreateAutoLinksForEmailAsync(email.Id, "", "Your booking #12345 is confirmed.");

        var links = await ctx.ApplicationLinks.ToListAsync();
        Assert.Single(links);
        var booking = await ctx.OsmBookings.FindAsync(links[0].OsmBookingId);
        Assert.Equal("12345", booking!.OsmBookingId);
    }

    [Fact]
    public async Task CreateAutoLinks_MatchesRefColon()
    {
        var (ctx, svc) = CreateService();
        ctx.OsmBookings.Add(MakeBooking("12345"));
        ctx.EmailMessages.Add(MakeEmail());
        await ctx.SaveChangesAsync();
        var email = await ctx.EmailMessages.FirstAsync();

        await svc.CreateAutoLinksForEmailAsync(email.Id, "", "Please quote Ref: 12345 in all correspondence.");

        Assert.Single(await ctx.ApplicationLinks.ToListAsync());
    }

    [Fact]
    public async Task CreateAutoLinks_MatchesREFColon()
    {
        var (ctx, svc) = CreateService();
        ctx.OsmBookings.Add(MakeBooking("12345"));
        ctx.EmailMessages.Add(MakeEmail());
        await ctx.SaveChangesAsync();
        var email = await ctx.EmailMessages.FirstAsync();

        await svc.CreateAutoLinksForEmailAsync(email.Id, "", "REF: 12345 has been received.");

        Assert.Single(await ctx.ApplicationLinks.ToListAsync());
    }

    [Fact]
    public async Task CreateAutoLinks_MatchesReference()
    {
        var (ctx, svc) = CreateService();
        ctx.OsmBookings.Add(MakeBooking("12345"));
        ctx.EmailMessages.Add(MakeEmail());
        await ctx.SaveChangesAsync();
        var email = await ctx.EmailMessages.FirstAsync();

        await svc.CreateAutoLinksForEmailAsync(email.Id, "", "Reference 12345 confirmed.");

        Assert.Single(await ctx.ApplicationLinks.ToListAsync());
    }

    [Fact]
    public async Task CreateAutoLinks_MatchesBookingHash()
    {
        var (ctx, svc) = CreateService();
        ctx.OsmBookings.Add(MakeBooking("12345"));
        ctx.EmailMessages.Add(MakeEmail());
        await ctx.SaveChangesAsync();
        var email = await ctx.EmailMessages.FirstAsync();

        await svc.CreateAutoLinksForEmailAsync(email.Id, "", "Regarding Booking #12345.");

        Assert.Single(await ctx.ApplicationLinks.ToListAsync());
    }

    [Fact]
    public async Task CreateAutoLinks_MatchesOsmHash()
    {
        var (ctx, svc) = CreateService();
        ctx.OsmBookings.Add(MakeBooking("12345"));
        ctx.EmailMessages.Add(MakeEmail());
        await ctx.SaveChangesAsync();
        var email = await ctx.EmailMessages.FirstAsync();

        await svc.CreateAutoLinksForEmailAsync(email.Id, "", "OSM #12345 is now active.");

        Assert.Single(await ctx.ApplicationLinks.ToListAsync());
    }

    [Fact]
    public async Task CreateAutoLinks_MatchesRefInSubject()
    {
        var (ctx, svc) = CreateService();
        ctx.OsmBookings.Add(MakeBooking("12345"));
        ctx.EmailMessages.Add(MakeEmail());
        await ctx.SaveChangesAsync();
        var email = await ctx.EmailMessages.FirstAsync();

        await svc.CreateAutoLinksForEmailAsync(email.Id, "Re: booking #12345", "No ref here.");

        Assert.Single(await ctx.ApplicationLinks.ToListAsync());
    }

    [Fact]
    public async Task CreateAutoLinks_Matches4DigitRef()
    {
        var (ctx, svc) = CreateService();
        ctx.OsmBookings.Add(MakeBooking("1234"));
        ctx.EmailMessages.Add(MakeEmail());
        await ctx.SaveChangesAsync();
        var email = await ctx.EmailMessages.FirstAsync();

        await svc.CreateAutoLinksForEmailAsync(email.Id, "", "Booking #1234 confirmed.");

        Assert.Single(await ctx.ApplicationLinks.ToListAsync());
    }

    [Fact]
    public async Task CreateAutoLinks_Matches6DigitRef()
    {
        var (ctx, svc) = CreateService();
        ctx.OsmBookings.Add(MakeBooking("123456"));
        ctx.EmailMessages.Add(MakeEmail());
        await ctx.SaveChangesAsync();
        var email = await ctx.EmailMessages.FirstAsync();

        await svc.CreateAutoLinksForEmailAsync(email.Id, "", "Booking #123456 confirmed.");

        Assert.Single(await ctx.ApplicationLinks.ToListAsync());
    }

    [Fact]
    public async Task CreateAutoLinks_IgnoresRefs_Under4Digits()
    {
        var (ctx, svc) = CreateService();
        ctx.OsmBookings.Add(MakeBooking("123"));
        ctx.EmailMessages.Add(MakeEmail());
        await ctx.SaveChangesAsync();
        var email = await ctx.EmailMessages.FirstAsync();

        await svc.CreateAutoLinksForEmailAsync(email.Id, "", "Booking #123 should not match.");

        Assert.Empty(await ctx.ApplicationLinks.ToListAsync());
    }

    [Fact]
    public async Task CreateAutoLinks_IgnoresRefs_Over6Digits()
    {
        var (ctx, svc) = CreateService();
        ctx.OsmBookings.Add(MakeBooking("1234567"));
        ctx.EmailMessages.Add(MakeEmail());
        await ctx.SaveChangesAsync();
        var email = await ctx.EmailMessages.FirstAsync();

        await svc.CreateAutoLinksForEmailAsync(email.Id, "", "Booking #1234567 should not match.");

        Assert.Empty(await ctx.ApplicationLinks.ToListAsync());
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAutoLinks_NoMatch_WhenBookingNotInDb()
    {
        var (ctx, svc) = CreateService();
        ctx.EmailMessages.Add(MakeEmail());
        await ctx.SaveChangesAsync();
        var email = await ctx.EmailMessages.FirstAsync();

        // ref is valid format but no matching booking in DB
        await svc.CreateAutoLinksForEmailAsync(email.Id, "", "Booking #99999 not in DB.");

        Assert.Empty(await ctx.ApplicationLinks.ToListAsync());
    }

    [Fact]
    public async Task CreateAutoLinks_NoMatch_WhenNoRefsInText()
    {
        var (ctx, svc) = CreateService();
        ctx.EmailMessages.Add(MakeEmail());
        await ctx.SaveChangesAsync();
        var email = await ctx.EmailMessages.FirstAsync();

        await svc.CreateAutoLinksForEmailAsync(email.Id, "Hello there", "Just a general enquiry with no references.");

        Assert.Empty(await ctx.ApplicationLinks.ToListAsync());
    }

    [Fact]
    public async Task CreateAutoLinks_MultipleMatches_CreatesMultipleLinks()
    {
        var (ctx, svc) = CreateService();
        ctx.OsmBookings.Add(MakeBooking("11111"));
        ctx.OsmBookings.Add(MakeBooking("22222"));
        ctx.EmailMessages.Add(MakeEmail());
        await ctx.SaveChangesAsync();
        var email = await ctx.EmailMessages.FirstAsync();

        await svc.CreateAutoLinksForEmailAsync(email.Id, "", "See bookings #11111 and #22222 for details.");

        Assert.Equal(2, await ctx.ApplicationLinks.CountAsync());
    }

    [Fact]
    public async Task CreateAutoLinks_DuplicateRefs_CreatesOnlyOneLink()
    {
        var (ctx, svc) = CreateService();
        ctx.OsmBookings.Add(MakeBooking("12345"));
        ctx.EmailMessages.Add(MakeEmail());
        await ctx.SaveChangesAsync();
        var email = await ctx.EmailMessages.FirstAsync();

        // Same ref appears twice in the body
        await svc.CreateAutoLinksForEmailAsync(email.Id, "Re: #12345", "Regarding #12345, see booking #12345.");

        Assert.Single(await ctx.ApplicationLinks.ToListAsync());
    }

    [Fact]
    public async Task CreateAutoLinks_SkipsDuplicateLink()
    {
        var (ctx, svc) = CreateService();
        var booking = MakeBooking("12345");
        ctx.OsmBookings.Add(booking);
        var email = MakeEmail();
        ctx.EmailMessages.Add(email);
        await ctx.SaveChangesAsync();

        // Create the link manually first
        ctx.ApplicationLinks.Add(new ApplicationLink
        {
            EmailMessageId = email.Id,
            OsmBookingId = booking.Id,
            CreatedDate = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        // Call again — should not create a duplicate
        await svc.CreateAutoLinksForEmailAsync(email.Id, "", "Booking #12345 confirmed.");

        Assert.Single(await ctx.ApplicationLinks.ToListAsync());
    }

    // ── Hash-based fallback ───────────────────────────────────────────────────

    [Fact]
    public async Task FindSuggested_MatchesBySenderEmailHash()
    {
        var (ctx, svc) = CreateService();
        const string emailHash = "abc123emailhash";
        ctx.OsmBookings.Add(new OsmBooking
        {
            OsmBookingId = "55001",
            CustomerName = "Hash Match",
            CustomerEmailHash = emailHash,
            StartDate = DateTime.UtcNow.AddDays(5),
            EndDate = DateTime.UtcNow.AddDays(7),
            Status = "Provisional",
            LastFetched = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var results = await svc.FindSuggestedBookingIdsAsync(emailHash, new List<string>());

        Assert.Single(results);
    }

    [Fact]
    public async Task FindSuggested_MatchesByNameHash()
    {
        var (ctx, svc) = CreateService();
        const string nameHash = "abc123namehash";
        ctx.OsmBookings.Add(new OsmBooking
        {
            OsmBookingId = "55002",
            CustomerName = "Name Hash Match",
            CustomerNameHash = nameHash,
            StartDate = DateTime.UtcNow.AddDays(5),
            EndDate = DateTime.UtcNow.AddDays(7),
            Status = "Provisional",
            LastFetched = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var results = await svc.FindSuggestedBookingIdsAsync("unrelated-email-hash", new List<string> { nameHash });

        Assert.Single(results);
    }

    [Fact]
    public async Task FindSuggested_ExcludesNoEmailSentinel()
    {
        var (ctx, svc) = CreateService();
        ctx.OsmBookings.Add(new OsmBooking
        {
            OsmBookingId = "55003",
            CustomerName = "No Email Customer",
            CustomerEmailHash = "no-email",
            StartDate = DateTime.UtcNow.AddDays(5),
            EndDate = DateTime.UtcNow.AddDays(7),
            Status = "Provisional",
            LastFetched = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        // Searching with the sentinel value itself should return nothing
        var results = await svc.FindSuggestedBookingIdsAsync("no-email", new List<string>());

        Assert.Empty(results);
    }

    [Fact]
    public async Task FindSuggested_ReturnsDistinct_WhenBothHashesMatch()
    {
        var (ctx, svc) = CreateService();
        const string emailHash = "shared-email-hash";
        const string nameHash = "shared-name-hash";
        ctx.OsmBookings.Add(new OsmBooking
        {
            OsmBookingId = "55004",
            CustomerName = "Both Match",
            CustomerEmailHash = emailHash,
            CustomerNameHash = nameHash,
            StartDate = DateTime.UtcNow.AddDays(5),
            EndDate = DateTime.UtcNow.AddDays(7),
            Status = "Provisional",
            LastFetched = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var results = await svc.FindSuggestedBookingIdsAsync(emailHash, new List<string> { nameHash });

        // Same booking matched by both email hash and name hash — should appear only once
        Assert.Single(results);
    }

    [Fact]
    public async Task FindSuggested_ReturnsEmpty_WhenNoMatches()
    {
        var (ctx, svc) = CreateService();
        ctx.OsmBookings.Add(new OsmBooking
        {
            OsmBookingId = "55005",
            CustomerName = "No Match",
            CustomerEmailHash = "some-other-hash",
            CustomerNameHash = "some-other-name-hash",
            StartDate = DateTime.UtcNow.AddDays(5),
            EndDate = DateTime.UtcNow.AddDays(7),
            Status = "Provisional",
            LastFetched = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var results = await svc.FindSuggestedBookingIdsAsync("no-match-hash", new List<string> { "also-no-match" });

        Assert.Empty(results);
    }

    // ── GetLinkedBookingIds / GetLinkedEmailIds ────────────────────────────────

    [Fact]
    public async Task GetLinkedBookingIds_ReturnsCorrectIds()
    {
        var (ctx, svc) = CreateService();
        var booking1 = MakeBooking("77001");
        var booking2 = MakeBooking("77002");
        var unrelatedBooking = MakeBooking("77003");
        var email = MakeEmail("msg-getbookingids");
        var unrelatedEmail = MakeEmail("msg-unrelated");
        ctx.OsmBookings.AddRange(booking1, booking2, unrelatedBooking);
        ctx.EmailMessages.AddRange(email, unrelatedEmail);
        await ctx.SaveChangesAsync();

        ctx.ApplicationLinks.AddRange(
            new ApplicationLink { EmailMessageId = email.Id, OsmBookingId = booking1.Id, CreatedDate = DateTime.UtcNow },
            new ApplicationLink { EmailMessageId = email.Id, OsmBookingId = booking2.Id, CreatedDate = DateTime.UtcNow },
            new ApplicationLink { EmailMessageId = unrelatedEmail.Id, OsmBookingId = unrelatedBooking.Id, CreatedDate = DateTime.UtcNow }
        );
        await ctx.SaveChangesAsync();

        var result = await svc.GetLinkedBookingIdsAsync(email.Id);

        Assert.Equal(2, result.Count);
        Assert.Contains(booking1.Id, result);
        Assert.Contains(booking2.Id, result);
        Assert.DoesNotContain(unrelatedBooking.Id, result);
    }

    [Fact]
    public async Task GetLinkedEmailIds_ReturnsCorrectIds()
    {
        var (ctx, svc) = CreateService();
        var booking = MakeBooking("77010");
        var email1 = MakeEmail("msg-a");
        var email2 = MakeEmail("msg-b");
        var unrelatedEmail = MakeEmail("msg-c");
        var unrelatedBooking = MakeBooking("77011");
        ctx.OsmBookings.AddRange(booking, unrelatedBooking);
        ctx.EmailMessages.AddRange(email1, email2, unrelatedEmail);
        await ctx.SaveChangesAsync();

        ctx.ApplicationLinks.AddRange(
            new ApplicationLink { EmailMessageId = email1.Id, OsmBookingId = booking.Id, CreatedDate = DateTime.UtcNow },
            new ApplicationLink { EmailMessageId = email2.Id, OsmBookingId = booking.Id, CreatedDate = DateTime.UtcNow },
            new ApplicationLink { EmailMessageId = unrelatedEmail.Id, OsmBookingId = unrelatedBooking.Id, CreatedDate = DateTime.UtcNow }
        );
        await ctx.SaveChangesAsync();

        var result = await svc.GetLinkedEmailIdsAsync(booking.Id);

        Assert.Equal(2, result.Count);
        Assert.Contains(email1.Id, result);
        Assert.Contains(email2.Id, result);
        Assert.DoesNotContain(unrelatedEmail.Id, result);
    }
}
