using BookingsAssistant.Api.Data.Entities;

namespace BookingsAssistant.Api.Services;

public interface ITokenService
{
    Task<string> GetValidAccessTokenAsync(ApplicationUser user);
    Task SaveTokensAsync(ApplicationUser user, string accessToken, string refreshToken, DateTime expiry);
}
