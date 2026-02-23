# Chrome Side Panel Migration Design

**Date:** 2026-02-23
**Status:** Approved

## Problem

The current extension injects a `position: fixed` sidebar directly into the OWA DOM and compensates with `document.body.paddingRight = '280px'`. OWA's internal layout ignores the body padding (it uses its own fixed/absolute containers), so the sidebar overlaps page content.

## Goal

Migrate to the Chrome Side Panel API so the sidebar occupies a real browser-native panel that shrinks the page viewport rather than overlapping it.

## Architecture

Three responsibilities split cleanly:

**`content-owa.js`** — DOM extraction only. Detects email changes, extracts subject/sender/body/candidateNames, sends `CAPTURE_EMAIL` to background. All rendering code and `document.body.paddingRight` removed. `sidebar.css` no longer injected into OWA.

**`background.js`** — API calls (unchanged) plus two new jobs:
1. Auto-open the side panel when an OWA tab becomes active (`chrome.tabs.onActivated` + `chrome.tabs.onUpdated`)
2. Cache the last backend response in memory and relay it to the panel via `chrome.runtime.sendMessage`

**`panel.html` / `panel.js`** — the UI. Same content as current sidebar (linked/suggested/empty states + manual link button). Sends `PANEL_READY` on load so background pushes the cached last result immediately.

## Message Flow

```
content-owa.js  →  CAPTURE_EMAIL   →  background.js  →  fetch backend
background.js   →  EMAIL_RESPONSE  →  panel.js        (renders result)
panel.js        →  PANEL_READY     →  background.js   →  EMAIL_RESPONSE (cached)
```

## Auto-Open

Background listens to `chrome.tabs.onActivated` and `chrome.tabs.onUpdated`. When the active tab URL matches `https://outlook.cloud.microsoft/*` or `https://outlook.office365.com/*`, calls `chrome.sidePanel.open({ tabId })`.

## Compact / Full Toggle

A chevron button in the panel header toggles between two modes:

- **Full mode** (default): header shows "Bookings Assistant" + refresh + collapse button; body shows all booking content
- **Compact mode**: body hidden; header shrinks to a slim bar with a status chip (`✅ #12345`, `🔍 Possible match`, or `· No booking`) + expand button

Toggle state stored in `sessionStorage` — persists across panel reloads within a browser session, resets to full on fresh browser start.

## File Changes

| File | Change |
|---|---|
| `manifest.json` | Add `"sidePanel"` permission; add `"side_panel": { "default_path": "panel.html" }`; remove `sidebar.css` from OWA content script CSS |
| `background.js` | Add tab listeners for auto-open; cache `lastEmailResult`; relay `EMAIL_RESPONSE` to panel |
| `content-owa.js` | Remove `injectSidebar`, `renderResponse`, `renderBookingCard`, `showLoading`, `showError`; remove `document.body.paddingRight`; keep extraction + `CAPTURE_EMAIL` send |
| `sidebar.css` | Remove layout rules (`position`, `top`, `right`, `width`, `height`, `z-index`); keep component styles; reference from `panel.html` |
| `panel.html` | New — panel markup, imports `sidebar.css` and `panel.js` |
| `panel.js` | New — `PANEL_READY` on load, listens for `EMAIL_RESPONSE`, renders response, handles compact/full toggle |

## Out of Scope

- Programmatic panel width control (Chrome controls the width; user drags to resize)
- Persistent compact-mode preference across browser restarts
- Changes to `content-osm.js` (OSM-side content script unaffected)
