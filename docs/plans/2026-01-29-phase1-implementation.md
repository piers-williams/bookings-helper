# Bookings Assistant Phase 1 MVP - Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build Phase 1 MVP - view and aggregate data from Office 365 and OSM with smart linking between emails and bookings.

**Architecture:** ASP.NET Core Web API backend with Entity Framework Core + SQLite, React + TypeScript frontend with Vite, deployed as single Docker container.

**Tech Stack:** .NET 8, EF Core, SQLite, Microsoft.Graph SDK, React 18, TypeScript, Vite, Axios, Tailwind CSS

---

## Implementation Order

**Phase 1A: Project Setup & Infrastructure**
1. Create solution structure
2. Set up database with EF Core
3. Configure authentication infrastructure

**Phase 1B: Backend API - Stub Implementation**
4. Create API controllers with mock data
5. Implement DTO models and mapping

**Phase 1C: Frontend Foundation**
6. Create React app with routing
7. Build dashboard UI
8. Build detail pages

**Phase 1D: Office 365 Integration**
9. Implement Microsoft Graph authentication
10. Implement Office 365 email service

**Phase 1E: OSM Integration (Discover & Implement)**
11. Reverse engineer OSM API
12. Implement OSM service

**Phase 1F: Smart Linking**
13. Implement booking reference extraction
14. Implement manual linking

**Phase 1G: Production Readiness**
15. Create Docker configuration
16. Create Home Assistant addon config
17. End-to-end testing

---

## Phase 1A: Project Setup & Infrastructure

### Task 1: Create .NET Solution Structure

**Files:**
- Create: `BookingsAssistant.sln`
- Create: `BookingsAssistant.Api/BookingsAssistant.Api.csproj`
- Create: `BookingsAssistant.Api/Program.cs`
- Create: `BookingsAssistant.Api/appsettings.json`
- Create: `BookingsAssistant.Api/appsettings.Development.json`

**Step 1: Create solution and API project**

```bash
cd S:\Work\bookings-helper\.worktrees\bookings-assistant-mvp
dotnet new sln -n BookingsAssistant
dotnet new webapi -n BookingsAssistant.Api -o BookingsAssistant.Api
dotnet sln add BookingsAssistant.Api/BookingsAssistant.Api.csproj
```

**Step 2: Install required NuGet packages**

```bash
cd BookingsAssistant.Api
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Microsoft.Graph
dotnet add package Microsoft.Identity.Web
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

Expected: Packages installed successfully

**Step 3: Configure Program.cs for minimal API**

Update `BookingsAssistant.Api/Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

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
```

**Step 4: Configure appsettings.json**

Update `BookingsAssistant.Api/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=bookings.db"
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "ClientId": "",
    "ClientSecret": "",
    "TenantId": "common",
    "CallbackPath": "/signin-oidc"
  },
  "Osm": {
    "BaseUrl": "https://www.onlinescoutmanager.co.uk"
  }
}
```

**Step 5: Test that API runs**

```bash
dotnet run --project BookingsAssistant.Api
```

Expected: API starts on https://localhost:5001, Swagger UI accessible

**Step 6: Commit**

```bash
git add .
git commit -m "feat: create .NET solution with Web API project

- Add BookingsAssistant.Api project
- Install EF Core, Microsoft.Graph, Identity packages
- Configure CORS for development
- Add appsettings for Azure AD and OSM
- Configure static file serving for React build

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

### Task 2: Create Database Entities and DbContext

**Files:**
- Create: `BookingsAssistant.Api/Data/Entities/ApplicationUser.cs`
- Create: `BookingsAssistant.Api/Data/Entities/EmailMessage.cs`
- Create: `BookingsAssistant.Api/Data/Entities/OsmBooking.cs`
- Create: `BookingsAssistant.Api/Data/Entities/OsmComment.cs`
- Create: `BookingsAssistant.Api/Data/Entities/ApplicationLink.cs`
- Create: `BookingsAssistant.Api/Data/ApplicationDbContext.cs`

**Step 1: Create ApplicationUser entity**

Create `BookingsAssistant.Api/Data/Entities/ApplicationUser.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingsAssistant.Api.Data.Entities;

[Table("ApplicationUsers")]
public class ApplicationUser
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Office365Email { get; set; } = string.Empty;

    public string? Office365AccessToken { get; set; }
    public string? Office365RefreshToken { get; set; }
    public DateTime? Office365TokenExpiry { get; set; }

    [MaxLength(255)]
    public string? OsmUsername { get; set; }
    public string? OsmApiToken { get; set; }
    public DateTime? OsmTokenExpiry { get; set; }

    public DateTime? LastSync { get; set; }
}
```

**Step 2: Create EmailMessage entity**

Create `BookingsAssistant.Api/Data/Entities/EmailMessage.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingsAssistant.Api.Data.Entities;

[Table("EmailMessages")]
public class EmailMessage
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string MessageId { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string SenderEmail { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? SenderName { get; set; }

    [Required]
    [MaxLength(500)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public DateTime ReceivedDate { get; set; }

    public bool IsRead { get; set; }

    [MaxLength(50)]
    public string? ExtractedBookingRef { get; set; }

    public DateTime? LastFetched { get; set; }

    // Navigation properties
    public ICollection<ApplicationLink> Links { get; set; } = new List<ApplicationLink>();
}
```

**Step 3: Create OsmBooking entity**

Create `BookingsAssistant.Api/Data/Entities/OsmBooking.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingsAssistant.Api.Data.Entities;

[Table("OsmBookings")]
public class OsmBooking
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string OsmBookingId { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? CustomerEmail { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty; // Provisional, Confirmed, Cancelled

    public DateTime? LastFetched { get; set; }

    // Navigation properties
    public ICollection<OsmComment> Comments { get; set; } = new List<OsmComment>();
    public ICollection<ApplicationLink> Links { get; set; } = new List<ApplicationLink>();
}
```

**Step 4: Create OsmComment entity**

Create `BookingsAssistant.Api/Data/Entities/OsmComment.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingsAssistant.Api.Data.Entities;

[Table("OsmComments")]
public class OsmComment
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string OsmBookingId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string OsmCommentId { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string AuthorName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? TextPreview { get; set; }

    [Required]
    public DateTime CreatedDate { get; set; }

    public bool IsNew { get; set; }

    public DateTime? LastFetched { get; set; }

    // Navigation property
    [ForeignKey(nameof(OsmBookingId))]
    public OsmBooking? Booking { get; set; }
}
```

**Step 5: Create ApplicationLink entity**

Create `BookingsAssistant.Api/Data/Entities/ApplicationLink.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingsAssistant.Api.Data.Entities;

[Table("ApplicationLinks")]
public class ApplicationLink
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int EmailMessageId { get; set; }

    [Required]
    public int OsmBookingId { get; set; }

    // Nullable - null means auto-linked
    public int? CreatedByUserId { get; set; }

    [Required]
    public DateTime CreatedDate { get; set; }

    // Navigation properties
    [ForeignKey(nameof(EmailMessageId))]
    public EmailMessage EmailMessage { get; set; } = null!;

    [ForeignKey(nameof(OsmBookingId))]
    public OsmBooking OsmBooking { get; set; } = null!;

    [ForeignKey(nameof(CreatedByUserId))]
    public ApplicationUser? CreatedByUser { get; set; }
}
```

**Step 6: Create ApplicationDbContext**

Create `BookingsAssistant.Api/Data/ApplicationDbContext.cs`:

```csharp
using BookingsAssistant.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ApplicationUser> ApplicationUsers { get; set; }
    public DbSet<EmailMessage> EmailMessages { get; set; }
    public DbSet<OsmBooking> OsmBookings { get; set; }
    public DbSet<OsmComment> OsmComments { get; set; }
    public DbSet<ApplicationLink> ApplicationLinks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Indexes for performance
        modelBuilder.Entity<EmailMessage>()
            .HasIndex(e => e.MessageId)
            .IsUnique();

        modelBuilder.Entity<OsmBooking>()
            .HasIndex(b => b.OsmBookingId)
            .IsUnique();

        modelBuilder.Entity<OsmComment>()
            .HasIndex(c => c.OsmCommentId)
            .IsUnique();

        modelBuilder.Entity<EmailMessage>()
            .HasIndex(e => e.ExtractedBookingRef);

        modelBuilder.Entity<OsmBooking>()
            .HasIndex(b => b.CustomerEmail);

        // Configure relationships
        modelBuilder.Entity<ApplicationLink>()
            .HasOne(l => l.EmailMessage)
            .WithMany(e => e.Links)
            .HasForeignKey(l => l.EmailMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ApplicationLink>()
            .HasOne(l => l.OsmBooking)
            .WithMany(b => b.Links)
            .HasForeignKey(l => l.OsmBookingId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OsmComment>()
            .HasOne(c => c.Booking)
            .WithMany(b => b.Comments)
            .HasForeignKey(c => c.OsmBookingId)
            .HasPrincipalKey(b => b.OsmBookingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**Step 7: Register DbContext in Program.cs**

Update `BookingsAssistant.Api/Program.cs` - add after `var builder = WebApplication.CreateBuilder(args);`:

```csharp
using BookingsAssistant.Api.Data;
using Microsoft.EntityFrameworkCore;

// ... existing code ...

// Add after var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
```

**Step 8: Create initial migration**

```bash
cd BookingsAssistant.Api
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Expected: Migration created, database file `bookings.db` created with all tables

**Step 9: Verify database schema**

```bash
sqlite3 bookings.db ".schema"
```

Expected: All five tables (ApplicationUsers, EmailMessages, OsmBookings, OsmComments, ApplicationLinks) with correct columns and indexes

**Step 10: Commit**

```bash
git add .
git commit -m "feat: create database entities and EF Core context

- Add ApplicationUser, EmailMessage, OsmBooking, OsmComment, ApplicationLink entities
- Create ApplicationDbContext with relationships and indexes
- Generate initial EF Core migration
- Create SQLite database

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

### Task 3: Create DTO Models

**Files:**
- Create: `BookingsAssistant.Api/Models/EmailDto.cs`
- Create: `BookingsAssistant.Api/Models/EmailDetailDto.cs`
- Create: `BookingsAssistant.Api/Models/BookingDto.cs`
- Create: `BookingsAssistant.Api/Models/BookingDetailDto.cs`
- Create: `BookingsAssistant.Api/Models/CommentDto.cs`
- Create: `BookingsAssistant.Api/Models/LinkDto.cs`
- Create: `BookingsAssistant.Api/Models/CreateLinkRequest.cs`

**Step 1: Create EmailDto**

Create `BookingsAssistant.Api/Models/EmailDto.cs`:

```csharp
namespace BookingsAssistant.Api.Models;

public class EmailDto
{
    public int Id { get; set; }
    public string SenderEmail { get; set; } = string.Empty;
    public string? SenderName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public bool IsRead { get; set; }
    public string? ExtractedBookingRef { get; set; }
}
```

**Step 2: Create EmailDetailDto**

Create `BookingsAssistant.Api/Models/EmailDetailDto.cs`:

```csharp
namespace BookingsAssistant.Api.Models;

public class EmailDetailDto
{
    public int Id { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string? SenderName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public bool IsRead { get; set; }
    public string Body { get; set; } = string.Empty; // Fetched from Office 365 on-demand
    public string? ExtractedBookingRef { get; set; }
    public List<BookingDto> LinkedBookings { get; set; } = new();
    public List<EmailDto> RelatedEmails { get; set; } = new(); // Same sender
}
```

**Step 3: Create BookingDto**

Create `BookingsAssistant.Api/Models/BookingDto.cs`:

```csharp
namespace BookingsAssistant.Api.Models;

public class BookingDto
{
    public int Id { get; set; }
    public string OsmBookingId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}
```

**Step 4: Create BookingDetailDto**

Create `BookingsAssistant.Api/Models/BookingDetailDto.cs`:

```csharp
namespace BookingsAssistant.Api.Models;

public class BookingDetailDto
{
    public int Id { get; set; }
    public string OsmBookingId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FullDetails { get; set; } = string.Empty; // JSON from OSM
    public List<CommentDto> Comments { get; set; } = new();
    public List<EmailDto> LinkedEmails { get; set; } = new();
}
```

**Step 5: Create CommentDto**

Create `BookingsAssistant.Api/Models/CommentDto.cs`:

```csharp
namespace BookingsAssistant.Api.Models;

public class CommentDto
{
    public int Id { get; set; }
    public string OsmBookingId { get; set; } = string.Empty;
    public string OsmCommentId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string TextPreview { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public bool IsNew { get; set; }
    public BookingDto? Booking { get; set; }
}
```

**Step 6: Create LinkDto and CreateLinkRequest**

Create `BookingsAssistant.Api/Models/LinkDto.cs`:

```csharp
namespace BookingsAssistant.Api.Models;

public class LinkDto
{
    public int Id { get; set; }
    public int EmailMessageId { get; set; }
    public int OsmBookingId { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsAutoLinked => CreatedByUserId == null;
}
```

Create `BookingsAssistant.Api/Models/CreateLinkRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace BookingsAssistant.Api.Models;

public class CreateLinkRequest
{
    [Required]
    public int EmailMessageId { get; set; }

    [Required]
    public int OsmBookingId { get; set; }
}
```

**Step 7: Commit**

```bash
git add .
git commit -m "feat: create DTO models for API responses

- Add EmailDto, EmailDetailDto for email endpoints
- Add BookingDto, BookingDetailDto for booking endpoints
- Add CommentDto for comment endpoints
- Add LinkDto and CreateLinkRequest for linking

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Phase 1B: Backend API - Stub Implementation

### Task 4: Create API Controllers with Mock Data

**Files:**
- Create: `BookingsAssistant.Api/Controllers/EmailsController.cs`
- Create: `BookingsAssistant.Api/Controllers/BookingsController.cs`
- Create: `BookingsAssistant.Api/Controllers/CommentsController.cs`
- Create: `BookingsAssistant.Api/Controllers/LinksController.cs`
- Create: `BookingsAssistant.Api/Controllers/SyncController.cs`

**Step 1: Create EmailsController with mock data**

Create `BookingsAssistant.Api/Controllers/EmailsController.cs`:

```csharp
using BookingsAssistant.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailsController : ControllerBase
{
    [HttpGet]
    public ActionResult<List<EmailDto>> GetUnread()
    {
        // Mock data for now
        var emails = new List<EmailDto>
        {
            new EmailDto
            {
                Id = 1,
                SenderEmail = "john@scouts.org.uk",
                SenderName = "John Smith",
                Subject = "Query about booking #12345",
                ReceivedDate = DateTime.UtcNow.AddHours(-2),
                IsRead = false,
                ExtractedBookingRef = "12345"
            },
            new EmailDto
            {
                Id = 2,
                SenderEmail = "jane@school.ac.uk",
                SenderName = "Jane Doe",
                Subject = "Availability for March?",
                ReceivedDate = DateTime.UtcNow.AddHours(-5),
                IsRead = false,
                ExtractedBookingRef = null
            }
        };

        return Ok(emails);
    }

    [HttpGet("{id}")]
    public ActionResult<EmailDetailDto> GetById(int id)
    {
        // Mock data for now
        var email = new EmailDetailDto
        {
            Id = id,
            MessageId = $"msg-{id}",
            SenderEmail = "john@scouts.org.uk",
            SenderName = "John Smith",
            Subject = "Query about booking #12345",
            ReceivedDate = DateTime.UtcNow.AddHours(-2),
            IsRead = false,
            Body = "Hi, I'd like to confirm the details for booking #12345...",
            ExtractedBookingRef = "12345",
            LinkedBookings = new List<BookingDto>
            {
                new BookingDto
                {
                    Id = 1,
                    OsmBookingId = "12345",
                    CustomerName = "John Smith",
                    CustomerEmail = "john@scouts.org.uk",
                    StartDate = new DateTime(2026, 3, 15),
                    EndDate = new DateTime(2026, 3, 17),
                    Status = "Provisional"
                }
            },
            RelatedEmails = new List<EmailDto>()
        };

        return Ok(email);
    }
}
```

**Step 2: Create BookingsController with mock data**

Create `BookingsAssistant.Api/Controllers/BookingsController.cs`:

```csharp
using BookingsAssistant.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    [HttpGet]
    public ActionResult<List<BookingDto>> GetProvisional([FromQuery] string? status = "Provisional")
    {
        // Mock data for now
        var bookings = new List<BookingDto>
        {
            new BookingDto
            {
                Id = 1,
                OsmBookingId = "12345",
                CustomerName = "John Smith",
                CustomerEmail = "john@scouts.org.uk",
                StartDate = new DateTime(2026, 3, 15),
                EndDate = new DateTime(2026, 3, 17),
                Status = "Provisional"
            },
            new BookingDto
            {
                Id = 2,
                OsmBookingId = "12346",
                CustomerName = "Jane Doe",
                CustomerEmail = "jane@school.ac.uk",
                StartDate = new DateTime(2026, 4, 10),
                EndDate = new DateTime(2026, 4, 12),
                Status = "Provisional"
            }
        };

        return Ok(bookings);
    }

    [HttpGet("{id}")]
    public ActionResult<BookingDetailDto> GetById(int id)
    {
        // Mock data for now
        var booking = new BookingDetailDto
        {
            Id = id,
            OsmBookingId = "12345",
            CustomerName = "John Smith",
            CustomerEmail = "john@scouts.org.uk",
            StartDate = new DateTime(2026, 3, 15),
            EndDate = new DateTime(2026, 3, 17),
            Status = "Provisional",
            FullDetails = "{\"site\": \"Main Field\", \"attendees\": 25}",
            Comments = new List<CommentDto>
            {
                new CommentDto
                {
                    Id = 1,
                    OsmBookingId = "12345",
                    OsmCommentId = "c1",
                    AuthorName = "Tammy",
                    TextPreview = "Called customer to confirm arrival time",
                    CreatedDate = DateTime.UtcNow.AddDays(-1),
                    IsNew = false
                }
            },
            LinkedEmails = new List<EmailDto>()
        };

        return Ok(booking);
    }
}
```

**Step 3: Create CommentsController with mock data**

Create `BookingsAssistant.Api/Controllers/CommentsController.cs`:

```csharp
using BookingsAssistant.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommentsController : ControllerBase
{
    [HttpGet]
    public ActionResult<List<CommentDto>> GetNew([FromQuery] bool? newOnly = true)
    {
        // Mock data for now
        var comments = new List<CommentDto>
        {
            new CommentDto
            {
                Id = 1,
                OsmBookingId = "12345",
                OsmCommentId = "c1",
                AuthorName = "Tammy",
                TextPreview = "Called customer to confirm arrival time",
                CreatedDate = DateTime.UtcNow.AddHours(-3),
                IsNew = true,
                Booking = new BookingDto
                {
                    Id = 1,
                    OsmBookingId = "12345",
                    CustomerName = "John Smith",
                    Status = "Provisional"
                }
            },
            new CommentDto
            {
                Id = 2,
                OsmBookingId = "12340",
                OsmCommentId = "c2",
                AuthorName = "Piers",
                TextPreview = "Deposit received via BACS",
                CreatedDate = DateTime.UtcNow.AddHours(-6),
                IsNew = true,
                Booking = new BookingDto
                {
                    Id = 2,
                    OsmBookingId = "12340",
                    CustomerName = "Sarah Johnson",
                    Status = "Confirmed"
                }
            }
        };

        return Ok(comments);
    }
}
```

**Step 4: Create LinksController**

Create `BookingsAssistant.Api/Controllers/LinksController.cs`:

```csharp
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LinksController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public LinksController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<ActionResult<LinkDto>> CreateLink([FromBody] CreateLinkRequest request)
    {
        // For Phase 1, userId is hardcoded - will be from auth in Phase 2
        var link = new ApplicationLink
        {
            EmailMessageId = request.EmailMessageId,
            OsmBookingId = request.OsmBookingId,
            CreatedByUserId = 1, // TODO: Get from authenticated user
            CreatedDate = DateTime.UtcNow
        };

        _context.ApplicationLinks.Add(link);
        await _context.SaveChangesAsync();

        var dto = new LinkDto
        {
            Id = link.Id,
            EmailMessageId = link.EmailMessageId,
            OsmBookingId = link.OsmBookingId,
            CreatedByUserId = link.CreatedByUserId,
            CreatedDate = link.CreatedDate
        };

        return CreatedAtAction(nameof(CreateLink), new { id = dto.Id }, dto);
    }

    [HttpGet]
    public async Task<ActionResult<List<LinkDto>>> GetLinks(
        [FromQuery] int? emailId = null,
        [FromQuery] int? bookingId = null)
    {
        var query = _context.ApplicationLinks.AsQueryable();

        if (emailId.HasValue)
            query = query.Where(l => l.EmailMessageId == emailId.Value);

        if (bookingId.HasValue)
            query = query.Where(l => l.OsmBookingId == bookingId.Value);

        var links = await query
            .Select(l => new LinkDto
            {
                Id = l.Id,
                EmailMessageId = l.EmailMessageId,
                OsmBookingId = l.OsmBookingId,
                CreatedByUserId = l.CreatedByUserId,
                CreatedDate = l.CreatedDate
            })
            .ToListAsync();

        return Ok(links);
    }
}
```

**Step 5: Create SyncController**

Create `BookingsAssistant.Api/Controllers/SyncController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Sync()
    {
        // TODO: Implement sync logic in Phase 1D/1E
        // For now, just return success
        await Task.Delay(500); // Simulate API call

        return Ok(new
        {
            EmailsSynced = 2,
            BookingsSynced = 3,
            CommentsSynced = 2,
            LastSync = DateTime.UtcNow
        });
    }
}
```

**Step 6: Test API endpoints**

```bash
dotnet run --project BookingsAssistant.Api
```

Then test with curl or Swagger UI:
- GET /api/emails
- GET /api/emails/1
- GET /api/bookings
- GET /api/bookings/1
- GET /api/comments
- POST /api/sync

Expected: All endpoints return mock data successfully

**Step 7: Commit**

```bash
git add .
git commit -m "feat: create API controllers with mock data

- Add EmailsController for email list and details
- Add BookingsController for booking list and details
- Add CommentsController for comment list
- Add LinksController for manual linking
- Add SyncController for refresh trigger
- All return mock data for frontend development

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Phase 1C: Frontend Foundation

### Task 5: Create React App with TypeScript

**Files:**
- Create: `BookingsAssistant.Web/package.json`
- Create: `BookingsAssistant.Web/tsconfig.json`
- Create: `BookingsAssistant.Web/vite.config.ts`
- Create: `BookingsAssistant.Web/index.html`
- Create: `BookingsAssistant.Web/src/main.tsx`
- Create: `BookingsAssistant.Web/src/App.tsx`
- Create: `BookingsAssistant.Web/src/vite-env.d.ts`

**Step 1: Create React app with Vite**

```bash
cd S:\Work\bookings-helper\.worktrees\bookings-assistant-mvp
npm create vite@latest BookingsAssistant.Web -- --template react-ts
cd BookingsAssistant.Web
npm install
```

Expected: Vite React TypeScript project created

**Step 2: Install dependencies**

```bash
npm install axios react-router-dom
npm install -D @types/react-router-dom tailwindcss postcss autoprefixer
npx tailwindcss init -p
```

Expected: Dependencies installed, Tailwind config created

**Step 3: Configure Tailwind CSS**

Update `BookingsAssistant.Web/tailwind.config.js`:

```js
/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {},
  },
  plugins: [],
}
```

Create `BookingsAssistant.Web/src/index.css`:

```css
@tailwind base;
@tailwind components;
@tailwind utilities;
```

**Step 4: Configure Vite proxy for API**

Update `BookingsAssistant.Web/vite.config.ts`:

```ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        secure: false,
      }
    }
  }
})
```

**Step 5: Create TypeScript types**

Create `BookingsAssistant.Web/src/types/index.ts`:

```ts
export interface Email {
  id: number;
  senderEmail: string;
  senderName?: string;
  subject: string;
  receivedDate: string;
  isRead: boolean;
  extractedBookingRef?: string;
}

export interface EmailDetail extends Email {
  messageId: string;
  body: string;
  linkedBookings: Booking[];
  relatedEmails: Email[];
}

export interface Booking {
  id: number;
  osmBookingId: string;
  customerName: string;
  customerEmail?: string;
  startDate: string;
  endDate: string;
  status: string;
}

export interface BookingDetail extends Booking {
  fullDetails: string;
  comments: Comment[];
  linkedEmails: Email[];
}

export interface Comment {
  id: number;
  osmBookingId: string;
  osmCommentId: string;
  authorName: string;
  textPreview: string;
  createdDate: string;
  isNew: boolean;
  booking?: Booking;
}

export interface Link {
  id: number;
  emailMessageId: number;
  osmBookingId: number;
  createdByUserId?: number;
  createdDate: string;
  isAutoLinked: boolean;
}

export interface CreateLinkRequest {
  emailMessageId: number;
  osmBookingId: number;
}
```

**Step 6: Create API client**

Create `BookingsAssistant.Web/src/services/apiClient.ts`:

```ts
import axios from 'axios';
import type { Email, EmailDetail, Booking, BookingDetail, Comment, Link, CreateLinkRequest } from '../types';

export const apiClient = axios.create({
  baseURL: '/api',
  withCredentials: true,
});

export const emailsApi = {
  getUnread: () => apiClient.get<Email[]>('/emails'),
  getById: (id: number) => apiClient.get<EmailDetail>(`/emails/${id}`),
};

export const bookingsApi = {
  getProvisional: (status: string = 'Provisional') =>
    apiClient.get<Booking[]>(`/bookings?status=${status}`),
  getById: (id: number) => apiClient.get<BookingDetail>(`/bookings/${id}`),
};

export const commentsApi = {
  getNew: () => apiClient.get<Comment[]>('/comments?newOnly=true'),
};

export const linksApi = {
  create: (request: CreateLinkRequest) => apiClient.post<Link>('/links', request),
  getByEmail: (emailId: number) => apiClient.get<Link[]>(`/links?emailId=${emailId}`),
  getByBooking: (bookingId: number) => apiClient.get<Link[]>(`/links?bookingId=${bookingId}`),
};

export const syncApi = {
  sync: () => apiClient.post('/sync'),
};
```

**Step 7: Update main.tsx**

Update `BookingsAssistant.Web/src/main.tsx`:

```tsx
import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import App from './App.tsx'
import './index.css'

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </React.StrictMode>,
)
```

**Step 8: Create basic App component**

Update `BookingsAssistant.Web/src/App.tsx`:

```tsx
import { Routes, Route } from 'react-router-dom';
import Dashboard from './components/Dashboard';
import EmailDetail from './components/EmailDetail';
import BookingDetail from './components/BookingDetail';

function App() {
  return (
    <div className="min-h-screen bg-gray-100">
      <Routes>
        <Route path="/" element={<Dashboard />} />
        <Route path="/emails/:id" element={<EmailDetail />} />
        <Route path="/bookings/:id" element={<BookingDetail />} />
      </Routes>
    </div>
  );
}

export default App;
```

**Step 9: Test that React app runs**

```bash
npm run dev
```

Expected: App starts on http://localhost:3000, shows "Dashboard" (will create component next)

**Step 10: Commit**

```bash
git add .
git commit -m "feat: create React TypeScript app with Vite

- Initialize Vite project with React and TypeScript
- Configure Tailwind CSS for styling
- Add React Router for navigation
- Create TypeScript types for all entities
- Create API client with Axios
- Configure proxy to backend API
- Set up basic App routing structure

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

### Task 6: Build Dashboard Component

**Files:**
- Create: `BookingsAssistant.Web/src/components/Dashboard.tsx`
- Create: `BookingsAssistant.Web/src/components/EmailCard.tsx`
- Create: `BookingsAssistant.Web/src/components/BookingCard.tsx`
- Create: `BookingsAssistant.Web/src/components/CommentCard.tsx`

**Step 1: Create Dashboard component**

Create `BookingsAssistant.Web/src/components/Dashboard.tsx`:

```tsx
import { useState, useEffect } from 'react';
import { emailsApi, bookingsApi, commentsApi, syncApi } from '../services/apiClient';
import type { Email, Booking, Comment } from '../types';
import EmailCard from './EmailCard';
import BookingCard from './BookingCard';
import CommentCard from './CommentCard';

export default function Dashboard() {
  const [emails, setEmails] = useState<Email[]>([]);
  const [bookings, setBookings] = useState<Booking[]>([]);
  const [comments, setComments] = useState<Comment[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchData = async () => {
    setLoading(true);
    setError(null);
    try {
      const [emailsRes, bookingsRes, commentsRes] = await Promise.all([
        emailsApi.getUnread(),
        bookingsApi.getProvisional(),
        commentsApi.getNew(),
      ]);
      setEmails(emailsRes.data);
      setBookings(bookingsRes.data);
      setComments(commentsRes.data);
    } catch (err) {
      setError('Failed to load data');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleRefresh = async () => {
    setLoading(true);
    try {
      await syncApi.sync();
      await fetchData();
    } catch (err) {
      setError('Failed to sync data');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  return (
    <div className="container mx-auto px-4 py-8">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-bold text-gray-800">Bookings Assistant</h1>
        <button
          onClick={handleRefresh}
          disabled={loading}
          className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:bg-gray-400"
        >
          {loading ? 'Loading...' : 'Refresh'}
        </button>
      </div>

      {error && (
        <div className="mb-4 p-4 bg-red-100 border border-red-400 text-red-700 rounded">
          {error}
        </div>
      )}

      <div className="space-y-6">
        {/* Unread Emails Section */}
        <section className="bg-white rounded-lg shadow p-6">
          <h2 className="text-xl font-semibold mb-4 flex items-center">
            <span className="mr-2">📧</span>
            Unread Emails ({emails.length})
          </h2>
          <div className="space-y-3">
            {emails.map(email => (
              <EmailCard key={email.id} email={email} />
            ))}
            {emails.length === 0 && (
              <p className="text-gray-500">No unread emails</p>
            )}
          </div>
        </section>

        {/* Provisional Bookings Section */}
        <section className="bg-white rounded-lg shadow p-6">
          <h2 className="text-xl font-semibold mb-4 flex items-center">
            <span className="mr-2">📋</span>
            Provisional Bookings ({bookings.length})
          </h2>
          <div className="space-y-3">
            {bookings.map(booking => (
              <BookingCard key={booking.id} booking={booking} />
            ))}
            {bookings.length === 0 && (
              <p className="text-gray-500">No provisional bookings</p>
            )}
          </div>
        </section>

        {/* New Comments Section */}
        <section className="bg-white rounded-lg shadow p-6">
          <h2 className="text-xl font-semibold mb-4 flex items-center">
            <span className="mr-2">💬</span>
            New Comments ({comments.length})
          </h2>
          <div className="space-y-3">
            {comments.map(comment => (
              <CommentCard key={comment.id} comment={comment} />
            ))}
            {comments.length === 0 && (
              <p className="text-gray-500">No new comments</p>
            )}
          </div>
        </section>
      </div>
    </div>
  );
}
```

**Step 2: Create EmailCard component**

Create `BookingsAssistant.Web/src/components/EmailCard.tsx`:

```tsx
import { Link } from 'react-router-dom';
import type { Email } from '../types';

interface Props {
  email: Email;
}

export default function EmailCard({ email }: Props) {
  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffHours = Math.floor(diffMs / (1000 * 60 * 60));

    if (diffHours < 1) return 'Just now';
    if (diffHours < 24) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;
    const diffDays = Math.floor(diffHours / 24);
    return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;
  };

  return (
    <Link
      to={`/emails/${email.id}`}
      className="block p-4 border border-gray-200 rounded hover:bg-gray-50 transition"
    >
      <div className="flex justify-between items-start">
        <div className="flex-1">
          <p className="text-sm text-gray-600">
            From: {email.senderName || email.senderEmail}
          </p>
          <p className="font-medium text-gray-900 mt-1">{email.subject}</p>
          {email.extractedBookingRef && (
            <p className="text-sm text-blue-600 mt-1">
              🔗 Booking #{email.extractedBookingRef}
            </p>
          )}
        </div>
        <span className="text-sm text-gray-500">
          {formatDate(email.receivedDate)}
        </span>
      </div>
    </Link>
  );
}
```

**Step 3: Create BookingCard component**

Create `BookingsAssistant.Web/src/components/BookingCard.tsx`:

```tsx
import { Link } from 'react-router-dom';
import type { Booking } from '../types';

interface Props {
  booking: Booking;
}

export default function BookingCard({ booking }: Props) {
  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-GB', {
      day: 'numeric',
      month: 'short',
      year: 'numeric'
    });
  };

  return (
    <Link
      to={`/bookings/${booking.id}`}
      className="block p-4 border border-gray-200 rounded hover:bg-gray-50 transition"
    >
      <div className="flex justify-between items-start">
        <div>
          <p className="font-medium text-gray-900">
            #{booking.osmBookingId} - {booking.customerName}
          </p>
          <p className="text-sm text-gray-600 mt-1">
            {formatDate(booking.startDate)} - {formatDate(booking.endDate)}
          </p>
        </div>
        <span className={`px-2 py-1 text-xs rounded ${
          booking.status === 'Provisional'
            ? 'bg-yellow-100 text-yellow-800'
            : 'bg-green-100 text-green-800'
        }`}>
          {booking.status}
        </span>
      </div>
    </Link>
  );
}
```

**Step 4: Create CommentCard component**

Create `BookingsAssistant.Web/src/components/CommentCard.tsx`:

```tsx
import { Link } from 'react-router-dom';
import type { Comment } from '../types';

interface Props {
  comment: Comment;
}

export default function CommentCard({ comment }: Props) {
  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffHours = Math.floor(diffMs / (1000 * 60 * 60));

    if (diffHours < 1) return 'Just now';
    if (diffHours < 24) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;
    const diffDays = Math.floor(diffHours / 24);
    return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;
  };

  return (
    <Link
      to={`/bookings/${comment.booking?.id || 0}`}
      className="block p-4 border border-gray-200 rounded hover:bg-gray-50 transition"
    >
      <div className="flex justify-between items-start">
        <div className="flex-1">
          <p className="text-sm text-gray-600">
            Booking #{comment.osmBookingId} - {comment.authorName}
          </p>
          <p className="text-gray-900 mt-1">"{comment.textPreview}..."</p>
        </div>
        <span className="text-sm text-gray-500">
          {formatDate(comment.createdDate)}
        </span>
      </div>
    </Link>
  );
}
```

**Step 5: Test dashboard with backend running**

Start backend:
```bash
dotnet run --project BookingsAssistant.Api
```

Start frontend (in another terminal):
```bash
cd BookingsAssistant.Web
npm run dev
```

Expected: Dashboard loads at http://localhost:3000, shows mock data, clicking items navigates to detail pages

**Step 6: Commit**

```bash
git add .
git commit -m "feat: build dashboard component with three sections

- Create Dashboard component with email/booking/comment sections
- Add EmailCard, BookingCard, CommentCard for list items
- Implement refresh button that triggers sync
- Add loading and error states
- Use Tailwind CSS for styling
- Connect to backend API for data

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

### Task 7: Build Detail Pages

**Files:**
- Create: `BookingsAssistant.Web/src/components/EmailDetail.tsx`
- Create: `BookingsAssistant.Web/src/components/BookingDetail.tsx`
- Create: `BookingsAssistant.Web/src/components/LinkBookingModal.tsx`

**Step 1: Create EmailDetail component**

Create `BookingsAssistant.Web/src/components/EmailDetail.tsx`:

```tsx
import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { emailsApi } from '../services/apiClient';
import type { EmailDetail as EmailDetailType } from '../types';
import LinkBookingModal from './LinkBookingModal';

export default function EmailDetail() {
  const { id } = useParams<{ id: string }>();
  const [email, setEmail] = useState<EmailDetailType | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showLinkModal, setShowLinkModal] = useState(false);

  useEffect(() => {
    const fetchEmail = async () => {
      if (!id) return;
      setLoading(true);
      try {
        const res = await emailsApi.getById(parseInt(id));
        setEmail(res.data);
      } catch (err) {
        setError('Failed to load email');
        console.error(err);
      } finally {
        setLoading(false);
      }
    };
    fetchEmail();
  }, [id]);

  if (loading) return <div className="p-8">Loading...</div>;
  if (error) return <div className="p-8 text-red-600">{error}</div>;
  if (!email) return <div className="p-8">Email not found</div>;

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString('en-GB', {
      dateStyle: 'long',
      timeStyle: 'short'
    });
  };

  const outlookUrl = `https://outlook.office.com/mail/inbox/id/${email.messageId}`;

  return (
    <div className="container mx-auto px-4 py-8 max-w-4xl">
      <Link to="/" className="text-blue-600 hover:underline mb-4 inline-block">
        ← Back to Dashboard
      </Link>

      <div className="bg-white rounded-lg shadow p-6">
        <div className="border-b pb-4 mb-4">
          <h1 className="text-2xl font-bold mb-2">{email.subject}</h1>
          <div className="text-sm text-gray-600 space-y-1">
            <p>From: {email.senderName || email.senderEmail}</p>
            <p>Date: {formatDate(email.receivedDate)}</p>
          </div>
        </div>

        <div className="prose max-w-none mb-6">
          <div className="whitespace-pre-wrap">{email.body}</div>
        </div>

        {/* Smart Links Section */}
        <div className="border-t pt-4 mb-4">
          <h2 className="text-lg font-semibold mb-3">Linked Bookings</h2>
          {email.linkedBookings.length > 0 ? (
            <div className="space-y-2">
              {email.linkedBookings.map(booking => (
                <Link
                  key={booking.id}
                  to={`/bookings/${booking.id}`}
                  className="block p-3 border border-green-200 bg-green-50 rounded hover:bg-green-100"
                >
                  <span className="text-green-700">
                    🔗 Booking #{booking.osmBookingId} - {booking.customerName}
                  </span>
                </Link>
              ))}
            </div>
          ) : (
            <div>
              <p className="text-gray-500 mb-2">No booking found</p>
              <button
                onClick={() => setShowLinkModal(true)}
                className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
              >
                Search & Link Manually
              </button>
            </div>
          )}
        </div>

        {/* Related Emails */}
        {email.relatedEmails.length > 0 && (
          <div className="border-t pt-4 mb-4">
            <h2 className="text-lg font-semibold mb-3">Related Emails</h2>
            <div className="space-y-2">
              {email.relatedEmails.map(related => (
                <Link
                  key={related.id}
                  to={`/emails/${related.id}`}
                  className="block p-3 border border-gray-200 rounded hover:bg-gray-50"
                >
                  <p className="font-medium">{related.subject}</p>
                  <p className="text-sm text-gray-600">{formatDate(related.receivedDate)}</p>
                </Link>
              ))}
            </div>
          </div>
        )}

        {/* Action Buttons */}
        <div className="border-t pt-4">
          <a
            href={outlookUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="px-4 py-2 bg-gray-600 text-white rounded hover:bg-gray-700"
          >
            Open in Outlook Web
          </a>
        </div>
      </div>

      {showLinkModal && (
        <LinkBookingModal
          emailId={email.id}
          onClose={() => setShowLinkModal(false)}
          onLinked={() => {
            setShowLinkModal(false);
            // Refresh email data
            emailsApi.getById(email.id).then(res => setEmail(res.data));
          }}
        />
      )}
    </div>
  );
}
```

**Step 2: Create BookingDetail component**

Create `BookingsAssistant.Web/src/components/BookingDetail.tsx`:

```tsx
import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { bookingsApi } from '../services/apiClient';
import type { BookingDetail as BookingDetailType } from '../types';

export default function BookingDetail() {
  const { id } = useParams<{ id: string }>();
  const [booking, setBooking] = useState<BookingDetailType | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchBooking = async () => {
      if (!id) return;
      setLoading(true);
      try {
        const res = await bookingsApi.getById(parseInt(id));
        setBooking(res.data);
      } catch (err) {
        setError('Failed to load booking');
        console.error(err);
      } finally {
        setLoading(false);
      }
    };
    fetchBooking();
  }, [id]);

  if (loading) return <div className="p-8">Loading...</div>;
  if (error) return <div className="p-8 text-red-600">{error}</div>;
  if (!booking) return <div className="p-8">Booking not found</div>;

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-GB', {
      dateStyle: 'long'
    });
  };

  const osmUrl = `https://www.onlinescoutmanager.co.uk/bookings/${booking.osmBookingId}`;

  return (
    <div className="container mx-auto px-4 py-8 max-w-4xl">
      <Link to="/" className="text-blue-600 hover:underline mb-4 inline-block">
        ← Back to Dashboard
      </Link>

      <div className="bg-white rounded-lg shadow p-6">
        {/* Header */}
        <div className="border-b pb-4 mb-4">
          <div className="flex justify-between items-start">
            <div>
              <h1 className="text-2xl font-bold mb-2">
                Booking #{booking.osmBookingId}
              </h1>
              <p className="text-lg text-gray-700">{booking.customerName}</p>
            </div>
            <span className={`px-3 py-1 rounded ${
              booking.status === 'Provisional'
                ? 'bg-yellow-100 text-yellow-800'
                : 'bg-green-100 text-green-800'
            }`}>
              {booking.status}
            </span>
          </div>
        </div>

        {/* Booking Details */}
        <div className="mb-6">
          <h2 className="text-lg font-semibold mb-3">Booking Information</h2>
          <div className="space-y-2 text-gray-700">
            <p><strong>Dates:</strong> {formatDate(booking.startDate)} - {formatDate(booking.endDate)}</p>
            {booking.customerEmail && (
              <p><strong>Email:</strong> {booking.customerEmail}</p>
            )}
          </div>
        </div>

        {/* Comments Timeline */}
        {booking.comments.length > 0 && (
          <div className="mb-6 border-t pt-4">
            <h2 className="text-lg font-semibold mb-3">Comments</h2>
            <div className="space-y-3">
              {booking.comments.map(comment => (
                <div key={comment.id} className="p-3 bg-gray-50 rounded border border-gray-200">
                  <div className="flex justify-between items-start mb-2">
                    <span className="font-medium text-gray-900">{comment.authorName}</span>
                    <span className="text-sm text-gray-500">
                      {new Date(comment.createdDate).toLocaleDateString('en-GB')}
                    </span>
                  </div>
                  <p className="text-gray-700">{comment.textPreview}</p>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Linked Emails */}
        {booking.linkedEmails.length > 0 && (
          <div className="mb-6 border-t pt-4">
            <h2 className="text-lg font-semibold mb-3">Linked Emails</h2>
            <div className="space-y-2">
              {booking.linkedEmails.map(email => (
                <Link
                  key={email.id}
                  to={`/emails/${email.id}`}
                  className="block p-3 border border-gray-200 rounded hover:bg-gray-50"
                >
                  <p className="font-medium">{email.subject}</p>
                  <p className="text-sm text-gray-600">From: {email.senderEmail}</p>
                </Link>
              ))}
            </div>
          </div>
        )}

        {/* Action Buttons */}
        <div className="border-t pt-4">
          <a
            href={osmUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="px-4 py-2 bg-gray-600 text-white rounded hover:bg-gray-700"
          >
            Open in OSM
          </a>
        </div>
      </div>
    </div>
  );
}
```

**Step 3: Create LinkBookingModal component**

Create `BookingsAssistant.Web/src/components/LinkBookingModal.tsx`:

```tsx
import { useState } from 'react';
import { bookingsApi, linksApi } from '../services/apiClient';
import type { Booking } from '../types';

interface Props {
  emailId: number;
  onClose: () => void;
  onLinked: () => void;
}

export default function LinkBookingModal({ emailId, onClose, onLinked }: Props) {
  const [searchTerm, setSearchTerm] = useState('');
  const [results, setResults] = useState<Booking[]>([]);
  const [loading, setLoading] = useState(false);

  const handleSearch = async () => {
    if (!searchTerm.trim()) return;

    setLoading(true);
    try {
      // For Phase 1, we just fetch all provisional bookings and filter client-side
      // Phase 2 will add proper search endpoint
      const res = await bookingsApi.getProvisional();
      const filtered = res.data.filter(b =>
        b.osmBookingId.includes(searchTerm) ||
        b.customerName.toLowerCase().includes(searchTerm.toLowerCase()) ||
        b.customerEmail?.toLowerCase().includes(searchTerm.toLowerCase())
      );
      setResults(filtered);
    } catch (err) {
      console.error('Search failed:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleLink = async (bookingId: number) => {
    try {
      await linksApi.create({ emailMessageId: emailId, osmBookingId: bookingId });
      onLinked();
    } catch (err) {
      console.error('Failed to create link:', err);
    }
  };

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-lg shadow-xl max-w-2xl w-full p-6">
        <div className="flex justify-between items-center mb-4">
          <h2 className="text-xl font-bold">Link Booking</h2>
          <button onClick={onClose} className="text-gray-500 hover:text-gray-700">
            ✕
          </button>
        </div>

        <div className="mb-4">
          <div className="flex gap-2">
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              onKeyPress={(e) => e.key === 'Enter' && handleSearch()}
              placeholder="Search by booking number or customer name"
              className="flex-1 px-3 py-2 border border-gray-300 rounded"
            />
            <button
              onClick={handleSearch}
              disabled={loading}
              className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:bg-gray-400"
            >
              {loading ? 'Searching...' : 'Search'}
            </button>
          </div>
        </div>

        <div className="space-y-2 max-h-96 overflow-y-auto">
          {results.map(booking => (
            <div
              key={booking.id}
              className="p-3 border border-gray-200 rounded hover:bg-gray-50 flex justify-between items-center"
            >
              <div>
                <p className="font-medium">
                  #{booking.osmBookingId} - {booking.customerName}
                </p>
                <p className="text-sm text-gray-600">
                  {new Date(booking.startDate).toLocaleDateString()} - {new Date(booking.endDate).toLocaleDateString()}
                </p>
              </div>
              <button
                onClick={() => handleLink(booking.id)}
                className="px-3 py-1 bg-green-600 text-white rounded hover:bg-green-700"
              >
                Link
              </button>
            </div>
          ))}
          {results.length === 0 && searchTerm && !loading && (
            <p className="text-gray-500 text-center py-4">No bookings found</p>
          )}
        </div>
      </div>
    </div>
  );
}
```

**Step 4: Test detail pages**

With both backend and frontend running, test:
1. Navigate to email detail page
2. Navigate to booking detail page
3. Click "Search & Link Manually" on email detail
4. Search for bookings and create link

Expected: All detail pages work, manual linking creates database entries

**Step 5: Commit**

```bash
git add .
git commit -m "feat: build email and booking detail pages

- Create EmailDetail page with full email body
- Add smart links section showing linked bookings
- Create BookingDetail page with comments timeline
- Add LinkBookingModal for manual linking
- Include external links to Outlook and OSM
- Support manual search and linking workflow

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Phase 1D: Office 365 Integration

**Note:** Tasks 8-10 implement Microsoft Graph API integration. This requires Azure AD app registration (see design doc). For initial development, you may want to test with mock tokens or skip to Phase 1E (OSM integration) first.

### Task 8: Implement Microsoft Identity Authentication

**Files:**
- Create: `BookingsAssistant.Api/Services/ITokenService.cs`
- Create: `BookingsAssistant.Api/Services/TokenService.cs`
- Create: `BookingsAssistant.Api/Controllers/AuthController.cs`
- Modify: `BookingsAssistant.Api/Program.cs`

**Step 1: Install Microsoft Identity packages**

```bash
cd BookingsAssistant.Api
dotnet add package Microsoft.Identity.Web
dotnet add package Microsoft.Identity.Web.MicrosoftGraph
```

**Step 2: Create ITokenService interface**

Create `BookingsAssistant.Api/Services/ITokenService.cs`:

```csharp
using BookingsAssistant.Api.Data.Entities;

namespace BookingsAssistant.Api.Services;

public interface ITokenService
{
    Task<string> GetValidAccessTokenAsync(ApplicationUser user);
    Task SaveTokensAsync(ApplicationUser user, string accessToken, string refreshToken, DateTime expiry);
}
```

**Step 3: Create TokenService implementation**

Create `BookingsAssistant.Api/Services/TokenService.cs`:

```csharp
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Azure.Identity;

namespace BookingsAssistant.Api.Services;

public class TokenService : ITokenService
{
    private readonly ApplicationDbContext _context;
    private readonly IDataProtector _protector;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        ApplicationDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration configuration,
        ILogger<TokenService> logger)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector("OAuth2Tokens");
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GetValidAccessTokenAsync(ApplicationUser user)
    {
        // Check if current token is still valid (with 5 minute buffer)
        if (user.Office365TokenExpiry.HasValue &&
            user.Office365TokenExpiry.Value > DateTime.UtcNow.AddMinutes(5) &&
            !string.IsNullOrEmpty(user.Office365AccessToken))
        {
            return _protector.Unprotect(user.Office365AccessToken);
        }

        // Token expired or missing, refresh it
        if (string.IsNullOrEmpty(user.Office365RefreshToken))
        {
            throw new InvalidOperationException("No refresh token available");
        }

        var refreshToken = _protector.Unprotect(user.Office365RefreshToken);
        var newTokens = await RefreshAccessTokenAsync(refreshToken);

        await SaveTokensAsync(user, newTokens.AccessToken, newTokens.RefreshToken, newTokens.Expiry);

        return newTokens.AccessToken;
    }

    public async Task SaveTokensAsync(ApplicationUser user, string accessToken, string refreshToken, DateTime expiry)
    {
        user.Office365AccessToken = _protector.Protect(accessToken);
        user.Office365RefreshToken = _protector.Protect(refreshToken);
        user.Office365TokenExpiry = expiry;

        _context.ApplicationUsers.Update(user);
        await _context.SaveChangesAsync();
    }

    private async Task<(string AccessToken, string RefreshToken, DateTime Expiry)> RefreshAccessTokenAsync(string refreshToken)
    {
        // TODO: Implement actual token refresh using Microsoft.Identity.Client
        // For now, throw to indicate this needs implementation
        throw new NotImplementedException("Token refresh not yet implemented - requires MSAL configuration");
    }
}
```

**Step 4: Create AuthController**

Create `BookingsAssistant.Api/Controllers/AuthController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    [HttpGet("login")]
    public IActionResult Login()
    {
        // TODO: Implement Microsoft Identity OAuth flow
        // This will redirect to Microsoft login
        return Challenge();
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code)
    {
        // TODO: Exchange code for tokens and save to database
        return Ok(new { message = "Authentication successful" });
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        // TODO: Check if user is authenticated
        return Ok(new { authenticated = false });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // TODO: Clear tokens
        return Ok();
    }
}
```

**Step 5: Register services in Program.cs**

Update `BookingsAssistant.Api/Program.cs`:

```csharp
using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.DataProtection;

// ... existing code ...

// Add after DbContext registration
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "keys")))
    .SetApplicationName("BookingsAssistant");

builder.Services.AddScoped<ITokenService, TokenService>();

// ... rest of code ...
```

**Step 6: Create keys directory**

```bash
mkdir BookingsAssistant.Api/keys
echo "keys/" >> .gitignore
```

**Step 7: Commit**

```bash
git add .
git commit -m "feat: add OAuth token management infrastructure

- Create ITokenService and TokenService for token management
- Add DataProtection for encrypting tokens at rest
- Create AuthController skeleton for OAuth flow
- Add token refresh logic structure
- Persist encryption keys to filesystem

Note: OAuth flow implementation deferred until Azure AD app registered

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

### Task 9: Implement Office 365 Email Service (Stub)

**Files:**
- Create: `BookingsAssistant.Api/Services/IOffice365Service.cs`
- Create: `BookingsAssistant.Api/Services/Office365Service.cs`

**Step 1: Create IOffice365Service interface**

Create `BookingsAssistant.Api/Services/IOffice365Service.cs`:

```csharp
namespace BookingsAssistant.Api.Services;

public interface IOffice365Service
{
    Task<List<Models.EmailDto>> GetUnreadEmailsAsync();
    Task<(string Body, List<string> BookingRefs)> GetEmailDetailsAsync(string messageId);
}
```

**Step 2: Create Office365Service stub**

Create `BookingsAssistant.Api/Services/Office365Service.cs`:

```csharp
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using System.Text.RegularExpressions;

namespace BookingsAssistant.Api.Services;

public class Office365Service : IOffice365Service
{
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly ILogger<Office365Service> _logger;

    public Office365Service(
        ApplicationDbContext context,
        ITokenService tokenService,
        ILogger<Office365Service> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<List<EmailDto>> GetUnreadEmailsAsync()
    {
        // TODO: Implement with Microsoft Graph SDK
        // For now, return empty list
        _logger.LogWarning("Office365Service.GetUnreadEmailsAsync not yet implemented");
        return new List<EmailDto>();
    }

    public async Task<(string Body, List<string> BookingRefs)> GetEmailDetailsAsync(string messageId)
    {
        // TODO: Implement with Microsoft Graph SDK
        _logger.LogWarning("Office365Service.GetEmailDetailsAsync not yet implemented");

        // Return mock data for now
        var body = "Email body will be fetched from Microsoft Graph API";
        var refs = ExtractBookingReferences("Sample email mentioning booking #12345");

        return (body, refs);
    }

    private List<string> ExtractBookingReferences(string text)
    {
        // Regex: (?:#|Ref:|REF:|Reference|Booking\s*#|OSM\s*#)\s*(\d{4,6})
        var pattern = @"(?:#|Ref:|REF:|Reference|Booking\s*#|OSM\s*#)\s*(\d{4,6})";
        var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);

        return matches
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }
}
```

**Step 3: Register service**

Update `BookingsAssistant.Api/Program.cs`:

```csharp
using BookingsAssistant.Api.Services;

// ... existing code ...

builder.Services.AddScoped<IOffice365Service, Office365Service>();
```

**Step 4: Commit**

```bash
git add .
git commit -m "feat: add Office 365 service stub with booking ref extraction

- Create IOffice365Service interface
- Implement Office365Service with stub methods
- Add regex-based booking reference extraction
- Register service in DI container

Will implement Graph API integration once auth is complete

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Phase 1E: OSM Integration

### Task 10: Reverse Engineer OSM API

**Files:**
- Create: `docs/osm-api-discovery.md`

**Step 1: Document OSM API endpoints**

This task requires manual exploration. Open OSM in browser with DevTools Network tab:

1. Log in to OSM
2. Navigate to bookings section
3. View a booking
4. Read comments
5. Document all API calls

Create `docs/osm-api-discovery.md` with findings:

```markdown
# OSM API Discovery

## Authentication

**Method:** [Cookie/Session/API Key - TBD]

**Headers:**
- ...

## Endpoints Discovered

### GET /api/bookings
**Purpose:** List bookings
**Query Params:** ...
**Response:** ...

### GET /api/bookings/{id}
**Purpose:** Get booking details
**Response:** ...

### GET /api/bookings/{id}/comments
**Purpose:** Get comments for booking
**Response:** ...

## Sample Requests

```bash
curl '...' -H 'Authorization: ...'
```

## Notes

- Rate limiting: ...
- Pagination: ...
- Error responses: ...
```

**Step 2: Commit findings**

```bash
git add docs/osm-api-discovery.md
git commit -m "docs: document OSM API endpoints and authentication

Initial findings from reverse engineering OSM web interface

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

### Task 11: Implement OSM Service

**Files:**
- Create: `BookingsAssistant.Api/Services/IOsmService.cs`
- Create: `BookingsAssistant.Api/Services/OsmService.cs`

**Step 1: Create IOsmService interface**

Create `BookingsAssistant.Api/Services/IOsmService.cs`:

```csharp
using BookingsAssistant.Api.Models;

namespace BookingsAssistant.Api.Services;

public interface IOsmService
{
    Task<List<BookingDto>> GetBookingsAsync(string status);
    Task<(string FullDetails, List<CommentDto> Comments)> GetBookingDetailsAsync(string osmBookingId);
}
```

**Step 2: Create OsmService stub**

Create `BookingsAssistant.Api/Services/OsmService.cs`:

```csharp
using BookingsAssistant.Api.Models;

namespace BookingsAssistant.Api.Services;

public class OsmService : IOsmService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OsmService> _logger;

    public OsmService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OsmService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        var baseUrl = _configuration["Osm:BaseUrl"] ?? "https://www.onlinescoutmanager.co.uk";
        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    public async Task<List<BookingDto>> GetBookingsAsync(string status)
    {
        // TODO: Implement with discovered OSM API
        _logger.LogWarning("OsmService.GetBookingsAsync not yet implemented");
        return new List<BookingDto>();
    }

    public async Task<(string FullDetails, List<CommentDto> Comments)> GetBookingDetailsAsync(string osmBookingId)
    {
        // TODO: Implement with discovered OSM API
        _logger.LogWarning("OsmService.GetBookingDetailsAsync not yet implemented");
        return ("{}", new List<CommentDto>());
    }
}
```

**Step 3: Register service with HttpClient**

Update `BookingsAssistant.Api/Program.cs`:

```csharp
// Add after other service registrations
builder.Services.AddHttpClient<IOsmService, OsmService>();
```

**Step 4: Commit**

```bash
git add .
git commit -m "feat: add OSM service stub

- Create IOsmService interface
- Implement OsmService with HttpClient
- Register as HttpClient-backed service
- Ready for API implementation once endpoints discovered

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Phase 1F: Smart Linking

### Task 12: Implement Smart Linking Service

**Files:**
- Create: `BookingsAssistant.Api/Services/ILinkingService.cs`
- Create: `BookingsAssistant.Api/Services/LinkingService.cs`
- Modify: `BookingsAssistant.Api/Controllers/EmailsController.cs`
- Modify: `BookingsAssistant.Api/Controllers/BookingsController.cs`

**Step 1: Create ILinkingService**

Create `BookingsAssistant.Api/Services/ILinkingService.cs`:

```csharp
namespace BookingsAssistant.Api.Services;

public interface ILinkingService
{
    Task CreateAutoLinksForEmailAsync(int emailId, string subject, string body);
    Task<List<int>> GetLinkedBookingIdsAsync(int emailId);
    Task<List<int>> GetLinkedEmailIdsAsync(int bookingId);
}
```

**Step 2: Create LinkingService**

Create `BookingsAssistant.Api/Services/LinkingService.cs`:

```csharp
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace BookingsAssistant.Api.Services;

public class LinkingService : ILinkingService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LinkingService> _logger;

    public LinkingService(ApplicationDbContext context, ILogger<LinkingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task CreateAutoLinksForEmailAsync(int emailId, string subject, string body)
    {
        var bookingRefs = ExtractBookingReferences(subject + " " + body);

        if (!bookingRefs.Any())
        {
            _logger.LogInformation("No booking references found in email {EmailId}", emailId);
            return;
        }

        foreach (var bookingRef in bookingRefs)
        {
            var booking = await _context.OsmBookings
                .FirstOrDefaultAsync(b => b.OsmBookingId == bookingRef);

            if (booking == null)
            {
                _logger.LogWarning("Booking {BookingRef} not found for auto-linking", bookingRef);
                continue;
            }

            // Check if link already exists
            var existingLink = await _context.ApplicationLinks
                .AnyAsync(l => l.EmailMessageId == emailId && l.OsmBookingId == booking.Id);

            if (existingLink)
            {
                _logger.LogInformation("Link already exists between email {EmailId} and booking {BookingId}",
                    emailId, booking.Id);
                continue;
            }

            // Create auto-link (CreatedByUserId = null)
            var link = new ApplicationLink
            {
                EmailMessageId = emailId,
                OsmBookingId = booking.Id,
                CreatedByUserId = null, // Auto-linked
                CreatedDate = DateTime.UtcNow
            };

            _context.ApplicationLinks.Add(link);
            _logger.LogInformation("Created auto-link between email {EmailId} and booking {BookingId}",
                emailId, booking.Id);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<int>> GetLinkedBookingIdsAsync(int emailId)
    {
        return await _context.ApplicationLinks
            .Where(l => l.EmailMessageId == emailId)
            .Select(l => l.OsmBookingId)
            .ToListAsync();
    }

    public async Task<List<int>> GetLinkedEmailIdsAsync(int bookingId)
    {
        return await _context.ApplicationLinks
            .Where(l => l.OsmBookingId == bookingId)
            .Select(l => l.EmailMessageId)
            .ToListAsync();
    }

    private List<string> ExtractBookingReferences(string text)
    {
        // Regex: (?:#|Ref:|REF:|Reference|Booking\s*#|OSM\s*#)\s*(\d{4,6})
        var pattern = @"(?:#|Ref:|REF:|Reference|Booking\s*#|OSM\s*#)\s*(\d{4,6})";
        var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);

        return matches
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }
}
```

**Step 3: Register service**

Update `BookingsAssistant.Api/Program.cs`:

```csharp
builder.Services.AddScoped<ILinkingService, LinkingService>();
```

**Step 4: Update EmailsController to use linking service**

Update `BookingsAssistant.Api/Controllers/EmailsController.cs`:

```csharp
using BookingsAssistant.Api.Services;

// Add to constructor
private readonly ILinkingService _linkingService;
private readonly ApplicationDbContext _context;

public EmailsController(ILinkingService linkingService, ApplicationDbContext context)
{
    _linkingService = linkingService;
    _context = context;
}

// Update GetById method
[HttpGet("{id}")]
public async Task<ActionResult<EmailDetailDto>> GetById(int id)
{
    // Mock email data
    var email = new EmailDetailDto
    {
        Id = id,
        MessageId = $"msg-{id}",
        SenderEmail = "john@scouts.org.uk",
        SenderName = "John Smith",
        Subject = "Query about booking #12345",
        ReceivedDate = DateTime.UtcNow.AddHours(-2),
        IsRead = false,
        Body = "Hi, I'd like to confirm the details for booking #12345...",
        ExtractedBookingRef = "12345"
    };

    // Get linked bookings
    var linkedBookingIds = await _linkingService.GetLinkedBookingIdsAsync(id);
    email.LinkedBookings = await _context.OsmBookings
        .Where(b => linkedBookingIds.Contains(b.Id))
        .Select(b => new BookingDto
        {
            Id = b.Id,
            OsmBookingId = b.OsmBookingId,
            CustomerName = b.CustomerName,
            CustomerEmail = b.CustomerEmail,
            StartDate = b.StartDate,
            EndDate = b.EndDate,
            Status = b.Status
        })
        .ToListAsync();

    // Get related emails (same sender)
    email.RelatedEmails = await _context.EmailMessages
        .Where(e => e.SenderEmail == email.SenderEmail && e.Id != id)
        .Select(e => new EmailDto
        {
            Id = e.Id,
            SenderEmail = e.SenderEmail,
            SenderName = e.SenderName,
            Subject = e.Subject,
            ReceivedDate = e.ReceivedDate,
            IsRead = e.IsRead,
            ExtractedBookingRef = e.ExtractedBookingRef
        })
        .ToListAsync();

    return Ok(email);
}
```

**Step 5: Update BookingsController similarly**

Update `BookingsAssistant.Api/Controllers/BookingsController.cs` to include linked emails.

**Step 6: Commit**

```bash
git add .
git commit -m "feat: implement smart linking service

- Create LinkingService with booking reference extraction
- Support auto-linking (CreatedByUserId = null)
- Update EmailsController to show linked bookings
- Update BookingsController to show linked emails
- Use regex to extract booking references from text

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Phase 1G: Production Readiness

### Task 13: Create Docker Configuration

**Files:**
- Create: `Dockerfile`
- Create: `.dockerignore`

**Step 1: Create Dockerfile**

Create `Dockerfile` in repository root:

```dockerfile
# Stage 1: Build React frontend
FROM node:20-alpine AS frontend-build
WORKDIR /app/frontend
COPY BookingsAssistant.Web/package*.json ./
RUN npm ci
COPY BookingsAssistant.Web/ ./
RUN npm run build

# Stage 2: Build .NET backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /app
COPY BookingsAssistant.sln ./
COPY BookingsAssistant.Api/BookingsAssistant.Api.csproj ./BookingsAssistant.Api/
RUN dotnet restore
COPY BookingsAssistant.Api/ ./BookingsAssistant.Api/
RUN dotnet publish BookingsAssistant.Api/BookingsAssistant.Api.csproj -c Release -o out

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=backend-build /app/out .
COPY --from=frontend-build /app/frontend/dist ./wwwroot

# Create data directory for SQLite and keys
RUN mkdir -p /data /app/keys

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENV ConnectionStrings__DefaultConnection="Data Source=/data/bookings.db"

ENTRYPOINT ["dotnet", "BookingsAssistant.Api.dll"]
```

**Step 2: Create .dockerignore**

Create `.dockerignore`:

```
**/.git
**/.gitignore
**/bin
**/obj
**/node_modules
**/dist
**/.vscode
**/.vs
**/*.db
**/*.db-shm
**/*.db-wal
**/keys/
```

**Step 3: Test Docker build**

```bash
cd S:\Work\bookings-helper\.worktrees\bookings-assistant-mvp
docker build -t bookings-assistant:dev .
```

Expected: Build succeeds, creates image

**Step 4: Test Docker run**

```bash
docker run -p 5000:5000 -v ${PWD}/data:/data bookings-assistant:dev
```

Expected: Container starts, API accessible at http://localhost:5000

**Step 5: Commit**

```bash
git add Dockerfile .dockerignore
git commit -m "feat: add Docker configuration for production build

- Multi-stage build: frontend (Node) + backend (.NET)
- Copy React build to wwwroot for serving
- Create data volume for SQLite and encryption keys
- Expose port 5000
- Configure environment for production

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

### Task 14: Create Home Assistant Addon Configuration

**Files:**
- Create: `config.yaml`
- Create: `README.md` (addon docs)
- Create: `run.sh`

**Step 1: Create config.yaml**

Create `config.yaml` in repository root:

```yaml
name: Bookings Assistant
version: 0.1.0
slug: bookings-assistant
description: Manage scout campsite bookings from OSM and Office 365
url: https://github.com/yourusername/bookings-assistant
arch:
  - amd64
ports:
  5000/tcp: 8099
ports_description:
  5000/tcp: Web interface
map:
  - data:rw
options:
  azure_client_id: ""
  azure_client_secret: ""
  azure_redirect_uri: ""
  osm_base_url: "https://www.onlinescoutmanager.co.uk"
schema:
  azure_client_id: str
  azure_client_secret: password
  azure_redirect_uri: url
  osm_base_url: url
image: ghcr.io/yourusername/bookings-assistant-{arch}
```

**Step 2: Create addon README**

Create `README.md` for addon documentation.

**Step 3: Create run.sh**

Create `run.sh`:

```bash
#!/usr/bin/with-contenv bashio

CONFIG_PATH=/data/options.json

AZURE_CLIENT_ID=$(bashio::config 'azure_client_id')
AZURE_CLIENT_SECRET=$(bashio::config 'azure_client_secret')
AZURE_REDIRECT_URI=$(bashio::config 'azure_redirect_uri')
OSM_BASE_URL=$(bashio::config 'osm_base_url')

export AzureAd__ClientId="${AZURE_CLIENT_ID}"
export AzureAd__ClientSecret="${AZURE_CLIENT_SECRET}"
export AzureAd__CallbackPath="${AZURE_REDIRECT_URI}"
export Osm__BaseUrl="${OSM_BASE_URL}"

bashio::log.info "Starting Bookings Assistant..."

cd /app
exec dotnet BookingsAssistant.Api.dll
```

**Step 4: Make run.sh executable**

```bash
chmod +x run.sh
```

**Step 5: Commit**

```bash
git add config.yaml README.md run.sh
git commit -m "feat: add Home Assistant addon configuration

- Create config.yaml with addon metadata
- Add options schema for Azure AD and OSM configuration
- Create run.sh startup script with environment mapping
- Map /data for persistent storage
- Expose port 8099 for web interface

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

### Task 15: End-to-End Testing & Documentation

**Files:**
- Create: `docs/deployment.md`
- Create: `docs/development.md`
- Update: `README.md`

**Step 1: Create deployment documentation**

Create `docs/deployment.md` with instructions for:
- Azure AD app registration
- Home Assistant addon installation
- Configuration

**Step 2: Create development documentation**

Create `docs/development.md` with:
- Local development setup
- Running backend and frontend
- Database migrations
- Testing

**Step 3: Update README**

Update `README.md` with project overview, features, quick start.

**Step 4: Run full test**

1. Start backend: `dotnet run --project BookingsAssistant.Api`
2. Start frontend: `cd BookingsAssistant.Web && npm run dev`
3. Test all features:
   - Dashboard loads
   - Refresh button works
   - Navigate to email detail
   - Navigate to booking detail
   - Create manual link
   - Verify link appears in both detail pages

**Step 5: Commit**

```bash
git add docs/ README.md
git commit -m "docs: add deployment and development documentation

- Document Azure AD app registration process
- Add Home Assistant addon installation guide
- Document local development setup
- Update README with project overview

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

**Step 6: Final commit and push**

```bash
git log --oneline
# Review commit history

# Push to main branch (after merging feature branch)
```

---

## Summary

This implementation plan creates a Phase 1 MVP with:

✅ **Backend:**
- ASP.NET Core Web API with EF Core + SQLite
- Database entities for all three domains
- API controllers with mock data
- OAuth infrastructure (stub)
- Service stubs for Office 365 and OSM

✅ **Frontend:**
- React + TypeScript with Vite
- Dashboard with three sections
- Email and booking detail pages
- Manual linking modal
- Tailwind CSS styling

✅ **Infrastructure:**
- Docker multi-stage build
- Home Assistant addon config
- Data persistence with volumes

**Next Steps (Post-MVP):**
1. Complete Azure AD app registration
2. Implement Office 365 Graph API integration
3. Discover and implement OSM API endpoints
4. Enable auto-linking based on booking references
5. Implement sync service that populates database
6. Test with real data
7. Deploy to Home Assistant

**Estimated Effort:** 15-20 tasks, ~2-5 minutes per step, ~50-100 steps total
