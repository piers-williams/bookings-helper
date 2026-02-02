using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Services;

public class Office365Service : IOffice365Service
{
    private readonly ApplicationDbContext _context;
    private readonly IDataProtector _protector;
    private readonly IConfiguration _configuration;
    private readonly ILogger<Office365Service> _logger;
    private readonly HttpClient _httpClient;

    private readonly string _instance;
    private readonly string _tenantId;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _redirectUri;
    private readonly string _scopes;

    public Office365Service(
        ApplicationDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration configuration,
        ILogger<Office365Service> logger,
        HttpClient httpClient)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector("Office365OAuth2Tokens");
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;

        _instance = _configuration["AzureAd:Instance"] ?? "https://login.microsoftonline.com/";
        _tenantId = _configuration["AzureAd:TenantId"] ?? "common";
        _clientId = _configuration["AzureAd:ClientId"] ?? throw new InvalidOperationException("Azure AD ClientId not configured");
        _clientSecret = _configuration["AzureAd:ClientSecret"] ?? throw new InvalidOperationException("Azure AD ClientSecret not configured");
        _redirectUri = _configuration["AzureAd:RedirectUri"] ?? throw new InvalidOperationException("Azure AD RedirectUri not configured");
        _scopes = _configuration["AzureAd:Scopes"] ?? "User.Read Mail.Read offline_access";

        _logger.LogInformation("Office365Service initialized with OAuth authentication support");
    }

    public string GetAuthorizationUrl()
    {
        var authUrl = $"{_instance}{_tenantId}/oauth2/v2.0/authorize?" +
                      $"client_id={Uri.EscapeDataString(_clientId)}&" +
                      $"response_type=code&" +
                      $"redirect_uri={Uri.EscapeDataString(_redirectUri)}&" +
                      $"scope={Uri.EscapeDataString(_scopes)}&" +
                      $"response_mode=query";

        _logger.LogInformation("Generated Office 365 authorization URL");
        return authUrl;
    }

    public async Task<bool> HandleCallbackAsync(string code, int userId)
    {
        try
        {
            _logger.LogInformation("Handling Office 365 OAuth callback for user {UserId} with code length {CodeLength}", userId, code?.Length ?? 0);

            // Exchange authorization code for tokens
            var tokenRequest = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = _redirectUri,
                ["scope"] = _scopes
            };

            // Create request with Basic Authentication
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_instance}{_tenantId}/oauth2/v2.0/token")
            {
                Content = new FormUrlEncodedContent(tokenRequest)
            };

            // Add Basic Authentication header
            var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{_clientId}:{_clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            _logger.LogInformation("Sending token request to Microsoft. Redirect URI: {RedirectUri}, ClientId length: {ClientIdLength}",
                _redirectUri, _clientId.Length);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Office 365 token exchange failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _logger.LogError("Office 365 token response was null or missing access token");
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
            user.Office365AccessToken = _protector.Protect(tokenResponse.AccessToken);
            user.Office365RefreshToken = !string.IsNullOrEmpty(tokenResponse.RefreshToken)
                ? _protector.Protect(tokenResponse.RefreshToken)
                : null;
            user.Office365TokenExpiry = expiry;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Office 365 tokens saved successfully for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Office 365 OAuth callback for user {UserId}", userId);
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
        if (string.IsNullOrEmpty(user.Office365AccessToken))
        {
            throw new InvalidOperationException("No Office 365 access token available. User needs to authenticate.");
        }

        // Check if token is expired or expiring soon (5-minute buffer)
        if (user.Office365TokenExpiry == null || user.Office365TokenExpiry <= DateTime.UtcNow.AddMinutes(5))
        {
            _logger.LogInformation("Office 365 access token expired or expiring soon for user {UserId}, refreshing...", userId);

            if (string.IsNullOrEmpty(user.Office365RefreshToken))
            {
                throw new InvalidOperationException("Office 365 refresh token not available. User needs to re-authenticate.");
            }

            await RefreshTokenAsync(user);
        }

        // Decrypt and return the access token
        return _protector.Unprotect(user.Office365AccessToken);
    }

    private async Task RefreshTokenAsync(Data.Entities.ApplicationUser user)
    {
        try
        {
            _logger.LogInformation("Refreshing Office 365 access token for user {UserId}", user.Id);

            // Decrypt the refresh token
            var refreshToken = _protector.Unprotect(user.Office365RefreshToken!);

            // Request new tokens
            var tokenRequest = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["scope"] = _scopes
            };

            // Create request with Basic Authentication
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_instance}{_tenantId}/oauth2/v2.0/token")
            {
                Content = new FormUrlEncodedContent(tokenRequest)
            };

            // Add Basic Authentication header
            var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{_clientId}:{_clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Office 365 token refresh failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new InvalidOperationException("Failed to refresh Office 365 access token. User needs to re-authenticate.");
            }

            var content = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _logger.LogError("Office 365 token refresh response was null or missing access token");
                throw new InvalidOperationException("Failed to refresh Office 365 access token. User needs to re-authenticate.");
            }

            // Calculate token expiry
            var expiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            // Encrypt and save new tokens
            user.Office365AccessToken = _protector.Protect(tokenResponse.AccessToken);
            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                user.Office365RefreshToken = _protector.Protect(tokenResponse.RefreshToken);
            }
            user.Office365TokenExpiry = expiry;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Office 365 access token refreshed successfully for user {UserId}", user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Office 365 access token for user {UserId}", user.Id);
            throw;
        }
    }

    public async Task<List<EmailDto>> GetUnreadEmailsAsync(int userId)
    {
        try
        {
            _logger.LogInformation("Fetching unread emails for user {UserId}", userId);

            var accessToken = await GetValidAccessTokenAsync(userId);

            var request = new HttpRequestMessage(HttpMethod.Get,
                "https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messages?$filter=isRead eq false&$select=id,subject,from,receivedDateTime&$top=50&$orderby=receivedDateTime desc");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Microsoft Graph API call failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to fetch emails: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var graphResponse = JsonSerializer.Deserialize<GraphEmailListResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (graphResponse?.Value == null)
            {
                _logger.LogWarning("No emails returned from Graph API for user {UserId}", userId);
                return new List<EmailDto>();
            }

            var emails = new List<EmailDto>();
            foreach (var message in graphResponse.Value)
            {
                // Get email body to extract booking references
                var (body, bookingRefs) = await GetEmailDetailsAsync(userId, message.Id);

                emails.Add(new EmailDto
                {
                    Id = 0, // Will be assigned when saved to database
                    SenderEmail = message.From?.EmailAddress?.Address ?? "unknown",
                    SenderName = message.From?.EmailAddress?.Name ?? "Unknown",
                    Subject = message.Subject ?? "(No Subject)",
                    ReceivedDate = message.ReceivedDateTime,
                    IsRead = false,
                    ExtractedBookingRef = bookingRefs.FirstOrDefault()
                });
            }

            _logger.LogInformation("Successfully fetched {Count} unread emails for user {UserId}", emails.Count, userId);
            return emails;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching unread emails for user {UserId}", userId);
            throw;
        }
    }

    public async Task<(string Body, List<string> BookingRefs)> GetEmailDetailsAsync(int userId, string messageId)
    {
        try
        {
            _logger.LogInformation("Fetching email details for message {MessageId}", messageId);

            var accessToken = await GetValidAccessTokenAsync(userId);

            var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://graph.microsoft.com/v1.0/me/messages/{messageId}?$select=body");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Microsoft Graph API call failed for message {MessageId}: {StatusCode} - {Error}",
                    messageId, response.StatusCode, errorContent);
                return (string.Empty, new List<string>());
            }

            var content = await response.Content.ReadAsStringAsync();
            var message = JsonSerializer.Deserialize<GraphMessageResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var body = message?.Body?.Content ?? string.Empty;
            var bookingRefs = ExtractBookingReferences(body);

            _logger.LogInformation("Extracted {Count} booking references from message {MessageId}", bookingRefs.Count, messageId);
            return (body, bookingRefs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching email details for message {MessageId}", messageId);
            return (string.Empty, new List<string>());
        }
    }

    private List<string> ExtractBookingReferences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        // Pattern matches:
        // - #12345
        // - Booking #12345
        // - Ref: 12345
        // - REF:12345
        // - Reference 12345
        // - OSM #12345
        var pattern = @"(?:#|Ref:|REF:|Reference|Booking\s*#|OSM\s*#)\s*(\d{4,6})";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);

        var matches = regex.Matches(text);
        var bookingRefs = new List<string>();

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                bookingRefs.Add(match.Groups[1].Value);
            }
        }

        // Return distinct booking references only (no duplicates)
        return bookingRefs.Distinct().ToList();
    }

    // OAuth token response model
    private class TokenResponse
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

    // Microsoft Graph API response models
    private class GraphEmailListResponse
    {
        [JsonPropertyName("value")]
        public List<GraphMessage>? Value { get; set; }
    }

    private class GraphMessage
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("from")]
        public GraphRecipient? From { get; set; }

        [JsonPropertyName("receivedDateTime")]
        public DateTime ReceivedDateTime { get; set; }
    }

    private class GraphRecipient
    {
        [JsonPropertyName("emailAddress")]
        public GraphEmailAddress? EmailAddress { get; set; }
    }

    private class GraphEmailAddress
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }
    }

    private class GraphMessageResponse
    {
        [JsonPropertyName("body")]
        public GraphBody? Body { get; set; }
    }

    private class GraphBody
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("contentType")]
        public string? ContentType { get; set; }
    }
}
