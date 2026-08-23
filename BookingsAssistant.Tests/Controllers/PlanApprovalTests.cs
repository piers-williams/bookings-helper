using System.Net;
using System.Net.Http.Json;
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using BookingsAssistant.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookingsAssistant.Tests.Controllers;

public class PlanApprovalTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeOsmService _fakeOsm;

    public PlanApprovalTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_PlanApproval_" + Guid.NewGuid();
        _fakeOsm = new FakeOsmService();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hashing:Iterations"] = "1"
                }));

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                services.RemoveAll<IOsmService>();
                services.AddSingleton<IOsmService>(_fakeOsm);
            });
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<string> SeedBookingAsync(string osmBookingId = "77500")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.OsmBookings.Add(new OsmBooking
        {
            OsmBookingId = osmBookingId,
            CustomerName = "1st Anytown Scouts",
            StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc),
            Status = "Confirmed"
        });
        await db.SaveChangesAsync();
        return osmBookingId;
    }

    private async Task<int> SeedPlanAsync(
        string actionsJson,
        string? osmBookingId,
        PlanStatus status = PlanStatus.AwaitingApproval,
        string? sourceEmailText = "Please note this on the booking")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var plan = new ProposedPlan
        {
            Status = status,
            SourceEmailText = sourceEmailText,
            OsmBookingId = osmBookingId,
            ActionsJson = actionsJson,
            CreatedAt = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc)
        };
        db.ProposedPlans.Add(plan);
        await db.SaveChangesAsync();
        return plan.Id;
    }

    private static BookingItemDto MakeSiteItem(string itemId = "site-item-1") => new()
    {
        ItemId = itemId,
        Type = "site",
        SiteId = "site-42",
        Label = "Pitch A",
        StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc)
    };

    private async Task<ProposedPlan?> GetPlanFromDbAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.ProposedPlans.FindAsync(id);
    }

    // ── Approve ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_SinglePostCommentSucceeds_MarksExecuted_PostsComment_PurgesSourceEmailText()
    {
        var bookingId = await SeedBookingAsync("77501");
        _fakeOsm.CommentToReturn = new CommentDto
        {
            OsmBookingId = bookingId,
            OsmCommentId = "cmt-approve-1",
            AuthorName = "Site Manager",
            TextPreview = "noted",
            CreatedDate = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
        };
        var planId = await SeedPlanAsync(
            "[{\"type\":\"postComment\",\"text\":\"Customer will arrive late\"}]",
            bookingId);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Executed", result.Status);
        Assert.Null(result.SourceEmailText);

        var (postedBookingId, comment) = Assert.Single(_fakeOsm.CommentsPosted);
        Assert.Equal(bookingId, postedBookingId);
        Assert.Equal("Customer will arrive late", comment);

        var stored = await GetPlanFromDbAsync(planId);
        Assert.NotNull(stored);
        Assert.Equal(PlanStatus.Executed, stored.Status);
        Assert.Null(stored.SourceEmailText);
        Assert.NotNull(stored.ExecutionResultJson);
        Assert.Contains("succeeded", stored.ExecutionResultJson);
    }

    [Fact]
    public async Task Approve_StopsAfterFirstFailure_MarksFailed_RecordsPerActionResults()
    {
        var bookingId = await SeedBookingAsync("77502");
        _fakeOsm.CommentToReturn = new CommentDto
        {
            OsmBookingId = bookingId,
            OsmCommentId = "cmt-approve-2",
            AuthorName = "Site Manager",
            TextPreview = "noted",
            CreatedDate = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
        };
        // moveDates will fail: an item exists, but the create call throws.
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeSiteItem() };
        _fakeOsm.FailCreateOnCall = (1, new InvalidOperationException("OSM create failed"));

        var planId = await SeedPlanAsync(
            "[{\"type\":\"postComment\",\"text\":\"noted\"},{\"type\":\"moveDates\",\"dayShift\":1}]",
            bookingId);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);
        Assert.Null(result.SourceEmailText);

        // postComment did run (its own action succeeded) before moveDates failed.
        Assert.Single(_fakeOsm.CommentsPosted);

        Assert.NotNull(result.ExecutionResultJson);
        var results = System.Text.Json.JsonSerializer.Deserialize<List<PlanActionExecutionResult>>(result.ExecutionResultJson!)!;
        Assert.Equal(2, results.Count);
        Assert.Equal("postComment", results[0].Type);
        Assert.Equal(PlanActionExecutionStatus.Succeeded, results[0].Status);
        Assert.Equal("moveDates", results[1].Type);
        Assert.Equal(PlanActionExecutionStatus.Failed, results[1].Status);
        Assert.NotNull(results[1].Reason);

        var stored = await GetPlanFromDbAsync(planId);
        Assert.NotNull(stored);
        Assert.Equal(PlanStatus.Failed, stored.Status);
        Assert.Null(stored.SourceEmailText);
    }

    [Fact]
    public async Task Approve_RecordsNotAttempted_ForActionsAfterFirstFailure()
    {
        var bookingId = await SeedBookingAsync("77503");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeSiteItem() };
        _fakeOsm.FailCreateOnCall = (1, new InvalidOperationException("OSM create failed"));

        var planId = await SeedPlanAsync(
            "[{\"type\":\"moveDates\",\"dayShift\":1},{\"type\":\"sendTemplateEmail\"}]",
            bookingId);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);

        var results = System.Text.Json.JsonSerializer.Deserialize<List<PlanActionExecutionResult>>(result.ExecutionResultJson!)!;
        Assert.Equal(2, results.Count);
        Assert.Equal(PlanActionExecutionStatus.Failed, results[0].Status);
        Assert.Equal("sendTemplateEmail", results[1].Type);
        Assert.Equal(PlanActionExecutionStatus.NotAttempted, results[1].Status);

        // sendTemplateEmail was never attempted.
        Assert.Empty(_fakeOsm.EmailsSent);
    }

    [Fact]
    public async Task Approve_BookingRequiredAction_FailsCleanly_WhenOsmBookingIdIsNull()
    {
        // Plan created without a linked booking (e.g. a general enquiry), but the LLM drafted
        // a booking-required action anyway. This must produce a clean Failed action result
        // with a clear reason -- not an unhandled exception -- and stop before any later
        // actions run.
        var planId = await SeedPlanAsync(
            "[{\"type\":\"postComment\",\"text\":\"noted\"},{\"type\":\"sendTemplateEmail\"}]",
            osmBookingId: null);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);

        var results = System.Text.Json.JsonSerializer.Deserialize<List<PlanActionExecutionResult>>(result.ExecutionResultJson!)!;
        Assert.Equal(2, results.Count);
        Assert.Equal("postComment", results[0].Type);
        Assert.Equal(PlanActionExecutionStatus.Failed, results[0].Status);
        Assert.NotNull(results[0].Reason);
        Assert.Contains("requires a booking", results[0].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("sendTemplateEmail", results[1].Type);
        Assert.Equal(PlanActionExecutionStatus.NotAttempted, results[1].Status);

        // No OSM calls of any kind were made.
        Assert.Empty(_fakeOsm.CommentsPosted);
        Assert.Empty(_fakeOsm.EmailsSent);

        var stored = await GetPlanFromDbAsync(planId);
        Assert.NotNull(stored);
        Assert.Equal(PlanStatus.Failed, stored.Status);
    }

    [Fact]
    public async Task Approve_OnlyDraftEmailReply_MarksExecuted_MakesNoOsmCalls()
    {
        var planId = await SeedPlanAsync(
            "[{\"type\":\"draftEmailReply\",\"text\":\"Thanks, we'll sort this.\"}]",
            osmBookingId: null);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Executed", result.Status);
        Assert.Null(result.SourceEmailText);

        // Zero OSM calls of any kind.
        Assert.Empty(_fakeOsm.CommentsPosted);
        Assert.Empty(_fakeOsm.EmailsSent);
        Assert.Empty(_fakeOsm.CapturedSpecs);
        Assert.Empty(_fakeOsm.DeletedItems);

        var results = System.Text.Json.JsonSerializer.Deserialize<List<PlanActionExecutionResult>>(result.ExecutionResultJson!)!;
        var single = Assert.Single(results);
        Assert.Equal(PlanActionExecutionStatus.Succeeded, single.Status);
    }

    [Fact]
    public async Task Approve_AddActivityActionSucceeds_MarksExecuted_CreatesItemAndPostsComment()
    {
        var bookingId = await SeedBookingAsync("77506");
        _fakeOsm.CreatedItemIds = new List<string> { "new-activity-item" };
        _fakeOsm.CommentToReturn = new CommentDto
        {
            OsmBookingId = bookingId,
            OsmCommentId = "cmt-add-activity-1",
            AuthorName = "Site Manager",
            TextPreview = "Added activity",
            CreatedDate = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
        };
        var planId = await SeedPlanAsync(
            "[{\"type\":\"addActivity\",\"activityId\":\"4962\",\"newStartDate\":\"2026-08-02\"," +
            "\"newEndDate\":\"2026-08-02\",\"numberPeople\":8}]",
            bookingId);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Executed", result.Status);

        var spec = Assert.Single(_fakeOsm.CapturedSpecs);
        Assert.Equal("4962", spec.CampsiteItemId);
        Assert.Equal(8, spec.NumberPeople);

        Assert.Single(_fakeOsm.CommentsPosted);
    }

    [Fact]
    public async Task Approve_AddActivityActionFails_MarksFailed_WhenOsmCreateFails()
    {
        var bookingId = await SeedBookingAsync("77507");
        _fakeOsm.FailCreateOnCall = (1, new InvalidOperationException("No available slot for the requested window"));

        var planId = await SeedPlanAsync(
            "[{\"type\":\"addActivity\",\"activityId\":\"4962\",\"newStartDate\":\"2026-08-02\"," +
            "\"newEndDate\":\"2026-08-02\",\"numberPeople\":8}]",
            bookingId);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);

        var results = System.Text.Json.JsonSerializer.Deserialize<List<PlanActionExecutionResult>>(result.ExecutionResultJson!)!;
        var single = Assert.Single(results);
        Assert.Equal("addActivity", single.Type);
        Assert.Equal(PlanActionExecutionStatus.Failed, single.Status);
        // Must be the OSM create failure surfacing, not a generic "unknown action type" —
        // proves addActivity actually reached IBookingItemActionService.AddActivityAsync.
        Assert.Contains("No available slot", single.Reason);
    }

    [Fact]
    public async Task Approve_RemoveActivityActionSucceeds_MarksExecuted_DeletesItemAndPostsComment()
    {
        var bookingId = await SeedBookingAsync("77508");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeSiteItem("site-item-1") };
        _fakeOsm.CommentToReturn = new CommentDto
        {
            OsmBookingId = bookingId,
            OsmCommentId = "cmt-remove-activity-1",
            AuthorName = "Site Manager",
            TextPreview = "Removed 'Pitch A'.",
            CreatedDate = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
        };
        var planId = await SeedPlanAsync(
            "[{\"type\":\"removeActivity\",\"itemId\":\"site-item-1\"}]",
            bookingId);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Executed", result.Status);

        Assert.Contains((bookingId, "site-item-1"), _fakeOsm.DeletedItems);
        Assert.Single(_fakeOsm.CommentsPosted);
    }

    [Fact]
    public async Task Approve_RemoveActivityActionFails_MarksFailed_WhenItemNotInBooking()
    {
        var bookingId = await SeedBookingAsync("77509");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto>(); // item-does-not-exist isn't among the booking's items

        var planId = await SeedPlanAsync(
            "[{\"type\":\"removeActivity\",\"itemId\":\"item-does-not-exist\"}]",
            bookingId);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);

        var results = System.Text.Json.JsonSerializer.Deserialize<List<PlanActionExecutionResult>>(result.ExecutionResultJson!)!;
        var single = Assert.Single(results);
        Assert.Equal("removeActivity", single.Type);
        Assert.Equal(PlanActionExecutionStatus.Failed, single.Status);
    }

    [Fact]
    public async Task Approve_ChangeNumbersActionSucceeds_MarksExecuted_ReplacesItemAndPostsComment()
    {
        var bookingId = await SeedBookingAsync("77511");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeSiteItem("site-item-1") };
        _fakeOsm.CreatedItemIds = new List<string> { "site-item-new" };
        _fakeOsm.CommentToReturn = new CommentDto
        {
            OsmBookingId = bookingId,
            OsmCommentId = "cmt-change-numbers-1",
            AuthorName = "Site Manager",
            TextPreview = "Number of people changed",
            CreatedDate = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
        };
        var planId = await SeedPlanAsync(
            "[{\"type\":\"changeNumbers\",\"itemId\":\"site-item-1\",\"newNumberPeople\":10}]",
            bookingId);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Executed", result.Status);

        var spec = Assert.Single(_fakeOsm.CapturedSpecs);
        Assert.Equal(10, spec.NumberPeople);
        Assert.Contains("site-item-1", _fakeOsm.DeletedItems.Select(d => d.ItemId));
        Assert.Single(_fakeOsm.CommentsPosted);
    }

    [Fact]
    public async Task Approve_ChangeNumbersActionFails_MarksFailed_WhenItemNotInBooking()
    {
        var bookingId = await SeedBookingAsync("77512");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto>(); // item-does-not-exist isn't among the booking's items

        var planId = await SeedPlanAsync(
            "[{\"type\":\"changeNumbers\",\"itemId\":\"item-does-not-exist\",\"newNumberPeople\":10}]",
            bookingId);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);

        var results = System.Text.Json.JsonSerializer.Deserialize<List<PlanActionExecutionResult>>(result.ExecutionResultJson!)!;
        var single = Assert.Single(results);
        Assert.Equal("changeNumbers", single.Type);
        Assert.Equal(PlanActionExecutionStatus.Failed, single.Status);
    }

    [Fact]
    public async Task Approve_ChangeNumbersActionFailure_StopsSubsequentActions()
    {
        var bookingId = await SeedBookingAsync("77513");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto>(); // changeNumbers' itemId won't be found

        var planId = await SeedPlanAsync(
            "[{\"type\":\"changeNumbers\",\"itemId\":\"item-does-not-exist\",\"newNumberPeople\":10}," +
            "{\"type\":\"postComment\",\"text\":\"should not run\"}]",
            bookingId);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);

        var results = System.Text.Json.JsonSerializer.Deserialize<List<PlanActionExecutionResult>>(result.ExecutionResultJson!)!;
        Assert.Equal(2, results.Count);
        Assert.Equal(PlanActionExecutionStatus.Failed, results[0].Status);
        Assert.Equal(PlanActionExecutionStatus.NotAttempted, results[1].Status);
        Assert.Empty(_fakeOsm.CommentsPosted);
    }

    [Fact]
    public async Task Approve_RemoveActivityActionFailure_StopsSubsequentActions()
    {
        var bookingId = await SeedBookingAsync("77510");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto>(); // removeActivity's itemId won't be found

        var planId = await SeedPlanAsync(
            "[{\"type\":\"removeActivity\",\"itemId\":\"item-does-not-exist\"}," +
            "{\"type\":\"postComment\",\"text\":\"should not run\"}]",
            bookingId);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);

        var results = System.Text.Json.JsonSerializer.Deserialize<List<PlanActionExecutionResult>>(result.ExecutionResultJson!)!;
        Assert.Equal(2, results.Count);
        Assert.Equal(PlanActionExecutionStatus.Failed, results[0].Status);
        Assert.Equal(PlanActionExecutionStatus.NotAttempted, results[1].Status);
        Assert.Empty(_fakeOsm.CommentsPosted);
    }

    [Fact]
    public async Task Approve_CheckAvailabilityAvailable_MarksExecuted_StatusSucceeded_DetailSaysAvailable()
    {
        // The important behavioral distinction: checkAvailability is read-only, so BOTH the
        // available and unavailable outcomes are a "succeeded" query -- only real OSM/auth
        // failures should ever produce "failed" here.
        var bookingId = await SeedBookingAsync("77514");
        _fakeOsm.AvailabilityResultToReturn = new AvailabilityResult { Available = true };

        var planId = await SeedPlanAsync(
            "[{\"type\":\"checkAvailability\",\"activityId\":\"4962\"," +
            "\"newStartDate\":\"2026-08-02\",\"newEndDate\":\"2026-08-02\"}]",
            bookingId);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Executed", result.Status);

        var results = System.Text.Json.JsonSerializer.Deserialize<List<PlanActionExecutionResult>>(result.ExecutionResultJson!)!;
        var single = Assert.Single(results);
        Assert.Equal("checkAvailability", single.Type);
        Assert.Equal(PlanActionExecutionStatus.Succeeded, single.Status);
        Assert.NotNull(single.Detail);
        Assert.Contains("Available", single.Detail);

        var call = Assert.Single(_fakeOsm.AvailabilityChecks);
        Assert.Equal(bookingId, call.OsmBookingId);
        Assert.Equal("4962", call.CampsiteItemId);
    }

    [Fact]
    public async Task Approve_CheckAvailabilityUnavailable_StillMarksExecuted_StatusSucceeded_DetailExplainsWhy()
    {
        var bookingId = await SeedBookingAsync("77515");
        _fakeOsm.AvailabilityResultToReturn = new AvailabilityResult
        {
            Available = false,
            Reason = "No available slot for 2026-08-02 to 2026-08-02"
        };

        var planId = await SeedPlanAsync(
            "[{\"type\":\"checkAvailability\",\"activityId\":\"4962\"," +
            "\"newStartDate\":\"2026-08-02\",\"newEndDate\":\"2026-08-02\"}]",
            bookingId);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        // Not a failure -- the query ran fine, it just reports the slot doesn't exist.
        Assert.Equal("Executed", result.Status);

        var results = System.Text.Json.JsonSerializer.Deserialize<List<PlanActionExecutionResult>>(result.ExecutionResultJson!)!;
        var single = Assert.Single(results);
        Assert.Equal("checkAvailability", single.Type);
        Assert.Equal(PlanActionExecutionStatus.Succeeded, single.Status);
        Assert.NotNull(single.Detail);
        Assert.Contains("No available slot", single.Detail);
        Assert.Null(single.Reason); // Reason is for actual failures, not this
    }

    [Fact]
    public async Task Approve_CheckAvailabilityActionFails_MarksFailed_WhenOsmThrows()
    {
        var bookingId = await SeedBookingAsync("77516");
        _fakeOsm.CheckAvailabilityError = new InvalidOperationException("OSM authentication failed checking availability");

        var planId = await SeedPlanAsync(
            "[{\"type\":\"checkAvailability\",\"activityId\":\"4962\"," +
            "\"newStartDate\":\"2026-08-02\",\"newEndDate\":\"2026-08-02\"}]",
            bookingId);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);

        var results = System.Text.Json.JsonSerializer.Deserialize<List<PlanActionExecutionResult>>(result.ExecutionResultJson!)!;
        var single = Assert.Single(results);
        Assert.Equal("checkAvailability", single.Type);
        Assert.Equal(PlanActionExecutionStatus.Failed, single.Status);
        Assert.NotNull(single.Reason);
    }

    [Fact]
    public async Task Approve_ReturnsConflict_WhenPlanNotAwaitingApproval()
    {
        var planId = await SeedPlanAsync(
            "[{\"type\":\"draftEmailReply\",\"text\":\"already done\"}]",
            osmBookingId: null,
            status: PlanStatus.Executed);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/approve", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // No re-execution happened — status is untouched.
        var stored = await GetPlanFromDbAsync(planId);
        Assert.NotNull(stored);
        Assert.Equal(PlanStatus.Executed, stored.Status);
    }

    [Fact]
    public async Task Approve_ReturnsNotFound_WhenPlanMissing()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/plans/999999/approve", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Approve_ConcurrentRequestsForSamePlan_OnlyOneSucceeds_OsmActionRunsOnce()
    {
        // Regression test for a TOCTOU race: without an atomic claim step, two
        // near-simultaneous /approve calls for the same plan could both read
        // Status == AwaitingApproval before either wrote back, and both would go on to post
        // the OSM comment — a double-execution bug. PlanTransitionLock (a process-wide async
        // lock, shared across requests via DI singleton — see its doc comment) serializes the
        // claim step so only one request ever wins.
        var bookingId = await SeedBookingAsync("77510");
        _fakeOsm.CommentToReturn = new CommentDto
        {
            OsmBookingId = bookingId,
            OsmCommentId = "cmt-race-1",
            AuthorName = "Site Manager",
            TextPreview = "noted",
            CreatedDate = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
        };
        var planId = await SeedPlanAsync(
            "[{\"type\":\"postComment\",\"text\":\"noted\"}]",
            bookingId);

        var client = _factory.CreateClient();

        var task1 = client.PostAsync($"/api/plans/{planId}/approve", content: null);
        var task2 = client.PostAsync($"/api/plans/{planId}/approve", content: null);
        var responses = await Task.WhenAll(task1, task2);

        var statusCodes = responses.Select(r => r.StatusCode).OrderBy(s => s).ToList();
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, statusCodes);

        // The OSM side effect happened exactly once — not twice.
        Assert.Single(_fakeOsm.CommentsPosted);

        var stored = await GetPlanFromDbAsync(planId);
        Assert.NotNull(stored);
        Assert.Equal(PlanStatus.Executed, stored.Status);
    }

    // ── Reject ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_AwaitingApprovalPlan_MarksRejected_PurgesSourceEmailText_MakesNoOsmCalls()
    {
        var planId = await SeedPlanAsync(
            "[{\"type\":\"postComment\",\"text\":\"noted\"}]",
            osmBookingId: "77504");

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/reject", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.SourceEmailText);

        Assert.Empty(_fakeOsm.CommentsPosted);
        Assert.Empty(_fakeOsm.EmailsSent);
        Assert.Empty(_fakeOsm.CapturedSpecs);

        var stored = await GetPlanFromDbAsync(planId);
        Assert.NotNull(stored);
        Assert.Equal(PlanStatus.Rejected, stored.Status);
        Assert.Null(stored.SourceEmailText);
    }

    [Fact]
    public async Task Reject_ReturnsConflict_WhenPlanNotAwaitingApproval()
    {
        var planId = await SeedPlanAsync(
            "[{\"type\":\"draftEmailReply\",\"text\":\"already done\"}]",
            osmBookingId: null,
            status: PlanStatus.Rejected);

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/plans/{planId}/reject", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Reject_ReturnsNotFound_WhenPlanMissing()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/plans/999999/reject", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reject_ConcurrentRequestsForSamePlan_OnlyOneSucceeds()
    {
        // Same TOCTOU concern as the Approve race test above, applied to Reject.
        var planId = await SeedPlanAsync(
            "[{\"type\":\"postComment\",\"text\":\"noted\"}]",
            osmBookingId: "77505");

        var client = _factory.CreateClient();

        var task1 = client.PostAsync($"/api/plans/{planId}/reject", content: null);
        var task2 = client.PostAsync($"/api/plans/{planId}/reject", content: null);
        var responses = await Task.WhenAll(task1, task2);

        var statusCodes = responses.Select(r => r.StatusCode).OrderBy(s => s).ToList();
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, statusCodes);

        var stored = await GetPlanFromDbAsync(planId);
        Assert.NotNull(stored);
        Assert.Equal(PlanStatus.Rejected, stored.Status);
    }
}
