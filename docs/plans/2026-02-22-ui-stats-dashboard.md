# UI Stats Dashboard Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the broken 3-pane dashboard with a working stats dashboard showing 4 booking counts, a sync button, and an auth status indicator.

**Architecture:** Add a `GET /api/bookings/stats` endpoint to the existing `BookingsController`. Fix the frontend `apiClient.ts` to use correct URLs. Rewrite `Dashboard.tsx` with stat cards. No new services or infrastructure needed — the sync endpoint already exists at `POST /api/bookings/sync`.

**Tech Stack:** ASP.NET Core 8 / EF Core / SQLite (backend); React 18 / TypeScript / Tailwind CSS / Vite (frontend); xUnit + WebApplicationFactory + EF InMemory (tests)

---

### Task 1: Add `BookingStatsDto` model

**Files:**
- Create: `BookingsAssistant.Api/Models/BookingStatsDto.cs`

**Step 1: Create the file**

```csharp
namespace BookingsAssistant.Api.Models;

public class BookingStatsDto
{
    public int OnSiteNow { get; set; }
    public int ArrivingThisWeek { get; set; }
    public int ArrivingNext30Days { get; set; }
    public int Provisional { get; set; }
    public DateTime? LastSynced { get; set; }
}
```

**Step 2: Verify it builds**

```bash
dotnet build BookingsAssistant.Api/BookingsAssistant.Api.csproj
```

Expected: Build succeeded, 0 errors.

---

### Task 2: Write failing test for stats endpoint

**Files:**
- Create: `BookingsAssistant.Tests/Controllers/BookingStatsTests.cs`

**Step 1: Write the test**

```csharp
using System.Net;
using System.Net.Http.Json;
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookingsAssistant.Tests.Controllers;

public class BookingStatsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BookingStatsTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_Stats_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                // Prevent startup sync from calling real OSM
                services.RemoveAll<IOsmService>();
                services.AddSingleton<IOsmService>(new NoOpOsmService());
            });
        });
    }

    [Fact]
    public async Task GetStats_ReturnsCorrectCounts()
    {
        var today = DateTime.UtcNow.Date;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.AddRange(
                // On site now: Confirmed, started yesterday, ends tomorrow
                new OsmBooking { OsmBookingId = "s1", CustomerName = "On Site Now",
                    Status = "Confirmed",
                    StartDate = today.AddDays(-1), EndDate = today.AddDays(1),
                    LastFetched = DateTime.UtcNow },
                // Arriving this week: Future, starts in 3 days
                new OsmBooking { OsmBookingId = "s2", CustomerName = "Arriving Week",
                    Status = "Future",
                    StartDate = today.AddDays(3), EndDate = today.AddDays(5),
                    LastFetched = DateTime.UtcNow },
                // Arriving next 30 days but NOT this week: Future, starts in 20 days
                new OsmBooking { OsmBookingId = "s3", CustomerName = "Arriving Month",
                    Status = "Future",
                    StartDate = today.AddDays(20), EndDate = today.AddDays(22),
                    LastFetched = DateTime.UtcNow },
                // Provisional: should count in Provisional only (starts in 40 days, out of 30-day window)
                new OsmBooking { OsmBookingId = "s4", CustomerName = "Provisional Group",
                    Status = "Provisional",
                    StartDate = today.AddDays(40), EndDate = today.AddDays(42),
                    LastFetched = DateTime.UtcNow },
                // Cancelled: must NOT appear in any arriving count
                new OsmBooking { OsmBookingId = "s5", CustomerName = "Cancelled Group",
                    Status = "Cancelled",
                    StartDate = today.AddDays(2), EndDate = today.AddDays(4),
                    LastFetched = DateTime.UtcNow }
            );
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/bookings/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stats = await response.Content.ReadFromJsonAsync<BookingStatsDto>();
        Assert.NotNull(stats);
        Assert.Equal(1, stats.OnSiteNow);
        Assert.Equal(1, stats.ArrivingThisWeek);
        Assert.Equal(2, stats.ArrivingNext30Days); // s2 (3 days) and s3 (20 days)
        Assert.Equal(1, stats.Provisional);
        Assert.NotNull(stats.LastSynced);
    }

    private class NoOpOsmService : IOsmService
    {
        public Task<List<BookingDto>> GetBookingsAsync(string status)
            => Task.FromResult(new List<BookingDto>());
        public Task<(string FullDetails, List<CommentDto> Comments)> GetBookingDetailsAsync(string osmBookingId)
            => Task.FromResult((string.Empty, new List<CommentDto>()));
    }
}
```

**Step 2: Run test to verify it fails**

```bash
dotnet test BookingsAssistant.Tests/BookingsAssistant.Tests.csproj --filter "BookingStatsTests" -v n
```

Expected: FAIL — `404 Not Found` (endpoint doesn't exist yet).

---

### Task 3: Implement `GET /api/bookings/stats`

**Files:**
- Modify: `BookingsAssistant.Api/Controllers/BookingsController.cs`

Add this action after the existing `GetAll` action (around line 50), before `HttpPost("sync")`:

```csharp
[HttpGet("stats")]
public async Task<ActionResult<BookingStatsDto>> GetStats()
{
    var today = DateTime.UtcNow.Date;

    var stats = new BookingStatsDto
    {
        OnSiteNow = await _context.OsmBookings
            .CountAsync(b => b.Status == "Confirmed"
                          && b.StartDate < today.AddDays(1)
                          && b.EndDate >= today),
        ArrivingThisWeek = await _context.OsmBookings
            .CountAsync(b => b.StartDate >= today
                          && b.StartDate < today.AddDays(8)
                          && b.Status != "Cancelled"
                          && b.Status != "Past"),
        ArrivingNext30Days = await _context.OsmBookings
            .CountAsync(b => b.StartDate >= today
                          && b.StartDate < today.AddDays(31)
                          && b.Status != "Cancelled"
                          && b.Status != "Past"),
        Provisional = await _context.OsmBookings
            .CountAsync(b => b.Status == "Provisional"),
        LastSynced = await _context.OsmBookings
            .MaxAsync(b => (DateTime?)b.LastFetched)
    };

    return Ok(stats);
}
```

**Step 2: Run test to verify it passes**

```bash
dotnet test BookingsAssistant.Tests/BookingsAssistant.Tests.csproj --filter "BookingStatsTests" -v n
```

Expected: PASS.

**Step 3: Run all tests to check nothing is broken**

```bash
dotnet test BookingsAssistant.Tests/BookingsAssistant.Tests.csproj -v n
```

Expected: All tests pass (note: `GetBookings_UpsertsFetchedBookingsToDatabase` is intentionally RED — leave it as-is).

**Step 4: Commit**

```bash
git add BookingsAssistant.Api/Models/BookingStatsDto.cs \
        BookingsAssistant.Api/Controllers/BookingsController.cs \
        BookingsAssistant.Tests/Controllers/BookingStatsTests.cs
git commit -m "feat: add GET /api/bookings/stats endpoint"
```

---

### Task 4: Add `BookingStats` type to frontend

**Files:**
- Modify: `BookingsAssistant.Web/src/types/index.ts`

Append to the end of the file:

```typescript
export interface BookingStats {
  onSiteNow: number;
  arrivingThisWeek: number;
  arrivingNext30Days: number;
  provisional: number;
  lastSynced: string | null;
}
```

---

### Task 5: Fix `apiClient.ts`

**Files:**
- Modify: `BookingsAssistant.Web/src/services/apiClient.ts`

Replace the entire file contents with:

```typescript
import axios from 'axios';
import type {
  Email,
  EmailDetail,
  Booking,
  BookingDetail,
  Comment,
  Link,
  CreateLinkRequest,
  BookingStats
} from '../types';

const apiClient = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

// Emails API (capture/detail only — no unread list endpoint)
export const emailsApi = {
  getById: async (id: number): Promise<EmailDetail> => {
    const response = await apiClient.get<EmailDetail>(`/emails/${id}`);
    return response.data;
  },
};

// Bookings API
export const bookingsApi = {
  getAll: async (status?: string): Promise<Booking[]> => {
    const params = status ? { status } : {};
    const response = await apiClient.get<Booking[]>('/bookings', { params });
    return response.data;
  },

  getStats: async (): Promise<BookingStats> => {
    const response = await apiClient.get<BookingStats>('/bookings/stats');
    return response.data;
  },

  getById: async (id: number): Promise<BookingDetail> => {
    const response = await apiClient.get<BookingDetail>(`/bookings/${id}`);
    return response.data;
  },
};

// Links API
export const linksApi = {
  create: async (request: CreateLinkRequest): Promise<Link> => {
    const response = await apiClient.post<Link>('/links', request);
    return response.data;
  },

  getByEmail: async (emailId: number): Promise<Link[]> => {
    const response = await apiClient.get<Link[]>(`/links/email/${emailId}`);
    return response.data;
  },

  getByBooking: async (bookingId: number): Promise<Link[]> => {
    const response = await apiClient.get<Link[]>(`/links/booking/${bookingId}`);
    return response.data;
  },
};

// Sync API — endpoint is POST /api/bookings/sync
export const syncApi = {
  sync: async (): Promise<{ added: number; updated: number; total: number }> => {
    const response = await apiClient.post<{ added: number; updated: number; total: number }>('/bookings/sync');
    return response.data;
  },
};

export default apiClient;
```

---

### Task 6: Rewrite `Dashboard.tsx`

**Files:**
- Modify: `BookingsAssistant.Web/src/components/Dashboard.tsx`

Replace the entire file contents with:

```tsx
import { useState, useEffect, useCallback } from 'react';
import { bookingsApi, syncApi } from '../services/apiClient';
import apiClient from '../services/apiClient';
import type { BookingStats } from '../types';

interface StatCardProps {
  label: string;
  value: number | null;
  colorClass: string;
}

function StatCard({ label, value, colorClass }: StatCardProps) {
  return (
    <div className="bg-white rounded-lg shadow p-6">
      <p className="text-sm text-gray-500 uppercase tracking-wide">{label}</p>
      <p className={`text-4xl font-bold mt-2 ${colorClass}`}>
        {value === null ? '–' : value}
      </p>
    </div>
  );
}

function formatLastSynced(iso: string | null): string {
  if (!iso) return 'Never';
  const d = new Date(iso);
  const diffMs = Date.now() - d.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  const diffHours = Math.floor(diffMins / 60);
  if (diffHours < 24) return `${diffHours}h ago`;
  return d.toLocaleDateString();
}

export default function Dashboard() {
  const [stats, setStats] = useState<BookingStats | null>(null);
  const [authenticated, setAuthenticated] = useState<boolean | null>(null);
  const [loading, setLoading] = useState(false);
  const [syncing, setSyncing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadStats = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [statsRes, authRes] = await Promise.all([
        bookingsApi.getStats(),
        apiClient.get<{ authenticated: boolean }>('/auth/osm/status'),
      ]);
      setStats(statsRes);
      setAuthenticated(authRes.data.authenticated);
    } catch {
      setError('Failed to load dashboard data');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadStats();
  }, [loadStats]);

  const handleSync = async () => {
    setSyncing(true);
    setError(null);
    try {
      await syncApi.sync();
      await loadStats();
    } catch {
      setError('Sync failed — check OSM authentication');
    } finally {
      setSyncing(false);
    }
  };

  return (
    <div className="container mx-auto px-4 py-8 max-w-4xl">
      {/* Header */}
      <div className="flex justify-between items-start mb-8">
        <div>
          <h1 className="text-3xl font-bold text-gray-800">Bookings Assistant</h1>
          <div className="mt-1 text-sm text-gray-500">
            Last synced: {stats ? formatLastSynced(stats.lastSynced) : '—'}
          </div>
        </div>
        <div className="flex flex-col items-end gap-2">
          {authenticated !== null && (
            <span className="flex items-center gap-1.5 text-sm">
              <span className={`w-2.5 h-2.5 rounded-full ${authenticated ? 'bg-green-500' : 'bg-amber-500'}`} />
              {authenticated ? (
                <span className="text-green-700">OSM connected</span>
              ) : (
                <span className="text-amber-700">
                  Not connected —{' '}
                  <a href="/api/auth/osm/login" className="underline hover:text-amber-900">
                    authenticate
                  </a>
                </span>
              )}
            </span>
          )}
          <button
            onClick={handleSync}
            disabled={syncing || loading}
            className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:bg-gray-400 text-sm"
          >
            {syncing ? 'Syncing…' : 'Sync from OSM'}
          </button>
        </div>
      </div>

      {error && (
        <div className="mb-6 p-4 bg-red-100 border border-red-400 text-red-700 rounded text-sm">
          {error}
        </div>
      )}

      {/* Stat cards */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <StatCard
          label="On site now"
          value={loading ? null : (stats?.onSiteNow ?? null)}
          colorClass="text-green-600"
        />
        <StatCard
          label="Arriving this week"
          value={loading ? null : (stats?.arrivingThisWeek ?? null)}
          colorClass="text-blue-600"
        />
        <StatCard
          label="Next 30 days"
          value={loading ? null : (stats?.arrivingNext30Days ?? null)}
          colorClass="text-indigo-600"
        />
        <StatCard
          label="Provisional"
          value={loading ? null : (stats?.provisional ?? null)}
          colorClass="text-amber-600"
        />
      </div>
    </div>
  );
}
```

**Step 2: Commit**

```bash
git add BookingsAssistant.Web/src/types/index.ts \
        BookingsAssistant.Web/src/services/apiClient.ts \
        BookingsAssistant.Web/src/components/Dashboard.tsx
git commit -m "feat: replace dashboard with stats cards and working sync button"
```

---

### Task 7: Build and verify

**Step 1: Build the frontend**

```bash
cd BookingsAssistant.Web && npm run build
```

Expected: Build succeeded, no TypeScript errors.

**Step 2: Run all backend tests**

```bash
dotnet test BookingsAssistant.Tests/BookingsAssistant.Tests.csproj -v n
```

Expected: All tests pass except the intentionally-RED `GetBookings_UpsertsFetchedBookingsToDatabase`.

**Step 3: Bump version and push**

Update `bookings-assistant/config.yaml`: change `version: 0.9.0` to `version: 0.9.1`

```bash
git add bookings-assistant/config.yaml
git commit -m "chore: bump addon version to 0.9.1"
git push
```

GitHub Actions will build and push the new image. Once complete, update the addon in Home Assistant.
