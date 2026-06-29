using BookingsAssistant.Api.Services;

namespace BookingsAssistant.Tests.Services;

/// <summary>
/// Unit tests for OsmService.ParseAvailableSites against the captured OSM catalogue fixture.
/// "Sites" are the bookable item-types under the "Campsites" / "Indoor Accommodation" categories;
/// activities and the category nodes themselves are excluded.
/// </summary>
public class OsmServiceAvailableSitesTests
{
    private static string Catalogue() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "OsmItems", "items-catalogue-list.json"));

    [Fact]
    public void ParseAvailableSites_IncludesIndoorAccommodationSite()
    {
        var sites = OsmService.ParseAvailableSites(Catalogue());
        var hayvern = sites.SingleOrDefault(s => s.Id == "1387");
        Assert.NotNull(hayvern);
        Assert.Equal("Hayvern", hayvern!.Name);
    }

    [Fact]
    public void ParseAvailableSites_IncludesCampsitePitch()
        => Assert.Contains(OsmService.ParseAvailableSites(Catalogue()), s => s.Id == "1404" && s.Name == "Birch");

    [Fact]
    public void ParseAvailableSites_ExcludesActivities()
    {
        var sites = OsmService.ParseAvailableSites(Catalogue());
        // 4962 = "ACTIVITY - Archery" (under the Activities category)
        Assert.DoesNotContain(sites, s => s.Id == "4962");
    }

    [Fact]
    public void ParseAvailableSites_ExcludesCategoryNodesThemselves()
    {
        var sites = OsmService.ParseAvailableSites(Catalogue());
        // 3868 Campsites, 3867 Indoor Accommodation, 10290 Activities are categories, not bookable sites
        Assert.DoesNotContain(sites, s => s.Id is "3868" or "3867" or "10290");
    }

    [Fact]
    public void ParseAvailableSites_AllEntriesHaveIdAndName()
    {
        var sites = OsmService.ParseAvailableSites(Catalogue());
        Assert.NotEmpty(sites);
        Assert.All(sites, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Id));
            Assert.False(string.IsNullOrWhiteSpace(s.Name));
        });
    }

    [Fact]
    public void ParseAvailableSites_ReturnsEmpty_ForBlankInput()
        => Assert.Empty(OsmService.ParseAvailableSites(""));
}
