using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Services;
using BookingsAssistant.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookingsAssistant.Tests.Controllers;

public class PlanStaleRecoveryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public PlanStaleRecoveryTests(WebApplicationFactory<Program> factory)
    {
        _baseFactory = factory;
    }

    /// <summary>
    /// Regression test for plans that crash mid-approval: if the process dies (or a
    /// SaveChangesAsync fails) between PlansController's atomic claim step
    /// (AwaitingApproval -> Processing) and the terminal status write that normally follows
    /// immediately, a plan is left stuck in Processing with no Approve/Reject path to recover
    /// it (both only accept AwaitingApproval). Program.cs's startup sweep
    /// (StalePlanRecovery.RecoverStaleProcessingPlansAsync) must reset any such row to Failed
    /// and purge SourceEmailText, since Processing can never legitimately persist across a
    /// process restart.
    /// </summary>
    [Fact]
    public async Task StartupSweep_ResetsPlansStuckInProcessing_ToFailed_AndPurgesSourceEmailText()
    {
        var dbName = "TestDb_StalePlanRecovery_" + Guid.NewGuid();

        // Seed a "crash-left-behind" Processing plan directly against the named in-memory
        // database, BEFORE the host is built, so it's already present when Program.cs's
        // startup sweep runs — simulating a plan that got stuck mid-approval across a process
        // restart (rather than one created via the normal API, which would never leave it in
        // Processing outside a crash).
        var seedOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        int planId;
        using (var seedContext = new ApplicationDbContext(seedOptions))
        {
            var plan = new ProposedPlan
            {
                Status = PlanStatus.Processing,
                SourceEmailText = "Customer email with PII that must not linger past a Failed plan",
                OsmBookingId = "77600",
                ActionsJson = "[{\"type\":\"postComment\",\"text\":\"noted\"}]",
                CreatedAt = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc)
            };
            seedContext.ProposedPlans.Add(plan);
            await seedContext.SaveChangesAsync();
            planId = plan.Id;
        }

        var fakeOsm = new FakeOsmService();
        var factory = _baseFactory.WithWebHostBuilder(builder =>
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
                services.AddSingleton<IOsmService>(fakeOsm);
            });
        });

        // Accessing .Services triggers the host to build, which runs Program.cs's top-level
        // migrate/seed/sweep/sync block — including the stale-plan recovery sweep — before this
        // call returns. No separate "trigger" step is needed: the sweep has already run by the
        // time we read the plan back below.
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await context.ProposedPlans.FindAsync(planId);

        Assert.NotNull(stored);
        Assert.Equal(PlanStatus.Failed, stored.Status);
        Assert.Null(stored.SourceEmailText);
    }

    [Fact]
    public async Task StartupSweep_LeavesOtherStatuses_Untouched()
    {
        var dbName = "TestDb_StalePlanRecovery_Untouched_" + Guid.NewGuid();

        var seedOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        int awaitingId, executedId;
        using (var seedContext = new ApplicationDbContext(seedOptions))
        {
            var awaiting = new ProposedPlan
            {
                Status = PlanStatus.AwaitingApproval,
                SourceEmailText = "Still pending review",
                OsmBookingId = "77601",
                ActionsJson = "[{\"type\":\"postComment\",\"text\":\"noted\"}]",
                CreatedAt = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc)
            };
            var executed = new ProposedPlan
            {
                Status = PlanStatus.Executed,
                SourceEmailText = null,
                OsmBookingId = "77602",
                ActionsJson = "[{\"type\":\"postComment\",\"text\":\"noted\"}]",
                CreatedAt = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc)
            };
            seedContext.ProposedPlans.AddRange(awaiting, executed);
            await seedContext.SaveChangesAsync();
            awaitingId = awaiting.Id;
            executedId = executed.Id;
        }

        var fakeOsm = new FakeOsmService();
        var factory = _baseFactory.WithWebHostBuilder(builder =>
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
                services.AddSingleton<IOsmService>(fakeOsm);
            });
        });

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var storedAwaiting = await context.ProposedPlans.FindAsync(awaitingId);
        Assert.NotNull(storedAwaiting);
        Assert.Equal(PlanStatus.AwaitingApproval, storedAwaiting.Status);
        Assert.Equal("Still pending review", storedAwaiting.SourceEmailText);

        var storedExecuted = await context.ProposedPlans.FindAsync(executedId);
        Assert.NotNull(storedExecuted);
        Assert.Equal(PlanStatus.Executed, storedExecuted.Status);
    }
}
