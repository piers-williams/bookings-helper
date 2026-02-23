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

// Add OSM service with HttpClient
builder.Services.AddHttpClient<IOsmService, OsmService>();

// Add linking service
builder.Services.AddScoped<ILinkingService, LinkingService>();

// Add hashing service (singleton — loaded once at startup with the secret)
builder.Services.AddSingleton<IHashingService, HashingService>();

// Add hosted services
builder.Services.AddHostedService<BookingDetailBackfillService>();

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

    // Allow Chrome extension to call capture and booking-links endpoints.
    // AllowAnyOrigin is acceptable: the backend runs on a private network
    // and these endpoints only store/read local data.
    options.AddPolicy("ExtensionCapture", policy =>
    {
        policy.AllowAnyOrigin()
              .WithMethods("POST", "GET", "OPTIONS")
              .AllowAnyHeader();
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

    // Backfill hash columns for any existing rows
    var hashingService = scope.ServiceProvider.GetRequiredService<IHashingService>();

    var emailsToHash = await context.EmailMessages
        .Where(e => e.SenderEmailHash == null && e.SenderEmail != null)
        .ToListAsync();
    foreach (var e in emailsToHash)
        e.SenderEmailHash = hashingService.HashValue(e.SenderEmail!);
    if (emailsToHash.Count > 0)
        await context.SaveChangesAsync();

    var bookingsToHash = await context.OsmBookings
        .Where(b => b.CustomerNameHash == null)
        .ToListAsync();
    foreach (var b in bookingsToHash)
        b.CustomerNameHash = hashingService.HashValue(b.CustomerName);
    if (bookingsToHash.Count > 0)
        await context.SaveChangesAsync();

    // If OSM tokens are already stored (e.g. after addon update), sync on startup
    try
    {
        var osmAuth = scope.ServiceProvider.GetRequiredService<IOsmAuthService>();
        await osmAuth.GetValidAccessTokenAsync(1); // throws if no token

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("OSM tokens found — running startup sync...");

        var osmService = scope.ServiceProvider.GetRequiredService<IOsmService>();
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
        logger.LogInformation("Startup OSM sync complete: {Count} bookings", allBookings.Count);
    }
    catch (Exception)
    {
        // No tokens yet or sync failed — user needs to authenticate via /api/auth/osm/login
    }
}

// Configure middleware
app.UseCors();

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
