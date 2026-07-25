using BookingsAssistant.Api.Models;

namespace BookingsAssistant.Api.Services;

/// <summary>
/// Builds human-readable audit-trail summaries for booking move actions. The result is
/// posted as an OSM comment by BookingActionsController after a successful mutation.
/// </summary>
public static class BookingActionCommentComposer
{
    public static string ComposeChangeSiteSummary(BookingItemDto original, ChangeSiteRequest request)
    {
        var newSiteLabel = string.IsNullOrWhiteSpace(request.NewSiteName)
            ? request.NewSiteId
            : request.NewSiteName;

        return AppendNote($"Site changed: {original.Label} → {newSiteLabel}.", request.Note);
    }

    public static string ComposeMoveActivitySummary(BookingItemDto original, MoveActivityRequest request)
    {
        var changes = new List<string>();

        if (request.NewStartDate.HasValue)
            changes.Add($"date {FormatDate(original.StartDate)} → {FormatDate(request.NewStartDate)}");

        if (request.NewStartTime != null)
            changes.Add($"start time {original.StartTime ?? "—"} → {request.NewStartTime}");

        if (request.NewEndTime != null)
            changes.Add($"end time {original.EndTime ?? "—"} → {request.NewEndTime}");

        var changeText = changes.Count > 0 ? string.Join(", ", changes) : "no fields changed";
        return AppendNote($"Moved '{original.Label}': {changeText}.", request.Note);
    }

    public static string ComposeMoveDatesSummary(MoveDatesRequest request)
        => AppendNote($"Dates shifted by {request.DayShift} day(s).", request.Note);

    public static string ComposeAddActivitySummary(AddActivityRequest request)
    {
        var when = $"{FormatDate(request.StartDate)} → {FormatDate(request.EndDate)}";
        var time = request.StartTime != null || request.EndTime != null
            ? $", {request.StartTime ?? "—"}-{request.EndTime ?? "—"}"
            : string.Empty;

        return AppendNote(
            $"Added activity (item type {request.ActivityId}): {when}{time}, {request.NumberPeople ?? 0} people.",
            request.Note);
    }

    private static string FormatDate(DateTime? date)
        => date.HasValue ? date.Value.ToString("d MMM yyyy") : "—";

    private static string AppendNote(string summary, string? note)
        => string.IsNullOrWhiteSpace(note) ? summary : $"{summary} Note: {note.Trim()}";
}
