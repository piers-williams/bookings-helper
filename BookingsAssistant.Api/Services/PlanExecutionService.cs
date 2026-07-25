using System.Text.Json;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Models;

namespace BookingsAssistant.Api.Services;

/// <inheritdoc cref="IPlanExecutionService"/>
public class PlanExecutionService : IPlanExecutionService
{
    private readonly IOsmService _osmService;
    private readonly IBookingItemActionService _itemActionService;
    private readonly ILogger<PlanExecutionService> _logger;

    public PlanExecutionService(
        IOsmService osmService,
        IBookingItemActionService itemActionService,
        ILogger<PlanExecutionService> logger)
    {
        _osmService = osmService;
        _itemActionService = itemActionService;
        _logger = logger;
    }

    public async Task<PlanExecutionOutcome> ExecuteAsync(ProposedPlan plan)
    {
        var actions = ParseActions(plan.ActionsJson);
        var results = new List<PlanActionExecutionResult>();
        var stopped = false;

        foreach (var action in actions)
        {
            var type = action.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String
                ? typeEl.GetString()!
                : "unknown";

            if (stopped)
            {
                results.Add(new PlanActionExecutionResult { Type = type, Status = PlanActionExecutionStatus.NotAttempted });
                continue;
            }

            try
            {
                await ExecuteActionAsync(type, action, plan.OsmBookingId);
                results.Add(new PlanActionExecutionResult { Type = type, Status = PlanActionExecutionStatus.Succeeded });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Plan execution: action '{Type}' failed for plan {PlanId}; stopping remaining actions",
                    type, plan.Id);
                results.Add(new PlanActionExecutionResult
                {
                    Type = type,
                    Status = PlanActionExecutionStatus.Failed,
                    Reason = ex.Message
                });
                stopped = true;
            }
        }

        return new PlanExecutionOutcome
        {
            Success = !stopped,
            Results = results
        };
    }

    private async Task ExecuteActionAsync(string type, JsonElement action, string? osmBookingId)
    {
        switch (type.ToLowerInvariant())
        {
            case "draftemailreply":
                // Purely informational — nothing to execute against OSM.
                return;

            case "postcomment":
            {
                var bookingId = RequireBookingId(osmBookingId, type);
                var text = RequireString(action, "text", type);
                var posted = await _osmService.PostCommentAsync(bookingId, text);
                if (posted == null)
                    throw new InvalidOperationException($"Failed to post comment to OSM for booking {bookingId}");
                return;
            }

            case "sendtemplateemail":
            {
                var bookingId = RequireBookingId(osmBookingId, type);
                var sent = await _osmService.SendBookingTemplateEmailAsync(bookingId);
                if (!sent)
                    throw new InvalidOperationException($"Failed to send template email for booking {bookingId}");
                return;
            }

            case "movedates":
            {
                var bookingId = RequireBookingId(osmBookingId, type);
                if (!action.TryGetProperty("dayShift", out var dayShiftEl) || dayShiftEl.ValueKind != JsonValueKind.Number)
                    throw new InvalidOperationException("Action \"moveDates\" requires a numeric \"dayShift\"");

                var request = new MoveDatesRequest
                {
                    DayShift = dayShiftEl.GetInt32(),
                    Note = OptionalString(action, "note")
                };
                var result = await _itemActionService.MoveDatesAsync(bookingId, request);
                ThrowIfNotSuccessful(result);
                return;
            }

            case "changesite":
            {
                var bookingId = RequireBookingId(osmBookingId, type);
                var request = new ChangeSiteRequest
                {
                    ItemId = RequireString(action, "itemId", type),
                    NewSiteId = RequireString(action, "newSiteId", type),
                    NewSiteName = OptionalString(action, "newSiteName"),
                    Note = OptionalString(action, "note")
                };
                var result = await _itemActionService.ChangeSiteAsync(bookingId, request);
                ThrowIfNotSuccessful(result);
                return;
            }

            case "moveactivity":
            {
                var bookingId = RequireBookingId(osmBookingId, type);
                var request = new MoveActivityRequest
                {
                    ItemId = RequireString(action, "itemId", type),
                    NewStartDate = OptionalDate(action, "newStartDate"),
                    NewStartTime = OptionalString(action, "newStartTime"),
                    NewEndTime = OptionalString(action, "newEndTime"),
                    Note = OptionalString(action, "note")
                };
                var result = await _itemActionService.MoveActivityAsync(bookingId, request);
                ThrowIfNotSuccessful(result);
                return;
            }

            case "addactivity":
            {
                var bookingId = RequireBookingId(osmBookingId, type);
                var request = new AddActivityRequest
                {
                    ActivityId = RequireString(action, "activityId", type),
                    StartDate = RequireDate(action, "newStartDate", type),
                    EndDate = RequireDate(action, "newEndDate", type),
                    StartTime = OptionalString(action, "newStartTime"),
                    EndTime = OptionalString(action, "newEndTime"),
                    NumberPeople = RequireInt(action, "numberPeople", type),
                    Note = OptionalString(action, "note")
                };
                var result = await _itemActionService.AddActivityAsync(bookingId, request);
                ThrowIfNotSuccessful(result);
                return;
            }

            case "removeactivity":
            {
                var bookingId = RequireBookingId(osmBookingId, type);
                var request = new RemoveActivityRequest
                {
                    ItemId = RequireString(action, "itemId", type),
                    Note = OptionalString(action, "note")
                };
                var result = await _itemActionService.RemoveActivityAsync(bookingId, request);
                ThrowIfNotSuccessful(result);
                return;
            }

            default:
                throw new InvalidOperationException($"Unknown action type \"{type}\"");
        }
    }

    private static void ThrowIfNotSuccessful(BookingActionResult result)
    {
        if (result.Status != BookingActionStatus.Completed && result.Status != BookingActionStatus.CompletedWithWarnings)
            throw new InvalidOperationException(result.Message);
    }

    private static string RequireBookingId(string? osmBookingId, string type)
    {
        if (string.IsNullOrWhiteSpace(osmBookingId))
            throw new InvalidOperationException($"Action \"{type}\" requires a booking, but the plan has no OsmBookingId");
        return osmBookingId;
    }

    private static string RequireString(JsonElement action, string prop, string type)
    {
        if (!action.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(el.GetString()))
            throw new InvalidOperationException($"Action \"{type}\" requires a non-empty string \"{prop}\"");
        return el.GetString()!;
    }

    private static string? OptionalString(JsonElement action, string prop)
        => action.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static DateTime? OptionalDate(JsonElement action, string prop)
    {
        var raw = OptionalString(action, prop);
        return raw != null && DateTime.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static DateTime RequireDate(JsonElement action, string prop, string type)
    {
        if (!action.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.String ||
            !DateTime.TryParse(el.GetString(), out var parsed))
            throw new InvalidOperationException($"Action \"{type}\" requires a valid date \"{prop}\"");
        return parsed;
    }

    private static int RequireInt(JsonElement action, string prop, string type)
    {
        if (!action.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new InvalidOperationException($"Action \"{type}\" requires a numeric \"{prop}\"");
        return v;
    }

    private static List<JsonElement> ParseActions(string? actionsJson)
    {
        if (string.IsNullOrWhiteSpace(actionsJson))
            return new List<JsonElement>();

        using var doc = JsonDocument.Parse(actionsJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return new List<JsonElement>();

        // Clone each element since `doc` is disposed at the end of this method.
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }
}
