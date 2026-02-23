# Chrome Side Panel Migration Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the injected fixed-position sidebar in OWA with a native Chrome Side Panel so the panel occupies its own space beside the page rather than overlapping it.

**Architecture:** A new `panel.html` / `panel.js` pair becomes the side panel UI. `background.js` auto-opens the panel when OWA loads, caches the last backend result, and relays `EMAIL_RESPONSE` messages to the panel. `content-owa.js` is stripped of all rendering code — it only extracts email data and sends `CAPTURE_EMAIL` to the background.

**Tech Stack:** Chrome Extension MV3, `chrome.sidePanel` API (Chrome 114+), `chrome.tabs` API, Vanilla JS, `sidebar.css` (shared component styles)

---

### Task 1: Manifest + panel.html

**Files:**
- Modify: `bookings-extension/manifest.json`
- Create: `bookings-extension/panel.html`

**Step 1: Update `manifest.json`**

Add `"sidePanel"` and `"tabs"` to permissions, add `"side_panel"` key, remove `"sidebar.css"` from the OWA content script CSS array (keep it in the OSM entry — that script still uses the injected overlay):

```json
{
  "manifest_version": 3,
  "name": "Bookings Assistant",
  "version": "0.1.0",
  "description": "Shows OSM booking context alongside OWA emails, and email context alongside OSM bookings.",
  "permissions": ["storage", "sidePanel", "tabs"],
  "side_panel": {
    "default_path": "panel.html"
  },
  "host_permissions": [
    "https://outlook.office365.com/*",
    "https://outlook.cloud.microsoft/*",
    "https://www.onlinescoutmanager.co.uk/*"
  ],
  "content_scripts": [
    {
      "matches": [
        "https://outlook.office365.com/*",
        "https://outlook.cloud.microsoft/*"
      ],
      "js": ["content-owa.js"],
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

**Step 2: Create `panel.html`**

```html
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <link rel="stylesheet" href="sidebar.css">
  <style>
    html, body { margin: 0; padding: 0; height: 100%; overflow: hidden; }
    /* Override sidebar.css layout rules — panel fills its container naturally */
    #ba-sidebar {
      position: static;
      width: 100%;
      height: 100vh;
      box-shadow: none;
      border-left: none;
    }
    /* Compact mode: hide body, shrink header */
    .ba-compact #ba-sidebar-body { display: none; }
    .ba-compact #ba-title { display: none; }
    .ba-compact #ba-status-chip { display: inline !important; font-size: 12px; color: white; }
    .ba-compact #ba-sidebar-header { padding: 6px 10px; }
  </style>
</head>
<body>
  <div id="ba-sidebar">
    <div id="ba-sidebar-header">
      <span id="ba-title">Bookings Assistant</span>
      <span id="ba-status-chip" style="display:none">·</span>
      <div style="display:flex;gap:4px">
        <button id="ba-refresh" title="Refresh">&#x27F3;</button>
        <button id="ba-toggle" title="Compact mode">&#x2039;</button>
      </div>
    </div>
    <div id="ba-sidebar-body">
      <div class="ba-loading">Open an email to see booking context.</div>
    </div>
  </div>
  <script src="panel.js"></script>
</body>
</html>
```

**Step 3: Verify the extension loads**

1. Open `chrome://extensions`, enable Developer Mode, click **Load unpacked**, select `bookings-extension/`
2. Check there are no manifest errors in the extensions page
3. Navigate to OWA — the side panel should not appear yet (panel.js doesn't exist)

Expected: extension loads without errors.

**Step 4: Commit**

```bash
git add bookings-extension/manifest.json bookings-extension/panel.html
git commit -m "feat: add side panel manifest entries and panel.html skeleton"
```

---

### Task 2: panel.js — UI, rendering, compact/full toggle

**Files:**
- Create: `bookings-extension/panel.js`

This file ports all rendering logic from `content-owa.js` (`renderResponse`, `renderBookingCard`) and adds the compact/full toggle.

**Step 1: Create `bookings-extension/panel.js`**

```javascript
(function () {
  'use strict';

  // --- Compact / full toggle ---

  const COMPACT_KEY = 'ba-compact';
  let isCompact = sessionStorage.getItem(COMPACT_KEY) === 'true';

  function applyCompactState() {
    const sidebar = document.getElementById('ba-sidebar');
    const toggleBtn = document.getElementById('ba-toggle');
    if (isCompact) {
      sidebar.classList.add('ba-compact');
      toggleBtn.textContent = '›';
      toggleBtn.title = 'Expand';
    } else {
      sidebar.classList.remove('ba-compact');
      toggleBtn.textContent = '‹';
      toggleBtn.title = 'Compact';
    }
  }

  document.getElementById('ba-toggle').addEventListener('click', () => {
    isCompact = !isCompact;
    sessionStorage.setItem(COMPACT_KEY, String(isCompact));
    applyCompactState();
  });

  applyCompactState();

  // --- Refresh button ---

  document.getElementById('ba-refresh').addEventListener('click', () => {
    chrome.runtime.sendMessage({ type: 'REFRESH_EMAIL' });
    showLoading();
  });

  // --- Background communication ---

  // Tell background we're ready — it will reply with the last cached result
  chrome.runtime.sendMessage({ type: 'PANEL_READY' });

  // Listen for results relayed from background
  chrome.runtime.onMessage.addListener((message) => {
    if (message.type === 'EMAIL_RESPONSE') {
      renderResponse(message.response, message.email);
    }
  });

  // --- Rendering ---

  function setStatusChip(text) {
    document.getElementById('ba-status-chip').textContent = text;
  }

  function showLoading() {
    document.getElementById('ba-sidebar-body').innerHTML =
      '<div class="ba-loading">Loading booking context...</div>';
    setStatusChip('⟳');
  }

  function showError(msg) {
    document.getElementById('ba-sidebar-body').innerHTML =
      `<div class="ba-error">${msg}</div>`;
    setStatusChip('⚠');
  }

  function renderResponse(response, email) {
    if (!response || response.error) {
      if (response && response.error === 'not_configured') {
        document.getElementById('ba-sidebar-body').innerHTML =
          '<div class="ba-error">Backend URL not configured.<br>' +
          '<a href="#" id="ba-open-options">Open settings \u2192</a></div>';
        document.getElementById('ba-open-options')?.addEventListener('click', (e) => {
          e.preventDefault();
          chrome.runtime.sendMessage({ type: 'OPEN_OPTIONS' });
        });
        setStatusChip('⚙');
      } else {
        const url = response?.url || '(unknown)';
        showError(`Can\u2019t reach backend at ${url}. Is it running?`);
      }
      return;
    }

    const body = document.getElementById('ba-sidebar-body');
    let html = '';
    let statusChip = '\u00b7 No booking';

    if (response.linkedBookings && response.linkedBookings.length > 0) {
      statusChip = `\u2705 #${response.linkedBookings[0].osmBookingId}`;
      html += '<div class="ba-section">';
      html += '<div class="ba-section-title">\u2705 Linked Booking</div>';
      response.linkedBookings.forEach(b => { html += renderBookingCard(b); });
      html += '</div>';
    } else if (response.suggestedBookings && response.suggestedBookings.length > 0) {
      statusChip = '\uD83D\uDD0D Possible match';
      html += '<div class="ba-section">';
      html += '<div class="ba-section-title">\uD83D\uDD0D Possible Match</div>';
      response.suggestedBookings.forEach(b => { html += renderBookingCard(b, true); });
      html += '</div>';
    } else {
      html += '<div class="ba-section"><div class="ba-empty">No booking linked.</div></div>';
    }

    html += '<div class="ba-section">';
    html += '<div class="ba-section-title">\uD83D\uDD17 Manual Link</div>';
    html += `<button class="ba-link-btn secondary" onclick="window.open('http://localhost:5000', '_blank')">Open Dashboard \u2192</button>`;
    html += '</div>';

    html += `<button class="ba-handle-btn" disabled title="Coming in Phase 2">\u2728 Handle with AI</button>`;

    body.innerHTML = html;
    setStatusChip(statusChip);
  }

  function renderBookingCard(booking, isSuggested) {
    isSuggested = isSuggested || false;
    const status = (booking.status || '').toLowerCase();
    const statusClass = `ba-status-${status}`;
    const start = booking.startDate
      ? new Date(booking.startDate).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })
      : '';
    const end = booking.endDate
      ? new Date(booking.endDate).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })
      : '';

    return `
      <div class="ba-booking-card">
        <div><span class="ba-booking-ref">#${booking.osmBookingId}</span> \u00b7 <span class="ba-booking-name">${booking.customerName}</span></div>
        <div class="ba-booking-dates">${start}${end ? ' \u2013 ' + end : ''}</div>
        <div><span class="ba-booking-status ${statusClass}">${booking.status}</span></div>
        ${isSuggested ? '<div style="font-size:11px;color:#666;margin-top:4px">Possible match</div>' : ''}
      </div>
    `;
  }
})();
```

**Step 2: Verify the panel loads**

1. Reload the extension in `chrome://extensions`
2. Navigate to OWA — the native side panel won't auto-open yet (background.js not updated), but you can open it manually via the extension toolbar icon → side panel icon
3. Panel should show "Open an email to see booking context."
4. Click the `‹` button — content area hides, header shrinks to compact mode
5. Click `›` — expands again

Expected: panel renders, toggle works.

**Step 3: Commit**

```bash
git add bookings-extension/panel.js
git commit -m "feat: add panel.js with rendering and compact/full toggle"
```

---

### Task 3: background.js — auto-open panel and relay messages

**Files:**
- Modify: `bookings-extension/background.js`

**Step 1: Replace `background.js` entirely**

```javascript
// Last backend result — cached so panel gets it immediately on open
let lastEmailResult = null; // { response, email }

// OWA URLs that should trigger auto-open
const OWA_ORIGINS = [
  'https://outlook.cloud.microsoft',
  'https://outlook.office365.com',
];

function isOwaUrl(url) {
  return url && OWA_ORIGINS.some(o => url.startsWith(o));
}

// Auto-open side panel when an OWA tab becomes active
chrome.tabs.onActivated.addListener(({ tabId }) => {
  chrome.tabs.get(tabId, (tab) => {
    if (tab && isOwaUrl(tab.url)) {
      chrome.sidePanel.open({ tabId }).catch(() => {});
    }
  });
});

// Auto-open side panel when an OWA tab finishes loading
chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
  if (changeInfo.status === 'complete' && isOwaUrl(tab.url)) {
    chrome.sidePanel.open({ tabId }).catch(() => {});
  }
});

// Relay a result to the side panel (panel may not be open — silently ignore)
function relayToPanel(response, email) {
  chrome.runtime.sendMessage({ type: 'EMAIL_RESPONSE', response, email })
    .catch(() => {}); // panel not open — result is cached for when it opens
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {

  if (message.type === 'CAPTURE_EMAIL') {
    handleCaptureEmail(message.payload).then(response => {
      lastEmailResult = { response, email: message.payload };
      relayToPanel(response, message.payload);
    });
    return false; // no async response back to content script
  }

  if (message.type === 'PANEL_READY') {
    // Panel just opened — send last cached result if available
    if (lastEmailResult) {
      relayToPanel(lastEmailResult.response, lastEmailResult.email);
    }
    sendResponse({});
    return false;
  }

  if (message.type === 'REFRESH_EMAIL') {
    // Ask the active OWA content script to re-trigger capture
    chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
      if (tabs[0]) {
        chrome.tabs.sendMessage(tabs[0].id, { type: 'REFRESH' }).catch(() => {});
      }
    });
    return false;
  }

  if (message.type === 'GET_BOOKING_LINKS') {
    handleGetBookingLinks(message.bookingId).then(sendResponse);
    return true;
  }

  if (message.type === 'OPEN_OPTIONS') {
    chrome.runtime.openOptionsPage();
    return false;
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

**Step 2: Verify auto-open**

1. Reload the extension
2. Navigate to OWA — the Chrome side panel should open automatically
3. Panel shows "Open an email to see booking context."
4. Select an email — backend captures it, panel shows booking context

Expected: panel auto-opens on OWA, populates on email selection.

**Step 3: Verify refresh**

1. With an email open, click the `⟳` button in the panel
2. Panel should show "Loading…" briefly then re-render

**Step 4: Commit**

```bash
git add bookings-extension/background.js
git commit -m "feat: auto-open side panel for OWA tabs, relay EMAIL_RESPONSE to panel"
```

---

### Task 4: Strip rendering from content-owa.js + version bump

**Files:**
- Modify: `bookings-extension/content-owa.js`
- Modify: `bookings-extension/manifest.json` (version bump)

**Step 1: Replace `content-owa.js` entirely**

Remove all rendering functions (`injectSidebar`, `showLoading`, `showError`, `renderResponse`, `renderBookingCard`) and the `document.body.paddingRight` call. Keep extraction + messaging only. Add a `REFRESH` message listener so the panel's refresh button works.

```javascript
(function () {
  'use strict';

  let lastEmailKey = null;
  let debounceTimer = null;

  // --- Email extraction from OWA DOM ---
  // Selectors verified against outlook.cloud.microsoft (Feb 2026).

  function extractEmail() {
    const subject = getTextBySelectors([
      '[id$="_SUBJECT"] span',
      '[data-testid="ConversationSubject"]',
    ]);

    const fromRaw = getTextBySelectors([
      '[id$="_FROM"] > span > div > span',
      '[id$="_FROM"] span',
      '[data-testid="SenderName"]',
    ]);
    const { name: senderName, email: senderEmail } = parseFromText(fromRaw);

    const bodyEl = document.querySelector(
      '[id$="_BODY"], #focused > div:nth-child(3), #focused'
    );
    const bodyText = bodyEl ? bodyEl.innerText.substring(0, 5000) : '';

    if (!subject && !senderName) return null;

    return {
      subject: subject || '(No Subject)',
      senderName: senderName || '',
      senderEmail: senderEmail || '',
      bodyText,
      receivedDate: new Date().toISOString(),
      candidateNames: extractCandidateNames(bodyText, senderName || ''),
    };
  }

  function parseFromText(text) {
    if (!text) return { name: '', email: '' };
    const match = text.match(/^(.+?)\s*<([\w.+\-]+@[\w.\-]+)>/);
    if (match) return { name: match[1].trim(), email: match[2] };
    const emailOnly = text.match(/([\w.+\-]+@[\w.\-]+)/);
    if (emailOnly) return { name: text.replace(emailOnly[0], '').trim(), email: emailOnly[1] };
    return { name: text, email: '' };
  }

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

  function getTextBySelectors(selectors) {
    for (const sel of selectors) {
      const el = document.querySelector(sel);
      if (el && el.textContent.trim()) return el.textContent.trim();
    }
    return null;
  }

  // --- Change detection ---

  function checkForEmailChange() {
    const email = extractEmail();
    if (!email) return;

    const key = `${email.subject}|${email.senderEmail}`;
    if (key === lastEmailKey) return;

    lastEmailKey = key;
    chrome.runtime.sendMessage({ type: 'CAPTURE_EMAIL', payload: email });
  }

  // --- Message listener (refresh triggered from panel) ---

  chrome.runtime.onMessage.addListener((message) => {
    if (message.type === 'REFRESH') {
      lastEmailKey = null;
      checkForEmailChange();
    }
  });

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
    startObserver();
    setTimeout(checkForEmailChange, 1000);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
```

**Step 2: Bump extension version**

In `bookings-extension/manifest.json` change `"version": "0.1.0"` to `"version": "0.2.0"`.

**Step 3: Reload and full manual test**

1. Reload the extension in `chrome://extensions`
2. Navigate to OWA — side panel opens automatically, no overlay on the page
3. OWA page content is NOT covered — the panel sits alongside
4. Select an email — panel shows booking context
5. Click `‹` — panel collapses to compact header with status chip
6. Click `›` — panel expands again
7. Click `⟳` — panel reloads current email
8. Navigate to a different email — panel updates

Expected: all 8 behaviours work, page content fully visible.

**Step 4: Commit and push**

```bash
git add bookings-extension/
git commit -m "feat: migrate OWA sidebar to native Chrome side panel v0.2.0"
git push
```
