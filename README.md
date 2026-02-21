# Bookings Assistant

A web application for managing scout campsite bookings by integrating OSM (Online Scout Manager) and Office 365 email. The application provides a unified dashboard that aggregates items requiring attention from both systems, smart linking between emails and bookings, and efficient booking management workflow.

**Status:** Phase 1 MVP Complete | Version 0.1.0

---

## Features (Phase 1 MVP)

### Unified Dashboard
- **Three-section layout** showing all items needing attention:
  - Unread emails from shared bookings inbox
  - Provisional bookings awaiting action
  - New comments on bookings
- **Manual refresh** button to sync latest data
- **Quick navigation** to detailed views

### Smart Email-to-Booking Linking
- **Automatic detection** of booking references in email subjects and bodies
- **Manual linking** through search interface for ambiguous cases
- **Bidirectional navigation** between linked emails and bookings

### Detailed Context Views
- **Email details** with full content, sender info, and linked bookings
- **Booking details** with customer info, dates, status, and all comments
- **Comment timeline** showing conversation history

### External Integration Links
- **Open in Outlook Web** button for handling complex email actions
- **Open in OSM** button for making booking changes in the source system

### Data Caching
- **Local SQLite database** caches metadata for fast access
- **Reduces API calls** and provides offline-ready data
- **Full details fetched on-demand** when viewing individual items

---

## Architecture

### Technology Stack

**Backend:**
- .NET 8 (ASP.NET Core Web API)
- Entity Framework Core 8
- SQLite database
- Microsoft.Graph SDK (for Office 365 integration)
- Custom HttpClient service (for OSM API)

**Frontend:**
- React 18 with TypeScript
- Vite (development server and build tool)
- React Router (client-side routing)
- Axios (HTTP client)
- CSS for styling

**Deployment:**
- Docker container (multi-stage build)
- Home Assistant addon (primary deployment target)
- Standalone Docker deployment supported

### System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    React Frontend (SPA)                     │
│  Dashboard │ Email Detail │ Booking Detail │ Link Modal     │
└────────────┬────────────────────────────────────────────────┘
             │ HTTP/JSON API
┌────────────┴────────────────────────────────────────────────┐
│              ASP.NET Core Web API Backend                   │
│  Controllers │ Services │ EF Core │ Token Management        │
└────┬──────────────────────────────────────────────────┬─────┘
     │                                                   │
     │ Microsoft Graph API              OSM API         │
     │ (OAuth 2.0)                      (Reverse Eng.)  │
     ▼                                                   ▼
┌────────────────────┐                        ┌─────────────────┐
│  Office 365        │                        │  Online Scout   │
│  Shared Mailbox    │                        │  Manager        │
└────────────────────┘                        └─────────────────┘

                   ┌──────────────────┐
                   │  SQLite Database │
                   │  (Local Cache)   │
                   └──────────────────┘
```

### Data Model

Three domain-separated table groups:

**Application Domain:**
- `ApplicationUsers` - User accounts and OAuth tokens
- `ApplicationLinks` - Email-to-booking associations

**Email Domain:**
- `EmailMessages` - Cached email metadata from Office 365

**OSM Domain:**
- `OsmBookings` - Cached booking data from OSM
- `OsmComments` - Cached comment data from OSM

See `docs/plans/2026-01-29-bookings-assistant-design.md` for detailed schema.

---

## Quick Start

### For End Users (Home Assistant)

1. **Install addon** in Home Assistant
2. **Configure** Azure AD and OSM credentials
3. **Start addon** and access via Web UI
4. **Sign in** with Microsoft OAuth
5. **Click Refresh** to load data

See [docs/deployment.md](docs/deployment.md) for detailed installation instructions.

### For Developers

1. **Clone repository:**
   ```bash
   git clone https://github.com/yourusername/bookings-assistant.git
   cd bookings-assistant
   ```

2. **Run backend:**
   ```bash
   cd BookingsAssistant.Api
   dotnet restore
   dotnet ef database update
   dotnet run
   ```

3. **Run frontend** (in new terminal):
   ```bash
   cd BookingsAssistant.Web
   npm install
   npm run dev
   ```

4. **Open browser:** http://localhost:3000

See [docs/development.md](docs/development.md) for detailed development setup.

---

## Documentation

- **[Deployment Guide](docs/deployment.md)** - Azure AD setup, Home Assistant addon installation, Docker deployment
- **[Development Guide](docs/development.md)** - Local setup, project structure, development workflow
- **[Design Document](docs/plans/2026-01-29-bookings-assistant-design.md)** - Architecture, data model, evolution path
- **[Implementation Plan](docs/plans/2026-01-29-phase1-implementation.md)** - Phase 1 task breakdown
- **[OSM API Discovery](docs/osm-api-discovery.md)** - Reverse-engineered OSM API endpoints

---

## Screenshots

*(Placeholder - screenshots to be added)*

**Dashboard:**
- Three-section layout with emails, bookings, and comments

**Email Detail:**
- Full email content with smart links to bookings

**Booking Detail:**
- Customer info, dates, status, and comment timeline

---

## Project Status

### What Works in Phase 1

- Backend API infrastructure with all controllers
- Frontend React application with routing
- Database schema and migrations
- Mock data for development and testing
- Smart linking service (auto-detect booking refs)
- OSM API service structure with discovered endpoints
- Docker build configuration
- Home Assistant addon packaging

### What's Stubbed (Requires Configuration)

**OAuth Integration:**
- Office365Service returns mock data
- Real implementation requires Azure AD app registration
- TokenService infrastructure is ready for OAuth flow

**OSM API Integration:**
- OsmService has endpoint structure
- Authentication method needs finalization (session vs API token)
- Real API calls require valid OSM credentials

**End-to-End Flow:**
- Manual testing pending OAuth and OSM setup
- Can test with mock data locally

### Known Limitations

- **No background sync** - requires manual refresh button click
- **No email actions** - can only view, must use Outlook for replies
- **No booking modifications** - must use OSM for status changes
- **Single user** - multi-user support planned for Phase 3
- **No mobile optimization** - responsive design but not mobile-first

---

## Roadmap

### Phase 2: Basic Actions (Next Steps)

- Implement Office 365 OAuth flow end-to-end
- Finalize OSM API authentication
- Add "Mark as Read" for emails
- Add "Add Comment" to OSM bookings
- Quick reply templates for common emails
- Local "reviewed" flag for bookings

### Phase 3: Full Workflow

- Confirm/cancel bookings via API
- Move booking dates (complex OSM API sequence)
- Send emails with templates and variable substitution
- Deposit tracking with reminders
- Multi-user support with permissions
- Background sync option
- Workflow automation

See design document for full evolution path.

---

## Contributing

This is currently a private project for Thorrington Scout Campsite management. If you're interested in adapting this for your own campsite or organization:

1. **Fork the repository**
2. **Adapt for your use case:**
   - Replace OSM integration with your booking system
   - Customize email detection patterns
   - Adjust UI for your workflow
3. **Share improvements** via pull request if generally applicable

### Development Guidelines

- Follow existing code style (C# conventions for backend, React best practices for frontend)
- Write descriptive commit messages
- Test changes locally before committing
- Update documentation for new features
- Keep DTOs synchronized between backend and frontend

---

## Technology Highlights

### Why ASP.NET Core?
- **Cross-platform** - runs on Linux (Docker/Home Assistant)
- **High performance** - efficient async/await model
- **Strong typing** - C# type safety reduces bugs
- **EF Core** - simple database migrations and LINQ queries
- **Built-in DI** - clean service architecture

### Why React + TypeScript?
- **Type safety** - catch errors at compile time
- **Component reusability** - modular UI architecture
- **Fast development** - hot module replacement with Vite
- **Modern** - latest React features (hooks, suspense)

### Why SQLite?
- **Embedded** - no separate database server required
- **Simple deployment** - single file database
- **Sufficient for use case** - single-user, moderate data volume
- **Easy backup** - copy one file
- **Upgradeable** - can migrate to PostgreSQL later if needed

### Why Home Assistant Addon?
- **Already running** - existing home automation infrastructure
- **Easy access** - integrated in HA UI
- **Persistent storage** - automatic data backup with HA
- **Network access** - configured internet access for APIs
- **Free hosting** - no additional cloud costs

---

## License

*(To be determined - currently private/internal use)*

---

## Contact

For questions or issues:
- Open GitHub issue
- Contact repository maintainer

---

## Acknowledgments

**Built with:**
- ASP.NET Core team for excellent web framework
- React and TypeScript teams for modern frontend development
- Entity Framework Core for database abstraction
- Home Assistant community for addon platform

**Designed for:**
- Thorrington Scout Campsite booking management team
- Reducing manual email and booking management overhead

---

**Project Status:** Phase 1 MVP Complete - Ready for OAuth/API Configuration
**Last Updated:** 2026-01-30
**Version:** 0.1.0
