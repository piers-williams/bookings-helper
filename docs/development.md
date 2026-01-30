# Bookings Assistant - Development Guide

This guide covers setting up a local development environment and working with the codebase.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development Setup](#local-development-setup)
3. [Running the Backend](#running-the-backend)
4. [Running the Frontend](#running-the-frontend)
5. [Database Migrations](#database-migrations)
6. [Testing Endpoints](#testing-endpoints)
7. [Project Structure](#project-structure)
8. [Development Workflow](#development-workflow)
9. [Common Tasks](#common-tasks)
10. [Debugging](#debugging)

---

## Prerequisites

Install the following tools before starting:

### Required Tools

**Backend:**
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (version 8.0 or later)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)
- [SQLite Browser](https://sqlitebrowser.org/) (optional, for viewing database)

**Frontend:**
- [Node.js](https://nodejs.org/) (version 20.x LTS recommended)
- [npm](https://www.npmjs.com/) (comes with Node.js)

**Tools:**
- [Git](https://git-scm.com/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (optional, for testing container builds)
- [Postman](https://www.postman.com/) or [curl](https://curl.se/) (for API testing)

### Verify Installation

```bash
# Check .NET version
dotnet --version
# Should show: 8.0.x or higher

# Check Node.js version
node --version
# Should show: v20.x.x or higher

# Check npm version
npm --version
# Should show: 10.x.x or higher

# Check Git version
git --version
```

---

## Local Development Setup

### Step 1: Clone Repository

```bash
git clone https://github.com/yourusername/bookings-assistant.git
cd bookings-assistant
```

### Step 2: Install Backend Dependencies

```bash
# Navigate to API project
cd BookingsAssistant.Api

# Restore NuGet packages
dotnet restore

# Verify build
dotnet build
```

You should see:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Step 3: Install Frontend Dependencies

```bash
# Navigate to Web project
cd ../BookingsAssistant.Web

# Install npm packages
npm install
```

This installs React, TypeScript, Vite, and all dependencies. Takes ~1-2 minutes.

### Step 4: Configure Development Settings

**Backend Configuration:**

Create `BookingsAssistant.Api/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "System": "Information",
      "Microsoft": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=bookings-dev.db"
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "common",
    "ClientId": "your-dev-client-id",
    "ClientSecret": "your-dev-client-secret",
    "CallbackPath": "/signin-oidc"
  },
  "Osm": {
    "BaseUrl": "https://www.onlinescoutmanager.co.uk",
    "ApiToken": "your-dev-osm-token"
  },
  "AllowedHosts": "*",
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000"]
  }
}
```

**Important:** Replace placeholder values with your actual development credentials (see `docs/deployment.md` for setup).

**Frontend Configuration:**

The frontend uses Vite's proxy configuration to route API calls. Check `BookingsAssistant.Web/vite.config.ts`:

```typescript
export default defineConfig({
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      }
    }
  }
})
```

This routes all `/api/*` requests to the backend running on port 5000.

### Step 5: Initialize Database

```bash
# Navigate to API project
cd BookingsAssistant.Api

# Apply migrations to create database
dotnet ef database update

# Verify database created
ls -la bookings-dev.db
```

You should see `bookings-dev.db` file created with initial schema.

**Alternative:** If `dotnet ef` command not found, install EF Core tools:

```bash
dotnet tool install --global dotnet-ef
```

---

## Running the Backend

### Quick Start

```bash
cd BookingsAssistant.Api
dotnet run
```

You should see:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

The API is now running at `http://localhost:5000`.

### Watch Mode (Hot Reload)

For automatic recompilation on code changes:

```bash
dotnet watch run
```

Now when you edit `.cs` files, the application automatically rebuilds and restarts.

### Launch with VS Code

Create `.vscode/launch.json`:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET Core Launch (web)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/BookingsAssistant.Api/bin/Debug/net8.0/BookingsAssistant.Api.dll",
      "args": [],
      "cwd": "${workspaceFolder}/BookingsAssistant.Api",
      "stopAtEntry": false,
      "serverReadyAction": {
        "action": "openExternally",
        "pattern": "\\bNow listening on:\\s+(https?://\\S+)"
      },
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  ]
}
```

Press **F5** to launch with debugger attached.

### Testing Backend Endpoints

```bash
# Health check
curl http://localhost:5000/api/health

# Get unread emails (returns mock data)
curl http://localhost:5000/api/emails

# Get bookings
curl http://localhost:5000/api/bookings

# Get comments
curl http://localhost:5000/api/comments
```

---

## Running the Frontend

### Quick Start

```bash
cd BookingsAssistant.Web
npm run dev
```

You should see:
```
  VITE v5.x.x  ready in xxx ms

  ➜  Local:   http://localhost:3000/
  ➜  Network: use --host to expose
```

The React app is now running at `http://localhost:3000`.

### Development Features

**Hot Module Replacement (HMR):**
- Edit any `.tsx` or `.ts` file
- Browser automatically updates without full page reload
- State is preserved when possible

**TypeScript Type Checking:**
- Vite performs type checking during build
- For continuous type checking in terminal:
  ```bash
  npm run type-check
  ```

**Linting (if configured):**
```bash
npm run lint
```

### Frontend Development Workflow

1. **Start Backend:** `cd BookingsAssistant.Api && dotnet run`
2. **Start Frontend:** `cd BookingsAssistant.Web && npm run dev`
3. **Open Browser:** Navigate to `http://localhost:3000`
4. **Edit Code:** Make changes to React components
5. **See Changes:** Browser updates automatically

### Building for Production

```bash
cd BookingsAssistant.Web
npm run build
```

This creates optimized production build in `dist/` directory.

**Preview Production Build:**
```bash
npm run preview
```

Serves the production build locally for testing.

---

## Database Migrations

The application uses Entity Framework Core Code-First migrations.

### Create New Migration

After modifying entity classes in `BookingsAssistant.Api/Data/Entities/`:

```bash
cd BookingsAssistant.Api

# Create migration
dotnet ef migrations add YourMigrationName

# Examples:
dotnet ef migrations add AddBookingStatusField
dotnet ef migrations add CreateCommentsTable
```

This generates migration files in `Migrations/` directory.

### Apply Migrations

```bash
# Apply pending migrations to database
dotnet ef database update

# Apply specific migration
dotnet ef database update MigrationName

# Roll back to previous migration
dotnet ef database update PreviousMigrationName
```

### View Migration History

```bash
# List all migrations
dotnet ef migrations list

# View SQL that would be executed
dotnet ef migrations script
```

### Remove Last Migration

If you haven't applied the migration yet:

```bash
dotnet ef migrations remove
```

**Warning:** Cannot remove migrations that have been applied to database.

### Common Migration Patterns

**Add New Property:**

1. Edit entity class:
   ```csharp
   public class EmailMessage
   {
       // ...existing properties
       public string NewProperty { get; set; }  // Add this
   }
   ```

2. Create migration:
   ```bash
   dotnet ef migrations add AddNewPropertyToEmail
   ```

3. Apply migration:
   ```bash
   dotnet ef database update
   ```

**Create New Table:**

1. Create entity class in `Data/Entities/`
2. Add `DbSet<>` to `ApplicationDbContext.cs`
3. Create and apply migration

---

## Testing Endpoints

### Using curl

**Get Unread Emails:**
```bash
curl -X GET http://localhost:5000/api/emails \
  -H "Content-Type: application/json"
```

**Get Email Details:**
```bash
curl -X GET http://localhost:5000/api/emails/1 \
  -H "Content-Type: application/json"
```

**Get Bookings:**
```bash
curl -X GET http://localhost:5000/api/bookings \
  -H "Content-Type: application/json"
```

**Get Booking Details:**
```bash
curl -X GET http://localhost:5000/api/bookings/1 \
  -H "Content-Type: application/json"
```

**Create Link:**
```bash
curl -X POST http://localhost:5000/api/links \
  -H "Content-Type: application/json" \
  -d '{"emailId": 1, "bookingId": 1}'
```

**Get Links for Email:**
```bash
curl -X GET http://localhost:5000/api/emails/1/links \
  -H "Content-Type: application/json"
```

**Trigger Sync:**
```bash
curl -X POST http://localhost:5000/api/sync \
  -H "Content-Type: application/json"
```

### Using Postman

1. **Import Collection:** Create new collection "Bookings Assistant API"
2. **Set Base URL:** Variable `base_url` = `http://localhost:5000`
3. **Create Requests:** Add requests for each endpoint above
4. **Test:** Run requests and verify responses

### Expected Responses

**GET /api/emails** (200 OK):
```json
[
  {
    "id": 1,
    "subject": "Booking Inquiry for March",
    "senderName": "John Smith",
    "senderEmail": "john@example.com",
    "receivedDate": "2026-01-29T10:30:00Z",
    "isRead": false,
    "extractedBookingRef": "12345",
    "hasLinkedBooking": true
  }
]
```

**GET /api/bookings** (200 OK):
```json
[
  {
    "id": 1,
    "osmBookingId": "12345",
    "customerName": "Jane Doe",
    "startDate": "2026-03-15T00:00:00Z",
    "endDate": "2026-03-17T00:00:00Z",
    "status": "Provisional",
    "commentCount": 2
  }
]
```

---

## Project Structure

```
BookingsAssistant/
├── BookingsAssistant.Api/              # .NET 8 Web API Backend
│   ├── Controllers/                    # API Controllers
│   │   ├── AuthController.cs           # OAuth authentication endpoints
│   │   ├── BookingsController.cs       # Booking CRUD endpoints
│   │   ├── CommentsController.cs       # Comment endpoints
│   │   ├── EmailsController.cs         # Email endpoints
│   │   ├── LinksController.cs          # Email-Booking link endpoints
│   │   └── SyncController.cs           # Data synchronization trigger
│   ├── Data/                           # Database layer
│   │   ├── ApplicationDbContext.cs     # EF Core DbContext
│   │   └── Entities/                   # Entity models
│   │       ├── ApplicationLink.cs      # Email↔Booking links
│   │       ├── ApplicationUser.cs      # User accounts
│   │       ├── EmailMessage.cs         # Cached email metadata
│   │       ├── OsmBooking.cs           # Cached booking data
│   │       └── OsmComment.cs           # Cached comment data
│   ├── Migrations/                     # EF Core migrations
│   │   ├── 20260129143710_InitialCreate.cs
│   │   └── ApplicationDbContextModelSnapshot.cs
│   ├── Models/                         # DTO models
│   │   ├── BookingDetailDto.cs         # Full booking response
│   │   ├── BookingDto.cs               # Booking list item
│   │   ├── CommentDto.cs               # Comment response
│   │   ├── CreateLinkRequest.cs        # Link creation request
│   │   ├── EmailDetailDto.cs           # Full email response
│   │   ├── EmailDto.cs                 # Email list item
│   │   └── LinkDto.cs                  # Link response
│   ├── Services/                       # Business logic services
│   │   ├── ILinkingService.cs          # Smart linking interface
│   │   ├── LinkingService.cs           # Smart linking implementation
│   │   ├── IOffice365Service.cs        # Email service interface
│   │   ├── Office365Service.cs         # Office 365 integration (stub)
│   │   ├── IOsmService.cs              # OSM service interface
│   │   ├── OsmService.cs               # OSM API integration
│   │   ├── ITokenService.cs            # Token encryption interface
│   │   └── TokenService.cs             # OAuth token management
│   ├── Program.cs                      # App entry point, DI config
│   ├── appsettings.json                # Default configuration
│   ├── appsettings.Development.json    # Dev overrides
│   └── BookingsAssistant.Api.csproj    # .NET project file
│
├── BookingsAssistant.Web/              # React + TypeScript Frontend
│   ├── src/
│   │   ├── components/                 # React components
│   │   │   ├── Dashboard.tsx           # Main dashboard page
│   │   │   ├── EmailCard.tsx           # Email list item
│   │   │   ├── EmailDetail.tsx         # Email detail page
│   │   │   ├── BookingCard.tsx         # Booking list item
│   │   │   ├── BookingDetail.tsx       # Booking detail page
│   │   │   ├── CommentCard.tsx         # Comment list item
│   │   │   └── LinkBookingModal.tsx    # Manual linking modal
│   │   ├── services/                   # API client
│   │   │   └── apiClient.ts            # Axios wrapper with typed API methods
│   │   ├── types/                      # TypeScript types
│   │   │   └── index.ts                # API response types (mirrors DTOs)
│   │   ├── App.tsx                     # Root component with routing
│   │   ├── main.tsx                    # React entry point
│   │   └── index.css                   # Global styles
│   ├── public/                         # Static assets
│   ├── index.html                      # HTML template
│   ├── package.json                    # npm dependencies
│   ├── tsconfig.json                   # TypeScript configuration
│   ├── vite.config.ts                  # Vite build configuration
│   └── README.md                       # Frontend-specific docs
│
├── docs/                               # Documentation
│   ├── deployment.md                   # This file
│   ├── development.md                  # Development guide
│   ├── osm-api-discovery.md            # OSM API reverse engineering notes
│   └── plans/                          # Design documents
│       ├── 2026-01-29-bookings-assistant-design.md
│       └── 2026-01-29-phase1-implementation.md
│
├── Dockerfile                          # Production Docker image
├── .dockerignore                       # Docker build exclusions
├── config.yaml                         # Home Assistant addon config
├── run.sh                              # Home Assistant addon entry point
├── ADDON_README.md                     # HA addon store description
├── .gitignore                          # Git exclusions
├── BookingsAssistant.sln               # Visual Studio solution
└── README.md                           # Project overview
```

### Key Architecture Decisions

**Separation of Concerns:**
- **Controllers** handle HTTP concerns (validation, status codes)
- **Services** contain business logic (integration, algorithms)
- **Repositories/DbContext** handle data access
- **DTOs** define API contract (separate from entities)

**Data Flow:**
```
Frontend (React)
  → API Client (Axios)
    → Controller (ASP.NET)
      → Service (Business Logic)
        → DbContext (EF Core)
          → SQLite Database

External APIs:
  → Office365Service → Microsoft Graph API
  → OsmService → OSM API
```

**Frontend Routing:**
- `/` - Dashboard
- `/emails/:id` - Email Detail
- `/bookings/:id` - Booking Detail

**API Routing:**
- `/api/emails` - Email endpoints
- `/api/bookings` - Booking endpoints
- `/api/comments` - Comment endpoints
- `/api/links` - Link management
- `/api/sync` - Data refresh trigger

---

## Development Workflow

### Feature Development

1. **Create Feature Branch:**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Backend Changes:**
   - Modify or create entity classes if needed
   - Create/update service interfaces and implementations
   - Add/update controller endpoints
   - Create DTOs for new responses
   - Write migrations if schema changed

3. **Frontend Changes:**
   - Update types in `types/index.ts` to match DTOs
   - Add/update API client methods in `apiClient.ts`
   - Create/modify React components
   - Update routing if new pages added

4. **Test Locally:**
   - Run backend: `dotnet run`
   - Run frontend: `npm run dev`
   - Manually test features in browser
   - Test API endpoints with curl/Postman

5. **Commit Changes:**
   ```bash
   git add .
   git commit -m "feat: add your feature description"
   ```

6. **Push and Create PR:**
   ```bash
   git push origin feature/your-feature-name
   ```

### Bug Fix Workflow

1. **Create Bug Fix Branch:**
   ```bash
   git checkout -b fix/bug-description
   ```

2. **Reproduce Bug:**
   - Add test case or manual steps to reproduce
   - Identify root cause

3. **Fix Issue:**
   - Make minimal changes to fix bug
   - Verify fix resolves issue

4. **Test:**
   - Test original bug scenario
   - Test related functionality to ensure no regression

5. **Commit and Push:**
   ```bash
   git commit -m "fix: resolve bug description"
   git push origin fix/bug-description
   ```

### Code Review Guidelines

**Before Creating PR:**
- [ ] Code builds without warnings
- [ ] No obvious bugs or security issues
- [ ] Follows existing code style
- [ ] DTOs match between backend and frontend
- [ ] API endpoints tested manually

**Reviewers Check:**
- [ ] Business logic is sound
- [ ] Error handling is appropriate
- [ ] No SQL injection or XSS vulnerabilities
- [ ] Performance considerations
- [ ] Code is maintainable

---

## Common Tasks

### Adding a New Entity

1. **Create Entity Class:**
   ```csharp
   // BookingsAssistant.Api/Data/Entities/NewEntity.cs
   public class NewEntity
   {
       public int Id { get; set; }
       public string Name { get; set; }
       // ... properties
   }
   ```

2. **Add to DbContext:**
   ```csharp
   // ApplicationDbContext.cs
   public DbSet<NewEntity> NewEntities { get; set; }
   ```

3. **Create Migration:**
   ```bash
   dotnet ef migrations add AddNewEntity
   dotnet ef database update
   ```

### Adding a New API Endpoint

1. **Create DTO:**
   ```csharp
   // BookingsAssistant.Api/Models/NewDto.cs
   public class NewDto
   {
       public int Id { get; set; }
       public string Name { get; set; }
   }
   ```

2. **Add Controller Method:**
   ```csharp
   // Controllers/NewController.cs
   [HttpGet]
   public async Task<ActionResult<List<NewDto>>> GetAll()
   {
       // Implementation
   }
   ```

3. **Update Frontend Types:**
   ```typescript
   // BookingsAssistant.Web/src/types/index.ts
   export interface NewDto {
     id: number;
     name: string;
   }
   ```

4. **Add API Client Method:**
   ```typescript
   // services/apiClient.ts
   export const newApi = {
     getAll: () => apiClient.get<NewDto[]>('/api/new')
   };
   ```

### Adding a New React Component

1. **Create Component File:**
   ```typescript
   // src/components/NewComponent.tsx
   import React from 'react';

   interface NewComponentProps {
     data: string;
   }

   export default function NewComponent({ data }: NewComponentProps) {
     return <div>{data}</div>;
   }
   ```

2. **Import and Use:**
   ```typescript
   // Dashboard.tsx
   import NewComponent from './NewComponent';

   <NewComponent data="test" />
   ```

### Updating Dependencies

**Backend:**
```bash
cd BookingsAssistant.Api
dotnet list package --outdated
dotnet add package PackageName --version x.x.x
```

**Frontend:**
```bash
cd BookingsAssistant.Web
npm outdated
npm update
# Or update specific package:
npm install package-name@latest
```

---

## Debugging

### Backend Debugging

**Visual Studio:**
1. Open `BookingsAssistant.sln`
2. Set breakpoints in code
3. Press F5 to start debugging
4. Make API request to trigger breakpoint

**VS Code:**
1. Use launch configuration (see "Running the Backend" section)
2. Set breakpoints
3. Press F5
4. Trigger endpoint

**Console Logging:**
```csharp
// Inject ILogger
private readonly ILogger<YourController> _logger;

// Log messages
_logger.LogInformation("Processing request for {Id}", id);
_logger.LogWarning("No booking found for {Id}", id);
_logger.LogError(ex, "Error processing request");
```

**Inspect Database:**
```bash
# Install sqlite3 CLI tool, then:
sqlite3 bookings-dev.db
sqlite> .tables
sqlite> SELECT * FROM EmailMessages;
sqlite> .quit
```

### Frontend Debugging

**Browser DevTools:**
1. Open Chrome DevTools (F12)
2. **Console tab:** View console.log() output and errors
3. **Network tab:** Inspect API requests/responses
4. **Sources tab:** Set breakpoints in TypeScript files
5. **React DevTools:** Install extension to inspect component state

**React Component Debugging:**
```typescript
// Add console logs
console.log('Component rendered with data:', data);

// Inspect props
useEffect(() => {
  console.log('Props changed:', props);
}, [props]);
```

**API Request Debugging:**
```typescript
// Log API calls
apiClient.get('/api/emails')
  .then(response => {
    console.log('Response:', response.data);
  })
  .catch(error => {
    console.error('Error:', error.response);
  });
```

**TypeScript Errors:**
```bash
# Check TypeScript compilation
npm run type-check

# Fix type errors before committing
```

### Common Issues

**Port Already in Use:**
```bash
# Backend (port 5000)
# Windows:
netstat -ano | findstr :5000
taskkill /PID <PID> /F

# Linux/Mac:
lsof -ti:5000 | xargs kill -9
```

**Database Locked:**
- Close SQLite Browser if open
- Stop any other running instances of the API

**CORS Errors:**
- Verify `vite.config.ts` proxy configuration
- Check backend allows `http://localhost:3000` in CORS policy

---

## Next Steps

Now that you have development environment set up:

1. **Explore the Code:**
   - Read through controllers to understand API surface
   - Review React components to understand UI flow
   - Check service implementations for business logic

2. **Make Changes:**
   - Pick a feature from Phase 2 (see design document)
   - Or fix a known issue
   - Follow development workflow above

3. **Test Your Changes:**
   - Run both backend and frontend
   - Test in browser
   - Verify API responses

4. **Review Documentation:**
   - `docs/deployment.md` - Deployment guide
   - `docs/plans/2026-01-29-bookings-assistant-design.md` - Full design
   - `docs/plans/2026-01-29-phase1-implementation.md` - Implementation plan

---

**Document Version:** 1.0.0
**Last Updated:** 2026-01-30
**Phase:** 1 MVP
