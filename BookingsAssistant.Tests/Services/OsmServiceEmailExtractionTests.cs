using BookingsAssistant.Api.Services;

namespace BookingsAssistant.Tests.Services;

public class OsmServiceEmailExtractionTests
{
    [Fact]
    public void ExtractFirstEmail_FindsAddressInJson()
        => Assert.Equal("scout@example.com",
            OsmService.ExtractFirstEmail("""{ "emails": { "scout@example.com": "A Scout" } }"""));

    [Fact]
    public void ExtractFirstEmail_ReturnsFirstWhenMultiple()
        => Assert.Equal("primary@example.com",
            OsmService.ExtractFirstEmail("""["primary@example.com","secondary@example.org"]"""));

    [Fact]
    public void ExtractFirstEmail_HandlesPlusAndDots()
        => Assert.Equal("first.last+tag@sub.example.co.uk",
            OsmService.ExtractFirstEmail("""{"value":"first.last+tag@sub.example.co.uk"}"""));

    [Fact]
    public void ExtractFirstEmail_FindsAddressInMemberKeyedObject()
        // The real OSM shape: emails keyed by member id (as in sendTemplate).
        => Assert.Equal("prwilliams92@gmail.com",
            OsmService.ExtractFirstEmail(
                """{"3360824":{"firstname":"PIERS","lastname":"WILLIAMS","member_id":3360824,"emails":["prwilliams92@gmail.com"]}}"""));

    [Fact]
    public void ExtractFirstEmail_ReturnsNull_WhenNoEmail()
        => Assert.Null(OsmService.ExtractFirstEmail("""{ "data": [], "status": true }"""));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractFirstEmail_ReturnsNull_ForEmptyInput(string? input)
        => Assert.Null(OsmService.ExtractFirstEmail(input));
}
