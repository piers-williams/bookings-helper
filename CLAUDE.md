# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

### Backend (.NET 8)
```bash
cd BookingsAssistant.Api
dotnet restore
dotnet ef database update   # apply migrations (needs ef tools: dotnet tool install -g dotnet-ef)
dotnet run                  # serves on localhost:5000
dotnet watch run            # auto-reload on changes
```

### Frontend (React/TypeScript/Vite)
```bash
cd BookingsAssistant.Web
npm install
npm run dev     # dev server on localhost:3000, proxies /api to localhost:5000
npm run build   # production build (tsc + vite)
npm run lint    # ESLint
```

### Tests
```bash
dotnet test                                        # all tests
dotnet test --filter "FullyQualifiedName~BookingDetailTests"  # single test class
dotnet test --filter "FullyQualifiedName~GetById_ReturnsBooking"       # single test
```

### Chrome Extension
No build step. Load unpacked from `bookings-extension/` in `chrome://extensions`.

### Docker
```bash
docker build -t bookings-helper .    # multi-stage: frontend → backend → runtime
```

## Architecture

Three deployable components sharing one repo:

1. **BookingsAssistant.Api** — ASP.NET Core Web API with SQLite (EF Core). Serves the React frontend in production.
2. **BookingsAssistant.Web** — React SPA. In dev, Vite proxies API calls to :5000.
3. **bookings-extension/** — Chrome Manifest V3 extension (vanilla JS, no bundler). A single content script (`content-osm.js`) runs on OSM booking pages and annotates dates in the page with weekday/school-holiday badges — purely client-side, no background worker, no messaging, no backend calls.

Deployed as a **Home Assistant addon** via Docker (`bookings-assistant/config.yaml`).

### Backend structure
- `Controllers/` — Thin REST controllers: Auth, Bookings, Comments
- `Services/` — Business logic: `OsmService` (OSM API client), `OsmAuthService` (OAuth token management)
- `Data/ApplicationDbContext.cs` — EF Core context. Entities: `OsmBooking`, `OsmComment`, `ApplicationUser`
- `Models/` — DTOs for API request/response
- `Program.cs` — DI registration, CORS policies, startup sync

### Extension behavior
`content-osm.js` runs standalone on `onlinescoutmanager.co.uk` pages: it scans the DOM for dates, annotates each with a weekday/school-holiday badge, and re-scans on DOM mutations (for OSM's SPA navigation). No messaging, no background worker, no network calls.

## Key Patterns

**Testing:** Integration tests use `WebApplicationFactory<Program>` with in-memory EF Core. Each test gets a unique DB via `Guid.NewGuid()` passed to `UseInMemoryDatabase`. Replace `IOsmService` with fakes using `services.RemoveAll<IOsmService>()` (required because it's registered via `AddHttpClient`).

**OSM sync:** `POST /api/bookings/sync` fetches all 5 booking statuses (provisional, current, future, past, cancelled) in parallel and upserts by `OsmBookingId`.

**CORS:** One policy — `Development` (localhost:3000 for React dev server).

## Custom Commands

| Command | Purpose | When to use |
|---------|---------|-------------|
| `/pm` | Product manager — discovers opportunities, generates backlog, creates GitHub issues | Planning what to build next |
| `/review` | Pre-commit reviewer — checks data protection, code quality, and testing patterns | Before every commit |
| `/scaffold` | Feature scaffolding — generates skeleton code following project conventions | Starting a new feature |
| `/privacy` | PII audit — scans for data protection issues and traces data flows | Periodic audit, or when adding new data storage |

## PII Field Inventory

| Entity | Field | Storage | Purpose |
|--------|-------|---------|---------|
| `OsmBooking` | `CustomerName` | plaintext | Display |
| `OsmComment` | `AuthorName` | plaintext | Display |
| `OsmComment` | `TextPreview` | plaintext (truncated) | Display |
| `ApplicationUser` | `Name` | plaintext | Display |
| `ApplicationUser` | `OsmUsername` | plaintext | OSM identity |
| `ApplicationUser` | `OsmAccessToken` | encrypted (DataProtection) | OAuth |
| `ApplicationUser` | `OsmRefreshToken` | encrypted (DataProtection) | OAuth |

Raw email addresses are NEVER stored. `SenderEmail` column was intentionally removed (migration `20260223085029`). Email capture/linking features (and the hash columns/tables they used) were removed entirely (migration `RemoveEmailFeatures`).

## Service Lifetimes

| Service | Registration | Reason |
|---------|-------------|--------|
| `ApplicationDbContext` | `AddDbContext` (Scoped) | EF Core default, one context per request |
| `IOsmService` / `OsmService` | `AddHttpClient` | Needs HttpClientFactory |
| `IOsmAuthService` / `OsmAuthService` | `AddHttpClient` | Needs HttpClientFactory |
| `GateCodeService` | `AddHostedService` | Background worker |

## Configuration

Backend config in `appsettings.json` with environment overrides (`appsettings.Development.json`, `appsettings.Local.json` — both gitignored). Key sections: `ConnectionStrings:DefaultConnection`, `Osm:BaseUrl/ClientId/ClientSecret/CampsiteId/SectionId`. In Docker, `entrypoint.sh` reads HA addon options from `/data/options.json`.

## Windows Dev Notes

When `dotnet build` fails with a file lock, kill the backend process with:
```bash
powershell -Command "Stop-Process -Id <PID> -Force"
```
Bash `kill` and `taskkill` don't work reliably in Git Bash on Windows.
