using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookingsAssistant.Api.Services;

internal class OpenWebUiClient : IOpenWebUiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenWebUiClient> _logger;
    private readonly string _model;

    public OpenWebUiClient(HttpClient httpClient, IConfiguration configuration, ILogger<OpenWebUiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Follows the Osm:* convention: `??` only catches a missing config key, not an
        // empty string, so the checked-in appsettings.json placeholder ("") is a valid
        // "not yet configured" value that doesn't throw at startup. We still guard the
        // Uri/Authorization-header setup below so an empty BaseUrl/ApiKey never blows up
        // construction (it just means calls will fail later, once actually attempted).
        var baseUrl = configuration["OpenWebUi:BaseUrl"] ?? throw new InvalidOperationException("OpenWebUi BaseUrl not configured");
        var apiKey = configuration["OpenWebUi:ApiKey"] ?? throw new InvalidOperationException("OpenWebUi ApiKey not configured");
        _model = configuration["OpenWebUi:Model"] ?? throw new InvalidOperationException("OpenWebUi Model not configured");

        if (!string.IsNullOrEmpty(baseUrl))
            _httpClient.BaseAddress = new Uri(baseUrl);

        if (!string.IsNullOrEmpty(apiKey))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> GetCompletionAsync(string systemPrompt, string userPrompt)
    {
        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        _logger.LogInformation("Calling Open WebUI chat completions endpoint (model: {Model})", _model);

        var response = await _httpClient.PostAsJsonAsync("/api/chat/completions", requestBody);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var text = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrEmpty(text))
            throw new InvalidOperationException("Open WebUI response contained no message content");

        return text;
    }

    // Only the fields we need from the OpenAI-compatible chat completions response shape.
    private class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatCompletionChoice>? Choices { get; set; }
    }

    private class ChatCompletionChoice
    {
        [JsonPropertyName("message")]
        public ChatCompletionMessage? Message { get; set; }
    }

    private class ChatCompletionMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
