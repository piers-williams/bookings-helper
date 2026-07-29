using System.Net;
using BookingsAssistant.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookingsAssistant.Tests.Services;

/// <summary>
/// Direct HTTP-level coverage for OsmService.CheckAvailabilityAsync: confirms it hits the same
/// per-item availability endpoint as CreateBookingItemAsync's slot-resolution step (see
/// OsmServiceItemMutationTests for ResolveSlotId itself) and reports Available/Reason based on
/// whether a slot covers the requested window. Mirrors the fake-HttpMessageHandler pattern used
/// in OsmServiceGetAvailableActivitiesTests.
/// </summary>
public class OsmServiceCheckAvailabilityTests
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

    private static string Availability1387() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "OsmItems", "availability-1387.json"));

    private static OsmService MakeService(HttpMessageHandler handler) =>
        new(new HttpClient(handler), BuildConfig(), NullLogger<OsmService>.Instance,
            new FakeOsmAuthService(), new OsmRateLimitCooldown());

    [Fact]
    public async Task CheckAvailabilityAsync_RequestsTheItemAvailabilityEndpoint()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Availability1387()) };
        var handler = new RecordingHandler(response);
        var service = MakeService(handler);

        await service.CheckAvailabilityAsync("179743", "1387", new DateTime(2027, 12, 4), new DateTime(2027, 12, 5));

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        var url = handler.LastRequest.RequestUri!.ToString();
        Assert.Contains("/v3/campsites/items/1387/availability", url);
        Assert.Contains("booking_id=179743", url);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_ReturnsAvailableTrue_WhenSlotMatchesDateRange()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Availability1387()) };
        var service = MakeService(new RecordingHandler(response));

        var result = await service.CheckAvailabilityAsync("179743", "1387", new DateTime(2027, 12, 4), new DateTime(2027, 12, 5));

        Assert.True(result.Available);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_ReturnsAvailableFalseWithReason_WhenNoSlotMatchesDateRange()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Availability1387()) };
        var service = MakeService(new RecordingHandler(response));

        var result = await service.CheckAvailabilityAsync("179743", "1387", new DateTime(2030, 1, 1), new DateTime(2030, 1, 2));

        Assert.False(result.Available);
        Assert.NotNull(result.Reason);
        Assert.Contains("2030-01-01", result.Reason);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_Throws_WhenOsmReturnsUnauthorized()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        var service = MakeService(new RecordingHandler(response));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CheckAvailabilityAsync("179743", "1387", new DateTime(2027, 12, 4), new DateTime(2027, 12, 5)));
        Assert.Contains("OSM", ex.Message);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_Throws_WhenOsmReturnsOtherError()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") };
        var service = MakeService(new RecordingHandler(response));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CheckAvailabilityAsync("179743", "1387", new DateTime(2027, 12, 4), new DateTime(2027, 12, 5)));
    }
}
