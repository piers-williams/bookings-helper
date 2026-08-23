using BookingsAssistant.Api.Services;

namespace BookingsAssistant.Tests.Services;

/// <summary>
/// Unit tests for OsmService.ParseAvailableActivities against the captured OSM catalogue
/// fixture. "Activities" are the bookable item-types under the "Activities" category
/// (previously excluded on purpose from ParseAvailableSites); category nodes (including
/// activity sub-categories like "Tower" and "Throwing Range") and site item-types are excluded.
/// </summary>
public class OsmServiceAvailableActivitiesTests
{
    private static string Catalogue() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "OsmItems", "items-catalogue-list.json"));

    [Fact]
    public void ParseAvailableActivities_IncludesActivityUnderActivitiesRoot()
    {
        var activities = OsmService.ParseAvailableActivities(Catalogue());
        var archery = activities.SingleOrDefault(a => a.Id == "4962");
        Assert.NotNull(archery);
        Assert.Equal("ACTIVITY - Archery", archery!.Name);
    }

    [Fact]
    public void ParseAvailableActivities_IncludesActivityNestedUnderASubCategory()
        // 8054 "ACTIVITY - Abseiling" is nested under the "Tower" sub-category (10376),
        // itself under "Activities" — confirms arbitrary nesting depth is walked.
        => Assert.Contains(OsmService.ParseAvailableActivities(Catalogue()), a => a.Id == "8054" && a.Name == "ACTIVITY - Abseiling");

    [Fact]
    public void ParseAvailableActivities_ExcludesSites()
    {
        var activities = OsmService.ParseAvailableActivities(Catalogue());
        // 1387 Hayvern, 1404 Birch are site item-types (under Campsites/Indoor Accommodation), not activities.
        Assert.DoesNotContain(activities, a => a.Id is "1387" or "1404");
    }

    [Fact]
    public void ParseAvailableActivities_ExcludesCategoryNodesThemselves()
    {
        var activities = OsmService.ParseAvailableActivities(Catalogue());
        // 10290 Activities, 10376 Tower, 10377 Throwing Range, 10374 Pond are categories, not bookable activities.
        Assert.DoesNotContain(activities, a => a.Id is "10290" or "10376" or "10377" or "10374");
    }

    [Fact]
    public void ParseAvailableActivities_AllEntriesHaveIdAndName()
    {
        var activities = OsmService.ParseAvailableActivities(Catalogue());
        Assert.NotEmpty(activities);
        Assert.All(activities, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Id));
            Assert.False(string.IsNullOrWhiteSpace(a.Name));
        });
    }

    [Fact]
    public void ParseAvailableActivities_ReturnsEmpty_ForBlankInput()
        => Assert.Empty(OsmService.ParseAvailableActivities(""));

    [Fact]
    public void ParseAvailableActivities_HandlesActivitiesNestedUnderASubCategory()
    {
        // Activities(1) → "Water Sports" sub-category(2) → leaf activity(3). The leaf is an
        // activity; the sub-category (itself a parent) is excluded.
        const string json = """
        {"data":[
            {"id":1,"parent_id":0,"name":"Activities"},
            {"id":2,"parent_id":1,"name":"Water Sports"},
            {"id":3,"parent_id":2,"name":"ACTIVITY - Canoeing"}
        ]}
        """;
        var activities = OsmService.ParseAvailableActivities(json);
        Assert.Contains(activities, a => a.Id == "3" && a.Name == "ACTIVITY - Canoeing");
        Assert.DoesNotContain(activities, a => a.Id == "2");
    }
}
