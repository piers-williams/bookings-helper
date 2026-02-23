# Email Matching & Privacy Hashing Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace plaintext email storage with PBKDF2-SHA256 hashes, add name-based booking suggestions via sign-off extraction in the Chrome extension, and add a background worker to populate customer email hashes from OSM booking details.

**Architecture:** A singleton `HashingService` does all PBKDF2 computation server-side. The Chrome extension sends plaintext candidate names; the backend hashes and compares. A `BackgroundService` gradually fetches OSM booking details to populate `CustomerEmailHash`. Two EF migrations handle the schema: one adds hash columns (and drops always-null `CustomerEmail`), a second drops the now-redundant `SenderEmail` column after the backfill code is in place.

**Tech Stack:** ASP.NET Core 8 / EF Core 8 / SQLite / `System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2` / Chrome Extension MV3 / Vanilla JS

---

### Task 1: IHashingService + HashingService

**Files:**
- Create: `BookingsAssistant.Api/Services/IHashingService.cs`
- Create: `BookingsAssistant.Api/Services/HashingService.cs`
- Modify: `BookingsAssistant.Api/Program.cs` — register singleton before `builder.Build()`
- Create: `BookingsAssistant.Tests/Services/HashingServiceTests.cs`

**Step 1: Create the interface**

```csharp
namespace BookingsAssistant.Api.Services;

public interface IHashingService
{
    string HashValue(string value);
}
```

**Step 2: Create the implementation**

```csharp
using System.Security.Cryptography;
using System.Text;

namespace BookingsAssistant.Api.Services;

public class HashingService : IHashingService
{
    private readonly byte[] _secret;
    private readonly int _iterations;

    public HashingService(IConfiguration configuration, ILogger<HashingService> logger)
    {
        _iterations = configuration.GetValue<int>("Hashing:Iterations", 200_000);

        var secretPath = configuration["Hashing:SecretPath"] ?? "/data/hash-secret.txt";

        if (File.Exists(secretPath))
        {
            _secret = Convert.FromHexString(File.ReadAllText(secretPath).Trim());
            logger.LogInformation("Loaded hash secret from {Path}", secretPath);
        }
        else if (Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(secretPath))))
        {
            _secret = RandomNumberGenerator.GetBytes(32);
            File.WriteAllText(secretPath, Convert.ToHexString(_secret));
            logger.LogInformation("Generated and saved new hash secret to {Path}", secretPath);
        }
        else
        {
            // Development / test: deterministic fallback — never used in production
            _secret = Encoding.UTF8.GetBytes("dev-fallback-secret-do-not-use-in-production!!!");
            logger.LogWarning("Hash secret path {Path} not accessible, using development fallback", secretPath);
        }
    }

    public string HashValue(string value)
    {
        var normalized = value.ToLowerInvariant().Trim();
        var passwordBytes = Encoding.UTF8.GetBytes(normalized);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: passwordBytes,
            salt: _secret,
            iterations: _iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

**Step 3: Register in Program.cs**

Add this line before `var app = builder.Build();`:

```csharp
builder.Services.AddSingleton<IHashingService, HashingService>();
```

**Step 4: Write the failing tests**

```csharp
using BookingsAssistant.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookingsAssistant.Tests.Services;

public class HashingServiceTests
{
    private static IHashingService Create() => new HashingService(
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hashing:Iterations"] = "1",
                ["Hashing:SecretPath"] = "/nonexistent/path/secret.txt"
            })
            .Build(),
        NullLogger<HashingService>.Instance);

    [Fact]
    public void HashValue_IsDeterministic()
    {
        var svc = Create();
        Assert.Equal(svc.HashValue("test@example.com"), svc.HashValue("test@example.com"));
    }

    [Fact]
    public void HashValue_NormalisesCase()
    {
        var svc = Create();
        Assert.Equal(svc.HashValue("Test@Example.COM"), svc.HashValue("test@example.com"));
    }

    [Fact]
    public void HashValue_NormalisesWhitespace()
    {
        var svc = Create();
        Assert.Equal(svc.HashValue("  test@example.com  "), svc.HashValue("test@example.com"));
    }

    [Fact]
    public void HashValue_DifferentInputsProduceDifferentHashes()
    {
        var svc = Create();
        Assert.NotEqual(svc.HashValue("a@example.com"), svc.HashValue("b@example.com"));
    }

    [Fact]
    public void HashValue_Returns64CharLowercaseHex()
    {
        var svc = Create();
        var hash = svc.HashValue("test@example.com");
        Assert.Equal(64, hash.Length);
        Assert.All(hash, c => Assert.True(c is >= '0' and <= '9' or >= 'a' and <= 'f'));
    }
}
```

**Step 5: Run tests to verify they fail**

```bash
dotnet test BookingsAssistant.Tests/BookingsAssistant.Tests.csproj --filter "HashingServiceTests" -v n
```

Expected: FAIL — `HashingService` does not exist yet.

**Step 6: Verify tests pass after implementation**

```bash
dotnet test BookingsAssistant.Tests/BookingsAssistant.Tests.csproj --filter "HashingServiceTests" -v n
```

Expected: 5/5 PASS.

**Step 7: Commit**

```bash
git add BookingsAssistant.Api/Services/IHashingService.cs \
        BookingsAssistant.Api/Services/HashingService.cs \
        BookingsAssistant.Api/Program.cs \
        BookingsAssistant.Tests/Services/HashingServiceTests.cs
git commit -m "feat: add PBKDF2-SHA256 HashingService"
```

---

### Task 2: DB migration — add hash columns

**Files:**
- Modify: `BookingsAssistant.Api/Data/Entities/EmailMessage.cs`
- Modify: `BookingsAssistant.Api/Data/Entities/OsmBooking.cs`
- Modify: `BookingsAssistant.Api/Models/BookingDto.cs`
- Modify: `BookingsAssistant.Api/Controllers/BookingsController.cs` — remove CustomerEmail from projections
- Run: `dotnet ef migrations add AddHashColumns` from `BookingsAssistant.Api/`

**Step 1: Update `EmailMessage.cs`**

Make `SenderEmail` nullable (kept temporarily for migration backfill) and add `SenderEmailHash`:

```csharp
[MaxLength(255)]
public string? SenderEmail { get; set; }   // nullable — will be dropped in Task 7

[MaxLength(64)]
public string? SenderEmailHash { get; set; }
```

Remove `[Required]` from `SenderEmail` if present.

**Step 2: Update `OsmBooking.cs`**

Remove `CustomerEmail`. Add two new nullable hash columns:

```csharp
// Remove this line entirely:
// [MaxLength(255)]
// public string? CustomerEmail { get; set; }

[MaxLength(64)]
public string? CustomerEmailHash { get; set; }

[MaxLength(64)]
public string? CustomerNameHash { get; set; }
```

**Step 3: Remove `CustomerEmail` from `BookingDto.cs`**

Delete the `CustomerEmail` property:

```csharp
// Remove: public string? CustomerEmail { get; set; }
```

**Step 4: Remove `CustomerEmail` from all BookingDto projections in `BookingsController.cs`**

In `GetAll`, `GetById`, `GetLinks` — anywhere you see `CustomerEmail = b.CustomerEmail,` in a `BookingDto` initialiser, delete that line.

**Step 5: Generate the migration**

```bash
cd BookingsAssistant.Api
dotnet ef migrations add AddHashColumns
cd ..
```

Review the generated file in `Migrations/` — it should:
- Add `SenderEmailHash` to `EmailMessages`
- Drop `CustomerEmail` from `OsmBookings`
- Add `CustomerEmailHash` to `OsmBookings`
- Add `CustomerNameHash` to `OsmBookings`

**Step 6: Build to verify zero errors**

```bash
dotnet build BookingsAssistant.Api/BookingsAssistant.Api.csproj
```

**Step 7: Commit**

```bash
git add BookingsAssistant.Api/
git commit -m "feat: add hash columns, remove plaintext CustomerEmail"
```

---

### Task 3: Startup backfill + update EmailCaptureTests

The startup code needs to hash existing `SenderEmail` and `CustomerName` values into their hash columns before any new requests arrive.

**Files:**
- Modify: `BookingsAssistant.Api/Program.cs`
- Modify: `BookingsAssistant.Tests/Controllers/EmailCaptureTests.cs`

**Step 1: Add startup backfill to `Program.cs`**

Inside the existing startup scope block (after `await DbSeeder.SeedAsync(context);`), add:

```csharp
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
```

**Step 2: Update `EmailCaptureTests` to configure fast hashing and fix seeding**

All three test classes that use `WebApplicationFactory` need low-iteration hashing to keep tests fast. Add `ConfigureAppConfiguration` to each test class's `WithWebHostBuilder`:

```csharp
_factory = factory.WithWebHostBuilder(builder =>
{
    builder.ConfigureAppConfiguration((_, cfg) =>
        cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Hashing:Iterations"] = "1"
        }));
    builder.ConfigureServices(services =>
    {
        // ... existing in-memory DB setup ...
    });
});
```

Also update the seeded `OsmBooking` in `CaptureEmail_WithBookingRef_Returns200AndLinkedBooking` — remove `CustomerEmail` (field no longer exists):

```csharp
db.OsmBookings.Add(new OsmBooking
{
    OsmBookingId = "99999",
    CustomerName = "Test Customer",
    // CustomerEmail removed — replaced by CustomerEmailHash
    StartDate = DateTime.UtcNow.AddDays(30),
    EndDate = DateTime.UtcNow.AddDays(33),
    Status = "Provisional",
    LastFetched = DateTime.UtcNow
});
```

Apply the same `ConfigureAppConfiguration` change to `OsmSyncTests` and `BookingStatsTests`.

**Step 3: Run all tests**

```bash
dotnet test BookingsAssistant.Tests/BookingsAssistant.Tests.csproj -v n
```

Expected: 6/7 pass (intentionally-RED test still fails).

**Step 4: Commit**

```bash
git add BookingsAssistant.Api/Program.cs \
        BookingsAssistant.Tests/
git commit -m "feat: startup backfill for hash columns; fix tests for removed CustomerEmail"
```

---

### Task 4: Update EmailsController to use hashes

**Files:**
- Modify: `BookingsAssistant.Api/Controllers/EmailsController.cs`
- Modify: `BookingsAssistant.Api/Models/CaptureEmailRequest.cs`

**Step 1: Inject `IHashingService` into `EmailsController`**

Add to constructor:

```csharp
private readonly IHashingService _hashingService;

public EmailsController(ILinkingService linkingService, ApplicationDbContext context, IHashingService hashingService)
{
    _linkingService = linkingService;
    _context = context;
    _hashingService = hashingService;
}
```

**Step 2: Update `Capture` — hash before storing**

At the top of `Capture`, compute the hash:

```csharp
var senderEmailHash = _hashingService.HashValue(request.SenderEmail);
```

Replace duplicate detection:

```csharp
// Was: e.SenderEmail == request.SenderEmail
var existing = await _context.EmailMessages.FirstOrDefaultAsync(e =>
    e.Subject == request.Subject &&
    e.SenderEmailHash == senderEmailHash &&
    e.ReceivedDate == request.ReceivedDate);
```

Replace `EmailMessage` construction (remove `SenderEmail`, add `SenderEmailHash`):

```csharp
var email = new BookingsAssistant.Api.Data.Entities.EmailMessage
{
    MessageId = Guid.NewGuid().ToString(),
    SenderEmailHash = senderEmailHash,
    SenderName = request.SenderName,
    Subject = request.Subject,
    ReceivedDate = request.ReceivedDate,
    IsRead = false,
    LastFetched = DateTime.UtcNow
};
```

Replace the suggested bookings query (use `CustomerEmailHash`):

```csharp
if (!linkedBookings.Any())
{
    suggestedBookings = await _context.OsmBookings
        .Where(b => b.CustomerEmailHash == senderEmailHash
                 && b.CustomerEmailHash != "no-email")
        .Select(b => new BookingDto { ... })
        .ToListAsync();
}
```

**Step 3: Add `CandidateNames` to `CaptureEmailRequest`**

```csharp
public List<string> CandidateNames { get; set; } = new();
```

**Step 4: Build**

```bash
dotnet build BookingsAssistant.Api/BookingsAssistant.Api.csproj
```

Expected: 0 errors.

**Step 5: Run all tests**

```bash
dotnet test BookingsAssistant.Tests/BookingsAssistant.Tests.csproj -v n
```

Expected: 6/7 pass.

**Step 6: Commit**

```bash
git add BookingsAssistant.Api/Controllers/EmailsController.cs \
        BookingsAssistant.Api/Models/CaptureEmailRequest.cs
git commit -m "feat: hash sender email before storage; add CandidateNames to capture request"
```

---

### Task 5: Name hash matching in LinkingService + new tests

**Files:**
- Modify: `BookingsAssistant.Api/Services/ILinkingService.cs`
- Modify: `BookingsAssistant.Api/Services/LinkingService.cs`
- Modify: `BookingsAssistant.Api/Controllers/EmailsController.cs` — call new method
- Modify: `BookingsAssistant.Tests/Controllers/EmailCaptureTests.cs` — add two new tests

**Step 1: Add method to `ILinkingService`**

```csharp
Task<List<int>> FindSuggestedBookingIdsAsync(string senderEmailHash, List<string> candidateNameHashes);
```

**Step 2: Write failing tests first**

Add these two tests to `EmailCaptureTests.cs`:

```csharp
[Fact]
public async Task CaptureEmail_SuggestsBooking_WhenSenderEmailHashMatchesCustomerEmailHash()
{
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var hashing = scope.ServiceProvider.GetRequiredService<IHashingService>();

    const string senderEmail = "match@example.com";
    db.OsmBookings.Add(new OsmBooking
    {
        OsmBookingId = "88001",
        CustomerName = "Match Group",
        CustomerEmailHash = hashing.HashValue(senderEmail),
        CustomerNameHash = hashing.HashValue("Match Group"),
        StartDate = DateTime.UtcNow.AddDays(10),
        EndDate = DateTime.UtcNow.AddDays(12),
        Status = "Provisional",
        LastFetched = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var client = _factory.CreateClient();
    var response = await client.PostAsJsonAsync("/api/emails/capture", new CaptureEmailRequest
    {
        Subject = "General enquiry",
        SenderEmail = senderEmail,
        SenderName = "Match Person",
        BodyText = "Hello there",
        ReceivedDate = DateTime.UtcNow,
        CandidateNames = new List<string>()
    });

    var result = await response.Content.ReadFromJsonAsync<CaptureEmailResponse>();
    Assert.NotNull(result);
    Assert.Single(result.SuggestedBookings);
    Assert.Equal("88001", result.SuggestedBookings[0].OsmBookingId);
    Assert.False(result.AutoLinked);
}

[Fact]
public async Task CaptureEmail_SuggestsBooking_WhenCandidateNameHashMatchesCustomerNameHash()
{
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var hashing = scope.ServiceProvider.GetRequiredService<IHashingService>();

    const string groupName = "Anytown Scouts";
    db.OsmBookings.Add(new OsmBooking
    {
        OsmBookingId = "88002",
        CustomerName = groupName,
        CustomerNameHash = hashing.HashValue(groupName),
        StartDate = DateTime.UtcNow.AddDays(10),
        EndDate = DateTime.UtcNow.AddDays(12),
        Status = "Future",
        LastFetched = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var client = _factory.CreateClient();
    var response = await client.PostAsJsonAsync("/api/emails/capture", new CaptureEmailRequest
    {
        Subject = "Booking enquiry",
        SenderEmail = "someone@anytown.com",
        SenderName = "Jane Doe",
        BodyText = "Hi there",
        ReceivedDate = DateTime.UtcNow,
        CandidateNames = new List<string> { "Jane Doe", groupName }
    });

    var result = await response.Content.ReadFromJsonAsync<CaptureEmailResponse>();
    Assert.NotNull(result);
    Assert.Single(result.SuggestedBookings);
    Assert.Equal("88002", result.SuggestedBookings[0].OsmBookingId);
}
```

**Step 3: Run tests to verify they fail**

```bash
dotnet test BookingsAssistant.Tests/BookingsAssistant.Tests.csproj \
  --filter "SuggestsBooking" -v n
```

Expected: FAIL (method doesn't exist yet).

**Step 4: Implement `FindSuggestedBookingIdsAsync` in `LinkingService`**

```csharp
public async Task<List<int>> FindSuggestedBookingIdsAsync(
    string senderEmailHash, List<string> candidateNameHashes)
{
    var byEmail = await _context.OsmBookings
        .Where(b => b.CustomerEmailHash == senderEmailHash
                 && b.CustomerEmailHash != "no-email")
        .Select(b => b.Id)
        .ToListAsync();

    var byName = candidateNameHashes.Count > 0
        ? await _context.OsmBookings
            .Where(b => b.CustomerNameHash != null
                     && candidateNameHashes.Contains(b.CustomerNameHash))
            .Select(b => b.Id)
            .ToListAsync()
        : new List<int>();

    return byEmail.Concat(byName).Distinct().ToList();
}
```

**Step 5: Update `EmailsController.Capture` to call the new method**

Replace the old `suggestedBookings` block entirely:

```csharp
// Hash candidate names from the extension
var candidateNameHashes = request.CandidateNames
    .Select(n => _hashingService.HashValue(n))
    .ToList();

// Suggestions: email hash match OR name hash match
var suggestedIds = await _linkingService.FindSuggestedBookingIdsAsync(
    senderEmailHash, candidateNameHashes);

var suggestedBookings = suggestedIds.Any()
    ? await _context.OsmBookings
        .Where(b => suggestedIds.Contains(b.Id))
        .Select(b => new BookingDto
        {
            Id = b.Id,
            OsmBookingId = b.OsmBookingId,
            CustomerName = b.CustomerName,
            StartDate = b.StartDate,
            EndDate = b.EndDate,
            Status = b.Status
        })
        .ToListAsync()
    : new List<BookingDto>();
```

Remove the old `if (!linkedBookings.Any())` suggested bookings block.

**Step 6: Run new tests to verify they pass**

```bash
dotnet test BookingsAssistant.Tests/BookingsAssistant.Tests.csproj \
  --filter "SuggestsBooking" -v n
```

Expected: 2/2 PASS.

**Step 7: Run all tests**

```bash
dotnet test BookingsAssistant.Tests/BookingsAssistant.Tests.csproj -v n
```

Expected: 8/9 pass (intentionally-RED still fails).

**Step 8: Commit**

```bash
git add BookingsAssistant.Api/Services/ \
        BookingsAssistant.Api/Controllers/EmailsController.cs \
        BookingsAssistant.Tests/Controllers/EmailCaptureTests.cs
git commit -m "feat: name and email hash matching in capture endpoint"
```

---

### Task 6: BookingDetailBackfillService

**Files:**
- Create: `BookingsAssistant.Api/Services/BookingDetailBackfillService.cs`
- Modify: `BookingsAssistant.Api/Program.cs` — register hosted service

**Step 1: Create the service**

```csharp
using System.Text.Json;
using BookingsAssistant.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Services;

public class BookingDetailBackfillService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingDetailBackfillService> _logger;
    private const int BatchSize = 20;
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(30);

    public BookingDetailBackfillService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingDetailBackfillService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting before first run
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunBatchAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { _logger.LogError(ex, "Backfill batch failed"); }

            await Task.Delay(RunInterval, stoppingToken);
        }
    }

    private async Task RunBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context  = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var osm      = scope.ServiceProvider.GetRequiredService<IOsmService>();
        var hashing  = scope.ServiceProvider.GetRequiredService<IHashingService>();

        var bookings = await context.OsmBookings
            .Where(b => b.CustomerEmailHash == null
                     && b.Status != "Past"
                     && b.Status != "Cancelled")
            .OrderBy(b => b.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (bookings.Count == 0)
        {
            _logger.LogDebug("Backfill: nothing to process");
            return;
        }

        _logger.LogInformation("Backfill: processing {Count} bookings", bookings.Count);

        foreach (var booking in bookings)
        {
            try
            {
                var (fullDetails, _) = await osm.GetBookingDetailsAsync(booking.OsmBookingId);
                var email = ExtractEmail(fullDetails);

                // "no-email" sentinel prevents retrying bookings that genuinely have no email
                booking.CustomerEmailHash = email != null
                    ? hashing.HashValue(email)
                    : "no-email";

                if (booking.CustomerNameHash == null)
                    booking.CustomerNameHash = hashing.HashValue(booking.CustomerName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Backfill: failed for booking {Id}", booking.OsmBookingId);
            }
        }

        await context.SaveChangesAsync(ct);
    }

    private string? ExtractEmail(string fullDetailsJson)
    {
        if (string.IsNullOrWhiteSpace(fullDetailsJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(fullDetailsJson);
            var root = doc.RootElement;

            // Attempt 1: { data: { contact: { email: "..." } } }
            if (root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("contact", out var contact) &&
                    contact.TryGetProperty("email", out var e1) &&
                    e1.ValueKind == JsonValueKind.String)
                    return e1.GetString();

                // Attempt 2: { data: [ { label: "..email..", value: "..." } ] }
                if (data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        if (item.TryGetProperty("label", out var label) &&
                            label.GetString()?.Contains("email", StringComparison.OrdinalIgnoreCase) == true &&
                            item.TryGetProperty("value", out var val))
                            return val.GetString();
                    }
                }
            }

            _logger.LogWarning("Backfill: no email field found in response (first 300 chars): {Snippet}",
                fullDetailsJson.Length > 300 ? fullDetailsJson[..300] : fullDetailsJson);
            return null;
        }
        catch (JsonException) { return null; }
    }
}
```

**Step 2: Register in `Program.cs`**

Add before `var app = builder.Build();`:

```csharp
builder.Services.AddHostedService<BookingDetailBackfillService>();
```

**Step 3: Build**

```bash
dotnet build BookingsAssistant.Api/BookingsAssistant.Api.csproj
```

Expected: 0 errors.

**Step 4: Run all tests**

```bash
dotnet test BookingsAssistant.Tests/BookingsAssistant.Tests.csproj -v n
```

Expected: 8/9 pass.

**Step 5: Commit**

```bash
git add BookingsAssistant.Api/Services/BookingDetailBackfillService.cs \
        BookingsAssistant.Api/Program.cs
git commit -m "feat: add BookingDetailBackfillService to populate CustomerEmailHash from OSM"
```

> **Note:** The `ExtractEmail` parsing paths are best-effort. After first deployment, check the logs — the warning line will print the actual OSM response shape so you can add the correct path if needed.

---

### Task 7: Chrome extension — sign-off name extraction

**Files:**
- Modify: `bookings-extension/content-owa.js`

**Step 1: Add `extractCandidateNames` function**

Add this function after `parseFromText`:

```javascript
function extractCandidateNames(bodyText, senderName) {
  const candidates = new Set();

  if (senderName && senderName.length >= 2)
    candidates.add(senderName.trim());

  const signOffRe = /^(kind\s+regards|best\s+regards|many\s+thanks|best\s+wishes|yours\s+sincerely|yours\s+faithfully|regards|thanks|cheers|sincerely|best),?\s*$/i;
  const lines = bodyText.split(/\r?\n/);

  for (let i = 0; i < lines.length; i++) {
    if (signOffRe.test(lines[i].trim())) {
      let collected = 0;
      for (let j = i + 1; j < lines.length && collected < 3; j++) {
        const line = lines[j].trim();
        if (line.length >= 2 && line.length < 100) {
          candidates.add(line);
          collected++;
        }
      }
      break;
    }
  }

  return [...candidates];
}
```

**Step 2: Update `extractEmail` to include `candidateNames`**

In the `return` statement of `extractEmail`, add:

```javascript
return {
  subject: subject || '(No Subject)',
  senderName: senderName || '',
  senderEmail: senderEmail || '',
  bodyText,
  receivedDate: new Date().toISOString(),
  candidateNames: extractCandidateNames(bodyText, senderName || ''),
};
```

**Step 3: Update `renderBookingCard` to show match reason**

Change the suggested match line from `"Matched by sender email"` to use a generic label, since matches now come from either email or name:

```javascript
${isSuggested ? '<div style="font-size:11px;color:#666;margin-top:4px">Possible match</div>' : ''}
```

**Step 4: Reload extension and test manually**

1. Open `chrome://extensions` → reload the Bookings Assistant extension
2. Open OWA and select an email with a sign-off (e.g. `Kind regards,\nJohn Smith\n1st Scouts UK`)
3. Open DevTools → Network tab → find the `capture` POST request
4. Verify the request body includes `candidateNames: ["John Smith", "1st Scouts UK"]` (plus the sender name)

**Step 5: Commit**

```bash
git add bookings-extension/content-owa.js
git commit -m "feat: extract sign-off names from email body and send as candidate names"
```

---

### Task 8: Drop SenderEmail column + version bump

Only proceed once all prior tasks are complete and tests pass.

**Files:**
- Modify: `BookingsAssistant.Api/Data/Entities/EmailMessage.cs` — remove `SenderEmail`
- Run: `dotnet ef migrations add RemoveSenderEmail` from `BookingsAssistant.Api/`
- Modify: `bookings-assistant/config.yaml` — bump version

**Step 1: Verify no references remain to `SenderEmail` on the entity**

```bash
grep -rn "SenderEmail" BookingsAssistant.Api/ \
  --include="*.cs" \
  --exclude-dir=Migrations
```

Expected: zero matches (only migration files reference it, which is expected).

**Step 2: Remove `SenderEmail` from `EmailMessage.cs`**

Delete both lines:

```csharp
// Remove:
[MaxLength(255)]
public string? SenderEmail { get; set; }
```

**Step 3: Generate migration**

```bash
cd BookingsAssistant.Api
dotnet ef migrations add RemoveSenderEmail
cd ..
```

Verify the migration drops `SenderEmail` from `EmailMessages`.

**Step 4: Build and run all tests**

```bash
dotnet build && dotnet test BookingsAssistant.Tests/BookingsAssistant.Tests.csproj -v n
```

Expected: 8/9 pass.

**Step 5: Bump version**

In `bookings-assistant/config.yaml`, change `version: 0.9.2` to `version: 0.9.3`.

**Step 6: Commit and push**

```bash
git add .
git commit -m "feat: drop SenderEmail column, bump to v0.9.3"
git push
```

GitHub Actions will build and push image `0.9.3`. Update the addon in Home Assistant once CI completes. After updating, re-authenticate with OSM once (tokens are encrypted with the existing Data Protection key, which now persists in `/data/keys` — so no re-auth needed unless you updated from a version before 0.9.2).
