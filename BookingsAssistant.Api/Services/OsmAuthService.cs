using System.Text.Json;
using System.Text.Json.Serialization;
using BookingsAssistant.Api.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Services;

public class OsmAuthService : IOsmAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IDataProtector _protector;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OsmAuthService> _logger;
    private readonly HttpClient _httpClient;

    private readonly string _baseUrl;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _redirectUri;
    private readonly string _scopes;

    public OsmAuthService(
        ApplicationDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration configuration,
        ILogger<OsmAuthService> logger,
        HttpClient httpClient)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector("OsmOAuth2Tokens");
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;

        _baseUrl = _configuration["Osm:BaseUrl"] ?? "https://www.onlinescoutmanager.co.uk";
        _clientId = _configuration["Osm:ClientId"] ?? throw new InvalidOperationException("OSM ClientId not configured");
        _clientSecret = _configuration["Osm:ClientSecret"] ?? throw new InvalidOperationException("OSM ClientSecret not configured");
        _redirectUri = _configuration["Osm:RedirectUri"] ?? throw new InvalidOperationException("OSM RedirectUri not configured");
        _scopes = _configuration["Osm:Scopes"] ?? "section:campsite_bookings:read section:campsite_bookings:write";

        _httpClient.BaseAddress = new Uri(_baseUrl);
    }

    public string GetAuthorizationUrl()
    {
        var authUrl = $"{_baseUrl}/oauth/authorize?" +
                      $"response_type=code&" +
                      $"client_id={Uri.EscapeDataString(_clientId)}&" +
                      $"redirect_uri={Uri.EscapeDataString(_redirectUri)}&" +
                      $"scope={Uri.EscapeDataString(_scopes)}";

        _logger.LogInformation("Generated OSM authorization URL");
        return authUrl;
    }

    public async Task<bool> HandleCallbackAsync(string code, int userId)
    {
        try
        {
            _logger.LogInformation("Handling OSM OAuth callback for user {UserId}", userId);

            // Exchange authorization code for tokens
            var tokenRequest = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["redirect_uri"] = _redirectUri
            };

            var response = await _httpClient.PostAsync("/oauth/token", new FormUrlEncodedContent(tokenRequest));

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("OSM token exchange failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<OsmTokenResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _logger.LogError("OSM token response was null or missing access token");
                return false;
            }

            // Calculate token expiry
            var expiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            // Get user and save tokens
            var user = await _context.ApplicationUsers.FindAsync(userId);
            if (user == null)
            {
                _logger.LogError("User {UserId} not found", userId);
                return false;
            }

            // Encrypt and save tokens
            user.OsmAccessToken = _protector.Protect(tokenResponse.AccessToken);
            user.OsmRefreshToken = !string.IsNullOrEmpty(tokenResponse.RefreshToken)
                ? _protector.Protect(tokenResponse.RefreshToken)
                : null;
            user.OsmTokenExpiry = expiry;

            await _context.SaveChangesAsync();

            _logger.LogInformation("OSM tokens saved successfully for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling OSM OAuth callback for user {UserId}", userId);
            return false;
        }
    }

    public async Task<string> GetValidAccessTokenAsync(int userId)
    {
        var user = await _context.ApplicationUsers.FindAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found");
        }

        // Check if token exists
        if (string.IsNullOrEmpty(user.OsmAccessToken))
        {
            throw new InvalidOperationException("No OSM access token available. User needs to authenticate.");
        }

        // Check if token is expired or expiring soon (5-minute buffer)
        if (user.OsmTokenExpiry == null || user.OsmTokenExpiry <= DateTime.UtcNow.AddMinutes(5))
        {
            _logger.LogInformation("OSM access token expired or expiring soon for user {UserId}, refreshing...", userId);

            if (string.IsNullOrEmpty(user.OsmRefreshToken))
            {
                throw new InvalidOperationException("OSM refresh token not available. User needs to re-authenticate.");
            }

            await RefreshTokenAsync(user);
        }

        // Decrypt and return the access token
        return _protector.Unprotect(user.OsmAccessToken);
    }

    private async Task RefreshTokenAsync(Data.Entities.ApplicationUser user)
    {
        try
        {
            _logger.LogInformation("Refreshing OSM access token for user {UserId}", user.Id);

            // Decrypt the refresh token
            var refreshToken = _protector.Unprotect(user.OsmRefreshToken!);

            // Request new tokens
            var tokenRequest = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret
            };

            var response = await _httpClient.PostAsync("/oauth/token", new FormUrlEncodedContent(tokenRequest));

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("OSM token refresh failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new InvalidOperationException("Failed to refresh OSM access token. User needs to re-authenticate.");
            }

            var content = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<OsmTokenResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _logger.LogError("OSM token refresh response was null or missing access token");
                throw new InvalidOperationException("Failed to refresh OSM access token. User needs to re-authenticate.");
            }

            // Calculate token expiry
            var expiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            // Encrypt and save new tokens
            user.OsmAccessToken = _protector.Protect(tokenResponse.AccessToken);
            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                user.OsmRefreshToken = _protector.Protect(tokenResponse.RefreshToken);
            }
            user.OsmTokenExpiry = expiry;

            await _context.SaveChangesAsync();

            _logger.LogInformation("OSM access token refreshed successfully for user {UserId}", user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing OSM access token for user {UserId}", user.Id);
            throw;
        }
    }

    // OAuth token response model
    private class OsmTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }
}
