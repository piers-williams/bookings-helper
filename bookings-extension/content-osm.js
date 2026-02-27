(function () {
  'use strict';

  let lastBookingId = null;
  let debounceTimer = null;

  // --- Booking ID extraction from OSM URL/DOM ---

  function extractBookingId() {
    const urlMatch = window.location.href.match(/bookingid[=\/](\d+)/i);
    if (urlMatch) return urlMatch[1];

    // Fallback: look for it in the DOM
    const domMatch = document.body.innerHTML.match(/bookingid[="\s]+(\d+)/i);
    if (domMatch) return domMatch[1];

    return null;
  }

  // --- Change detection ---

  function checkForBookingChange() {
    const bookingId = extractBookingId();
    if (!bookingId || bookingId === lastBookingId) return;

    lastBookingId = bookingId;
    chrome.runtime.sendMessage({ type: 'BOOKING_CHANGED', bookingId }).catch(() => {});
  }

  // --- Message listener (refresh triggered from panel) ---

  chrome.runtime.onMessage.addListener((message) => {
    if (message.type === 'REFRESH_BOOKING') {
      lastBookingId = null;
      checkForBookingChange();
    }
    return false;
  });

  // --- MutationObserver for SPA navigation ---

  function startObserver() {
    let lastUrl = window.location.href;

    const observer = new MutationObserver(() => {
      if (window.location.href !== lastUrl) {
        lastUrl = window.location.href;
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(checkForBookingChange, 400);
      }
    });

    observer.observe(document.body, { childList: true, subtree: true });
  }

  // --- Init ---

  function init() {
    startObserver();
    setTimeout(checkForBookingChange, 1000);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
