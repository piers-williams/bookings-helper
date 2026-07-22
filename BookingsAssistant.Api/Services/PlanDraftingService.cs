using System.Text;
using System.Text.Json;
using BookingsAssistant.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Services;

internal class PlanDraftingService : IPlanDraftingService
{
    // The six action types the LLM is allowed to propose. Kept in sync with the schema
    // described in the system prompt below and with chunk 3 (execution), which will map
    // these onto BookingActionsController's move-activity / change-site / move-dates DTOs.
    private static readonly HashSet<string> KnownActionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "draftEmailReply", "postComment", "sendTemplateEmail", "moveDates", "changeSite", "moveActivity"
    };

    private const string SystemPrompt =
        "You are an assistant that drafts action plans for a Scout campsite bookings team. " +
        "Given a customer's email (and, if available, context about their existing booking), " +
        "respond with ONLY a JSON object of the shape:\n" +
        "{ \"actions\": [ { \"type\": \"<one of the types below>\", ...type-specific fields } ] }\n\n" +
        "Valid action types and their fields (all fields are flat, alongside \"type\" — not nested):\n" +
        "- draftEmailReply: { \"text\": string } — a suggested reply to send back to the customer\n" +
        "- postComment: { \"text\": string } — an internal note to post as an OSM comment on the booking\n" +
        "- sendTemplateEmail: {} — sends the booking's standard template email (no extra fields; the booking is already known)\n" +
        "- moveDates: { \"dayShift\": number, \"note\"?: string } — shifts every item in the booking by this many days\n" +
        "- changeSite: { \"itemId\": string, \"newSiteId\": string, \"newSiteName\"?: string, \"note\"?: string } — moves a site item to a different site\n" +
        "- moveActivity: { \"itemId\": string, \"newStartDate\"?: string, \"newStartTime\"?: string, \"newEndTime\"?: string, \"note\"?: string } — reschedules an activity item\n\n" +
        "Choose whichever actions (and however many, including zero if none apply) best address the email. " +
        "You decide the order — e.g. draftEmailReply can come first, last, or be omitted entirely. " +
        "Return JSON only: no prose, no markdown code fences, no explanation.";

    private readonly IOpenWebUiClient _client;
    private readonly ApplicationDbContext _context;
    private readonly IOsmService _osmService;
    private readonly ILogger<PlanDraftingService> _logger;

    public PlanDraftingService(
        IOpenWebUiClient client,
        ApplicationDbContext context,
        IOsmService osmService,
        ILogger<PlanDraftingService> logger)
    {
        _client = client;
        _context = context;
        _osmService = osmService;
        _logger = logger;
    }

    public async Task<PlanDraftResult> DraftPlanAsync(string sourceEmailText, string? osmBookingId)
    {
        var userPrompt = await BuildUserPromptAsync(sourceEmailText, osmBookingId);

        var firstResponse = await _client.GetCompletionAsync(SystemPrompt, userPrompt);
        if (TryValidate(firstResponse, out var actionsJson, out var reason))
            return PlanDraftResult.Ok(actionsJson);

        _logger.LogWarning("Plan drafting: first LLM response was invalid ({Reason}); retrying once", reason);

        var retryPrompt = userPrompt +
            $"\n\nYour previous response was invalid: {reason}. Return valid JSON only, matching the schema exactly.";
        var retryResponse = await _client.GetCompletionAsync(SystemPrompt, retryPrompt);
        if (TryValidate(retryResponse, out actionsJson, out reason))
            return PlanDraftResult.Ok(actionsJson);

        _logger.LogWarning("Plan drafting: retry LLM response was also invalid ({Reason}); giving up", reason);
        return PlanDraftResult.Fail(reason ?? "LLM response invalid after retry");
    }

    private async Task<string> BuildUserPromptAsync(string sourceEmailText, string? osmBookingId)
    {
        var sb = new StringBuilder();

        var bookingContext = await BuildBookingContextAsync(osmBookingId);
        if (bookingContext != null)
        {
            sb.AppendLine("Booking context:");
            sb.AppendLine(bookingContext);
            sb.AppendLine();
        }

        sb.AppendLine("Customer email:");
        sb.AppendLine(sourceEmailText);

        return sb.ToString();
    }

    /// <summary>
    /// Pulls the booking's core fields from the DB and its site/activity line-items from OSM
    /// (best-effort — a failed OSM fetch, e.g. no auth yet, just omits the items section rather
    /// than failing the whole drafting attempt). Returns null if no booking id was given or the
    /// booking isn't known locally.
    /// </summary>
    private async Task<string?> BuildBookingContextAsync(string? osmBookingId)
    {
        if (string.IsNullOrWhiteSpace(osmBookingId))
            return null;

        var booking = await _context.OsmBookings
            .FirstOrDefaultAsync(b => b.OsmBookingId == osmBookingId);
        if (booking == null)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine($"- Customer: {booking.CustomerName}");
        sb.AppendLine($"- Dates: {booking.StartDate:yyyy-MM-dd} to {booking.EndDate:yyyy-MM-dd}");
        sb.AppendLine($"- Status: {booking.Status}");

        try
        {
            var items = await _osmService.GetBookingItemsAsync(osmBookingId);
            foreach (var item in items)
            {
                var when = item.StartDate.HasValue
                    ? $", {item.StartDate:yyyy-MM-dd}" +
                      (item.StartTime != null ? $" {item.StartTime}-{item.EndTime}" : string.Empty)
                    : string.Empty;
                sb.AppendLine($"- Item [{item.ItemId}] ({item.Type}): {item.Label}{when}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plan drafting: failed to fetch booking items for {BookingId}; omitting from prompt", osmBookingId);
        }

        return sb.ToString().TrimEnd();
    }

    private static bool TryValidate(string llmResponse, out string actionsJson, out string? failureReason)
    {
        actionsJson = string.Empty;
        failureReason = null;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(ExtractJsonPayload(llmResponse));
        }
        catch (JsonException ex)
        {
            failureReason = $"response was not valid JSON ({ex.Message})";
            return false;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("actions", out var actionsEl) ||
                actionsEl.ValueKind != JsonValueKind.Array ||
                actionsEl.GetArrayLength() == 0)
            {
                failureReason = "response must be a JSON object with a non-empty \"actions\" array";
                return false;
            }

            foreach (var action in actionsEl.EnumerateArray())
            {
                if (action.ValueKind != JsonValueKind.Object ||
                    !action.TryGetProperty("type", out var typeEl) ||
                    typeEl.ValueKind != JsonValueKind.String)
                {
                    failureReason = "each action must be an object with a string \"type\"";
                    return false;
                }

                var type = typeEl.GetString()!;
                if (!KnownActionTypes.Contains(type))
                {
                    failureReason = $"unknown action type \"{type}\"";
                    return false;
                }

                if (!HasRequiredParams(type, action, out failureReason))
                    return false;
            }

            actionsJson = actionsEl.GetRawText();
            return true;
        }
    }

    private static bool HasRequiredParams(string type, JsonElement action, out string? reason)
    {
        // Returns null if `prop` is present as a non-empty string on `action`; otherwise
        // returns the failure message. A local function can't capture the outer `out`
        // parameter, so this returns the reason instead of assigning it directly.
        string? MissingNonEmptyString(string prop)
        {
            if (!action.TryGetProperty(prop, out var el) ||
                el.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(el.GetString()))
            {
                return $"action \"{type}\" requires a non-empty string \"{prop}\"";
            }
            return null;
        }

        switch (type.ToLowerInvariant())
        {
            case "draftemailreply":
            case "postcomment":
                reason = MissingNonEmptyString("text");
                return reason == null;

            case "sendtemplateemail":
                // No extra fields — the booking id comes from the plan itself.
                reason = null;
                return true;

            case "movedates":
                if (!action.TryGetProperty("dayShift", out var dayShiftEl) || dayShiftEl.ValueKind != JsonValueKind.Number)
                {
                    reason = "action \"moveDates\" requires a numeric \"dayShift\"";
                    return false;
                }
                reason = null;
                return true;

            case "changesite":
                reason = MissingNonEmptyString("itemId") ?? MissingNonEmptyString("newSiteId");
                return reason == null;

            case "moveactivity":
                reason = MissingNonEmptyString("itemId");
                return reason == null;

            default:
                reason = $"unknown action type \"{type}\"";
                return false;
        }
    }

    /// <summary>
    /// LLMs frequently wrap JSON replies in ```json fences despite being told not to.
    /// Strips a single leading/trailing fence if present; otherwise returns the input trimmed.
    /// </summary>
    private static string ExtractJsonPayload(string raw)
    {
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("```"))
            return trimmed;

        var firstNewline = trimmed.IndexOf('\n');
        var withoutOpeningFence = firstNewline >= 0 ? trimmed[(firstNewline + 1)..] : trimmed;
        var closingFenceIndex = withoutOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
        var unfenced = closingFenceIndex >= 0 ? withoutOpeningFence[..closingFenceIndex] : withoutOpeningFence;
        return unfenced.Trim();
    }
}
