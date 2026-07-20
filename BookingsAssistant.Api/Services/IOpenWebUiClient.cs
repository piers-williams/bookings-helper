namespace BookingsAssistant.Api.Services;

/// <summary>
/// Thin client for Open WebUI's OpenAI-compatible chat completions endpoint.
/// Mirrors the <see cref="IOsmService"/> pattern: registered via AddHttpClient,
/// one call in, one string out — no retry/validation logic lives here (that's
/// <see cref="IPlanDraftingService"/>'s job).
/// </summary>
public interface IOpenWebUiClient
{
    /// <summary>
    /// Sends a single system/user message pair to the configured model and returns
    /// the raw text content of the model's reply (choices[0].message.content).
    /// </summary>
    Task<string> GetCompletionAsync(string systemPrompt, string userPrompt);
}
