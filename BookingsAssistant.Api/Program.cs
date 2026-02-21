using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add DataProtection for token encryption
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "keys")))
    .SetApplicationName("BookingsAssistant");

// Add token service
builder.Services.AddScoped<ITokenService, TokenService>();

// Add Office 365 service with HttpClient
builder.Services.AddHttpClient<IOffice365Service, Office365Service>();

// Add OSM OAuth service with HttpClient
builder.Services.AddHttpClient<IOsmAuthService, OsmAuthService>();

// Add OSM service with HttpClient
builder.Services.AddHttpClient<IOsmService, OsmService>();

// Add linking service
builder.Services.AddScoped<ILinkingService, LinkingService>();

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

// Apply migrations and seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
    await DbSeeder.SeedAsync(context);
}

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("Development");
}

app.UseHttpsRedirection();
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
