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
    }).catch(() => {});
    return false; // no async response back to content script
  }

  if (message.type === 'PANEL_READY') {
    // Panel just opened — send last cached result if available
    if (lastEmailResult) {
      relayToPanel(lastEmailResult.response, lastEmailResult.email);
    }
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
