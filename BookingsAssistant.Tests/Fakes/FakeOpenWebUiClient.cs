using BookingsAssistant.Api.Services;

namespace BookingsAssistant.Tests.Fakes;

/// <summary>
/// Test double for <see cref="IOpenWebUiClient"/>. Captures every (systemPrompt, userPrompt)
/// pair it's called with (so tests can assert on booking context being included) and returns
/// canned responses in order — one entry per call, falling back to <see cref="DefaultResponse"/>
/// once the queue is exhausted (e.g. for the plan-drafting service's single retry).
/// </summary>
public class FakeOpenWebUiClient : IOpenWebUiClient
{
    public List<(string SystemPrompt, string UserPrompt)> Calls { get; } = new();
    public Queue<string> ResponsesToReturn { get; } = new();
    public string DefaultResponse { get; set; } = "{\"actions\":[]}";

    /// <summary>
    /// If set, GetCompletionAsync throws this instead of returning a response — simulates
    /// DraftPlanAsync's underlying HTTP call failing (network error, non-2xx, etc.).
    /// </summary>
    public Exception? ExceptionToThrow { get; set; }

    public Task<string> GetCompletionAsync(string systemPrompt, string userPrompt)
    {
        Calls.Add((systemPrompt, userPrompt));
        if (ExceptionToThrow != null)
            throw ExceptionToThrow;

        var response = ResponsesToReturn.Count > 0 ? ResponsesToReturn.Dequeue() : DefaultResponse;
        return Task.FromResult(response);
    }
}
