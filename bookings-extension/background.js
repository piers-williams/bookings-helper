// Last backend results — cached so panel gets them immediately on open
let lastEmailResult = null;   // { response, email }
let lastBookingResult = null; // { response, bookingId }
let lastOwaTabId = null;
let lastOsmTabId = null;

// OWA URLs that should trigger auto-open
const OWA_ORIGINS = [
  'https://outlook.cloud.microsoft',
  'https://outlook.office365.com',
];

const OSM_ORIGIN = 'https://www.onlinescoutmanager.co.uk';

function isOwaUrl(url) {
  return url && OWA_ORIGINS.some(o => url.startsWith(o));
}

function isOsmUrl(url) {
  return url && url.startsWith(OSM_ORIGIN);
}

// Auto-open side panel when an OWA or OSM tab becomes active
chrome.tabs.onActivated.addListener(({ tabId }) => {
  chrome.tabs.get(tabId, (tab) => {
    if (tab && (isOwaUrl(tab.url) || isOsmUrl(tab.url))) {
      chrome.sidePanel.open({ tabId }).catch(() => {});
    }
  });
});

// Auto-open side panel when an OWA or OSM tab finishes loading
chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
  if (changeInfo.status === 'complete' && (isOwaUrl(tab.url) || isOsmUrl(tab.url))) {
    chrome.sidePanel.open({ tabId }).catch(() => {});
  }
});

// Relay an email result to the side panel (panel may not be open — silently ignore)
function relayEmailToPanel(response, email) {
  chrome.runtime.sendMessage({ type: 'EMAIL_RESPONSE', response, email })
    .catch(() => {}); // panel not open — result is cached for when it opens
}

// Relay a booking result to the side panel (panel may not be open — silently ignore)
function relayBookingToPanel(response, bookingId) {
  chrome.runtime.sendMessage({ type: 'BOOKING_RESPONSE', response, bookingId })
    .catch(() => {}); // panel not open — result is cached for when it opens
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {

  if (message.type === 'CAPTURE_EMAIL') {
    if (sender.tab) lastOwaTabId = sender.tab.id;
    handleCaptureEmail(message.payload).then(response => {
      lastEmailResult = { response, email: message.payload };
      relayEmailToPanel(response, message.payload);
    }).catch(() => {});
    return false; // no async response back to content script
  }

  if (message.type === 'BOOKING_CHANGED') {
    if (sender.tab) lastOsmTabId = sender.tab.id;
    handleGetBookingLinks(message.bookingId).then(response => {
      lastBookingResult = { response, bookingId: message.bookingId };
      relayBookingToPanel(response, message.bookingId);
    }).catch(() => {});
    return false; // no async response back to content script
  }

  if (message.type === 'PANEL_READY') {
    // Panel just opened — send last cached results if available
    if (lastEmailResult) {
      relayEmailToPanel(lastEmailResult.response, lastEmailResult.email);
    }
    if (lastBookingResult) {
      relayBookingToPanel(lastBookingResult.response, lastBookingResult.bookingId);
    }
    return false;
  }

  if (message.type === 'REFRESH_EMAIL') {
    if (lastOwaTabId !== null) {
      chrome.tabs.sendMessage(lastOwaTabId, { type: 'REFRESH' }).catch(() => {});
    }
    return false;
  }

  if (message.type === 'REFRESH_BOOKING') {
    if (lastOsmTabId !== null) {
      chrome.tabs.sendMessage(lastOsmTabId, { type: 'REFRESH_BOOKING' }).catch(() => {});
    }
    return false;
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
