using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;

namespace BookingsAssistant.Tests.Services;

/// <summary>
/// Unit tests for the OSM item create/delete mapping helpers, against captured fixtures
/// (BookingsAssistant.Tests/Fixtures/OsmItems/, see README.md). These pin the slot-resolution,
/// create-payload, response-parsing, and question-replay logic that the live HTTP methods rely on.
/// </summary>
public class OsmServiceItemMutationTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "OsmItems", name));

    // ── ResolveSlotId ──────────────────────────────────────────────────────────

    [Fact]
    public void ResolveSlotId_MatchesMultiDaySiteSlot_ByDateSpan()
        => Assert.Equal("8279", OsmService.ResolveSlotId(
            Fixture("availability-1387.json"),
            new DateTime(2027, 12, 4), new DateTime(2027, 12, 5)));

    [Fact]
    public void ResolveSlotId_MatchesActivitySlot_OnTargetDay()
        => Assert.Equal("9235", OsmService.ResolveSlotId(
            Fixture("availability-4961.json"),
            new DateTime(2027, 12, 5), new DateTime(2027, 12, 5)));

    [Fact]
    public void ResolveSlotId_ReturnsNull_WhenNoSlotMatchesDateSpan()
        => Assert.Null(OsmService.ResolveSlotId(
            Fixture("availability-1387.json"),
            new DateTime(2030, 1, 1), new DateTime(2030, 1, 2)));

    [Fact]
    public void ResolveSlotId_SkipsUnavailableSlot_AndPicksAvailableMatch()
    {
        // Two slots share the same date span; the first is unavailable and must be skipped.
        const string json = """
        {"status":true,"data":[
            {"start":"2027-12-04 00:01:00","end":"2027-12-05 23:59:00","available":false,"id":111},
            {"start":"2027-12-04 00:01:00","end":"2027-12-05 23:59:00","available":true,"id":222}
        ]}
        """;

        Assert.Equal("222", OsmService.ResolveSlotId(json,
            new DateTime(2027, 12, 4), new DateTime(2027, 12, 5)));
    }

    // ── BuildCreateForm ────────────────────────────────────────────────────────

    [Fact]
    public void BuildCreateForm_ProducesOsmAddItemFields()
    {
        var spec = new BookingItemCreateSpec
        {
            CampsiteItemId = "1387",
            StartDate = new DateTime(2027, 12, 4),
            EndDate = new DateTime(2027, 12, 5),
            StartTime = "00:01",
            EndTime = "23:59",
            NumberPeople = 20
        };

        var form = OsmService.BuildCreateForm(spec, "8279");

        Assert.Equal("8279", form["slot_id"]);
        Assert.Equal("2027-12-04", form["start"]);
        Assert.Equal("2027-12-05", form["end"]);
        Assert.Equal("20", form["number_people"]);
        Assert.Equal("00:01", form["start_time"]);
        Assert.Equal("23:59", form["end_time"]);
    }

    // ── ParseCreatedItemId ─────────────────────────────────────────────────────

    [Fact]
    public void ParseCreatedItemId_ReturnsNewItemId()
        => Assert.Equal("411467", OsmService.ParseCreatedItemId(
            """{"status":true,"error":null,"data":{"id":411467,"item_name":"Hayvern","questions":3},"meta":[]}"""));

    [Fact]
    public void ParseCreatedItemId_Throws_WhenStatusFalse()
        => Assert.Throws<InvalidOperationException>(() => OsmService.ParseCreatedItemId(
            """{"status":false,"error":"insufficient scope","data":null,"meta":[]}"""));

    [Fact]
    public void ParseCreatedItemId_Throws_WhenDataIdMissing()
        => Assert.Throws<InvalidOperationException>(() => OsmService.ParseCreatedItemId(
            """{"status":true,"error":null,"data":{},"meta":[]}"""));

    // ── ParseDeleteSucceeded ───────────────────────────────────────────────────

    [Fact]
    public void ParseDeleteSucceeded_TrueOnStatusTrue()
        => Assert.True(OsmService.ParseDeleteSucceeded("""{"status":true,"error":null,"data":[],"meta":[]}"""));

    [Fact]
    public void ParseDeleteSucceeded_FalseOnStatusFalse()
        => Assert.False(OsmService.ParseDeleteSucceeded("""{"status":false,"error":"nope","data":[],"meta":[]}"""));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseDeleteSucceeded_FalseOnBlankInput(string? input)
        => Assert.False(OsmService.ParseDeleteSucceeded(input));

    // ── ParseItemQuestions ─────────────────────────────────────────────────────

    [Fact]
    public void ParseItemQuestions_ExtractsRowIdQuestionDefAndAnswer()
    {
        var questions = OsmService.ParseItemQuestions(Fixture("questions-get-411467.json"));

        Assert.Equal(3, questions.Count);
        var first = questions[0];
        Assert.Equal(234758, first.RowId);
        Assert.Equal(989, first.QuestionDefId);
        Assert.Equal("", first.Answer);
    }

    // ── BuildAnswersJson (replay) ──────────────────────────────────────────────

    [Fact]
    public void BuildAnswersJson_MapsOriginalAnswersToCloneRowIds_ByQuestionDefId()
    {
        // Original answers keyed by stable question-definition id
        var originalByDefId = new Dictionary<int, string> { [989] = "-", [990] = "-", [991] = "-" };

        // The clone has DIFFERENT row ids for the SAME question definitions
        var cloneQuestions = new List<OsmItemQuestion>
        {
            new(555001, 989, ""),
            new(555002, 990, ""),
            new(555003, 991, "")
        };

        var json = OsmService.BuildAnswersJson(originalByDefId, cloneQuestions);

        // Answers posted against the CLONE's row ids, carrying the original answers
        Assert.Contains("\"id\":555001", json);
        Assert.Contains("\"id\":555002", json);
        Assert.Contains("\"id\":555003", json);
        Assert.Contains("\"answer\":\"-\"", json);
        Assert.DoesNotContain("234758", json); // not the original row ids
    }

    [Fact]
    public void BuildAnswersJson_SkipsQuestions_WithNoOriginalAnswer()
    {
        var originalByDefId = new Dictionary<int, string> { [989] = "yes" };
        var cloneQuestions = new List<OsmItemQuestion>
        {
            new(555001, 989, ""),   // has an original answer → included
            new(555002, 990, "")    // no original answer / blank → skipped
        };

        var json = OsmService.BuildAnswersJson(originalByDefId, cloneQuestions);

        Assert.Contains("\"id\":555001", json);
        Assert.DoesNotContain("555002", json);
    }

    [Fact]
    public void BuildAnswersJson_SkipsQuestion_WhenOriginalAnswerIsEmptyString()
    {
        // An original answer that is present but blank should not be replayed (the
        // clone's row is already blank).
        var originalByDefId = new Dictionary<int, string> { [989] = "" };
        var cloneQuestions = new List<OsmItemQuestion> { new(555001, 989, "") };

        Assert.Equal("[]", OsmService.BuildAnswersJson(originalByDefId, cloneQuestions));
    }
}
