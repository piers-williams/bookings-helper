# Chrome Extension + Email Capture Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the blocked Graph API email integration with a Chrome extension that reads emails from OWA and injects a booking-context sidebar, plus a matching sidebar for OSM.

**Architecture:** A Manifest V3 Chrome extension injects a sidebar into OWA and OSM. When an email is opened, a content script reads the DOM and passes the data to a background service worker, which POSTs it to the .NET backend via a new `/api/emails/capture` endpoint. The backend runs the existing booking-ref extraction and linking logic and returns matched bookings for the sidebar to display.

**Tech Stack:** Chrome Extension (Manifest V3, vanilla JS), .NET 8 ASP.NET Core (existing), xUnit (new test project), SQLite (existing)

**Working directory:** `.worktrees/bookings-assistant-mvp/`

---

## Task 1: Add xUnit test project

**Files:**
- Create: `BookingsAssistant.Tests/BookingsAssistant.Tests.csproj`
- Create: `BookingsAssistant.Tests/Controllers/EmailCaptureTests.cs`
- Modify: `BookingsAssistant.sln`

**Step 1: Create test project**

```bash
cd .worktrees/bookings-assistant-mvp
dotnet new xunit -n BookingsAssistant.Tests
dotnet sln add BookingsAssistant.Tests/BookingsAssistant.Tests.csproj
cd BookingsAssistant.Tests
dotnet add reference ../BookingsAssistant.Api/BookingsAssistant.Api.csproj
dotnet add package Microsoft.AspNetCore.Mvc.Testing
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```

**Step 2: Replace the default test file**

Delete `UnitTest1.cs`. Create `Controllers/EmailCaptureTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookingsAssistant.Tests.Controllers;

public class EmailCaptureTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EmailCaptureTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace SQLite with in-memory database for tests
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));
            });
        });
    }

    [Fact]
    public async Task CaptureEmail_WithBookingRef_Returns200AndLinkedBooking()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Seed a booking into the in-memory DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.OsmBookings.Add(new BookingsAssistant.Api.Data.Entities.OsmBooking
        {
            OsmBookingId = "99999",
            CustomerName = "Test Customer",
            CustomerEmail = "test@example.com",
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(33),
            Status = "Provisional",
            LastFetched = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var request = new CaptureEmailRequest
        {
            Subject = "Query about booking #99999",
            SenderEmail = "test@example.com",
            SenderName = "Test Customer",
            BodyText = "Hi, just checking on booking #99999",
            ReceivedDate = DateTime.UtcNow
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/emails/capture", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CaptureEmailResponse>();
        Assert.NotNull(result);
        Assert.True(result.EmailId > 0);
        Assert.Single(result.LinkedBookings);
        Assert.Equal("99999", result.LinkedBookings[0].OsmBookingId);
        Assert.True(result.AutoLinked);
    }

    [Fact]
    public async Task CaptureEmail_NoDuplicates_WhenSameEmailSentTwice()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CaptureEmailRequest
        {
            Subject = "No booking ref here",
            SenderEmail = "once@example.com",
            SenderName = "Once Only",
            BodyText = "Just a plain email",
            ReceivedDate = new DateTime(2026, 2, 21, 10, 0, 0, DateTimeKind.Utc)
        };

        // Act — send twice
        var first = await client.PostAsJsonAsync("/api/emails/capture", request);
        var second = await client.PostAsJsonAsync("/api/emails/capture", request);

        // Assert — both succeed but return same emailId
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var r1 = await first.Content.ReadFromJsonAsync<CaptureEmailResponse>();
        var r2 = await second.Content.ReadFromJsonAsync<CaptureEmailResponse>();
        Assert.Equal(r1!.EmailId, r2!.EmailId);
    }

    [Fact]
    public async Task CaptureEmail_NoBookingRef_Returns200WithEmptyLinkedBookings()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CaptureEmailRequest
        {
            Subject = "General enquiry",
            SenderEmail = "visitor@example.com",
            SenderName = "A Visitor",
            BodyText = "Do you have availability in summer?",
            ReceivedDate = DateTime.UtcNow
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/emails/capture", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CaptureEmailResponse>();
        Assert.NotNull(result);
        Assert.Empty(result.LinkedBookings);
        Assert.False(result.AutoLinked);
    }
}
```

**Step 3: Verify tests fail (endpoint doesn't exist yet)**

```bash
cd .worktrees/bookings-assistant-mvp
dotnet test BookingsAssistant.Tests --no-build 2>&1 | tail -20
```

Expected: Build errors referencing missing `CaptureEmailRequest`, `CaptureEmailResponse`, and the endpoint. That's fine — we'll add them next.

**Step 4: Commit**

```bash
git add BookingsAssistant.Tests/ BookingsAssistant.sln
git commit -m "test: add xUnit test project with email capture tests"
```

---

## Task 2: Add request/response models

**Files:**
- Create: `BookingsAssistant.Api/Models/CaptureEmailRequest.cs`
- Create: `BookingsAssistant.Api/Models/CaptureEmailResponse.cs`

**Step 1: Create CaptureEmailRequest.cs**

```csharp
namespace BookingsAssistant.Api.Models;

public class CaptureEmailRequest
{
    public string Subject { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
}
```

**Step 2: Create CaptureEmailResponse.cs**

```csharp
namespace BookingsAssistant.Api.Models;

public class CaptureEmailResponse
{
    public int EmailId { get; set; }
    public bool AutoLinked { get; set; }
    public List<BookingDto> LinkedBookings { get; set; } = new();
    public List<BookingDto> SuggestedBookings { get; set; } = new();
}
```

**Step 3: Verify tests still fail (endpoint missing, not models)**

```bash
dotnet build BookingsAssistant.Tests
dotnet test BookingsAssistant.Tests 2>&1 | grep -E "FAIL|PASS|Error"
```

Expected: Build succeeds, tests fail with 404.

**Step 4: Commit**

```bash
git add BookingsAssistant.Api/Models/CaptureEmailRequest.cs \
        BookingsAssistant.Api/Models/CaptureEmailResponse.cs
git commit -m "feat: add CaptureEmailRequest and CaptureEmailResponse models"
```

---

## Task 3: Add POST /api/emails/capture endpoint

**Files:**
- Modify: `BookingsAssistant.Api/Controllers/EmailsController.cs`

**Step 1: Add the Capture action to EmailsController**

Add this method inside the `EmailsController` class, after the existing `GetById` action:

```csharp
[HttpPost("capture")]
public async Task<ActionResult<CaptureEmailResponse>> Capture([FromBody] CaptureEmailRequest request)
{
    // Duplicate detection: same subject + sender + date already captured
    var existing = await _context.EmailMessages.FirstOrDefaultAsync(e =>
        e.Subject == request.Subject &&
        e.SenderEmail == request.SenderEmail &&
        e.ReceivedDate == request.ReceivedDate);

    int emailId;
    if (existing != null)
    {
        emailId = existing.Id;
    }
    else
    {
        var email = new BookingsAssistant.Api.Data.Entities.EmailMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            Subject = request.Subject,
            SenderEmail = request.SenderEmail,
            SenderName = request.SenderName,
            ReceivedDate = request.ReceivedDate,
            IsRead = false,
            LastFetched = DateTime.UtcNow
        };
        _context.EmailMessages.Add(email);
        await _context.SaveChangesAsync();
        emailId = email.Id;

        await _linkingService.CreateAutoLinksForEmailAsync(emailId, request.Subject, request.BodyText);
    }

    // Fetch linked bookings
    var linkedBookingIds = await _linkingService.GetLinkedBookingIdsAsync(emailId);
    var linkedBookings = await _context.OsmBookings
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

    // Suggested bookings: match by sender email if no auto-links found
    var suggestedBookings = new List<BookingDto>();
    if (!linkedBookings.Any())
    {
        suggestedBookings = await _context.OsmBookings
            .Where(b => b.CustomerEmail == request.SenderEmail)
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
    }

    return Ok(new CaptureEmailResponse
    {
        EmailId = emailId,
        AutoLinked = linkedBookings.Any(),
        LinkedBookings = linkedBookings,
        SuggestedBookings = suggestedBookings
    });
}
```

**Step 2: Run the tests**

```bash
dotnet test BookingsAssistant.Tests -v normal 2>&1 | tail -30
```

Expected: All 3 tests pass.

**Step 3: Commit**

```bash
git add BookingsAssistant.Api/Controllers/EmailsController.cs
git commit -m "feat: add POST /api/emails/capture endpoint"
```

---

## Task 4: Update CORS to allow Chrome extension requests

The extension's background service worker sends requests from a `chrome-extension://` origin. The backend must accept these.

**Files:**
- Modify: `BookingsAssistant.Api/Program.cs`

**Step 1: Replace the CORS configuration in Program.cs**

Find this block:

```csharp
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
```

Replace with:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });

    // Allow Chrome extension to POST to capture endpoint
    // AllowAnyOrigin is acceptable here: the backend runs on a private network
    // and the capture endpoint only stores data — it is not a sensitive read
    options.AddPolicy("ExtensionCapture", policy =>
    {
        policy.AllowAnyOrigin()
              .WithMethods("POST", "GET", "OPTIONS")
              .AllowAnyHeader();
    });
});
```

Then find `app.UseCors("Development")` and add the new policy on the capture endpoint. Replace:

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("Development");
}
```

With:

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("Development");
}

// Allow extension to reach capture and bookings-links endpoints from any environment
app.MapControllers();
```

Then add the `[EnableCors]` attribute to just the capture and bookings-links actions by adding the attribute to the `Capture` action in `EmailsController.cs`:

```csharp
[HttpPost("capture")]
[Microsoft.AspNetCore.Cors.EnableCors("ExtensionCapture")]
public async Task<ActionResult<CaptureEmailResponse>> Capture(...)
```

And to the `GetLinks` action in `BookingsController.cs` (the endpoint the OSM sidebar will call — see Task 8):

```csharp
[HttpGet("{id}/links")]
[Microsoft.AspNetCore.Cors.EnableCors("ExtensionCapture")]
```

**Step 2: Verify the API still starts**

```bash
cd BookingsAssistant.Api
dotnet build
```

Expected: Build succeeded, 0 errors.

**Step 3: Commit**

```bash
git add BookingsAssistant.Api/Program.cs BookingsAssistant.Api/Controllers/EmailsController.cs
git commit -m "feat: add ExtensionCapture CORS policy for Chrome extension requests"
```

---

## Task 5: Create extension directory and manifest

**Files:**
- Create: `bookings-extension/manifest.json`

**Step 1: Create the directory and manifest**

```bash
mkdir -p bookings-extension
```

Create `bookings-extension/manifest.json`:

```json
{
  "manifest_version": 3,
  "name": "Bookings Assistant",
  "version": "0.1.0",
  "description": "Shows OSM booking context alongside OWA emails, and email context alongside OSM bookings.",
  "permissions": ["storage"],
  "host_permissions": [
    "https://outlook.office365.com/*",
    "https://www.onlinescoutmanager.co.uk/*"
  ],
  "content_scripts": [
    {
      "matches": ["https://outlook.office365.com/*"],
      "js": ["content-owa.js"],
      "css": ["sidebar.css"],
      "run_at": "document_idle"
    },
    {
      "matches": ["https://www.onlinescoutmanager.co.uk/*"],
      "js": ["content-osm.js"],
      "css": ["sidebar.css"],
      "run_at": "document_idle"
    }
  ],
  "background": {
    "service_worker": "background.js"
  },
  "options_page": "options.html",
  "action": {
    "default_title": "Bookings Assistant",
    "default_popup": "options.html"
  }
}
```

**Step 2: Load the extension in Chrome to verify manifest is valid**

1. Open Chrome → `chrome://extensions`
2. Enable **Developer mode** (top right toggle)
3. Click **Load unpacked**
4. Select the `bookings-extension/` folder
5. Verify no manifest errors appear (missing files are OK at this stage)

**Step 3: Commit**

```bash
git add bookings-extension/manifest.json
git commit -m "feat: add Chrome extension manifest"
```

---

## Task 6: Create the options page

The options page is also used as the extension popup. It lets you set the backend URL.

**Files:**
- Create: `bookings-extension/options.html`
- Create: `bookings-extension/options.js`

**Step 1: Create options.html**

```html
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <title>Bookings Assistant Settings</title>
  <style>
    body { font-family: sans-serif; padding: 16px; min-width: 300px; }
    label { display: block; margin-bottom: 4px; font-weight: bold; font-size: 13px; }
    input { width: 100%; box-sizing: border-box; padding: 6px 8px; border: 1px solid #ccc; border-radius: 4px; font-size: 13px; }
    button { margin-top: 12px; padding: 6px 16px; background: #0078d4; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 13px; }
    button:hover { background: #106ebe; }
    #status { margin-top: 8px; font-size: 12px; color: green; }
    .hint { font-size: 11px; color: #666; margin-top: 4px; }
  </style>
</head>
<body>
  <h3 style="margin-top:0">Bookings Assistant</h3>
  <label for="backendUrl">Backend URL</label>
  <input type="text" id="backendUrl" placeholder="http://192.168.1.50:5000">
  <div class="hint">The address of the Bookings Assistant backend on your network.</div>
  <button id="save">Save</button>
  <div id="status"></div>
  <script src="options.js"></script>
</body>
</html>
```

**Step 2: Create options.js**

```javascript
document.addEventListener('DOMContentLoaded', () => {
  const input = document.getElementById('backendUrl');
  const status = document.getElementById('status');

  // Load saved value
  chrome.storage.sync.get(['backendUrl'], (result) => {
    if (result.backendUrl) input.value = result.backendUrl;
  });

  document.getElementById('save').addEventListener('click', () => {
    const url = input.value.trim().replace(/\/$/, ''); // strip trailing slash
    if (!url.startsWith('http')) {
      status.textContent = 'URL must start with http:// or https://';
      status.style.color = 'red';
      return;
    }
    chrome.storage.sync.set({ backendUrl: url }, () => {
      status.textContent = 'Saved!';
      status.style.color = 'green';
      setTimeout(() => status.textContent = '', 2000);
    });
  });
});
```

**Step 3: Reload extension and verify**

1. Go to `chrome://extensions`, click the reload icon on Bookings Assistant
2. Click the extension icon in the toolbar
3. Verify the options popup opens, enter `http://localhost:5000`, click Save
4. Reopen — confirm the URL is still there

**Step 4: Commit**

```bash
git add bookings-extension/options.html bookings-extension/options.js
git commit -m "feat: add extension options/popup page for backend URL configuration"
```

---

## Task 7: Create the background service worker

**Files:**
- Create: `bookings-extension/background.js`

**Step 1: Create background.js**

```javascript
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.type === 'CAPTURE_EMAIL') {
    handleCaptureEmail(message.payload).then(sendResponse);
    return true; // keep channel open for async response
  }
  if (message.type === 'GET_BOOKING_LINKS') {
    handleGetBookingLinks(message.bookingId).then(sendResponse);
    return true;
  }
});

async function getBackendUrl() {
  return new Promise((resolve) => {
    chrome.storage.sync.get(['backendUrl'], (result) => {
      resolve(result.backendUrl || null);
    });
  });
}

async function handleCaptureEmail(payload) {
  const backendUrl = await getBackendUrl();
  if (!backendUrl) {
    return { error: 'not_configured' };
  }
  try {
    const response = await fetch(`${backendUrl}/api/emails/capture`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    if (!response.ok) {
      return { error: 'server_error', status: response.status };
    }
    return await response.json();
  } catch (e) {
    return { error: 'unreachable', url: backendUrl, message: e.message };
  }
}

async function handleGetBookingLinks(bookingId) {
  const backendUrl = await getBackendUrl();
  if (!backendUrl) {
    return { error: 'not_configured' };
  }
  try {
    const response = await fetch(`${backendUrl}/api/bookings/${bookingId}/links`);
    if (!response.ok) {
      return { error: 'server_error', status: response.status };
    }
    return await response.json();
  } catch (e) {
    return { error: 'unreachable', url: backendUrl, message: e.message };
  }
}
```

**Step 2: Reload extension, verify no errors**

1. Reload extension at `chrome://extensions`
2. Click "Service Worker" link — verify no errors in the console

**Step 3: Commit**

```bash
git add bookings-extension/background.js
git commit -m "feat: add background service worker for proxying backend requests"
```

---

## Task 8: Create the sidebar CSS

**Files:**
- Create: `bookings-extension/sidebar.css`

**Step 1: Create sidebar.css**

```css
#ba-sidebar {
  position: fixed;
  top: 0;
  right: 0;
  width: 280px;
  height: 100vh;
  background: #ffffff;
  border-left: 1px solid #e0e0e0;
  box-shadow: -2px 0 8px rgba(0,0,0,0.08);
  z-index: 99999;
  font-family: 'Segoe UI', sans-serif;
  font-size: 13px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

#ba-sidebar-header {
  padding: 10px 12px;
  background: #0078d4;
  color: white;
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-weight: 600;
  font-size: 13px;
  flex-shrink: 0;
}

#ba-sidebar-header button {
  background: none;
  border: none;
  color: white;
  cursor: pointer;
  font-size: 16px;
  padding: 0;
  line-height: 1;
}

#ba-sidebar-body {
  flex: 1;
  overflow-y: auto;
  padding: 0;
}

.ba-section {
  border-bottom: 1px solid #f0f0f0;
  padding: 10px 12px;
}

.ba-section-title {
  font-weight: 600;
  font-size: 11px;
  text-transform: uppercase;
  color: #666;
  margin-bottom: 6px;
  letter-spacing: 0.5px;
}

.ba-booking-card {
  background: #f8f8f8;
  border-radius: 4px;
  padding: 8px 10px;
  margin-bottom: 6px;
}

.ba-booking-ref {
  font-weight: 600;
  color: #0078d4;
}

.ba-booking-name {
  color: #333;
}

.ba-booking-dates {
  font-size: 11px;
  color: #666;
  margin-top: 2px;
}

.ba-booking-status {
  display: inline-block;
  font-size: 10px;
  padding: 1px 6px;
  border-radius: 10px;
  margin-top: 4px;
  font-weight: 600;
}

.ba-status-provisional { background: #fff4ce; color: #9a6700; }
.ba-status-confirmed { background: #dff6dd; color: #107c10; }
.ba-status-cancelled { background: #fde7e9; color: #a4262c; }

.ba-link-btn {
  display: block;
  width: 100%;
  margin-top: 6px;
  padding: 5px 10px;
  background: #0078d4;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 12px;
  text-align: center;
}

.ba-link-btn:hover { background: #106ebe; }
.ba-link-btn.secondary { background: #f3f3f3; color: #333; border: 1px solid #ccc; }
.ba-link-btn.secondary:hover { background: #e8e8e8; }

.ba-loading { color: #666; font-style: italic; padding: 10px 12px; }
.ba-error { color: #a4262c; padding: 10px 12px; font-size: 12px; }
.ba-empty { color: #999; padding: 10px 12px; font-size: 12px; }

.ba-email-item {
  padding: 6px 0;
  border-bottom: 1px solid #f0f0f0;
}
.ba-email-item:last-child { border-bottom: none; }
.ba-email-subject { font-weight: 500; color: #333; }
.ba-email-meta { font-size: 11px; color: #888; margin-top: 2px; }

.ba-handle-btn {
  margin: 10px 12px;
  width: calc(100% - 24px);
  padding: 7px 10px;
  background: #6b21a8;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 12px;
  font-weight: 600;
}
.ba-handle-btn:hover { background: #581c87; }
```

**Step 2: Commit**

```bash
git add bookings-extension/sidebar.css
git commit -m "feat: add sidebar CSS styles"
```

---

## Task 9: Create the OWA content script

This is the core of the OWA integration. It injects the sidebar and reads emails from the DOM.

**Files:**
- Create: `bookings-extension/content-owa.js`

**Step 1: Create content-owa.js**

```javascript
(function () {
  'use strict';

  let sidebar = null;
  let lastEmailKey = null;
  let debounceTimer = null;

  // --- Sidebar injection ---

  function injectSidebar() {
    if (document.getElementById('ba-sidebar')) return;

    sidebar = document.createElement('div');
    sidebar.id = 'ba-sidebar';
    sidebar.innerHTML = `
      <div id="ba-sidebar-header">
        <span>Bookings Assistant</span>
        <button id="ba-refresh" title="Refresh">⟳</button>
      </div>
      <div id="ba-sidebar-body">
        <div class="ba-loading">Open an email to see booking context.</div>
      </div>
    `;
    document.body.appendChild(sidebar);

    document.getElementById('ba-refresh').addEventListener('click', () => {
      lastEmailKey = null; // force re-fetch
      checkForEmailChange();
    });

    // Push OWA content left to make room for sidebar
    document.body.style.paddingRight = '280px';
  }

  // --- Email extraction from OWA DOM ---

  function extractEmail() {
    // OWA renders the reading pane in a div with role="main" or similar.
    // We target the most stable selectors available.
    const subject = getTextBySelectors([
      '[data-testid="ConversationSubject"]',
      '.allowTextSelection.customScrollBar .ms-font-xxl',
      'div[aria-label] h1',
    ]);

    const sender = getTextBySelectors([
      '[data-testid="SenderName"]',
      '.allowTextSelection .ms-Persona-primaryText',
      '[aria-label="From"] .ms-Persona-primaryText',
    ]);

    const senderEmail = getAttributeBySelectors([
      '[data-testid="SenderEmail"]',
      '[aria-label="From"] [title]',
    ], 'title') || extractEmailFromText(sender);

    // Body text — strip HTML
    const bodyEl = document.querySelector(
      '[data-testid="messageBody"], .ReadingPaneContent, [aria-label="Message body"]'
    );
    const bodyText = bodyEl ? bodyEl.innerText.substring(0, 5000) : '';

    if (!subject && !sender) return null;

    return {
      subject: subject || '(No Subject)',
      senderName: sender || '',
      senderEmail: senderEmail || '',
      bodyText,
      receivedDate: new Date().toISOString(), // OWA date not easily extracted; use now as fallback
    };
  }

  function getTextBySelectors(selectors) {
    for (const sel of selectors) {
      const el = document.querySelector(sel);
      if (el && el.textContent.trim()) return el.textContent.trim();
    }
    return null;
  }

  function getAttributeBySelectors(selectors, attr) {
    for (const sel of selectors) {
      const el = document.querySelector(sel);
      if (el && el.getAttribute(attr)) return el.getAttribute(attr);
    }
    return null;
  }

  function extractEmailFromText(text) {
    if (!text) return '';
    const match = text.match(/[\w.+-]+@[\w-]+\.[\w.]+/);
    return match ? match[0] : '';
  }

  // --- Change detection ---

  function checkForEmailChange() {
    const email = extractEmail();
    if (!email) return;

    const key = `${email.subject}|${email.senderEmail}`;
    if (key === lastEmailKey) return;

    lastEmailKey = key;
    showLoading();
    sendToBackend(email);
  }

  // --- Backend communication ---

  function sendToBackend(email) {
    chrome.runtime.sendMessage(
      { type: 'CAPTURE_EMAIL', payload: email },
      (response) => {
        if (chrome.runtime.lastError) {
          showError('Extension error: ' + chrome.runtime.lastError.message);
          return;
        }
        renderResponse(response, email);
      }
    );
  }

  // --- Sidebar rendering ---

  function showLoading() {
    document.getElementById('ba-sidebar-body').innerHTML =
      '<div class="ba-loading">Loading booking context...</div>';
  }

  function showError(msg) {
    document.getElementById('ba-sidebar-body').innerHTML =
      `<div class="ba-error">${msg}</div>`;
  }

  function renderResponse(response, email) {
    if (!response || response.error) {
      if (response && response.error === 'not_configured') {
        document.getElementById('ba-sidebar-body').innerHTML =
          '<div class="ba-error">Backend URL not configured.<br>' +
          '<a href="#" id="ba-open-options">Open settings →</a></div>';
        document.getElementById('ba-open-options')?.addEventListener('click', (e) => {
          e.preventDefault();
          chrome.runtime.sendMessage({ type: 'OPEN_OPTIONS' });
        });
      } else {
        const url = response?.url || '(unknown)';
        showError(`Can't reach backend at ${url}. Is it running?`);
      }
      return;
    }

    const body = document.getElementById('ba-sidebar-body');
    let html = '';

    if (response.linkedBookings && response.linkedBookings.length > 0) {
      html += '<div class="ba-section">';
      html += '<div class="ba-section-title">✅ Linked Booking</div>';
      response.linkedBookings.forEach(b => {
        html += renderBookingCard(b);
      });
      html += '</div>';
    } else if (response.suggestedBookings && response.suggestedBookings.length > 0) {
      html += '<div class="ba-section">';
      html += '<div class="ba-section-title">🔍 Possible Match</div>';
      response.suggestedBookings.forEach(b => {
        html += renderBookingCard(b, true);
      });
      html += '</div>';
    } else {
      html += '<div class="ba-section">';
      html += '<div class="ba-empty">No booking linked.</div>';
      html += '</div>';
    }

    // Manual link search
    html += '<div class="ba-section">';
    html += '<div class="ba-section-title">🔗 Manual Link</div>';
    html += `<button class="ba-link-btn secondary" onclick="window.open('http://localhost:5000', '_blank')">Open Dashboard →</button>`;
    html += '</div>';

    // Handle with AI (placeholder for Phase 2)
    html += `<button class="ba-handle-btn" disabled title="Coming in Phase 2">✨ Handle with AI</button>`;

    body.innerHTML = html;
  }

  function renderBookingCard(booking, isSuggested = false) {
    const status = (booking.status || '').toLowerCase();
    const statusClass = `ba-status-${status}`;
    const start = booking.startDate ? new Date(booking.startDate).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' }) : '';
    const end = booking.endDate ? new Date(booking.endDate).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' }) : '';

    return `
      <div class="ba-booking-card">
        <div><span class="ba-booking-ref">#${booking.osmBookingId}</span> · <span class="ba-booking-name">${booking.customerName}</span></div>
        <div class="ba-booking-dates">${start}${end ? ' – ' + end : ''}</div>
        <div><span class="ba-booking-status ${statusClass}">${booking.status}</span></div>
        ${isSuggested ? '<div style="font-size:11px;color:#666;margin-top:4px">Matched by sender email</div>' : ''}
      </div>
    `;
  }

  // --- MutationObserver for SPA navigation ---

  function startObserver() {
    const observer = new MutationObserver(() => {
      clearTimeout(debounceTimer);
      debounceTimer = setTimeout(checkForEmailChange, 400);
    });

    observer.observe(document.body, {
      childList: true,
      subtree: true,
    });
  }

  // --- Init ---

  function init() {
    injectSidebar();
    startObserver();
    // Initial check in case an email is already open
    setTimeout(checkForEmailChange, 1000);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
```

**Step 2: Reload extension and manual test**

1. Reload extension at `chrome://extensions`
2. Open `https://outlook.office365.com` and navigate to the shared mailbox
3. Verify the blue sidebar appears on the right
4. Click an email — verify "Loading booking context..." appears
5. If backend is running locally: verify a response appears (or "Can't reach backend" error which is expected if backend isn't running)

> **Note on OWA selectors:** OWA's DOM changes with updates. If the sidebar shows "Open an email to see booking context" even after clicking an email, the selectors need adjusting. Open DevTools on OWA, inspect the email subject and sender elements, and update `getTextBySelectors` in `content-owa.js` to match the actual selectors. This is expected during initial setup.

**Step 3: Commit**

```bash
git add bookings-extension/content-owa.js
git commit -m "feat: add OWA content script with sidebar injection and email reading"
```

---

## Task 10: Create the OSM content script

**Files:**
- Create: `bookings-extension/content-osm.js`

**Step 1: Create content-osm.js**

```javascript
(function () {
  'use strict';

  let sidebar = null;
  let lastBookingId = null;
  let debounceTimer = null;

  // --- Sidebar injection ---

  function injectSidebar() {
    if (document.getElementById('ba-sidebar')) return;

    sidebar = document.createElement('div');
    sidebar.id = 'ba-sidebar';
    sidebar.innerHTML = `
      <div id="ba-sidebar-header">
        <span>Bookings Assistant</span>
        <button id="ba-refresh" title="Refresh">⟳</button>
      </div>
      <div id="ba-sidebar-body">
        <div class="ba-loading">Navigate to a booking to see linked emails.</div>
      </div>
    `;
    document.body.appendChild(sidebar);

    document.getElementById('ba-refresh').addEventListener('click', () => {
      lastBookingId = null;
      checkForBookingChange();
    });

    document.body.style.paddingRight = '280px';
  }

  // --- Booking ID extraction from OSM URL/DOM ---

  function extractBookingId() {
    // OSM URLs typically contain bookingid= or similar
    const urlMatch = window.location.href.match(/bookingid[=\/](\d+)/i);
    if (urlMatch) return urlMatch[1];

    // Fallback: look for it in the DOM
    const domMatch = document.body.innerHTML.match(/bookingid[="\s]+(\d+)/i);
    if (domMatch) return domMatch[1];

    return null;
  }

  // --- Change detection ---

  function checkForBookingChange() {
    const bookingId = extractBookingId();
    if (!bookingId || bookingId === lastBookingId) return;

    lastBookingId = bookingId;
    showLoading();
    fetchLinkedEmails(bookingId);
  }

  // --- Backend communication ---

  function fetchLinkedEmails(bookingId) {
    chrome.runtime.sendMessage(
      { type: 'GET_BOOKING_LINKS', bookingId },
      (response) => {
        if (chrome.runtime.lastError) {
          showError('Extension error: ' + chrome.runtime.lastError.message);
          return;
        }
        renderResponse(response);
      }
    );
  }

  // --- Sidebar rendering ---

  function showLoading() {
    document.getElementById('ba-sidebar-body').innerHTML =
      '<div class="ba-loading">Loading linked emails...</div>';
  }

  function showError(msg) {
    document.getElementById('ba-sidebar-body').innerHTML =
      `<div class="ba-error">${msg}</div>`;
  }

  function renderResponse(response) {
    const body = document.getElementById('ba-sidebar-body');

    if (!response || response.error) {
      if (response?.error === 'not_configured') {
        body.innerHTML = '<div class="ba-error">Backend URL not configured. Click the extension icon to set it.</div>';
      } else {
        showError(`Can't reach backend at ${response?.url || '(unknown)'}. Is it running?`);
      }
      return;
    }

    // response is an array of LinkDto or similar — adjust based on actual API response shape
    const emails = Array.isArray(response) ? response : (response.emails || []);
    let html = '';

    if (emails.length === 0) {
      html = '<div class="ba-section"><div class="ba-empty">No emails linked to this booking yet.</div></div>';
    } else {
      html += `<div class="ba-section">`;
      html += `<div class="ba-section-title">📧 Linked Emails (${emails.length})</div>`;
      emails.forEach(email => {
        const date = email.receivedDate
          ? new Date(email.receivedDate).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })
          : '';
        html += `
          <div class="ba-email-item">
            <div class="ba-email-subject">${email.subject || '(No Subject)'}</div>
            <div class="ba-email-meta">${email.senderName || email.senderEmail || ''} · ${date}</div>
          </div>
        `;
      });
      html += '</div>';
    }

    html += `<button class="ba-handle-btn" disabled title="Coming in Phase 2">✨ Handle with AI</button>`;

    body.innerHTML = html;
  }

  // --- MutationObserver for SPA navigation ---

  function startObserver() {
    let lastUrl = window.location.href;

    const observer = new MutationObserver(() => {
      if (window.location.href !== lastUrl) {
        lastUrl = window.location.href;
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(checkForBookingChange, 400);
      }
    });

    observer.observe(document.body, { childList: true, subtree: true });
  }

  // --- Init ---

  function init() {
    injectSidebar();
    startObserver();
    setTimeout(checkForBookingChange, 1000);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
```

**Step 2: Add `[EnableCors]` to the bookings links endpoint**

Open `BookingsAssistant.Api/Controllers/BookingsController.cs`, find `GetLinks` (the `GET /api/bookings/{id}/links` action) and add:

```csharp
[HttpGet("{id}/links")]
[Microsoft.AspNetCore.Cors.EnableCors("ExtensionCapture")]
public async Task<ActionResult<List<LinkDto>>> GetLinks(int id)
```

**Step 3: Manual test on OSM**

1. Reload extension
2. Navigate to `https://www.onlinescoutmanager.co.uk` and open a booking
3. Verify sidebar appears and attempts to fetch linked emails
4. Verify the correct bookingId is extracted from the URL

> **Note on OSM URL patterns:** If the sidebar doesn't detect the booking ID, open DevTools, check `window.location.href` on a booking page, and update the regex in `extractBookingId()` to match the actual URL pattern.

**Step 4: Commit**

```bash
git add bookings-extension/content-osm.js BookingsAssistant.Api/Controllers/BookingsController.cs
git commit -m "feat: add OSM content script with linked email sidebar"
```

---

## Task 11: End-to-end smoke test

**Goal:** Confirm the full loop works: OWA email → backend → sidebar.

**Step 1: Start the backend**

On the network machine (or locally):

```bash
cd .worktrees/bookings-assistant-mvp/BookingsAssistant.Api
dotnet run
```

Verify it starts at `http://localhost:5000` (or the network machine's IP).

**Step 2: Configure the extension**

Click the extension icon → enter the backend URL (e.g. `http://192.168.1.50:5000`) → Save.

**Step 3: Open OWA and click an email in the shared bookings mailbox**

Expected:
- Sidebar shows "Loading booking context..."
- Within 1–2 seconds, either:
  - A booking card appears (if the email contained a booking ref)
  - "No booking linked" with a suggested match or empty state

**Step 4: Check it was stored in the backend**

```bash
curl http://localhost:5000/api/emails
```

Expected: The email you just viewed appears in the list.

**Step 5: Run the automated tests one final time**

```bash
cd .worktrees/bookings-assistant-mvp
dotnet test BookingsAssistant.Tests -v normal
```

Expected: All tests pass.

**Step 6: Final commit**

```bash
git add -A
git commit -m "feat: complete Chrome extension with OWA and OSM sidebar integration"
```

---

## Selector Troubleshooting Reference

OWA's DOM structure changes with updates. If selectors break, use DevTools:

1. Open OWA, open an email
2. Press F12 → Elements tab
3. Use the inspector (Ctrl+Shift+C) to click the email subject
4. Right-click the element → Copy → Copy selector
5. Update `getTextBySelectors` in `content-owa.js`

For OSM booking ID extraction, check `window.location.href` in the DevTools console while on a booking page and update the regex in `extractBookingId()` in `content-osm.js`.
