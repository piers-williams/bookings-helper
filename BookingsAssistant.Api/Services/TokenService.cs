using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using Microsoft.AspNetCore.DataProtection;

namespace BookingsAssistant.Api.Services;

public class TokenService : ITokenService
{
    private readonly ApplicationDbContext _context;
    private readonly IDataProtector _protector;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        ApplicationDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration configuration,
        ILogger<TokenService> logger)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector("OAuth2Tokens");
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GetValidAccessTokenAsync(ApplicationUser user)
    {
        // Check if token exists and is still valid
        if (string.IsNullOrEmpty(user.Office365AccessToken) ||
            user.Office365TokenExpiry == null ||
            user.Office365TokenExpiry <= DateTime.UtcNow.AddMinutes(5))
        {
            _logger.LogInformation("Access token expired or expiring soon for user {UserId}, refreshing...", user.Id);

            // Token expired or expiring soon, refresh it
            if (string.IsNullOrEmpty(user.Office365RefreshToken))
            {
                throw new InvalidOperationException("No refresh token available. User needs to re-authenticate.");
            }

            await RefreshAccessTokenAsync(user);
        }

        // Decrypt and return the access token
        return _protector.Unprotect(user.Office365AccessToken);
    }

    public async Task SaveTokensAsync(ApplicationUser user, string accessToken, string refreshToken, DateTime expiry)
    {
        _logger.LogInformation("Saving tokens for user {UserId}", user.Id);

        // Encrypt tokens before storing
        user.Office365AccessToken = _protector.Protect(accessToken);
        user.Office365RefreshToken = _protector.Protect(refreshToken);
        user.Office365TokenExpiry = expiry;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Tokens saved successfully for user {UserId}", user.Id);
    }

    private async Task RefreshAccessTokenAsync(ApplicationUser user)
    {
        // This method will be implemented once Azure AD app is registered
        // and MSAL configuration is added
        throw new NotImplementedException(
            "Token refresh not implemented yet. " +
            "This requires MSAL configuration with Azure AD app credentials. " +
            "To be implemented after Azure AD app registration.");
    }
}
