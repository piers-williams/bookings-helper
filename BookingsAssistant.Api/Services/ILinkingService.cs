namespace BookingsAssistant.Api.Services;

public interface ILinkingService
{
    Task CreateAutoLinksForEmailAsync(int emailId, string subject, string body);
    Task<List<int>> GetLinkedBookingIdsAsync(int emailId);
    Task<List<int>> GetLinkedEmailIdsAsync(int bookingId);
    Task<List<int>> FindSuggestedBookingIdsAsync(string senderEmailHash, List<string> candidateNameHashes);
}
