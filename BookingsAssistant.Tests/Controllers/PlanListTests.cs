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

public class PlanListTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PlanListTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_PlanList_" + Guid.NewGuid();
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
                services.AddSingleton<IOsmService>(new FakeOsmService());
            });
        });
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyList_WhenNoPlans()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<ProposedPlanDto>>();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAll_ReturnsAllPlans_WhenNoStatusFilter()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ProposedPlans.AddRange(
                new ProposedPlan
                {
                    Status = PlanStatus.AwaitingApproval,
                    SourceEmailText = "Please move our booking a day later",
                    OsmBookingId = null,
                    ActionsJson = "[{\"type\":\"MoveDates\"}]",
                    CreatedAt = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc)
                },
                new ProposedPlan
                {
                    Status = PlanStatus.Executed,
                    SourceEmailText = "Cancel our pitch please",
                    OsmBookingId = null,
                    ActionsJson = "[{\"type\":\"Cancel\"}]",
                    CreatedAt = new DateTime(2026, 7, 2, 9, 0, 0, DateTimeKind.Utc)
                }
            );
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<ProposedPlanDto>>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Status == "AwaitingApproval" && p.SourceEmailText == "Please move our booking a day later");
        Assert.Contains(result, p => p.Status == "Executed" && p.SourceEmailText == "Cancel our pitch please");
    }

    [Fact]
    public async Task GetAll_FiltersByStatus()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ProposedPlans.AddRange(
                new ProposedPlan
                {
                    Status = PlanStatus.AwaitingApproval,
                    SourceEmailText = "Awaiting one",
                    CreatedAt = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc)
                },
                new ProposedPlan
                {
                    Status = PlanStatus.Rejected,
                    SourceEmailText = "Rejected one",
                    CreatedAt = new DateTime(2026, 7, 2, 9, 0, 0, DateTimeKind.Utc)
                }
            );
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/plans?status=Rejected");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<ProposedPlanDto>>();
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Rejected", result[0].Status);
        Assert.Equal("Rejected one", result[0].SourceEmailText);
    }

    [Fact]
    public async Task GetAll_ReturnsBadRequest_ForInvalidStatus()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/plans?status=NotARealStatus");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
