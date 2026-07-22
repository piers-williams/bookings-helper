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

public class PlanDetailTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PlanDetailTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_PlanDetail_" + Guid.NewGuid();
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
    public async Task GetById_ReturnsPlan_WhenExists()
    {
        int planId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var plan = new ProposedPlan
            {
                Status = PlanStatus.AwaitingApproval,
                SourceEmailText = "Please add an extra night",
                OsmBookingId = "77001",
                ActionsJson = "[{\"type\":\"MoveDates\"}]",
                CreatedAt = new DateTime(2026, 7, 5, 9, 0, 0, DateTimeKind.Utc)
            };
            db.ProposedPlans.Add(plan);
            await db.SaveChangesAsync();
            planId = plan.Id;
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/plans/{planId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProposedPlanDto>();
        Assert.NotNull(result);
        Assert.Equal(planId, result.Id);
        Assert.Equal("AwaitingApproval", result.Status);
        Assert.Equal("Please add an extra night", result.SourceEmailText);
        Assert.Equal("77001", result.OsmBookingId);
        Assert.Equal("[{\"type\":\"MoveDates\"}]", result.ActionsJson);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/plans/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
