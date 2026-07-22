using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add DataProtection for token encryption
// Persist keys to the HA data volume (/data) so they survive container updates.
// Falls back to /app/keys in development where /data is not mounted.
var keysDir = Directory.Exists("/data")
    ? "/data/keys"
    : Path.Combine(builder.Environment.ContentRootPath, "keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
    .SetApplicationName("BookingsAssistant");

// Add OSM OAuth service with HttpClient
builder.Services.AddHttpClient<IOsmAuthService, OsmAuthService>();

// Add OSM rate-limit cooldown (singleton — shared across every OsmService instance,
// since AddHttpClient below creates a new OsmService per resolution/request)
builder.Services.AddSingleton<OsmRateLimitCooldown>();

// Add OSM service with HttpClient
builder.Services.AddHttpClient<IOsmService, OsmService>();

// Add booking mutation service (scoped — depends on IOsmService which is per-request via HttpClient)
builder.Services.AddScoped<IBookingMutationService, BookingMutationService>();

// Add booking item action service (scoped — depends on ApplicationDbContext and IOsmService).
// Shared dispatch for move-activity/change-site/move-dates, used by both BookingActionsController
// and the plan-execution path.
builder.Services.AddScoped<IBookingItemActionService, BookingItemActionService>();

// Add Open WebUI client (LLM plan drafting) with HttpClient
builder.Services.AddHttpClient<IOpenWebUiClient, OpenWebUiClient>();

// Add plan drafting service (scoped — depends on ApplicationDbContext and IOsmService)
builder.Services.AddScoped<IPlanDraftingService, PlanDraftingService>();

// Add plan execution service (scoped — depends on IOsmService and IBookingItemActionService).
// Only invoked after a human approves a plan (PlansController.Approve) — the sole place
// allowed to mutate OSM state on the LLM's behalf.
builder.Services.AddScoped<IPlanExecutionService, PlanExecutionService>();

// Add plan transition lock (singleton — shared across every PlansController instance, same
// reasoning as OsmRateLimitCooldown above). Serializes Approve/Reject's claim step so two
// concurrent requests for the same plan can't both execute/reject it.
builder.Services.AddSingleton<PlanTransitionLock>();

// Add hosted services
builder.Services.AddHostedService<GateCodeService>();

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Apply migrations, seed database, and attempt initial OSM sync
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        await context.Database.MigrateAsync();
    await DbSeeder.SeedAsync(context);

    // Recover any ProposedPlan rows left stuck in Processing by a crash or unhandled error
    // between the atomic claim step and the terminal status write that normally follows it
    // immediately (see PlansController.TryClaimAwaitingApprovalAsync and PlanStatus.Processing's
    // doc comment). No request can still legitimately be "in progress" across a process
    // restart, so any such row is stale by definition.
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await StalePlanRecovery.RecoverStaleProcessingPlansAsync(context, startupLogger);

    // If OSM tokens are already stored (e.g. after addon update), sync on startup
    try
    {
        var osmService = scope.ServiceProvider.GetRequiredService<IOsmService>();
        var isAuthenticated = await osmService.IsAuthenticatedAsync(1);
        if (!isAuthenticated)
            throw new InvalidOperationException("Not authenticated");

        startupLogger.LogInformation("OSM tokens found — running startup sync...");
        var tasks = await Task.WhenAll(
            osmService.GetBookingsAsync("provisional"),
            osmService.GetBookingsAsync("confirmed"),
            osmService.GetBookingsAsync("future"),
            osmService.GetBookingsAsync("past"),
            osmService.GetBookingsAsync("cancelled"));

        var allBookings = tasks.SelectMany(b => b)
            .GroupBy(b => b.OsmBookingId)
            .Select(g => g.First())
            .ToList();

        var existing = await context.OsmBookings
            .ToDictionaryAsync(b => b.OsmBookingId);

        foreach (var booking in allBookings)
        {
            if (existing.TryGetValue(booking.OsmBookingId, out var entity))
            {
                entity.CustomerName = booking.CustomerName;
                entity.StartDate    = booking.StartDate;
                entity.EndDate      = booking.EndDate;
                entity.Status       = booking.Status;
                entity.LastFetched  = DateTime.UtcNow;
            }
            else
            {
                context.OsmBookings.Add(new BookingsAssistant.Api.Data.Entities.OsmBooking
                {
                    OsmBookingId = booking.OsmBookingId,
                    CustomerName = booking.CustomerName,
                    StartDate    = booking.StartDate,
                    EndDate      = booking.EndDate,
                    Status       = booking.Status,
                    LastFetched  = DateTime.UtcNow
                });
            }
        }

        await context.SaveChangesAsync();
        startupLogger.LogInformation("Startup OSM sync complete: {Count} bookings", allBookings.Count);
    }
    catch (Exception)
    {
        // No tokens yet or sync failed — user needs to authenticate via /api/auth/osm/login
    }
}

// Configure middleware
app.UseCors();

// Shared API token guard. No-op until Auth:ApiToken is configured (addon option
// api_token). Guards /api/* except the OSM OAuth handshake; SPA assets stay open.
var apiToken = app.Configuration["Auth:ApiToken"];
app.Use(async (context, next) =>
{
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var bearer = authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? authHeader["Bearer ".Length..].Trim()
        : null;
    var provided = context.Request.Headers["X-Api-Token"].FirstOrDefault() ?? bearer;

    if (!ApiTokenPolicy.IsAllowed(context.Request.Path.Value ?? string.Empty, apiToken, provided))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { message = "API token required" });
        return;
    }

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// Serve React app from wwwroot in production
if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

app.Run();

public partial class Program { }
