chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.type === 'CAPTURE_EMAIL') {
    handleCaptureEmail(message.payload).then(sendResponse);
    return true; // keep channel open for async response
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
