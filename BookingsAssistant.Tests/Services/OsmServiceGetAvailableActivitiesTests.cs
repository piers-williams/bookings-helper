using System.Net;
using BookingsAssistant.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookingsAssistant.Tests.Services;

/// <summary>
/// Direct HTTP-level coverage for OsmService.GetAvailableActivitiesAsync: confirms it hits the
/// same catalogue endpoint as GetAvailableSitesAsync and returns the activities parsed by
/// ParseAvailableActivities. Mirrors the fake-HttpMessageHandler pattern used in
/// OsmRateLimitCooldownTests / OpenWebUiClientTests — everywhere else, OsmService is exercised
/// via FakeOsmService, which never calls the real HTTP path.
/// </summary>
public class OsmServiceGetAvailableActivitiesTests
{
    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Osm:CampsiteId"] = "219",
                ["Osm:SectionId"] = "2"
            })
            .Build();

    private class FakeOsmAuthService : IOsmAuthService
    {
        public Task<string> GetValidAccessTokenAsync(int userId) => Task.FromResult("fake-token");
        public string GetAuthorizationUrl(string redirectUri) => string.Empty;
        public Task<bool> HandleCallbackAsync(string code, int userId, string redirectUri) => Task.FromResult(true);
    }

    private class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public HttpRequestMessage? LastRequest { get; private set; }
        public RecordingHandler(HttpResponseMessage response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(_response);
        }
    }

    private static string Catalogue() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "OsmItems", "items-catalogue-list.json"));

    [Fact]
    public async Task GetAvailableActivitiesAsync_RequestsTheCampsiteItemsCatalogueEndpoint_AndReturnsParsedActivities()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Catalogue()) };
        var handler = new RecordingHandler(response);
        var service = new OsmService(new HttpClient(handler), BuildConfig(), NullLogger<OsmService>.Instance,
            new FakeOsmAuthService(), new OsmRateLimitCooldown());

        var activities = await service.GetAvailableActivitiesAsync("179743");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        var url = handler.LastRequest.RequestUri!.ToString();
        Assert.Contains("/v3/campsites/219/items", url);
        Assert.Contains("booking_id=179743", url);
        Assert.Contains("mode=booking", url);

        Assert.Contains(activities, a => a.Id == "4962" && a.Name == "ACTIVITY - Archery");
        Assert.DoesNotContain(activities, a => a.Id == "1387"); // a site, not an activity
    }

    [Fact]
    public async Task GetAvailableActivitiesAsync_Throws_WhenOsmReturnsUnauthorized()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        var handler = new RecordingHandler(response);
        var service = new OsmService(new HttpClient(handler), BuildConfig(), NullLogger<OsmService>.Instance,
            new FakeOsmAuthService(), new OsmRateLimitCooldown());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetAvailableActivitiesAsync("179743"));
        Assert.Contains("OSM", ex.Message);
    }
}
