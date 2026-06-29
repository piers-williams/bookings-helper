namespace BookingsAssistant.Api.Services;

/// <summary>
/// A question-answer row as returned by OSM's per-item questions endpoint
/// (GET /v3/campsites/bookings/items/{itemId}/questions). <see cref="RowId"/> is the
/// per-item answer row (used when POSTing answers back); <see cref="QuestionDefId"/> is
/// the stable question definition (used to match answers across original ↔ clone).
/// </summary>
public record OsmItemQuestion(int RowId, int QuestionDefId, string Answer);
