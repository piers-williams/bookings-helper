using System.Net;
using BookingsAssistant.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookingsAssistant.Tests.Services;

/// <summary>
/// Proves the rate-limit cooldown is shared across separate OsmService instances —
/// simulating how AddHttpClient resolves a new OsmService per HTTP request, so a
/// per-instance field would give zero cross-request throttling (see OsmRateLimitCooldown).
/// </summary>
public class OsmRateLimitCooldownTests
{
    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Osm:CampsiteId"] = "1",
                ["Osm:SectionId"] = "2"
            })
            .Build();

    private class FakeOsmAuthService : IOsmAuthService
    {
        public Task<string> GetValidAccessTokenAsync(int userId) => Task.FromResult("fake-token");
        public string GetAuthorizationUrl(string redirectUri) => string.Empty;
        public Task<bool> HandleCallbackAsync(string code, int userId, string redirectUri) => Task.FromResult(true);
    }

    private class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public StubHandler(HttpResponseMessage response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_response);
    }

    private static OsmService CreateService(HttpResponseMessage response, OsmRateLimitCooldown cooldown)
    {
        var httpClient = new HttpClient(new StubHandler(response));
        return new OsmService(httpClient, BuildConfig(), NullLogger<OsmService>.Instance,
            new FakeOsmAuthService(), cooldown);
    }

    private static HttpResponseMessage BookingsResponse(string? remaining = null, string? reset = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"status":true,"data":[]}""")
        };
        if (remaining != null) response.Headers.Add("X-RateLimit-Remaining", remaining);
        if (reset != null) response.Headers.Add("X-RateLimit-Reset", reset);
        return response;
    }

    [Fact]
    public async Task PauseTriggeredByOneInstance_IsVisibleOnTheSharedSingleton()
    {
        // Two separate OsmService instances, as AddHttpClient would resolve for two
        // different HTTP requests, sharing one injected cooldown singleton.
        var cooldown = new OsmRateLimitCooldown();
        var serviceA = CreateService(BookingsResponse(remaining: "3", reset: "30"), cooldown);

        Assert.Null(cooldown.TimeUntilReady());
        await serviceA.GetBookingsAsync("confirmed");

        // The pause instance A observed must land on the shared singleton (not a field
        // local to serviceA), so a second, freshly-constructed instance sees it too.
        var wait = cooldown.TimeUntilReady();
        Assert.NotNull(wait);
        Assert.InRange(wait.Value.TotalSeconds, 1, 30);
    }

    [Fact]
    public async Task SecondFreshInstance_WaitsForCooldownSetByFirstInstance()
    {
        var cooldown = new OsmRateLimitCooldown();

        // Instance A (request #1): OSM reports quota nearly exhausted. Reset window is
        // 1s — the smallest whole-second pause GetProactiveDelay can report — so this
        // test's real wait stays well under a second rather than a multi-second sleep.
        var serviceA = CreateService(BookingsResponse(remaining: "3", reset: "1"), cooldown);
        await serviceA.GetBookingsAsync("confirmed");

        // Instance B (request #2): brand new OsmService, own HttpClient/handler, no
        // local cooldown state of its own — only the shared singleton says to wait.
        var serviceB = CreateService(BookingsResponse(), cooldown);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await serviceB.GetBookingsAsync("confirmed");
        stopwatch.Stop();

        // If the cooldown were still a per-instance field (the bug this fix addresses),
        // instance B would have its own fresh MinValue cooldown and return near-instantly.
        Assert.True(stopwatch.Elapsed.TotalMilliseconds >= 700,
            $"expected instance B to honour instance A's cooldown; elapsed={stopwatch.Elapsed}");
    }

    [Fact]
    public void SeparateCooldownInstances_DoNotShareState()
    {
        // Sanity check on OsmRateLimitCooldown itself: only a shared *instance* (as DI's
        // singleton registration guarantees) propagates the pause — two independent
        // instances behave like the old per-OsmService field.
        var cooldownA = new OsmRateLimitCooldown();
        var cooldownB = new OsmRateLimitCooldown();

        cooldownA.PauseUntil(DateTimeOffset.UtcNow.AddSeconds(30));

        Assert.NotNull(cooldownA.TimeUntilReady());
        Assert.Null(cooldownB.TimeUntilReady());
    }
}
