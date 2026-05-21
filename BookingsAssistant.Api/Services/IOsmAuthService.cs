namespace BookingsAssistant.Api.Services;

internal interface IOsmAuthService
{
    Task<string> GetValidAccessTokenAsync(int userId);
    string GetAuthorizationUrl(string redirectUri);
    Task<bool> HandleCallbackAsync(string code, int userId, string redirectUri);
}
