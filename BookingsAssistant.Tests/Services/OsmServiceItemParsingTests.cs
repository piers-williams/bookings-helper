using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;

namespace BookingsAssistant.Tests.Services;

/// <summary>
/// Unit tests for OsmService.ParseBookingItems against captured OSM response fixtures
/// (BookingsAssistant.Tests/Fixtures/OsmItems/, see README.md). These pin the booked-item
/// shape so the GetBookingItemsAsync seam stays correct.
/// </summary>
public class OsmServiceItemParsingTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "OsmItems", name));

    private static List<BookingItemDto> ParseRealBooking() =>
        OsmService.ParseBookingItems(Fixture("booking-detail-with-items.json"));

    [Fact]
    public void ParseBookingItems_ReturnsBothBookedItems()
        => Assert.Equal(2, ParseRealBooking().Count);

    [Fact]
    public void ParseBookingItems_MapsSiteItem()
    {
        var site = ParseRealBooking().Single(i => i.ItemId == "411467");

        Assert.Equal("site", site.Type);
        Assert.Equal("1387", site.SiteId);          // campsite_item_id → the item-type id used to re-add
        Assert.Null(site.ActivityId);
        Assert.Equal(new DateTime(2027, 12, 4), site.StartDate);
        Assert.Equal(new DateTime(2027, 12, 5), site.EndDate);
        Assert.Equal("00:01", site.StartTime);
        Assert.Equal("23:59", site.EndTime);
        Assert.Equal(20, site.NumberPeople);
        Assert.Equal("Hayvern", site.Label);
    }

    [Fact]
    public void ParseBookingItems_MapsActivityItem_ViaInstructorFields()
    {
        var activity = ParseRealBooking().Single(i => i.ItemId == "411468");

        Assert.Equal("activity", activity.Type);    // discriminated by instructor fields, not the name prefix
        Assert.Equal("4961", activity.ActivityId);
        Assert.Null(activity.SiteId);
        Assert.Equal(new DateTime(2027, 12, 5), activity.StartDate);
        Assert.Equal("09:00", activity.StartTime);
        Assert.Equal("10:00", activity.EndTime);
        Assert.Equal(10, activity.NumberPeople);
        Assert.Equal("ACTIVITY - Air Rifle Shooting", activity.Label);
    }

    [Fact]
    public void ParseBookingItems_ReturnsEmpty_WhenNoItems()
        => Assert.Empty(OsmService.ParseBookingItems(
            """{"status":true,"error":null,"data":{"id":1,"items":[]},"meta":[]}"""));

    [Fact]
    public void ParseBookingItems_ReturnsEmpty_WhenStatusFalse()
        => Assert.Empty(OsmService.ParseBookingItems(
            """{"status":false,"error":"nope","data":null,"meta":[]}"""));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseBookingItems_ReturnsEmpty_ForBlankInput(string? input)
        => Assert.Empty(OsmService.ParseBookingItems(input));
}
