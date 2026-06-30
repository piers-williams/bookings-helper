using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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

    public async Task<string?> GetBookingContactEmailAsync(string osmBookingId)
    {
        try
        {
            // Same path the sender uses, so we resolve exactly the address that
            // would be emailed. The booking "items" endpoint has no contact data.
            // Each step logs why it failed so we can pinpoint the cause without
            // leaking PII (we never log the raw response, only its shape).
            var memberId = await GetBookingMemberIdAsync(osmBookingId);
            if (memberId == null)
            {
                _logger.LogWarning("Email resolve: no member_id for booking {BookingId}", osmBookingId);
                return null;
            }

            var emailsJson = await ResolveContactEmailsAsync(memberId);
            if (emailsJson == null)
            {
                _logger.LogWarning("Email resolve: contacts call returned nothing for booking {BookingId} (member {MemberId})",
                    osmBookingId, memberId);
                return null;
            }

            var email = ExtractFirstEmail(emailsJson);
            if (email == null)
                // "contains '@'" tells us whether the address is present at all:
                // true → our extraction missed it; false → the contacts query
                // (member/contact-group) returned no email to begin with.
                _logger.LogWarning(
                    "Email resolve: no email extracted for booking {BookingId} (member {MemberId}, contacts length {Length}, contains '@': {ContainsAt})",
                    osmBookingId, memberId, emailsJson.Length, emailsJson.Contains('@'));
            return email;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error resolving contact email for booking {BookingId}", osmBookingId);
            return null;
        }
    }

    private static readonly Regex EmailRegex = new(
        @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);

    /// <summary>
    /// Extracts the first email address from the contacts JSON. The endpoint is
    /// scoped to the primary campsite contact, so the first match is that
    /// address — structure-agnostic, since OSM's exact shape isn't documented.
    /// </summary>
    internal static string? ExtractFirstEmail(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var match = EmailRegex.Match(json);
        return match.Success ? match.Value : null;
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

    public async Task<string> CreateBookingItemAsync(string osmBookingId, BookingItemCreateSpec spec)
    {
        if (spec.StartDate is null || spec.EndDate is null)
            throw new InvalidOperationException(
                $"Cannot create OSM item {spec.CampsiteItemId} for booking {osmBookingId} without start/end dates");

        // 1. Resolve the availability slot for the requested date window. OSM's addItem
        //    requires a slot_id, which is NOT a property of the item — it's looked up from
        //    the item-type's availability for this booking.
        var availUrl = $"/v3/campsites/items/{Uri.EscapeDataString(spec.CampsiteItemId)}/availability?booking_id={Uri.EscapeDataString(osmBookingId)}";
        var availResponse = await SendAuthorizedAsync(HttpMethod.Get, availUrl, null);
        if (!availResponse.IsSuccessStatusCode)
            throw await CreateOsmExceptionAsync(availResponse, $"fetching availability for item {spec.CampsiteItemId}");

        var availJson = await availResponse.Content.ReadAsStringAsync();
        var slotId = ResolveSlotId(availJson, spec.StartDate.Value, spec.EndDate.Value)
            ?? throw new InvalidOperationException(
                $"No available OSM slot for item {spec.CampsiteItemId} on {spec.StartDate:yyyy-MM-dd}..{spec.EndDate:yyyy-MM-dd}");

        // 2. Create (add) the item.
        var createUrl = $"/v3/campsites/bookings/{Uri.EscapeDataString(osmBookingId)}/addItem/{Uri.EscapeDataString(spec.CampsiteItemId)}";
        var form = BuildCreateForm(spec, slotId);
        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, createUrl,
            () => new FormUrlEncodedContent(form));
        if (!createResponse.IsSuccessStatusCode)
            throw await CreateOsmExceptionAsync(createResponse, $"creating item {spec.CampsiteItemId} for booking {osmBookingId}");

        var createJson = await createResponse.Content.ReadAsStringAsync();
        var newItemId = ParseCreatedItemId(createJson);
        _logger.LogInformation("Created OSM item {NewItemId} on booking {BookingId}", newItemId, osmBookingId);

        // 3. Replay the original item's question answers onto the clone (best-effort —
        //    OSM creates blank question rows on add, and the clone's row ids differ from
        //    the original's, so we match on the stable question-definition id).
        if (spec.QuestionAnswers.Count > 0)
            await ReplayQuestionAnswersAsync(newItemId, spec.QuestionAnswers);

        return newItemId;
    }

    public async Task<bool> DeleteBookingItemAsync(string osmBookingId, string itemId)
    {
        var url = $"/v3/campsites/bookings/items/{Uri.EscapeDataString(itemId)}/delete";
        var response = await SendAuthorizedAsync(HttpMethod.Post, url, null);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OSM delete returned {StatusCode} for item {ItemId}", response.StatusCode, itemId);
            return false;
        }

        var json = await response.Content.ReadAsStringAsync();
        return ParseDeleteSucceeded(json);
    }

    public async Task<List<AvailableSiteDto>> GetAvailableSitesAsync(string osmBookingId)
    {
        // The bookable site/pitch catalogue comes from the same /items catalogue endpoint
        // (the item-type tree), filtered to the site categories. See ParseAvailableSites.
        var url = $"/v3/campsites/{_campsiteId}/items?booking_id={Uri.EscapeDataString(osmBookingId)}&mode=booking&audience=venue";

        var response = await SendAuthorizedAsync(HttpMethod.Get, url, null);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new InvalidOperationException($"OSM authentication failed fetching sites for booking {osmBookingId}");

            _logger.LogError("OSM catalogue endpoint returned {StatusCode} for booking {BookingId}",
                response.StatusCode, osmBookingId);
            return new List<AvailableSiteDto>();
        }

        return ParseAvailableSites(await response.Content.ReadAsStringAsync());
    }

    private async Task ReplayQuestionAnswersAsync(string itemId, IReadOnlyDictionary<int, string> answersByDefId)
    {
        try
        {
            var url = $"/v3/campsites/bookings/items/{Uri.EscapeDataString(itemId)}/questions";
            var getResponse = await SendAuthorizedAsync(HttpMethod.Get, url, null);
            if (!getResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Could not fetch questions for cloned item {ItemId}; answers not replayed", itemId);
                return;
            }

            var cloneQuestions = ParseItemQuestions(await getResponse.Content.ReadAsStringAsync());
            var answersJson = BuildAnswersJson(answersByDefId, cloneQuestions);
            if (answersJson == "[]") return;   // nothing to replay

            var postResponse = await SendAuthorizedAsync(HttpMethod.Post, url,
                () => new FormUrlEncodedContent(new Dictionary<string, string> { ["answers"] = answersJson }));
            if (!postResponse.IsSuccessStatusCode)
                _logger.LogWarning("Replaying answers to cloned item {ItemId} returned {StatusCode}", itemId, postResponse.StatusCode);
        }
        catch (Exception ex)
        {
            // Answer replay is best-effort: a failure here must not fail the whole mutation.
            _logger.LogWarning(ex, "Failed to replay question answers to cloned item {ItemId}", itemId);
        }
    }

    private Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method, string url, Func<HttpContent>? contentFactory)
        => SendWithRateLimitAsync(async () =>
        {
            var token = await GetAccessTokenAsync();
            var req = new HttpRequestMessage(method, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            if (contentFactory != null) req.Content = contentFactory();   // fresh content per attempt
            return req;
        });

    private async Task<Exception> CreateOsmExceptionAsync(HttpResponseMessage response, string context)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return new InvalidOperationException($"OSM authentication failed {context}");
        var body = await response.Content.ReadAsStringAsync();
        return new InvalidOperationException($"OSM error {(int)response.StatusCode} {context}: {body}");
    }

    /// <summary>
    /// Finds the availability slot id for the requested start/end dates (times within the
    /// slot are flexible and supplied separately). Returns null when no available slot
    /// covers the window.
    ///
    /// OSM returns availability as per-session slots, not one slot per arbitrary stay
    /// length. A multi-night stay therefore rarely has a slot whose <c>end</c> falls on the
    /// departure date: instead there is a <c>multi_day</c> slot starting on the arrival date
    /// whose <c>end</c> is only the first night but whose <c>available_until</c> marks how
    /// far the stay can run. We take an exact single-slot span match if one exists
    /// (same-day bookings, and slots already covering the window), otherwise fall back to a
    /// slot on the arrival date whose <c>available_until</c> reaches the departure date.
    /// </summary>
    internal static string? ResolveSlotId(string? availabilityJson, DateTime startDate, DateTime endDate)
    {
        if (string.IsNullOrWhiteSpace(availabilityJson)) return null;

        using var doc = JsonDocument.Parse(availabilityJson);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return null;

        string? multiNightMatch = null;

        foreach (var slot in data.EnumerateArray())
        {
            // OSM marks booked-out slots with available:false; an absent flag counts as
            // available. ("available_until" is a separate deadline field, not this flag.)
            if (slot.TryGetProperty("available", out var avail) && avail.ValueKind == JsonValueKind.False)
                continue;

            // Every candidate must begin on the arrival date; times within the slot are
            // flexible and supplied separately in the create form.
            var (slotStart, _) = SplitTimestamp(GetString(slot, "start"));
            if (slotStart != startDate.Date)
                continue;

            // Exact single-slot span match — first one wins (same-day stays, or a slot that
            // already spans the whole window).
            var (slotEnd, _) = SplitTimestamp(GetString(slot, "end"));
            if (slotEnd == endDate.Date)
                return GetString(slot, "id");

            // Multi-night fallback: a slot on the arrival date whose availability extends
            // (via available_until) to at least the departure date. First such slot wins,
            // but only if no exact match is found, so it never overrides a same-day slot.
            if (multiNightMatch is null && endDate.Date > startDate.Date)
            {
                var (availableUntil, _) = SplitTimestamp(GetString(slot, "available_until"));
                if (availableUntil is not null && availableUntil >= endDate.Date)
                    multiNightMatch = GetString(slot, "id");
            }
        }

        return multiNightMatch;
    }

    /// <summary>Builds the OSM addItem form fields from a create spec and resolved slot id.</summary>
    internal static Dictionary<string, string> BuildCreateForm(BookingItemCreateSpec spec, string slotId)
        => new()
        {
            ["slot_id"] = slotId,
            ["start"] = spec.StartDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ["end"] = spec.EndDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ["number_people"] = spec.NumberPeople?.ToString() ?? string.Empty,
            ["start_time"] = spec.StartTime ?? string.Empty,
            ["end_time"] = spec.EndTime ?? string.Empty
        };

    /// <summary>Reads the new booked-item id from an addItem response; throws if OSM rejected the create.</summary>
    internal static string ParseCreatedItemId(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.False)
            throw new InvalidOperationException($"OSM rejected item creation: {GetString(root, "error") ?? "unknown error"}");

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("id", out var id))
            return id.ValueKind == JsonValueKind.Number ? id.GetInt64().ToString() : id.GetString() ?? string.Empty;

        throw new InvalidOperationException("OSM create response missing data.id");
    }

    /// <summary>True when an OSM delete response reports success.</summary>
    internal static bool ParseDeleteSucceeded(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("status", out var status) &&
               status.ValueKind == JsonValueKind.True;
    }

    /// <summary>Parses an OSM per-item questions response into row/definition/answer triples.</summary>
    internal static List<OsmItemQuestion> ParseItemQuestions(string? json)
    {
        var result = new List<OsmItemQuestion>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return result;
        if (!data.TryGetProperty("questions", out var questions) || questions.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var q in questions.EnumerateArray())
            result.Add(new OsmItemQuestion(
                GetInt(q, "id"),
                GetInt(q, "campsite_booking_question_id"),
                GetString(q, "answer") ?? string.Empty));

        return result;
    }

    /// <summary>
    /// Builds the OSM questions-POST payload (a JSON array of {id, answer}) by mapping the
    /// original answers (keyed by question-definition id) onto the clone's answer-row ids.
    /// Only questions with a non-empty original answer are included.
    /// </summary>
    internal static string BuildAnswersJson(
        IReadOnlyDictionary<int, string> answersByDefId, IEnumerable<OsmItemQuestion> cloneQuestions)
    {
        var payload = cloneQuestions
            .Where(q => answersByDefId.TryGetValue(q.QuestionDefId, out var a) && !string.IsNullOrEmpty(a))
            .Select(q => new { id = q.RowId, answer = answersByDefId[q.QuestionDefId] })
            .ToList();
        return JsonSerializer.Serialize(payload);
    }

    public async Task<List<BookingItemDto>> GetBookingItemsAsync(string osmBookingId)
    {
        // Booked items live on the booking-detail resource, NOT the /items catalogue
        // endpoint (which lists bookable item-types). Confirmed from captured OSM data
        // (see BookingsAssistant.Tests/Fixtures/OsmItems/README.md).
        var url = $"/v3/campsites/bookings/{Uri.EscapeDataString(osmBookingId)}";

        _logger.LogInformation("Fetching OSM items for booking {BookingId}", osmBookingId);

        var response = await SendWithRateLimitAsync(async () =>
        {
            var token = await GetAccessTokenAsync();
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return req;
        });

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OSM items endpoint returned {StatusCode} for booking {BookingId}",
                response.StatusCode, osmBookingId);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new InvalidOperationException($"OSM authentication failed fetching items for booking {osmBookingId}");

            return new List<BookingItemDto>();
        }

        var content = await response.Content.ReadAsStringAsync();
        return ParseBookingItems(content);
    }

    /// <summary>
    /// Parses an OSM booking-detail response (<c>GET /v3/campsites/bookings/{id}</c>)
    /// into our booked-item DTOs. Pure/static so it can be unit-tested against captured
    /// fixtures (see OsmServiceItemParsingTests). Returns an empty list for blank input,
    /// a non-success status, or a missing/empty items array.
    /// </summary>
    internal static List<BookingItemDto> ParseBookingItems(string? json)
    {
        var items = new List<BookingItemDto>();
        if (string.IsNullOrWhiteSpace(json))
            return items;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.False)
            return items;

        if (!root.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("items", out var itemsEl) ||
            itemsEl.ValueKind != JsonValueKind.Array)
            return items;

        foreach (var item in itemsEl.EnumerateArray())
            items.Add(MapBookingItem(item));

        return items;
    }

    private static BookingItemDto MapBookingItem(JsonElement item)
    {
        var (startDate, startTime) = SplitTimestamp(GetString(item, "start_timestamp"));
        var (endDate, endTime) = SplitTimestamp(GetString(item, "end_timestamp"));

        // Site vs activity: activities carry an instructor type / required instructors;
        // sites/pitches have neither. (The "ACTIVITY - " name prefix is cosmetic only.)
        var isActivity = GetInt(item, "campsite_instructor_type_id") > 0 ||
                         GetInt(item, "number_instructors_required") > 0;

        // campsite_item_id is the item-TYPE id (e.g. 1387 Hayvern, 4961 Air Rifle) —
        // the id used in the addItem URL when cloning.
        var typeId = GetString(item, "campsite_item_id");

        var label = item.TryGetProperty("item", out var nested) &&
                    nested.ValueKind == JsonValueKind.Object
            ? GetString(nested, "name") ?? string.Empty
            : string.Empty;

        return new BookingItemDto
        {
            ItemId = GetString(item, "id") ?? string.Empty,   // booked-item id (for delete)
            Type = isActivity ? "activity" : "site",
            SiteId = isActivity ? null : typeId,
            ActivityId = isActivity ? typeId : null,
            StartDate = startDate,
            EndDate = endDate,
            StartTime = startTime,
            EndTime = endTime,
            NumberPeople = item.TryGetProperty("number_people", out var np) &&
                           np.TryGetInt32(out var npVal) ? npVal : null,
            Label = label,
            Questions = ParseBookingQuestions(item)
        };
    }

    private static List<BookingItemQuestion> ParseBookingQuestions(JsonElement item)
    {
        var list = new List<BookingItemQuestion>();
        if (item.TryGetProperty("booking_questions", out var bq) && bq.ValueKind == JsonValueKind.Array)
            foreach (var q in bq.EnumerateArray())
                list.Add(new BookingItemQuestion
                {
                    QuestionDefId = GetInt(q, "campsite_booking_question_id"),
                    Answer = GetString(q, "answer") ?? string.Empty
                });
        return list;
    }

    /// <summary>
    /// Parses the OSM item-type catalogue (GET /v3/campsites/{id}/items?mode=booking) into the bookable
    /// sites/pitches: the leaf item-types (nodes that are not themselves parents) under the "Campsites" and
    /// "Indoor Accommodation" categories. Category nodes and the (separate) "Activities" tree are excluded.
    /// Pure/static for testing.
    /// </summary>
    internal static List<AvailableSiteDto> ParseAvailableSites(string? catalogueJson)
    {
        var sites = new List<AvailableSiteDto>();
        if (string.IsNullOrWhiteSpace(catalogueJson)) return sites;

        using var doc = JsonDocument.Parse(catalogueJson);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return sites;

        // First pass: index nodes by id, group children by parent, find the site category roots
        // (by name), and record which ids are themselves parents (i.e. categories, not leaves).
        var nameById = new Dictionary<int, string>();
        var childrenByParent = new Dictionary<int, List<int>>();
        var parentIds = new HashSet<int>();
        var siteCategoryIds = new List<int>();
        foreach (var node in data.EnumerateArray())
        {
            var id = GetInt(node, "id");
            var name = GetString(node, "name") ?? string.Empty;
            nameById[id] = name;
            if (node.TryGetProperty("parent_id", out var p) && p.TryGetInt32(out var pid))
            {
                parentIds.Add(pid);
                (childrenByParent.TryGetValue(pid, out var list) ? list : childrenByParent[pid] = new List<int>()).Add(id);
            }
            if (name is "Campsites" or "Indoor Accommodation")
                siteCategoryIds.Add(id);
        }

        // Walk all descendants of the site categories; the bookable sites are the leaves
        // (nodes that are not themselves parents of anything). Handles arbitrary nesting depth.
        var queue = new Queue<int>(siteCategoryIds);
        var seen = new HashSet<int>(siteCategoryIds);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children)) continue;
            foreach (var child in children)
            {
                if (!seen.Add(child)) continue;
                if (parentIds.Contains(child))
                    queue.Enqueue(child);   // sub-category — descend
                else
                    sites.Add(new AvailableSiteDto { Id = child.ToString(), Name = nameById[child] });
            }
        }

        return sites;
    }

    /// <summary>Splits an OSM "yyyy-MM-dd HH:mm:ss" timestamp into a date and the "HH:mm" portion of the time.</summary>
    private static (DateTime? Date, string? Time) SplitTimestamp(string? timestamp)
    {
        if (string.IsNullOrWhiteSpace(timestamp))
            return (null, null);

        var parts = timestamp.Split(' ', 2);
        DateTime? date = DateTime.TryParse(parts[0], out var d) ? d.Date : null;
        string? time = parts.Length > 1 && parts[1].Length >= 5 ? parts[1][..5] : null;
        return (date, time);
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop)) return null;
        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.ToString(),
            _ => null
        };
    }

    private static int GetInt(JsonElement el, string name)
        => el.TryGetProperty(name, out var prop) && prop.TryGetInt32(out var v) ? v : 0;

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

        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            // Diagnostic: list the id-like fields on the booking so we can confirm
            // which identifier the contacts/email API expects (ids only, PII-safe).
            static bool LooksLikeId(string n) =>
                n.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                n.EndsWith("_id", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("member", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("scout", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("contact", StringComparison.OrdinalIgnoreCase);
            var idFields = data.EnumerateObject()
                .Where(p => LooksLikeId(p.Name) &&
                            p.Value.ValueKind is JsonValueKind.Number or JsonValueKind.String)
                .Select(p => $"{p.Name}={p.Value}");
            _logger.LogInformation("Booking {BookingId} detail id-fields: {Ids}", osmBookingId, string.Join(", ", idFields));

            if (data.TryGetProperty("member_id", out var memberId))
                return memberId.ToString();
        }

        if (doc.RootElement.TryGetProperty("member_id", out var rootMemberId))
            return rootMemberId.ToString();

        _logger.LogError("member_id not found in booking detail for {BookingId}. Keys: {Keys}",
            osmBookingId,
            string.Join(", ", doc.RootElement.EnumerateObject().Select(p => p.Name)));
        return null;
    }

    // OSM stores a member's contact email under several "primary" contact
    // groups. The send UI queries only the campsite-specific one, which works
    // for members booked through the camp's own section. External bookers (e.g.
    // "1st Great Horkesley: Beavers" — booking 155867, member 3217785) have an
    // empty campsite group but a real address under the lead/member primary
    // groups. We try the campsite group first (byte-identical to OSM's UI, so
    // normal bookings are unaffected) and only fall back to the broader set when
    // it's empty.
    private const string PrimaryCampsiteGroup = "[\"contact_primary_campsite\"]";
    private const string AllPrimaryGroups =
        "[\"contact_primary_campsite\",\"contact_primary_member\",\"contact_primary_1\",\"contact_primary_2\"]";

    private async Task<string?> ResolveContactEmailsAsync(string memberId)
    {
        // First: exactly what OSM's send UI does.
        var emails = await QueryContactEmailsAsync(memberId, PrimaryCampsiteGroup);
        if (emails != null)
            return emails;

        // Fallback: the campsite group was empty. Widen to the other primary
        // contact groups, where external bookers' addresses live.
        emails = await QueryContactEmailsAsync(memberId, AllPrimaryGroups);
        if (emails != null)
        {
            _logger.LogInformation(
                "getSelectedEmailsFromContacts: resolved member {MemberId} via broader primary groups (campsite group was empty)",
                memberId);
            return emails;
        }

        _logger.LogWarning(
            "getSelectedEmailsFromContacts: no email for member {MemberId} in any primary contact group", memberId);
        return null;
    }

    /// <summary>
    /// Queries OSM's getSelectedEmailsFromContacts for one member across the
    /// given contact groups. Returns the raw "emails" payload (an object keyed
    /// by member id — exactly sendTemplate's "emails" shape) when populated, or
    /// null when the group(s) hold no address. Never logs the address.
    /// </summary>
    private async Task<string?> QueryContactEmailsAsync(string memberId, string contactGroupsJson)
    {
        var response = await SendWithRateLimitAsync(async () =>
        {
            var token = await GetAccessTokenAsync();
            var req = new HttpRequestMessage(HttpMethod.Post,
                $"/ext/members/email/?action=getSelectedEmailsFromContacts&sectionid={_sectionId}");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                // Contact group(s) keyed by the member/scout id, matching OSM's
                // send flow (captured HAR).
                ["contactGroups"] = contactGroupsJson,
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

        // Response shape: { "emails": <map>, "count": N, ... }. When populated,
        // "emails" is an object keyed by member id —
        //   {"3360824":{...,"emails":["a@b.com"]}} — which is exactly
        // sendTemplate's "emails" payload. PHP encodes the empty case as []. So
        // return the raw "emails" object for the sender; callers extract the
        // address from it. Treat empty as "no email".
        if (doc.RootElement.TryGetProperty("emails", out var emails))
        {
            var isEmpty = (emails.ValueKind == JsonValueKind.Array && emails.GetArrayLength() == 0)
                       || (emails.ValueKind == JsonValueKind.Object && !emails.EnumerateObject().Any());
            return isEmpty ? null : emails.GetRawText();
        }

        _logger.LogWarning("getSelectedEmailsFromContacts: no 'emails' field for member {MemberId} (keys: {Keys})",
            memberId, string.Join(", ", doc.RootElement.EnumerateObject().Select(p => p.Name)));
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
