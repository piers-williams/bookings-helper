# Chrome Extension + MCP Backend Design

**Date:** 2026-02-21
**Status:** Approved for Implementation
**Supersedes:** Office 365 Graph API integration (blocked — no Azure AD admin approval)

## Context

The original design used Microsoft Graph API to access the `bookings@thorringtonscoutcamp.co.uk` shared mailbox. This required Azure AD admin approval, which is not available. IMAP is also disabled on the tenant. The user now has their own account (`piers.williams@`) with delegate access to the shared mailbox via Outlook Web App (OWA).

The solution is a Chrome extension that reads email content directly from the OWA page and feeds it to the existing backend, bypassing the need for any server-side email authentication.

## Goals

- Replace Graph API email access with a Chrome extension that reads from OWA
- Inject a sidebar into OWA showing linked OSM booking context while reading emails
- Inject the same sidebar into OSM showing linked emails while viewing a booking
- Design the backend as an MCP (Model Context Protocol) server from the start, enabling future local LLM integration
- Preserve the existing dashboard, OSM integration, smart linking, and SQLite cache unchanged

## Non-Goals (this phase)

- Local LLM integration (designed for, not built yet)
- Automated background email sync (user triggers by opening emails)
- Publishing the extension to the Chrome Web Store (loaded unpacked)
- Multi-user support

## Architecture

```
OWA (browser tab)                OSM (browser tab)
┌────────────────────┐           ┌────────────────────┐
│  Email reading pane │           │  Booking detail    │
│  ┌──────────────┐   │           │  ┌──────────────┐  │
│  │ Email body   │   │           │  │ Booking data │  │
│  └──────────────┘   │           │  └──────────────┘  │
│  ┌──────────────┐   │           │  ┌──────────────┐  │
│  │  [Sidebar]   │   │           │  │  [Sidebar]   │  │
│  │ Booking #123 │   │           │  │ 2 emails     │  │
│  │ Jane Smith   │   │           │  │ linked       │  │
│  │ [Handle AI▸] │   │           │  │ [Handle AI▸] │  │
│  └──────────────┘   │           │  └──────────────┘  │
└────────────────────┘           └────────────────────┘
          │  chrome.runtime.sendMessage        │
          └──────────────┬─────────────────────┘
                         │ background service worker
                         │ fetch (HTTP — no mixed content issue)
                         ▼
              ┌─────────────────────┐
              │  .NET Backend       │
              │  (network machine)  │
              │                     │
              │  /api/emails/capture│
              │  /api/mcp/tools     │  ← MCP server (future)
              │  /api/mcp/call      │  ← MCP server (future)
              │                     │
              │  SQLite cache       │
              │  OSM integration    │
              │  Smart linking      │
              └─────────────────────┘
                         │ (future)
                         ▼
              ┌─────────────────────┐
              │  Local LLM (Ollama) │
              │  tool calls via MCP │
              └─────────────────────┘
```

## Chrome Extension

### Structure

```
bookings-extension/
├── manifest.json        # Manifest V3, permissions, content script declarations
├── background.js        # Service worker — proxies HTTP requests to backend
├── content-owa.js       # Content script for OWA — reads email DOM, manages sidebar
├── content-osm.js       # Content script for OSM — reads booking DOM, manages sidebar
├── sidebar.css          # Shared sidebar styles
└── options.html         # Settings page — configure backend URL
└── options.js
```

### Manifest permissions

```json
{
  "manifest_version": 3,
  "permissions": ["storage"],
  "host_permissions": [
    "https://outlook.office365.com/*",
    "https://www.onlinescoutmanager.co.uk/*"
  ],
  "content_scripts": [
    {
      "matches": ["https://outlook.office365.com/*"],
      "js": ["content-owa.js"],
      "css": ["sidebar.css"]
    },
    {
      "matches": ["https://www.onlinescoutmanager.co.uk/*"],
      "js": ["content-osm.js"],
      "css": ["sidebar.css"]
    }
  ],
  "background": { "service_worker": "background.js" },
  "options_page": "options.html"
}
```

### content-owa.js responsibilities

1. On page load, inject the sidebar panel into the OWA layout
2. Use `MutationObserver` to detect when a new email is opened in the reading pane
3. Debounce (~300ms) to wait for the reading pane DOM to settle
4. Extract from DOM: subject, sender name, sender email, body text, received date
5. Send to background worker via `chrome.runtime.sendMessage`
6. Update sidebar with the response (linked bookings, suggested matches)

### content-osm.js responsibilities

1. Inject the same sidebar panel into OSM booking detail pages
2. Detect booking ID from the URL or DOM
3. Send booking ID to background worker requesting linked emails
4. Update sidebar with emails linked to this booking

### background.js responsibilities

1. Read backend URL from `chrome.storage.sync`
2. For OWA messages: POST to `{backendUrl}/api/emails/capture`
3. For OSM messages: GET `{backendUrl}/api/bookings/{id}/links`
4. Return responses to content scripts
5. Handle fetch errors and return structured error objects

### Sidebar UI

Both sidebars share the same HTML structure and CSS, rendered differently based on context.

**OWA sidebar — email context:**
```
┌────────────────────────────┐
│ Bookings Assistant      ⟳  │
├────────────────────────────┤
│ ✅ Linked Booking           │
│ #12345 · Jane Smith        │
│ 15–17 Mar · Provisional    │
│ Deposit: due 1 Feb ⚠️      │
│ [View in Dashboard →]      │
├────────────────────────────┤
│ 💬 Comments (2)            │
│ Tammy: "Called to confirm  │
│ arrival time..."           │
├────────────────────────────┤
│ 🔗 Link manually           │
│ [Search bookings...]       │
├────────────────────────────┤
│ [✨ Handle with AI]        │
└────────────────────────────┘
```

**OSM sidebar — booking context:**
```
┌────────────────────────────┐
│ Bookings Assistant      ⟳  │
├────────────────────────────┤
│ 📧 Linked Emails (2)       │
│ "Query about deposit"      │
│ from: jane@example.com     │
│ 3 days ago                 │
├────────────────────────────┤
│ 📧 "Arrival time query"    │
│ from: jane@example.com     │
│ 1 week ago                 │
├────────────────────────────┤
│ [✨ Handle with AI]        │
└────────────────────────────┘
```

### Options page

Single configuration field: **Backend URL** (e.g. `http://192.168.1.50:5000`).
Saved to `chrome.storage.sync`. Opens automatically on first install if not configured.

## Backend Changes

### New endpoint: POST /api/emails/capture

Accepts email data from the extension. Runs the same extraction and linking logic that would have been triggered via Graph API sync.

**Request:**
```json
{
  "subject": "Re: Booking #12345 query",
  "senderEmail": "jane@example.com",
  "senderName": "Jane Smith",
  "bodyText": "Hi, just checking on our booking #12345...",
  "receivedDate": "2026-02-21T09:30:00Z"
}
```

**Response:**
```json
{
  "emailId": 42,
  "linkedBookings": [
    {
      "osmBookingId": "12345",
      "customerName": "Jane Smith",
      "startDate": "2026-03-15",
      "endDate": "2026-03-17",
      "status": "Provisional"
    }
  ],
  "suggestedBookings": [],
  "autoLinked": true
}
```

Duplicate detection: if an email with the same subject + senderEmail + receivedDate already exists, return the cached record immediately.

### CORS update

Add `chrome-extension://*` to allowed origins on the `/api/emails/capture` endpoint (and all API endpoints). Since the backend is on a private network this is low risk.

### Tool-first API design (MCP-ready)

The backend API is designed so each action is a clean, single-purpose endpoint. This serves both the existing frontend and the future LLM integration without any refactoring.

**Tools to expose (already exist or are being added):**

| Tool | Endpoint | Description |
|------|----------|-------------|
| `get_email` | `GET /api/emails/{id}` | Full email content |
| `get_booking` | `GET /api/bookings/{id}` | Full booking details |
| `get_booking_emails` | `GET /api/bookings/{id}/links` | Emails linked to booking |
| `add_comment` | `POST /api/bookings/{id}/comments` | Add comment in OSM |
| `link_email_to_booking` | `POST /api/links` | Create manual link |
| `capture_email` | `POST /api/emails/capture` | Store email from extension |

### MCP server (future phase)

A future `/api/mcp/` set of endpoints will expose these tools in the MCP protocol format, enabling Ollama (or any MCP-compatible LLM) to call them. The **"Handle with AI"** button in the sidebar will POST the current context (email + booking) to a `/api/agent/handle` endpoint that invokes the LLM with the available tools. The LLM response (actions taken + summary) is displayed back in the sidebar for review before committing.

No implementation now — but no refactoring needed later because the tool endpoints already exist.

## Data Flow

### Opening an email in OWA (happy path)

1. User clicks email in OWA reading pane
2. `MutationObserver` fires in `content-owa.js`, 300ms debounce
3. Content script extracts subject, sender, body from DOM
4. `chrome.runtime.sendMessage(emailData)` to background worker
5. Background worker reads backend URL from `chrome.storage.sync`
6. Background worker `POST /api/emails/capture`
7. Backend: extract booking refs → match OsmBookings → create ApplicationLink → cache EmailMessage
8. Backend returns linked/suggested bookings
9. Background worker sends response to content script
10. Sidebar renders booking card with status, dates, deposit info, recent comments

### Opening a booking in OSM

1. User navigates to booking detail page in OSM
2. `content-osm.js` extracts booking ID from URL
3. `chrome.runtime.sendMessage({ bookingId })` to background worker
4. Background worker `GET /api/bookings/{id}/links`
5. Backend returns emails linked to this booking
6. Sidebar renders linked email list

### Backend unreachable

Background worker catches fetch error, returns `{ error: "unreachable", url: "..." }`.
Sidebar shows: *"Can't reach backend at http://... — is it running?"* with link to options page.

### Email not yet in OSM

Backend returns `linkedBookings: []`, `suggestedBookings` populated from sender email match.
Sidebar shows suggested match with confidence indicator and manual link option.

## Error Handling

| Scenario | Behaviour |
|----------|-----------|
| Extension not configured | Options page opens automatically |
| Backend unreachable | Sidebar shows clear error + options link |
| OWA DOM structure changes | Fallback to polling; extraction degrades gracefully to subject-only |
| OSM DOM structure changes | Sidebar shows booking ID only, offers "open in dashboard" link |
| Duplicate email capture | Backend returns cached record silently |
| OSM API unavailable | Backend returns cached booking data flagged as stale |
| No booking reference found | Sidebar shows sender-based suggestions |

## Testing

- **Extension**: Loaded unpacked via `chrome://extensions` — no publishing required
- **OWA content script**: Verified by opening a real email and checking sidebar populates with correct data
- **OSM content script**: Verified by opening a real booking and checking sidebar shows linked emails
- **Backend endpoint**: Tested with curl from a browser context with extension origin header
- **CORS**: Verified by checking network tab in DevTools for blocked requests
- **Offline resilience**: Backend stopped, verify sidebar shows friendly error not a crash

## Evolution Path

### Phase 1 (this design)
- Chrome extension reads OWA emails → feeds existing backend
- OWA sidebar shows linked booking context
- OSM sidebar shows linked emails
- Tool-first backend API ready for LLM

### Phase 2
- **"Handle with AI" button** — sends context to backend, calls local Ollama LLM
- LLM tools: add comment, send reply, confirm booking
- User reviews proposed actions before committing

### Phase 3
- MCP server endpoint — any MCP-compatible client can drive the backend
- Autonomous mode — LLM handles routine emails end-to-end with notification
- Booking activity scheduling via LLM tool call

## Open Questions

- [ ] What is the exact DOM structure of the OWA reading pane? (Verify during implementation)
- [ ] What is the exact DOM structure / URL pattern for OSM booking detail pages? (Already partially documented in `docs/osm-api-discovery.md`)
- [ ] Which local LLM / Ollama model to use for Phase 2?
- [ ] Should "Handle with AI" actions require explicit user confirmation, or run autonomously?
