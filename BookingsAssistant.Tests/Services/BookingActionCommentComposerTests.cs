using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using Xunit;

namespace BookingsAssistant.Tests.Services;

public class BookingActionCommentComposerTests
{
    private static BookingItemDto MakeSiteItem() => new()
    {
        ItemId = "site-item-1",
        Type = "site",
        SiteId = "site-42",
        Label = "Pitch 4"
    };

    private static BookingItemDto MakeActivityItem() => new()
    {
        ItemId = "act-item-1",
        Type = "activity",
        ActivityId = "act-10",
        Label = "Archery Session",
        StartDate = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
        StartTime = "10:00",
        EndTime = "12:00"
    };

    [Fact]
    public void ComposeChangeSiteSummary_UsesNewSiteName_WhenProvided()
    {
        var summary = BookingActionCommentComposer.ComposeChangeSiteSummary(
            MakeSiteItem(),
            new ChangeSiteRequest { ItemId = "site-item-1", NewSiteId = "site-99", NewSiteName = "Pitch 7" });

        Assert.Equal("Site changed: Pitch 4 → Pitch 7.", summary);
    }

    [Fact]
    public void ComposeChangeSiteSummary_FallsBackToSiteId_WhenNewSiteNameMissing()
    {
        var summary = BookingActionCommentComposer.ComposeChangeSiteSummary(
            MakeSiteItem(),
            new ChangeSiteRequest { ItemId = "site-item-1", NewSiteId = "site-99" });

        Assert.Equal("Site changed: Pitch 4 → site-99.", summary);
    }

    [Fact]
    public void ComposeChangeSiteSummary_AppendsNote_WhenProvided()
    {
        var summary = BookingActionCommentComposer.ComposeChangeSiteSummary(
            MakeSiteItem(),
            new ChangeSiteRequest
            {
                ItemId = "site-item-1",
                NewSiteId = "site-99",
                NewSiteName = "Pitch 7",
                Note = "customer requested closer pitch"
            });

        Assert.Equal("Site changed: Pitch 4 → Pitch 7. Note: customer requested closer pitch", summary);
    }

    [Fact]
    public void ComposeMoveActivitySummary_DescribesOnlyChangedFields()
    {
        var summary = BookingActionCommentComposer.ComposeMoveActivitySummary(
            MakeActivityItem(),
            new MoveActivityRequest { ItemId = "act-item-1", NewStartTime = "14:00", NewEndTime = "16:00" });

        Assert.Equal(
            "Moved 'Archery Session': start time 10:00 → 14:00, end time 12:00 → 16:00.",
            summary);
    }

    [Fact]
    public void ComposeMoveActivitySummary_DescribesDateChange()
    {
        var summary = BookingActionCommentComposer.ComposeMoveActivitySummary(
            MakeActivityItem(),
            new MoveActivityRequest
            {
                ItemId = "act-item-1",
                NewStartDate = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc)
            });

        Assert.Equal("Moved 'Archery Session': date 2 Aug 2026 → 3 Aug 2026.", summary);
    }

    [Fact]
    public void ComposeRemoveActivitySummary_DescribesRemovedItem()
    {
        var summary = BookingActionCommentComposer.ComposeRemoveActivitySummary(
            MakeActivityItem(),
            new RemoveActivityRequest { ItemId = "act-item-1" });

        Assert.Equal("Removed 'Archery Session'.", summary);
    }

    [Fact]
    public void ComposeRemoveActivitySummary_AppendsNote_WhenProvided()
    {
        var summary = BookingActionCommentComposer.ComposeRemoveActivitySummary(
            MakeActivityItem(),
            new RemoveActivityRequest { ItemId = "act-item-1", Note = "customer cancelled this session" });

        Assert.Equal("Removed 'Archery Session'. Note: customer cancelled this session", summary);
    }

    [Fact]
    public void ComposeChangeNumbersSummary_DescribesNumberChange()
    {
        var activity = MakeActivityItem();
        activity.NumberPeople = 4;

        var summary = BookingActionCommentComposer.ComposeChangeNumbersSummary(
            activity,
            new ChangeNumbersRequest { ItemId = "act-item-1", NewNumberPeople = 10 });

        Assert.Equal("Number of people changed for 'Archery Session': 4 → 10.", summary);
    }

    [Fact]
    public void ComposeChangeNumbersSummary_AppendsNote_WhenProvided()
    {
        var activity = MakeActivityItem();
        activity.NumberPeople = 4;

        var summary = BookingActionCommentComposer.ComposeChangeNumbersSummary(
            activity,
            new ChangeNumbersRequest { ItemId = "act-item-1", NewNumberPeople = 10, Note = "two more joined" });

        Assert.Equal("Number of people changed for 'Archery Session': 4 → 10. Note: two more joined", summary);
    }

    [Fact]
    public void ComposeMoveDatesSummary_DescribesShift()
    {
        var summary = BookingActionCommentComposer.ComposeMoveDatesSummary(
            new MoveDatesRequest { DayShift = 7 });

        Assert.Equal("Dates shifted by 7 day(s).", summary);
    }

    [Fact]
    public void ComposeMoveDatesSummary_AppendsNote_WhenProvided()
    {
        var summary = BookingActionCommentComposer.ComposeMoveDatesSummary(
            new MoveDatesRequest { DayShift = -2, Note = "weather forecast" });

        Assert.Equal("Dates shifted by -2 day(s). Note: weather forecast", summary);
    }
}
