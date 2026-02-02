namespace BookingsAssistant.Api.Services;

public interface IOsmAuthService
{
    Task<string> GetValidAccessTokenAsync(int userId);
    string GetAuthorizationUrl();
    Task<bool> HandleCallbackAsync(string code, int userId);
}
