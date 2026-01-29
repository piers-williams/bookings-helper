namespace BookingsAssistant.Api.Services;

public interface IOffice365Service
{
    Task<List<Models.EmailDto>> GetUnreadEmailsAsync();
    Task<(string Body, List<string> BookingRefs)> GetEmailDetailsAsync(string messageId);
}
