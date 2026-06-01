using System.Text.Json;
using System.Text.Json.Serialization;
using BookingsAssistant.Api.Models;

namespace BookingsAssistant.Api.Services;

internal class OsmService : IOsmService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OsmService> _logger;
    private readonly IOsmAuthService _osmAuthService;
    private readonly string _baseUrl;
    private readonly string _campsiteId;
    private readonly string _sectionId;

    // When OSM signals we're nearly out of quota we pause new requests until
    // this time. Shared across the request loop within a single OsmService
    // instance (e.g. one sync's comment loop or one backfill batch).
    private DateTimeOffset _cooldownUntil = DateTimeOffset.MinValue;

    public OsmService(HttpClient httpClient, IConfiguration configuration, ILogger<OsmService> logger, IOsmAuthService osmAuthService)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _osmAuthService = osmAuthService;

        _baseUrl = _configuration["Osm:BaseUrl"] ?? "https://www.onlinescoutmanager.co.uk";
        _campsiteId = _configuration["Osm:CampsiteId"] ?? throw new InvalidOperationException("OSM CampsiteId not configured");
        _sectionId = _configuration["Osm:SectionId"] ?? throw new InvalidOperationException("OSM SectionId not configured");

        _httpClient.BaseAddress = new Uri(_baseUrl);

        _logger.LogInformation("OSM service initialized with OAuth authentication support");
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var userId = 1; // TODO: Get from authenticated user context
        return await _osmAuthService.GetValidAccessTokenAsync(userId);
    }

    public async Task<List<BookingDto>> GetBookingsAsync(string status)
    {
        try
        {
            // Map our status to OSM mode parameter
            var mode = MapStatusToMode(status);
            var url = $"/v3/campsites/{_campsiteId}/bookings?mode={mode}";

            _logger.LogInformation("Fetching OSM bookings with mode: {Mode}", mode);

            // Make the authenticated request, honouring OSM rate limits
            var response = await SendWithRateLimitAsync(async () =>
            {
                var token = await GetAccessTokenAsync();
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                return req;
            });

            // Check HTTP status
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OSM API returned error status: {StatusCode}", response.StatusCode);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("OSM API authentication failed. Token may be invalid or expired.");
                }

                return new List<BookingDto>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var osmResponse = JsonSerializer.Deserialize<OsmApiResponse<List<OsmBooking>>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Check response status field
            if (osmResponse == null || !osmResponse.Status)
            {
                _logger.LogError("OSM API returned error: {Error}", osmResponse?.Error ?? "Unknown error");
                return new List<BookingDto>();
            }

            // Map OSM bookings to our DTOs
            var bookings = osmResponse.Data?.Select(MapOsmBookingToDto).ToList() ?? new List<BookingDto>();

            _logger.LogInformation("Successfully fetched {Count} bookings from OSM", bookings.Count);
            return bookings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bookings from OSM API");
            return new List<BookingDto>();
        }
    }

    public async Task<(string FullDetails, List<CommentDto> Comments)> GetBookingDetailsAsync(string osmBookingId)
    {
        try
        {
            _logger.LogInformation("Fetching OSM booking details for booking: {BookingId}", osmBookingId);

            // Fetch booking details and comments in parallel
            var detailsUrl = $"/v3/campsites/{_campsiteId}/items?booking_id={osmBookingId}&mode=booking&audience=venue";
            var commentsUrl = $"/v3/comments/campsite_booking/{osmBookingId}/list?section_id={_sectionId}";

            // Get access token and make authenticated requests (rate-limit aware)
            var token = await GetAccessTokenAsync();

            HttpRequestMessage BuildGet(string url)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                return req;
            }

            var detailsTask = SendWithRateLimitAsync(() => Task.FromResult(BuildGet(detailsUrl)));
            var commentsTask = SendWithRateLimitAsync(() => Task.FromResult(BuildGet(commentsUrl)));

            await Task.WhenAll(detailsTask, commentsTask);

            var detailsResponse = await detailsTask;
            var commentsResponse = await commentsTask;

            // Process details
            string fullDetails = string.Empty;
            if (detailsResponse.IsSuccessStatusCode)
            {
                fullDetails = await detailsResponse.Content.ReadAsStringAsync();
            }
            else
            {
                _logger.LogError("OSM API returned error status for details: {StatusCode}", detailsResponse.StatusCode);

                if (detailsResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("OSM API authentication failed for details. Token may be invalid or expired.");
                }
            }

            // Process comments
            var comments = new List<CommentDto>();
            if (commentsResponse.IsSuccessStatusCode)
            {
                var commentsContent = await commentsResponse.Content.ReadAsStringAsync();
                var osmCommentsResponse = JsonSerializer.Deserialize<OsmApiResponse<List<OsmComment>>>(commentsContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (osmCommentsResponse != null && osmCommentsResponse.Status)
                {
                    comments = osmCommentsResponse.Data?.Select(c => MapOsmCommentToDto(c, osmBookingId)).ToList() ?? new List<CommentDto>();
                }
                else
                {
                    _logger.LogError("OSM API returned error for comments: {Error}", osmCommentsResponse?.Error ?? "Unknown error");
                }
            }
            else
            {
                _logger.LogError("OSM API returned error status for comments: {StatusCode}", commentsResponse.StatusCode);
            }

            _logger.LogInformation("Successfully fetched booking details and {Count} comments from OSM", comments.Count);
            return (fullDetails, comments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching booking details from OSM API");
            return (string.Empty, new List<CommentDto>());
        }
    }

    public async Task<CommentDto?> PostCommentAsync(string osmBookingId, string comment)
    {
        try
        {
            _logger.LogInformation("Posting comment to OSM booking: {BookingId}", osmBookingId);

            var url = $"/v3/comments/campsite_booking/{osmBookingId}/add?section_id={_sectionId}";

            var response = await SendWithRateLimitAsync(async () =>
            {
                var token = await GetAccessTokenAsync();
                var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                req.Content = new StringContent(
                    JsonSerializer.Serialize(new { comment }),
                    System.Text.Encoding.UTF8,
                    "application/json");
                return req;
            });

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OSM API returned error status when posting comment: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var osmResponse = JsonSerializer.Deserialize<OsmApiResponse<OsmComment>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (osmResponse == null || !osmResponse.Status || osmResponse.Data == null)
            {
                _logger.LogError("OSM API returned error when posting comment: {Error}", osmResponse?.Error ?? "Unknown error");
                return null;
            }

            var result = MapOsmCommentToDto(osmResponse.Data, osmBookingId);
            _logger.LogInformation("Successfully posted comment to OSM booking {BookingId}", osmBookingId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting comment to OSM API for booking {BookingId}", osmBookingId);
            return null;
        }
    }

    public async Task<bool> SendBookingTemplateEmailAsync(string osmBookingId)
    {
        try
        {
            _logger.LogInformation("Sending gate code email for booking {BookingId}", osmBookingId);

            var memberId = await GetBookingMemberIdAsync(osmBookingId);
            if (memberId == null)
            {
                _logger.LogError("Could not resolve member_id for booking {BookingId}", osmBookingId);
                return false;
            }

            var emailsJson = await ResolveContactEmailsAsync(memberId);
            if (emailsJson == null)
            {
                _logger.LogError("Could not resolve contact emails for member {MemberId} (booking {BookingId})", memberId, osmBookingId);
                return false;
            }

            var sent = await SendTemplateAsync(osmBookingId, emailsJson);
            if (sent)
                _logger.LogInformation("Successfully sent gate code email for booking {BookingId}", osmBookingId);
            return sent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending gate code email for booking {BookingId}", osmBookingId);
            return false;
        }
    }

    public string GetAuthorizationUrl(string redirectUri)
    {
        return _osmAuthService.GetAuthorizationUrl(redirectUri);
    }

    public async Task<bool> HandleOAuthCallbackAsync(string code, int userId, string redirectUri)
    {
        return await _osmAuthService.HandleCallbackAsync(code, userId, redirectUri);
    }

    public async Task<bool> IsAuthenticatedAsync(int userId)
    {
        try
        {
            await _osmAuthService.GetValidAccessTokenAsync(userId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string?> GetBookingMemberIdAsync(string osmBookingId)
    {
        var response = await SendWithRateLimitAsync(async () =>
        {
            var token = await GetAccessTokenAsync();
            var req = new HttpRequestMessage(HttpMethod.Get, $"/v3/campsites/bookings/{osmBookingId}");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return req;
        });

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OSM booking detail returned {StatusCode} for booking {BookingId}", response.StatusCode, osmBookingId);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        if (doc.RootElement.TryGetProperty("data", out var data) &&
            data.TryGetProperty("member_id", out var memberId))
        {
            return memberId.ToString();
        }

        if (doc.RootElement.TryGetProperty("member_id", out var rootMemberId))
            return rootMemberId.ToString();

        _logger.LogError("member_id not found in booking detail for {BookingId}. Keys: {Keys}",
            osmBookingId,
            string.Join(", ", doc.RootElement.EnumerateObject().Select(p => p.Name)));
        return null;
    }

    private async Task<string?> ResolveContactEmailsAsync(string memberId)
    {
        var response = await SendWithRateLimitAsync(async () =>
        {
            var token = await GetAccessTokenAsync();
            var req = new HttpRequestMessage(HttpMethod.Post,
                $"/ext/members/email/?action=getSelectedEmailsFromContacts&sectionid={_sectionId}");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["contactGroups"] = "[\"contact_primary_campsite\"]",
                ["scouts"] = memberId
            });
            return req;
        });

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("getSelectedEmailsFromContacts returned {StatusCode} for member {MemberId}",
                response.StatusCode, memberId);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        if (doc.RootElement.TryGetProperty("data", out var data))
            return data.GetRawText();

        _logger.LogError("Unexpected getSelectedEmailsFromContacts response for member {MemberId}: {Response}",
            memberId, content);
        return null;
    }

    private async Task<bool> SendTemplateAsync(string osmBookingId, string emailsJson)
    {
        var campaignId = _configuration["GateCode:CampaignId"]
            ?? throw new InvalidOperationException("GateCode:CampaignId not configured");
        var fromName = _configuration["GateCode:FromName"]
            ?? throw new InvalidOperationException("GateCode:FromName not configured");
        var fromEmail = _configuration["GateCode:FromEmail"]
            ?? throw new InvalidOperationException("GateCode:FromEmail not configured");
        var subject = _configuration["GateCode:Subject"] ?? "Gate code";

        var response = await SendWithRateLimitAsync(async () =>
        {
            var token = await GetAccessTokenAsync();
            var req = new HttpRequestMessage(HttpMethod.Post, "/ext/members/email/?action=sendTemplate");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["cc"] = "",
                ["from"] = $"{fromName} <{fromEmail}>",
                ["save_email_from"] = "false",
                ["subject"] = subject,
                ["emails"] = emailsJson,
                ["edits"] = "{}",
                ["sectionid"] = _sectionId,
                ["campaign_id"] = campaignId,
                ["email_session_key"] = "blank",
                ["guid"] = Guid.NewGuid().ToString(),
                ["current_section_id"] = _sectionId,
                ["draft_email_id"] = "0",
                ["scheduled_email_id"] = "0"
            });
            return req;
        });

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("sendTemplate returned {StatusCode} for booking {BookingId}",
                response.StatusCode, osmBookingId);
            return false;
        }

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        if (doc.RootElement.TryGetProperty("status", out var status) && !status.GetBoolean())
        {
            var error = doc.RootElement.TryGetProperty("error", out var err) ? err.GetString() : "unknown";
            _logger.LogError("sendTemplate returned error for booking {BookingId}: {Error}", osmBookingId, error);
            return false;
        }

        return true;
    }

    private string MapStatusToMode(string status)
    {
        return status.ToLower() switch
        {
            "provisional" => "provisional",
            "confirmed" => "current",
            "future" => "future",
            "past" => "past",
            "cancelled" => "cancelled",
            _ => "current" // Default to current for unknown statuses
        };
    }

    private static string? FirstHeader(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    /// <summary>
    /// Sends a request and honours OSM's rate limits: pauses before sending if a
    /// prior response said quota was nearly exhausted, and on a 429 waits for the
    /// Retry-After / reset window and retries (the request is rebuilt each attempt
    /// since an HttpRequestMessage and its content can only be sent once).
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRateLimitAsync(
        Func<Task<HttpRequestMessage>> requestFactory, CancellationToken ct = default)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; ; attempt++)
        {
            var wait = _cooldownUntil - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                _logger.LogWarning("OSM rate limit: pausing {Seconds:F0}s before next request", wait.TotalSeconds);
                await Task.Delay(wait, ct);
            }

            var request = await requestFactory();
            var response = await _httpClient.SendAsync(request, ct);
            HandleRateLimiting(response);

            if (response.StatusCode != System.Net.HttpStatusCode.TooManyRequests || attempt >= maxAttempts)
                return response;

            var delay = OsmRateLimit.GetRetryAfterDelay(
                FirstHeader(response, "Retry-After"),
                FirstHeader(response, "X-RateLimit-Reset"),
                DateTimeOffset.UtcNow);
            _logger.LogWarning("OSM API rate limit hit (429); attempt {Attempt}/{Max}, backing off {Seconds:F0}s",
                attempt, maxAttempts, delay.TotalSeconds);
            response.Dispose();
            await Task.Delay(delay, ct);
        }
    }

    private void HandleRateLimiting(HttpResponseMessage response)
    {
        var remaining = FirstHeader(response, "X-RateLimit-Remaining");
        var reset = FirstHeader(response, "X-RateLimit-Reset");

        _logger.LogDebug("Rate limit: {Limit}, remaining {Remaining}, reset {Reset}s",
            FirstHeader(response, "X-RateLimit-Limit"), remaining, reset);

        // Proactively pause once quota is nearly gone, so the next request waits
        // for the window to refresh instead of triggering a 429.
        var pause = OsmRateLimit.GetProactiveDelay(remaining, reset, DateTimeOffset.UtcNow);
        if (pause is not null)
        {
            _cooldownUntil = DateTimeOffset.UtcNow + pause.Value;
            _logger.LogWarning("OSM API rate limit low ({Remaining} left); pausing {Seconds:F0}s until reset",
                remaining, pause.Value.TotalSeconds);
        }
    }

    private BookingDto MapOsmBookingToDto(OsmBooking osmBooking)
    {
        return new BookingDto
        {
            OsmBookingId = osmBooking.Id.ToString(),
            CustomerName = osmBooking.GroupName ?? string.Empty,
            StartDate = DateTime.TryParse(osmBooking.StartDate, out var startDate) ? startDate : DateTime.MinValue,
            EndDate = DateTime.TryParse(osmBooking.EndDate, out var endDate) ? endDate : DateTime.MinValue,
            Status = CapitalizeFirstLetter(osmBooking.Status ?? "Unknown")
        };
    }

    private CommentDto MapOsmCommentToDto(OsmComment osmComment, string osmBookingId)
    {
        var authorName = string.Empty;
        if (osmComment.User != null)
        {
            authorName = $"{osmComment.User.FirstName} {osmComment.User.LastName}".Trim();
        }

        var textPreview = osmComment.Comment ?? string.Empty;
        if (textPreview.Length > 200)
        {
            textPreview = textPreview.Substring(0, 200) + "...";
        }

        return new CommentDto
        {
            OsmBookingId = osmBookingId,
            OsmCommentId = osmComment.Id.ToString(),
            AuthorName = authorName,
            TextPreview = textPreview,
            CreatedDate = DateTime.TryParse(osmComment.CreatedAt, out var createdDate) ? createdDate : DateTime.MinValue,
            IsNew = false // TODO: Implement "new" tracking in Phase 2
        };
    }

    private string CapitalizeFirstLetter(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (text.Length == 1)
            return text.ToUpper();

        return char.ToUpper(text[0]) + text.Substring(1).ToLower();
    }

    // OSM API response models
    private class OsmApiResponse<T>
    {
        [JsonPropertyName("status")]
        public bool Status { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    private class OsmBooking
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("member_id")]
        public int? MemberId { get; set; }

        [JsonPropertyName("group_name")]
        public string? GroupName { get; set; }

        [JsonPropertyName("start_date")]
        public string? StartDate { get; set; }

        [JsonPropertyName("end_date")]
        public string? EndDate { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    private class OsmComment
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("user")]
        public OsmUser? User { get; set; }
    }

    private class OsmUser
    {
        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }
    }
}
