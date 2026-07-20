using System.Net;
using BookingsAssistant.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookingsAssistant.Tests.Services;

/// <summary>
/// Direct coverage for <see cref="OpenWebUiClient"/> itself (the HTTP call, JSON response
/// parsing, EnsureSuccessStatusCode, empty-message-content handling) — everything else in the
/// plan-drafting tests goes through <see cref="Fakes.FakeOpenWebUiClient"/>, which never
/// exercises this class. Mirrors the fake-HttpMessageHandler pattern already used for
/// OsmService in OsmRateLimitCooldownTests.
/// </summary>
public class OpenWebUiClientTests
{
    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenWebUi:BaseUrl"] = "https://openwebui.example.test",
                ["OpenWebUi:ApiKey"] = "test-api-key",
                ["OpenWebUi:Model"] = "test-model"
            })
            .Build();

    private class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public StubHandler(HttpResponseMessage response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_response);
    }

    private static OpenWebUiClient CreateClient(HttpResponseMessage response)
    {
        var httpClient = new HttpClient(new StubHandler(response));
        return new OpenWebUiClient(httpClient, BuildConfig(), NullLogger<OpenWebUiClient>.Instance);
    }

    [Fact]
    public async Task GetCompletionAsync_ReturnsMessageContent_OnSuccessfulResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"choices":[{"message":{"content":"{\"actions\":[]}"}}]}""")
        };
        var client = CreateClient(response);

        var result = await client.GetCompletionAsync("system prompt", "user prompt");

        Assert.Equal("{\"actions\":[]}", result);
    }

    [Fact]
    public async Task GetCompletionAsync_Throws_WhenResponseIsNonSuccessStatusCode()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("upstream error")
        };
        var client = CreateClient(response);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetCompletionAsync("system prompt", "user prompt"));
    }

    [Fact]
    public async Task GetCompletionAsync_Throws_WhenMessageContentIsEmpty()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"choices":[{"message":{"content":""}}]}""")
        };
        var client = CreateClient(response);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetCompletionAsync("system prompt", "user prompt"));
        Assert.Contains("no message content", ex.Message);
    }

    [Fact]
    public async Task GetCompletionAsync_Throws_WhenChoicesArrayIsMissing()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"some-id"}""")
        };
        var client = CreateClient(response);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetCompletionAsync("system prompt", "user prompt"));
        Assert.Contains("no message content", ex.Message);
    }
}
