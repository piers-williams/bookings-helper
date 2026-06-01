using BookingsAssistant.Api.Services;

namespace BookingsAssistant.Tests.Services;

public class BookingDetailBackfillServiceTests
{
    // Regression: the OSM items endpoint returns { "data": [ ... ] } where `data`
    // is an Array. ExtractEmail must not throw InvalidOperationException when it
    // calls TryGetProperty on a non-Object element.
    [Fact]
    public void ExtractEmail_WhenDataIsArray_DoesNotThrowAndFindsEmailByLabel()
    {
        var json = """
        {
            "data": [
                { "label": "Customer name", "value": "Jane Smith" },
                { "label": "Contact email", "value": "jane@example.com" }
            ]
        }
        """;

        var email = BookingDetailBackfillService.ExtractEmail(json);

        Assert.Equal("jane@example.com", email);
    }

    [Fact]
    public void ExtractEmail_WhenDataIsObjectWithContact_ReturnsEmail()
    {
        var json = """
        { "data": { "contact": { "email": "bob@example.com" } } }
        """;

        var email = BookingDetailBackfillService.ExtractEmail(json);

        Assert.Equal("bob@example.com", email);
    }

    [Fact]
    public void ExtractEmail_WhenNoEmailPresent_ReturnsNull()
    {
        var json = """
        { "data": [ { "label": "Customer name", "value": "Jane Smith" } ] }
        """;

        var email = BookingDetailBackfillService.ExtractEmail(json);

        Assert.Null(email);
    }

    [Fact]
    public void ExtractEmail_WhenDataIsArrayWithNonObjectItems_DoesNotThrow()
    {
        var json = """
        { "data": [ "just-a-string", 42, null ] }
        """;

        var email = BookingDetailBackfillService.ExtractEmail(json);

        Assert.Null(email);
    }

    [Fact]
    public void ExtractEmail_WhenInvalidJson_ReturnsNull()
    {
        var email = BookingDetailBackfillService.ExtractEmail("not json");

        Assert.Null(email);
    }
}
