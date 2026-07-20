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
}
