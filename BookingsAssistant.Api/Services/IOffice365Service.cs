namespace BookingsAssistant.Api.Services;

public interface IOffice365Service
{
    // OAuth methods
    string GetAuthorizationUrl();
    string GetAdminConsentUrl();
    Task<bool> HandleCallbackAsync(string code, int userId);
    Task<string> GetValidAccessTokenAsync(int userId);

    // Email methods
    Task<List<Models.EmailDto>> GetUnreadEmailsAsync(int userId);
    Task<(string Body, List<string> BookingRefs)> GetEmailDetailsAsync(int userId, string messageId);
}
