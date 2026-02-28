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
dotnet test --filter "FullyQualifiedName~EmailCaptureTests"  # single test class
dotnet test --filter "FullyQualifiedName~CaptureEmail_WithBookingRef"  # single test
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
3. **bookings-extension/** — Chrome Manifest V3 extension (vanilla JS, no bundler). Content scripts for OWA and OSM, native side panel, background service worker.

Deployed as a **Home Assistant addon** via Docker (`bookings-assistant/config.yaml`).

### Backend structure
- `Controllers/` — Thin REST controllers: Auth, Bookings, Emails, Comments, Links
- `Services/` — Business logic: `OsmService` (OSM API client), `OsmAuthService` (OAuth token management), `LinkingService` (email↔booking matching), `HashingService` (PBKDF2 for PII)
- `Data/ApplicationDbContext.cs` — EF Core context. Entities: `OsmBooking`, `EmailMessage`, `ApplicationLink`, `OsmComment`, `ApplicationUser`
- `Models/` — DTOs for API request/response
- `Program.cs` — DI registration, CORS policies, startup sync

### Extension message flow
OWA content script extracts email → `CAPTURE_EMAIL` → background.js → `POST /api/emails/capture` → response relayed to side panel. OSM content script extracts booking ID → `GET_BOOKING_LINKS` → background.js → `GET /api/bookings/{id}/links` → sidebar rendered.

## Key Patterns

**Testing:** Integration tests use `WebApplicationFactory<Program>` with in-memory EF Core. Each test gets a unique DB via `Guid.NewGuid()` passed to `UseInMemoryDatabase`. Replace `IOsmService` with fakes using `services.RemoveAll<IOsmService>()` (required because it's registered via `AddHttpClient`).

**Email→booking linking:** `LinkingService` extracts booking refs from email text via regex (`(?:#|Ref:|REF:|Reference|Booking\s*#|OSM\s*#)\s*(\d{4,6})`). Falls back to hash-based matching (sender email hash or candidate name hash against customer records).

**Privacy:** Raw emails and customer names are never stored. `HashingService` produces PBKDF2-SHA256 hashes with a secret from `/data/hash-secret.txt`. Only hashes are persisted for matching.

**OSM sync:** `POST /api/bookings/sync` fetches all 5 booking statuses (provisional, current, future, past, cancelled) in parallel and upserts by `OsmBookingId`.

**OWA DOM selectors:** Subject: `[id$="_SUBJECT"] span`, Sender: `[id$="_FROM"] > span > div > span` (format: "Name<email>"), Body: `#focused > div:nth-child(3)`. These target `outlook.cloud.microsoft`.

**CORS:** Two policies — `Development` (localhost:3000 for React dev server) and `ExtensionCapture` (AllowAnyOrigin, used on `/capture` and `/links` endpoints for the Chrome extension).

## Configuration

Backend config in `appsettings.json` with environment overrides (`appsettings.Development.json`, `appsettings.Local.json` — both gitignored). Key sections: `ConnectionStrings:DefaultConnection`, `Osm:BaseUrl/ClientId/ClientSecret/CampsiteId/SectionId`. In Docker, `entrypoint.sh` reads HA addon options from `/data/options.json`.

## Windows Dev Notes

When `dotnet build` fails with a file lock, kill the backend process with:
```bash
powershell -Command "Stop-Process -Id <PID> -Force"
```
Bash `kill` and `taskkill` don't work reliably in Git Bash on Windows.
