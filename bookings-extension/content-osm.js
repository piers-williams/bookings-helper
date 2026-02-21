(function () {
  'use strict';

  let sidebar = null;
  let lastBookingId = null;
  let debounceTimer = null;

  // --- Sidebar injection ---

  function injectSidebar() {
    if (document.getElementById('ba-sidebar')) return;

    sidebar = document.createElement('div');
    sidebar.id = 'ba-sidebar';
    sidebar.innerHTML = `
      <div id="ba-sidebar-header">
        <span>Bookings Assistant</span>
        <button id="ba-refresh" title="Refresh">&#x27F3;</button>
      </div>
      <div id="ba-sidebar-body">
        <div class="ba-loading">Navigate to a booking to see linked emails.</div>
      </div>
    `;
    document.body.appendChild(sidebar);

    document.getElementById('ba-refresh').addEventListener('click', () => {
      lastBookingId = null;
      checkForBookingChange();
    });

    document.body.style.paddingRight = '280px';
  }

  // --- Booking ID extraction from OSM URL/DOM ---

  function extractBookingId() {
    // OSM URLs typically contain bookingid= or similar
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
    showLoading();
    fetchLinkedEmails(bookingId);
  }

  // --- Backend communication ---

  function fetchLinkedEmails(bookingId) {
    chrome.runtime.sendMessage(
      { type: 'GET_BOOKING_LINKS', bookingId },
      (response) => {
        if (chrome.runtime.lastError) {
          showError('Extension error: ' + chrome.runtime.lastError.message);
          return;
        }
        renderResponse(response);
      }
    );
  }

  // --- Sidebar rendering ---

  function showLoading() {
    document.getElementById('ba-sidebar-body').innerHTML =
      '<div class="ba-loading">Loading linked emails...</div>';
  }

  function showError(msg) {
    document.getElementById('ba-sidebar-body').innerHTML =
      `<div class="ba-error">${msg}</div>`;
  }

  function renderResponse(response) {
    const body = document.getElementById('ba-sidebar-body');

    if (!response || response.error) {
      if (response && response.error === 'not_configured') {
        body.innerHTML = '<div class="ba-error">Backend URL not configured. Click the extension icon to set it.</div>';
      } else {
        showError(`Can't reach backend at ${response && response.url ? response.url : '(unknown)'}. Is it running?`);
      }
      return;
    }

    // response is an array of EmailDto
    const emails = Array.isArray(response) ? response : (response.emails || []);
    let html = '';

    if (emails.length === 0) {
      html = '<div class="ba-section"><div class="ba-empty">No emails linked to this booking yet.</div></div>';
    } else {
      html += `<div class="ba-section">`;
      html += `<div class="ba-section-title">\uD83D\uDCE7 Linked Emails (${emails.length})</div>`;
      emails.forEach(function (email) {
        const date = email.receivedDate
          ? new Date(email.receivedDate).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })
          : '';
        html += `
          <div class="ba-email-item">
            <div class="ba-email-subject">${email.subject || '(No Subject)'}</div>
            <div class="ba-email-meta">${email.senderName || email.senderEmail || ''} \u00B7 ${date}</div>
          </div>
        `;
      });
      html += '</div>';
    }

    html += `<button class="ba-handle-btn" disabled title="Coming in Phase 2">\u2728 Handle with AI</button>`;

    body.innerHTML = html;
  }

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
    injectSidebar();
    startObserver();
    setTimeout(checkForBookingChange, 1000);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
