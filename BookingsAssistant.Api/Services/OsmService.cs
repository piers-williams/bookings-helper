using System.Text.Json;
using System.Text.Json.Serialization;
using BookingsAssistant.Api.Models;

namespace BookingsAssistant.Api.Services;

public class OsmService : IOsmService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OsmService> _logger;
    private readonly string _baseUrl;
    private readonly string _campsiteId;
    private readonly string _sectionId;

    public OsmService(HttpClient httpClient, IConfiguration configuration, ILogger<OsmService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        _baseUrl = _configuration["Osm:BaseUrl"] ?? "https://www.onlinescoutmanager.co.uk";
        _campsiteId = _configuration["Osm:CampsiteId"] ?? throw new InvalidOperationException("OSM CampsiteId not configured");
        _sectionId = _configuration["Osm:SectionId"] ?? throw new InvalidOperationException("OSM SectionId not configured");

        _httpClient.BaseAddress = new Uri(_baseUrl);

        // TODO: OAuth token management (Task 8) - for now, service will log warning
        _logger.LogWarning("OSM service initialized without authentication. OAuth token management is pending.");
    }

    public async Task<List<BookingDto>> GetBookingsAsync(string status)
    {
        try
        {
            // Map our status to OSM mode parameter
            var mode = MapStatusToMode(status);
            var url = $"/v3/campsites/{_campsiteId}/bookings?mode={mode}";

            _logger.LogInformation("Fetching OSM bookings with mode: {Mode}", mode);

            // TODO: Add Bearer token once OAuth is implemented
            var response = await _httpClient.GetAsync(url);

            // Log rate limiting headers
            HandleRateLimiting(response);

            // Check HTTP status
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OSM API returned error status: {StatusCode}", response.StatusCode);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("OSM API authentication required. OAuth token management is not yet implemented.");
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

            // TODO: Add Bearer token once OAuth is implemented
            var detailsTask = _httpClient.GetAsync(detailsUrl);
            var commentsTask = _httpClient.GetAsync(commentsUrl);

            await Task.WhenAll(detailsTask, commentsTask);

            var detailsResponse = await detailsTask;
            var commentsResponse = await commentsTask;

            // Log rate limiting
            HandleRateLimiting(detailsResponse);
            HandleRateLimiting(commentsResponse);

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
                    _logger.LogWarning("OSM API authentication required. OAuth token management is not yet implemented.");
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

    private void HandleRateLimiting(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-RateLimit-Limit", out var limitValues))
        {
            var limit = limitValues.FirstOrDefault();
            _logger.LogDebug("Rate limit: {Limit}", limit);
        }

        if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues))
        {
            var remaining = remainingValues.FirstOrDefault();
            _logger.LogDebug("Rate limit remaining: {Remaining}", remaining);

            // Warn if getting low
            if (int.TryParse(remaining, out var remainingInt) && remainingInt < 10)
            {
                _logger.LogWarning("OSM API rate limit running low: {Remaining} requests remaining", remaining);
            }
        }

        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues))
        {
            var reset = resetValues.FirstOrDefault();
            _logger.LogDebug("Rate limit reset in: {Reset} seconds", reset);
        }

        // Check for rate limit exceeded
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            if (response.Headers.TryGetValues("Retry-After", out var retryAfterValues))
            {
                var retryAfter = retryAfterValues.FirstOrDefault();
                _logger.LogError("OSM API rate limit exceeded. Retry after: {RetryAfter} seconds", retryAfter);
            }
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
