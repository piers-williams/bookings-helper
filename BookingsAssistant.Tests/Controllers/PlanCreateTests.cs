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

public class PlanCreateTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeOsmService _fakeOsm;
    private readonly FakeOpenWebUiClient _fakeLlm;

    public PlanCreateTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_PlanCreate_" + Guid.NewGuid();
        _fakeOsm = new FakeOsmService();
        _fakeLlm = new FakeOpenWebUiClient();
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

                services.RemoveAll<IOpenWebUiClient>();
                services.AddSingleton<IOpenWebUiClient>(_fakeLlm);
            });
        });
    }

    private async Task<string> SeedBookingAsync(
        string osmBookingId = "88001",
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.OsmBookings.Add(new OsmBooking
        {
            OsmBookingId = osmBookingId,
            CustomerName = "1st Anytown Scouts",
            StartDate = startDate ?? new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = endDate ?? new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc),
            Status = "Confirmed"
        });
        await db.SaveChangesAsync();
        return osmBookingId;
    }

    [Fact]
    public async Task Create_ReturnsAwaitingApprovalWithActionsJson_WhenLlmReturnsValidJson()
    {
        _fakeLlm.ResponsesToReturn.Enqueue(
            "{\"actions\":[{\"type\":\"draftEmailReply\",\"text\":\"Thanks, we'll move your dates.\"}," +
            "{\"type\":\"moveDates\",\"dayShift\":1}]}");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/plans", new CreatePlanRequest
        {
            SourceEmailText = "Please could you move our booking one day later?"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("AwaitingApproval", result.Status);
        Assert.NotNull(result.ActionsJson);
        Assert.Contains("draftEmailReply", result.ActionsJson);
        Assert.Contains("moveDates", result.ActionsJson);

        // Persisted, not just returned in the response.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.ProposedPlans.FindAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal(PlanStatus.AwaitingApproval, stored.Status);
        Assert.NotNull(stored.ActionsJson);
    }

    [Fact]
    public async Task Create_ReturnsFailedStatus_WhenLlmReturnsMalformedJsonOnBothAttempts()
    {
        _fakeLlm.ResponsesToReturn.Enqueue("this is not json at all");
        _fakeLlm.ResponsesToReturn.Enqueue("{ also not json");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/plans", new CreatePlanRequest
        {
            SourceEmailText = "Can you cancel our pitch?"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);
        Assert.Null(result.ActionsJson);

        // Two attempts were made (initial + one retry).
        Assert.Equal(2, _fakeLlm.Calls.Count);
    }

    [Fact]
    public async Task Create_PurgesSourceEmailText_WhenLlmReturnsMalformedJsonOnBothAttempts()
    {
        // A Failed plan has no Approve/Reject path (both require AwaitingApproval), so the
        // raw customer email must be purged right here or it would never be purged at all.
        _fakeLlm.ResponsesToReturn.Enqueue("this is not json at all");
        _fakeLlm.ResponsesToReturn.Enqueue("{ also not json");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/plans", new CreatePlanRequest
        {
            SourceEmailText = "Can you cancel our pitch? My email is jane@example.com"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);
        Assert.Null(result.SourceEmailText);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.ProposedPlans.FindAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal(PlanStatus.Failed, stored.Status);
        Assert.Null(stored.SourceEmailText);
    }

    [Fact]
    public async Task Create_ReturnsFailedStatus_AndPurgesSourceEmailText_WhenDraftingServiceThrows()
    {
        // Simulates a network failure / non-2xx from Open WebUI propagating as an exception
        // out of DraftPlanAsync. Create must not let this become an unhandled 500 leaving the
        // plan stuck at AwaitingApproval with no ActionsJson forever -- it should deterministically
        // reach a terminal, PII-free Failed state, same as a validation failure.
        _fakeLlm.ExceptionToThrow = new HttpRequestException("Open WebUI unreachable");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/plans", new CreatePlanRequest
        {
            SourceEmailText = "Can you cancel our pitch?"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);
        Assert.Null(result.ActionsJson);
        Assert.Null(result.SourceEmailText);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.ProposedPlans.FindAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal(PlanStatus.Failed, stored.Status);
        Assert.Null(stored.SourceEmailText);
        Assert.Null(stored.ActionsJson);
    }

    [Fact]
    public async Task Create_ReturnsFailedStatus_WhenLlmReturnsUnknownActionType()
    {
        // Both attempts return an unknown action type — invalid on the first try, and the
        // retry (which the service always attempts once) is still invalid.
        _fakeLlm.ResponsesToReturn.Enqueue("{\"actions\":[{\"type\":\"launchRocket\",\"text\":\"go\"}]}");
        _fakeLlm.ResponsesToReturn.Enqueue("{\"actions\":[{\"type\":\"launchRocket\",\"text\":\"go\"}]}");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/plans", new CreatePlanRequest
        {
            SourceEmailText = "Please do something unusual."
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);
        Assert.Null(result.ActionsJson);
        Assert.Equal(2, _fakeLlm.Calls.Count);

        // The retry prompt should mention the failure so the LLM can self-correct.
        Assert.Contains("invalid", _fakeLlm.Calls[1].UserPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_ReturnsAwaitingApprovalWithActionsJson_WhenLlmReturnsValidAddActivity()
    {
        _fakeLlm.ResponsesToReturn.Enqueue(
            "{\"actions\":[{\"type\":\"addActivity\",\"activityId\":\"4962\"," +
            "\"newStartDate\":\"2026-08-02\",\"newEndDate\":\"2026-08-02\",\"numberPeople\":8}]}");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/plans", new CreatePlanRequest
        {
            SourceEmailText = "Can you add an archery session for our group?"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("AwaitingApproval", result.Status);
        Assert.NotNull(result.ActionsJson);
        Assert.Contains("addActivity", result.ActionsJson);
    }

    [Theory]
    [InlineData("{\"actions\":[{\"newStartDate\":\"2026-08-02\",\"newEndDate\":\"2026-08-02\",\"numberPeople\":8,\"type\":\"addActivity\"}]}")]
    public async Task Create_ReturnsFailedStatus_WhenLlmReturnsAddActivityMissingActivityId(string malformedFirstAttempt)
    {
        // Both attempts omit activityId — invalid on the first try and the retry.
        _fakeLlm.ResponsesToReturn.Enqueue(malformedFirstAttempt);
        _fakeLlm.ResponsesToReturn.Enqueue(malformedFirstAttempt);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/plans", new CreatePlanRequest
        {
            SourceEmailText = "Can you add an archery session for our group?"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);
        Assert.Null(result.ActionsJson);
    }

    [Fact]
    public async Task Create_ReturnsFailedStatus_WhenLlmReturnsAddActivityMissingNumberPeople()
    {
        var malformed = "{\"actions\":[{\"type\":\"addActivity\",\"activityId\":\"4962\"," +
                         "\"newStartDate\":\"2026-08-02\",\"newEndDate\":\"2026-08-02\"}]}";
        _fakeLlm.ResponsesToReturn.Enqueue(malformed);
        _fakeLlm.ResponsesToReturn.Enqueue(malformed);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/plans", new CreatePlanRequest
        {
            SourceEmailText = "Can you add an archery session for our group?"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);
        Assert.Null(result.ActionsJson);
    }

    [Fact]
    public async Task Create_ReturnsAwaitingApprovalWithActionsJson_WhenLlmReturnsValidRemoveActivity()
    {
        _fakeLlm.ResponsesToReturn.Enqueue(
            "{\"actions\":[{\"type\":\"removeActivity\",\"itemId\":\"act-item-1\",\"note\":\"customer cancelled\"}]}");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/plans", new CreatePlanRequest
        {
            SourceEmailText = "Please cancel our archery session."
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("AwaitingApproval", result.Status);
        Assert.NotNull(result.ActionsJson);
        Assert.Contains("removeActivity", result.ActionsJson);
    }

    [Fact]
    public async Task Create_ReturnsFailedStatus_WhenLlmReturnsRemoveActivityMissingItemId()
    {
        var malformed = "{\"actions\":[{\"type\":\"removeActivity\"}]}";
        _fakeLlm.ResponsesToReturn.Enqueue(malformed);
        _fakeLlm.ResponsesToReturn.Enqueue(malformed);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/plans", new CreatePlanRequest
        {
            SourceEmailText = "Please cancel our archery session."
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);
        Assert.Null(result.ActionsJson);
    }

    [Fact]
    public async Task Create_IncludesBookingContextInPrompt_WhenOsmBookingIdProvided()
    {
        var bookingId = await SeedBookingAsync();
        _fakeLlm.ResponsesToReturn.Enqueue("{\"actions\":[{\"type\":\"postComment\",\"text\":\"noted\"}]}");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/plans", new CreatePlanRequest
        {
            SourceEmailText = "Can you note that we'll arrive late?",
            OsmBookingId = bookingId
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(_fakeLlm.Calls);
        Assert.Contains("1st Anytown Scouts", _fakeLlm.Calls[0].UserPrompt);
        Assert.Contains("2026-08-01", _fakeLlm.Calls[0].UserPrompt);
    }

    [Fact]
    public async Task Create_Succeeds_WhenOsmBookingIdOmitted()
    {
        _fakeLlm.ResponsesToReturn.Enqueue("{\"actions\":[{\"type\":\"draftEmailReply\",\"text\":\"Sure thing!\"}]}");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/plans", new CreatePlanRequest
        {
            SourceEmailText = "General enquiry, no specific booking."
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("AwaitingApproval", result.Status);
        Assert.Null(result.OsmBookingId);

        Assert.Single(_fakeLlm.Calls);
        Assert.DoesNotContain("Booking context", _fakeLlm.Calls[0].UserPrompt);
    }

    [Fact]
    public async Task Create_Succeeds_WhenFirstAttemptInvalidButRetrySucceeds()
    {
        _fakeLlm.ResponsesToReturn.Enqueue("not json");
        _fakeLlm.ResponsesToReturn.Enqueue("{\"actions\":[{\"type\":\"postComment\",\"text\":\"noted\"}]}");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/plans", new CreatePlanRequest
        {
            SourceEmailText = "Please note this on the booking."
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal("AwaitingApproval", result.Status);
        Assert.NotNull(result.ActionsJson);
        Assert.Contains("postComment", result.ActionsJson);
        Assert.Equal(2, _fakeLlm.Calls.Count);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenSourceEmailTextMissing()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/plans", new CreatePlanRequest
        {
            SourceEmailText = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsNotFound_WhenOsmBookingIdDoesNotExist()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/plans", new CreatePlanRequest
        {
            SourceEmailText = "Can you move our booking?",
            OsmBookingId = "no-such-booking"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // No plan row, no LLM call — rejected before either happens.
        Assert.Empty(_fakeLlm.Calls);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(db.ProposedPlans);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenBookingHasAlreadyEnded()
    {
        var bookingId = await SeedBookingAsync(
            startDate: DateTime.UtcNow.Date.AddDays(-10),
            endDate: DateTime.UtcNow.Date.AddDays(-8));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/plans", new CreatePlanRequest
        {
            SourceEmailText = "Can you move our booking?",
            OsmBookingId = bookingId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.Empty(_fakeLlm.Calls);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(db.ProposedPlans);
    }
}
