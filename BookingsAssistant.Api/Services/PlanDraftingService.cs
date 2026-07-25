using System.Linq;
using System.Text;
using System.Text.Json;
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Services;

internal class PlanDraftingService : IPlanDraftingService
{
    // The action types the LLM is allowed to propose. Kept in sync with the schema described
    // in the system prompt below and with PlanExecutionService, which maps these onto
    // BookingActionsController's move-activity / change-site / move-dates / add-activity /
    // remove-activity / change-numbers / availability DTOs.
    private static readonly HashSet<string> KnownActionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "draftEmailReply", "postComment", "sendTemplateEmail", "moveDates", "changeSite", "moveActivity", "addActivity", "removeActivity", "changeNumbers", "checkAvailability"
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
        "- moveActivity: { \"itemId\": string, \"newStartDate\"?: string, \"newStartTime\"?: string, \"newEndTime\"?: string, \"note\"?: string } — " +
        "reschedules an activity item. Omitting newStartDate while supplying newStartTime/newEndTime reschedules only the time, keeping the item's original date\n" +
        "- addActivity: { \"activityId\": string, \"newStartDate\": string, \"newEndDate\": string, \"newStartTime\"?: string, \"newEndTime\"?: string, \"numberPeople\": number, \"note\"?: string } — adds a brand-new activity item to the booking\n" +
        "- removeActivity: { \"itemId\": string, \"note\"?: string } — permanently removes an existing item (activity or site) from the booking\n" +
        "- changeNumbers: { \"itemId\": string, \"newNumberPeople\": number, \"note\"?: string } — changes the number of people on an existing item\n" +
        "- checkAvailability: { \"activityId\": string, \"newStartDate\": string, \"newEndDate\": string, \"note\"?: string } — " +
        "read-only check of whether a site/activity item-type has an available slot for the given date range. " +
        "This is the only action that never creates/modifies/deletes anything — use it to check before proposing addActivity\n\n" +
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

    /// <summary>
    /// Drafts a plan, allowing itself exactly ONE retry across the whole attempt — shared
    /// between two different reasons a response might not be usable: failing JSON/schema
    /// validation (<see cref="TryValidate"/>), or passing validation but proposing a
    /// date-carrying action (currently only addActivity) for a slot the availability pre-check
    /// says isn't actually available (<see cref="CheckActionsAvailabilityAsync"/>). Whichever of
    /// those the first attempt hits, the retry prompt describes it and asks the LLM to
    /// self-correct; the retry's outcome is then final — a still-invalid retry fails drafting,
    /// while a still-unavailable retry succeeds anyway with a warning (see
    /// <see cref="PlanDraftResult.Warning"/>) rather than failing, since an availability
    /// conflict is something a human can review, not a broken response.
    /// </summary>
    public async Task<PlanDraftResult> DraftPlanAsync(string sourceEmailText, string? osmBookingId)
    {
        var userPrompt = await BuildUserPromptAsync(sourceEmailText, osmBookingId);

        var firstResponse = await _client.GetCompletionAsync(SystemPrompt, userPrompt);
        var firstAttempt = await EvaluateAttemptAsync(firstResponse, osmBookingId);
        if (firstAttempt.IsClean)
            return PlanDraftResult.Ok(firstAttempt.ActionsJson!);

        var retryFeedback = firstAttempt.ValidationFailureReason != null
            ? $"Your previous response was invalid: {firstAttempt.ValidationFailureReason}. Return valid JSON only, matching the schema exactly."
            : BuildConflictFeedback(firstAttempt.Conflicts);

        _logger.LogWarning(
            "Plan drafting: first attempt was not usable ({Reason}); retrying once",
            firstAttempt.ValidationFailureReason ?? DescribeConflicts(firstAttempt.Conflicts));

        var retryPrompt = userPrompt + "\n\n" + retryFeedback;
        var retryResponse = await _client.GetCompletionAsync(SystemPrompt, retryPrompt);
        var retryAttempt = await EvaluateAttemptAsync(retryResponse, osmBookingId);

        if (retryAttempt.ValidationFailureReason != null)
        {
            _logger.LogWarning("Plan drafting: retry LLM response was also invalid ({Reason}); giving up", retryAttempt.ValidationFailureReason);
            return PlanDraftResult.Fail(retryAttempt.ValidationFailureReason);
        }

        if (retryAttempt.Conflicts.Count == 0)
            return PlanDraftResult.Ok(retryAttempt.ActionsJson!);

        // The one retry drafting allows itself is now spent, and a conflict remains. Don't fail
        // drafting for this -- save the plan with a warning so a human sees it before approving.
        _logger.LogWarning(
            "Plan drafting: availability conflict remained after the retry ({Reason}); saving with a warning",
            DescribeConflicts(retryAttempt.Conflicts));
        return PlanDraftResult.Ok(retryAttempt.ActionsJson!, BuildWarning(retryAttempt.Conflicts));
    }

    /// <summary>
    /// One LLM response, evaluated all the way through: JSON/schema validation, then (only if
    /// that passed) the availability pre-check. <see cref="IsClean"/> is true only when both
    /// passed, meaning the response is directly usable as a <see cref="PlanDraftResult.Ok"/>.
    /// </summary>
    private sealed class DraftAttempt
    {
        public string? ActionsJson { get; init; }
        public string? ValidationFailureReason { get; init; }
        public List<AvailabilityConflict> Conflicts { get; init; } = new();

        public bool IsClean => ValidationFailureReason == null && Conflicts.Count == 0;
    }

    /// <summary>One date-carrying action whose availability pre-check found no slot.</summary>
    private sealed record AvailabilityConflict(string ActionType, string ActivityId, DateTime StartDate, DateTime EndDate, string? Reason);

    private async Task<DraftAttempt> EvaluateAttemptAsync(string llmResponse, string? osmBookingId)
    {
        if (!TryValidate(llmResponse, out var actionsJson, out var reason))
            return new DraftAttempt { ValidationFailureReason = reason };

        var conflicts = await CheckActionsAvailabilityAsync(actionsJson, osmBookingId);
        return new DraftAttempt { ActionsJson = actionsJson, Conflicts = conflicts };
    }

    /// <summary>
    /// Scans the validated actions for any that carry a full date range against a resolvable
    /// item-type id — currently only addActivity (activityId + newStartDate + newEndDate, both
    /// required by <see cref="HasRequiredParams"/>). moveActivity is deliberately excluded: its
    /// schema has no newEndDate, and its itemId refers to an item already on the booking rather
    /// than the catalogue item-type id CheckAvailabilityAsync needs, so there's no date range to
    /// meaningfully pre-check there. Returns no conflicts (rather than throwing) when there's no
    /// known booking to check against, or when the OSM call itself fails — this pre-check is
    /// best-effort, same as <see cref="BuildBookingContextAsync"/>'s item fetch, not something
    /// that should make drafting itself brittle.
    /// </summary>
    private async Task<List<AvailabilityConflict>> CheckActionsAvailabilityAsync(string actionsJson, string? osmBookingId)
    {
        var conflicts = new List<AvailabilityConflict>();
        if (string.IsNullOrWhiteSpace(osmBookingId))
            return conflicts;

        using var doc = JsonDocument.Parse(actionsJson);
        foreach (var action in doc.RootElement.EnumerateArray())
        {
            if (!action.TryGetProperty("type", out var typeEl) ||
                typeEl.ValueKind != JsonValueKind.String ||
                !string.Equals(typeEl.GetString(), "addActivity", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var activityId = action.GetProperty("activityId").GetString()!;
            var startDate = DateTime.Parse(action.GetProperty("newStartDate").GetString()!);
            var endDate = DateTime.Parse(action.GetProperty("newEndDate").GetString()!);

            AvailabilityResult availability;
            try
            {
                availability = await _osmService.CheckAvailabilityAsync(osmBookingId, activityId, startDate, endDate);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Plan drafting: availability pre-check failed for addActivity {ActivityId}; skipping the check", activityId);
                continue;
            }

            if (!availability.Available)
                conflicts.Add(new AvailabilityConflict("addActivity", activityId, startDate, endDate, availability.Reason));
        }

        return conflicts;
    }

    private static string BuildConflictFeedback(List<AvailabilityConflict> conflicts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Your previous response was valid JSON, but an availability check found a problem:");
        foreach (var c in conflicts)
        {
            sb.AppendLine(
                $"- {c.ActionType} for activityId \"{c.ActivityId}\" from {c.StartDate:yyyy-MM-dd} to {c.EndDate:yyyy-MM-dd} " +
                $"is not available: {c.Reason ?? "no slot"}.");
        }
        sb.Append("Propose different dates for the affected action(s), or drop them, and return valid JSON only.");
        return sb.ToString();
    }

    private static string BuildWarning(List<AvailabilityConflict> conflicts)
    {
        var parts = conflicts.Select(c =>
            $"{c.ActionType} for activityId \"{c.ActivityId}\" ({c.StartDate:yyyy-MM-dd} to {c.EndDate:yyyy-MM-dd}) is not available: {c.Reason ?? "no slot"}");
        return "Availability conflict — review before approving: " + string.Join("; ", parts);
    }

    private static string DescribeConflicts(List<AvailabilityConflict> conflicts) =>
        string.Join("; ", conflicts.Select(c => $"{c.ActionType}:{c.ActivityId}"));

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
            case "removeactivity":
                reason = MissingNonEmptyString("itemId");
                return reason == null;

            case "changenumbers":
                reason = MissingNonEmptyString("itemId");
                if (reason != null) return false;

                if (!action.TryGetProperty("newNumberPeople", out var newNumberPeopleEl) || newNumberPeopleEl.ValueKind != JsonValueKind.Number)
                {
                    reason = "action \"changeNumbers\" requires a numeric \"newNumberPeople\"";
                    return false;
                }
                reason = null;
                return true;

            case "addactivity":
                reason = MissingNonEmptyString("activityId") ?? MissingNonEmptyString("newStartDate") ?? MissingNonEmptyString("newEndDate");
                if (reason != null) return false;

                if (!action.TryGetProperty("numberPeople", out var numberPeopleEl) || numberPeopleEl.ValueKind != JsonValueKind.Number)
                {
                    reason = "action \"addActivity\" requires a numeric \"numberPeople\"";
                    return false;
                }
                reason = null;
                return true;

            case "checkavailability":
                reason = MissingNonEmptyString("activityId") ?? MissingNonEmptyString("newStartDate") ?? MissingNonEmptyString("newEndDate");
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
