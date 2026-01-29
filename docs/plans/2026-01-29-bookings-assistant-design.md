# Bookings Assistant - Design Document

**Date:** 2026-01-29
**Version:** 0.1.0
**Status:** Approved for Implementation

## Overview

A web application for managing scout campsite bookings by integrating OSM (Online Scout Manager) and Office 365 email. The application aggregates items requiring attention from both systems, provides context through smart linking, and enables efficient booking management workflow.

## Goals

- **Unified Dashboard**: Single view of all items needing attention (unread emails, provisional bookings, new OSM comments)
- **Context Aggregation**: Link emails to bookings automatically or manually to provide full context
- **Reduce Context Switching**: View all relevant information without jumping between OSM and Outlook
- **Progressive Enhancement**: Start with viewing/context, evolve to actions, then full workflow automation

## Non-Goals (MVP)

- Background/automated email monitoring (manual refresh only)
- Advanced workflow automation (Phase 3)
- Mobile app (web-first, responsive design)
- Multi-user management UI (single user, designed for future multi-user)

## Architecture

### Application Type

ASP.NET Core Web API + React TypeScript SPA, deployed as a Home Assistant addon in Docker container.

### Technology Stack

**Backend:**
- .NET 8 (ASP.NET Core Web API)
- Entity Framework Core
- SQLite (development/production, upgradeable to PostgreSQL)
- Microsoft.Graph SDK for Office 365 integration
- Custom HttpClient for OSM API integration

**Frontend:**
- React 18 + TypeScript
- Vite (dev server and build tool)
- React Router (navigation)
- Axios (API client)
- Tailwind CSS or Material-UI (styling)

**Deployment:**
- Docker container (amd64 only)
- Home Assistant addon
- Single container serving both API and React build

### Data Flow

1. User clicks "Refresh" button on dashboard
2. Backend queries Microsoft Graph API for unread emails
3. Backend queries OSM API for provisional bookings and recent comments
4. Metadata cached in SQLite with timestamps
5. Dashboard renders three sections from cached data
6. User clicks item → frontend fetches full details from backend → backend fetches from API if needed

## Data Model

### Domain Separation

Three distinct domains represented in SQLite with table prefixes:
- **Application**: Users of this application and app-created data
- **Email**: Emails from shared Office 365 inbox
- **OSM**: Bookings and comments from OSM system

### Database Schema

#### ApplicationUsers
| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER PK | Primary key |
| Name | TEXT | User's display name (e.g., "Piers", "Tammy") |
| Office365Email | TEXT | O365 email for accessing shared bookings inbox |
| Office365AccessToken | TEXT | Encrypted OAuth access token |
| Office365RefreshToken | TEXT | Encrypted OAuth refresh token |
| Office365TokenExpiry | DATETIME | When O365 token expires |
| OsmUsername | TEXT | OSM login username |
| OsmApiToken | TEXT | Encrypted OSM API token |
| OsmTokenExpiry | DATETIME | When OSM token expires |
| LastSync | DATETIME | Last time data was synced |

#### EmailMessages
| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER PK | Primary key |
| MessageId | TEXT | Office 365 message ID (unique) |
| SenderEmail | TEXT | Email address of sender |
| SenderName | TEXT | Display name of sender |
| Subject | TEXT | Email subject |
| ReceivedDate | DATETIME | When email was received |
| IsRead | BOOLEAN | Read status (tracked separately from O365) |
| ExtractedBookingRef | TEXT | Auto-extracted booking reference (nullable) |
| LastFetched | DATETIME | When full content was last fetched |

#### OsmBookings
| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER PK | Primary key |
| OsmBookingId | TEXT | Booking ID in OSM (unique) |
| CustomerName | TEXT | Name of customer who made booking |
| CustomerEmail | TEXT | Customer's email address |
| StartDate | DATETIME | Booking start date |
| EndDate | DATETIME | Booking end date |
| Status | TEXT | Status (Provisional, Confirmed, Cancelled) |
| LastFetched | DATETIME | When full details were last fetched |

#### OsmComments
| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER PK | Primary key |
| OsmBookingId | TEXT | Foreign key to OsmBookings |
| OsmCommentId | TEXT | Comment ID in OSM (unique) |
| AuthorName | TEXT | Name of person who wrote comment |
| TextPreview | TEXT | First 200 chars of comment |
| CreatedDate | DATETIME | When comment was created |
| IsNew | BOOLEAN | Flag for highlighting new comments |
| LastFetched | DATETIME | When full comment was last fetched |

#### ApplicationLinks
| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER PK | Primary key |
| EmailMessageId | INTEGER | FK to EmailMessages |
| OsmBookingId | INTEGER | FK to OsmBookings |
| CreatedByUserId | INTEGER | FK to ApplicationUsers (nullable) |
| CreatedDate | DATETIME | When link was created |

**Link Types:**
- `CreatedByUserId = NULL`: Auto-linked (booking ref found in email)
- `CreatedByUserId = <value>`: Manually linked by that user

### What's NOT Cached

Full details fetched on-demand to reduce storage and keep data fresh:
- Complete email body content
- Full booking details (items, pricing, deposits)
- Complete comment text (only preview cached)

## API Integration

### Microsoft Graph API (Office 365)

**Setup:**
- App registered in personal Azure AD tenant
- Multi-tenant support: "Accounts in any organizational directory and personal Microsoft accounts"
- Delegated permissions: `Mail.Read`, `Mail.ReadWrite`, `User.Read`
- Microsoft.Graph SDK NuGet package

**Service: Office365Service**

Methods:
- `GetUnreadMessagesAsync()` - List unread emails from bookings inbox
- `GetMessageDetailsAsync(messageId)` - Full email content
- `MarkAsReadAsync(messageId)` - Phase 2

**Authentication:**
- User signs in with bookings inbox credentials (e.g., bookings@thorringtonscoutcamp.co.uk)
- Interactive OAuth2 flow with 2FA support
- Refresh token stored encrypted in ApplicationUsers table
- Automatic token refresh when expired

### OSM API (Reverse Engineered)

**Discovery Process:**
1. Open OSM web UI in browser
2. Open DevTools Network tab
3. Perform actions (view bookings, read comments, etc.)
4. Document API endpoints, headers, authentication
5. Test with Postman/curl
6. Implement in `OsmService`

**Service: OsmService**

Expected methods (to be confirmed during implementation):
- `GetBookingsAsync(status)` - Fetch bookings by status (e.g., provisional)
- `GetBookingDetailsAsync(bookingId)` - Full booking information
- `GetCommentsAsync(bookingId)` - Comments for a booking
- `GetRecentCommentsAsync()` - All recent comments (new since last check)
- `AddCommentAsync(bookingId, text)` - Phase 2

**Implementation Notes:**
- Custom HttpClient with retry logic
- Rate limiting handling
- Store OSM credentials per-user in ApplicationUsers table
- Error handling for undocumented API quirks

## Authentication Flow

### Azure AD Setup (One-Time)

1. Create free Azure account with personal Microsoft email
2. Azure Portal → App Registrations → New Registration
3. Configure:
   - **Name:** "Bookings Assistant"
   - **Supported accounts:** "Accounts in any organizational directory and personal Microsoft accounts"
   - **Redirect URI:** `https://your-homeassistant:5001/signin-oidc`
4. Create client secret
5. API Permissions → Add delegated permissions: `Mail.Read`, `Mail.ReadWrite`, `User.Read`
6. Grant admin consent

### User Sign-In Flow

**First Time:**
1. User opens app, clicks "Sign in with Microsoft"
2. App redirects to Microsoft login page (Microsoft.Identity.Web)
3. User enters bookings inbox credentials + completes 2FA
4. Microsoft shows consent screen
5. User approves → redirected back with authorization code
6. App exchanges code for access token + refresh token
7. Tokens stored encrypted in ApplicationUsers table
8. User profile fetched and ApplicationUsers record created/updated

**Subsequent Visits:**
- App checks for valid refresh token
- Automatically refreshes access token if expired (no user interaction)
- User stays signed in until explicit sign-out or token revocation

### OSM Authentication

**To be determined during OSM API exploration:**
- API key-based authentication?
- Session token?
- OAuth?

Store per-user in ApplicationUsers table so actions are attributed correctly.

## User Interface

### Project Structure

```
BookingsAssistant/
├── BookingsAssistant.Api/              # .NET Web API
│   ├── Controllers/
│   │   ├── EmailsController.cs
│   │   ├── BookingsController.cs
│   │   ├── CommentsController.cs
│   │   └── LinksController.cs
│   ├── Services/
│   │   ├── Office365Service.cs
│   │   └── OsmService.cs
│   ├── Data/
│   │   ├── ApplicationDbContext.cs
│   │   └── Entities/
│   └── wwwroot/                        # Built React app
├── BookingsAssistant.Web/              # React + TypeScript
│   ├── src/
│   │   ├── components/
│   │   │   ├── Dashboard.tsx
│   │   │   ├── EmailDetail.tsx
│   │   │   ├── BookingDetail.tsx
│   │   │   └── CommentDetail.tsx
│   │   ├── services/
│   │   │   └── apiClient.ts
│   │   ├── types/
│   │   │   └── index.ts
│   │   ├── App.tsx
│   │   └── main.tsx
│   ├── package.json
│   └── vite.config.ts
└── BookingsAssistant.sln
```

### Dashboard Component

Three-section layout showing items needing attention:

```
┌──────────────────────────────────────────────┐
│  Bookings Assistant           [Refresh] [⚙️] │
├──────────────────────────────────────────────┤
│ 📧 Unread Emails (5)                         │
│ ┌──────────────────────────────────────────┐ │
│ │ From: john@scouts.org.uk                 │ │
│ │ Subject: Query about booking #12345      │ │
│ │ Received: 2 hours ago                    │ │
│ └──────────────────────────────────────────┘ │
│ [Show more...]                               │
├──────────────────────────────────────────────┤
│ 📋 Provisional Bookings (3)                  │
│ ┌──────────────────────────────────────────┐ │
│ │ #12345 - Jane Smith                      │ │
│ │ Mar 15-17, 2026 - Deposit due Feb 1      │ │
│ └──────────────────────────────────────────┘ │
├──────────────────────────────────────────────┤
│ 💬 New Comments (2)                          │
│ ┌──────────────────────────────────────────┐ │
│ │ Booking #12340 - Tammy: "Called customer │ │
│ │ to confirm arrival time..."              │ │
│ └──────────────────────────────────────────┘ │
└──────────────────────────────────────────────┘
```

**Features:**
- Click any item → navigate to detail page
- Refresh button triggers API sync
- Counts show number of items in each section
- Items sorted by date (newest first)

### Email Detail Page

**Layout:**
- Email metadata (from, to, date, subject)
- Full email body (HTML or plain text)
- **Smart Links Section:**
  - If booking reference detected: "🔗 Linked to Booking #12345 [View Booking]"
  - If multiple matches: Show all with confidence indicator
  - If no auto-link: "No booking found - [Search & Link Manually]" button
- **Related Emails:** Other emails from same sender
- **Action Buttons (Phase 1):**
  - "Open in Outlook Web" (external link)

**Manual Link Flow:**
1. Click "Search & Link Manually"
2. Modal opens with search box
3. Type booking number or customer name
4. Select booking from results
5. Link created → appears in Smart Links section

### Booking Detail Page

**Layout:**
- Booking header (number, status, dates)
- Customer information (name, email, contact details)
- Site/items booked
- Deposit status
- **Comments Timeline:** All comments in chronological order
- **Linked Emails Section:** Emails linked to this booking
- **Action Buttons (Phase 1):**
  - "Open in OSM" (external link)

### Comment Detail Page

**Layout:**
- Full comment text
- Author and date
- Parent booking summary
- "View Full Booking" link

### API Client (Frontend)

**apiClient.ts:**
```typescript
// Axios wrapper with base URL and auth
export const apiClient = axios.create({
  baseURL: '/api',
  withCredentials: true
});

// Typed API methods
export const emailsApi = {
  getUnread: () => apiClient.get<Email[]>('/emails'),
  getById: (id: number) => apiClient.get<EmailDetail>(`/emails/${id}`),
  // Phase 2: markAsRead: (id: number) => apiClient.patch(`/emails/${id}/read`)
};

export const bookingsApi = {
  getProvisional: () => apiClient.get<Booking[]>('/bookings?status=provisional'),
  getById: (id: number) => apiClient.get<BookingDetail>(`/bookings/${id}`),
};

export const commentsApi = {
  getNew: () => apiClient.get<Comment[]>('/comments?new=true'),
};

export const linksApi = {
  create: (emailId: number, bookingId: number) =>
    apiClient.post('/links', { emailId, bookingId }),
};
```

## Deployment

### Docker Configuration

**Dockerfile (Production):**
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
COPY BookingsAssistant.Api/*.csproj ./BookingsAssistant.Api/
RUN dotnet restore BookingsAssistant.Api/BookingsAssistant.Api.csproj
COPY BookingsAssistant.Api/ ./BookingsAssistant.Api/
RUN dotnet publish BookingsAssistant.Api/BookingsAssistant.Api.csproj -c Release -o out

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=backend-build /app/out .
COPY --from=frontend-build /app/frontend/dist ./wwwroot
EXPOSE 5000
ENTRYPOINT ["dotnet", "BookingsAssistant.Api.dll"]
```

### Home Assistant Addon

**config.yaml:**
```yaml
name: Bookings Assistant
version: 0.1.0
slug: bookings-assistant
description: Manage campsite bookings from OSM and Office 365
arch:
  - amd64
ports:
  5000/tcp: 8099
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
```

**Persistent Storage:**
- `/data/bookings.db` - SQLite database
- `/data/keys/` - Data protection keys for token encryption
- Configuration via Home Assistant addon options UI

### Development vs Production

**Development:**
- React dev server on `http://localhost:3000` (hot reload)
- .NET API on `http://localhost:5000`
- React proxy configuration routes `/api/*` to `:5000`
- CORS enabled for local development

**Production:**
- Built React app copied to `wwwroot/`
- Single .NET app serves both API and static files
- Port 5000 exposed to Home Assistant
- Access via Home Assistant ingress or direct port

## Evolution Path

### Phase 1: View & Context (MVP)

**Goal:** Prove the concept - aggregate data and provide context

**Features:**
- Dashboard with three sections
- Manual refresh button
- Email/booking/comment detail pages
- Smart linking (auto-detect booking refs in emails)
- Manual linking (search and link)
- External links to OSM and Outlook

**API Endpoints:**
- `GET /api/emails` - List unread emails
- `GET /api/emails/{id}` - Full email details
- `GET /api/bookings` - List provisional bookings
- `GET /api/bookings/{id}` - Full booking details
- `GET /api/comments` - List new comments
- `POST /api/links` - Create manual link
- `POST /api/refresh` - Trigger data sync

**Success Criteria:**
- Can see all unread emails without opening Outlook
- Can see provisional bookings without opening OSM
- Can view email with linked booking details in one place
- Saves time by reducing context switching

### Phase 2: Basic Actions (Reduce Context Switching)

**Goal:** Perform common actions without leaving the app

**New Features:**
- Mark email as read
- Add comment to OSM booking (appears in OSM)
- Quick reply templates (common responses)
- Mark provisional booking as "reviewed" (local flag)
- Dismiss comments from "new" list

**New API Endpoints:**
- `PATCH /api/emails/{id}/read` - Mark as read in Office 365
- `POST /api/bookings/{id}/comments` - Add comment to OSM
- `POST /api/emails/{id}/reply` - Send email reply

**Success Criteria:**
- Can handle simple email inquiries end-to-end without opening external apps
- Comments logged in OSM are visible to wardens and Tammy
- Reduced clicks and tab-switching

### Phase 3: Full Workflow (Automation)

**Goal:** Complete booking management within the app

**New Features:**
- Confirm/cancel bookings (update status in OSM)
- Move booking dates (OSM API sequence: extend end → duplicate items → remove old → move start)
- Update customer details
- Send emails with variable substitution (e.g., "Dear {{customerName}}")
- Track deposit status with reminders
- Generate reports/analytics from cached data
- Multi-user support with permissions

**Advanced Automation:**
- Workflow templates: "Confirm booking + send confirmation email + add comment" as single action
- Email parsing suggestions: App analyzes email and suggests actions
- Background sync option (periodic polling)

**Success Criteria:**
- Can manage entire booking lifecycle without opening OSM
- Multi-user team can collaborate through the app
- Reduced manual steps through automation

## Smart Linking Algorithm

### Booking Reference Extraction

Patterns to detect booking references in email subject/body:

```
#12345
Booking #12345
Ref: 12345
REF:12345
Reference 12345
OSM #12345
Booking reference: 12345
```

**Implementation:**
- Regex: `(?:#|Ref:|REF:|Reference|Booking\s*#|OSM\s*#)\s*(\d{4,6})`
- Extract all matches from subject + body
- Query OsmBookings table for matching OsmBookingId
- Create ApplicationLinks entries with `CreatedByUserId = NULL` (auto-linked)

**Confidence Score (Future):**
- Exact match in subject: High confidence
- Match in body only: Medium confidence
- Multiple potential matches: Show all, let user choose

### Email-to-Customer Matching

Secondary linking method if no booking ref found:

- Extract sender email from EmailMessages
- Query OsmBookings where CustomerEmail matches
- Show as "Possible match" (not auto-linked, but suggested)
- User can confirm to create link

## Security Considerations

### Token Storage

- OAuth tokens encrypted at rest using ASP.NET Core Data Protection
- Data Protection keys stored in persistent volume (`/data/keys/`)
- SQLite database file protected by filesystem permissions

### API Security

- Frontend→Backend: Cookie-based authentication (ASP.NET Core Identity)
- Backend→Microsoft Graph: OAuth2 access token (refreshed automatically)
- Backend→OSM: API token or session token (TBD based on OSM API)

### Home Assistant Integration

- Addon runs in isolated Docker container
- Network access limited to required APIs (Office 365, OSM)
- Configuration secrets (client ID, client secret) stored in HA addon options (encrypted by HA)

### CORS

- Development: Allow `http://localhost:3000`
- Production: No CORS needed (API and frontend served from same origin)

## Open Questions

**OSM API:**
- [ ] Authentication method? (API key, session token, OAuth)
- [ ] Rate limiting? How to handle?
- [ ] Pagination for bookings/comments?
- [ ] Exact endpoints for fetching provisional bookings, adding comments, confirming bookings

**Office 365:**
- [ ] Shared mailbox vs. direct login - confirmed direct login for now
- [ ] Email folder structure - do we only look in Inbox or other folders?
- [ ] How to handle email threading/conversations?

**Business Logic:**
- [ ] What defines a "provisional" booking in OSM?
- [ ] What makes a comment "new"? (timestamp threshold? flag in OSM?)
- [ ] Should we cache all bookings or only provisional ones?

## Success Metrics

**Phase 1:**
- Time saved per email/booking (compared to manual process)
- Number of emails processed per day
- User satisfaction (qualitative feedback)

**Phase 2:**
- Percentage of emails handled without opening Outlook
- Percentage of bookings handled without opening OSM

**Phase 3:**
- Complete booking management time (from inquiry to confirmed booking)
- Error rate reduction
- Multi-user adoption

## Future Enhancements (Post-MVP)

- Mobile-responsive design improvements
- Push notifications (via Home Assistant notifications)
- Email rules/filters (auto-ignore spam, auto-tag)
- Analytics dashboard (booking trends, popular dates, revenue forecasting)
- Integration with other Home Assistant automations
- Backup/export functionality for audit trail
- Advanced search across emails and bookings
- Templated workflows for common scenarios

---

**Document Status:** Ready for implementation planning
**Next Steps:** Create implementation plan with superpowers:writing-plans skill
