(function () {
  'use strict';

  let sidebar = null;
  let lastEmailKey = null;
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
        <div class="ba-loading">Open an email to see booking context.</div>
      </div>
    `;
    document.body.appendChild(sidebar);

    document.getElementById('ba-refresh').addEventListener('click', () => {
      lastEmailKey = null; // force re-fetch
      checkForEmailChange();
    });

    // Push OWA content left to make room for sidebar
    document.body.style.paddingRight = '280px';
  }

  // --- Email extraction from OWA DOM ---
  // Selectors verified against outlook.cloud.microsoft (Feb 2026).
  // IDs like CONV_xxx_SUBJECT and MSG_xxx_FROM are dynamic but the suffix is stable.

  function extractEmail() {
    const subject = getTextBySelectors([
      '[id$="_SUBJECT"] span',
      '[data-testid="ConversationSubject"]',
    ]);

    // FROM element contains "Name<email@domain>" in a single span
    const fromRaw = getTextBySelectors([
      '[id$="_FROM"] > span > div > span',
      '[id$="_FROM"] span',
      '[data-testid="SenderName"]',
    ]);
    const { name: senderName, email: senderEmail } = parseFromText(fromRaw);

    // Body — try stable suffix pattern first, fall back to #focused reading pane
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
    };
  }

  // Parse "Display Name<email@domain>" or plain email into name + email parts
  function parseFromText(text) {
    if (!text) return { name: '', email: '' };
    const match = text.match(/^(.+?)\s*<([\w.+\-]+@[\w.\-]+)>/);
    if (match) return { name: match[1].trim(), email: match[2] };
    const emailOnly = text.match(/([\w.+\-]+@[\w.\-]+)/);
    if (emailOnly) return { name: text.replace(emailOnly[0], '').trim(), email: emailOnly[1] };
    return { name: text, email: '' };
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
    showLoading();
    sendToBackend(email);
  }

  // --- Backend communication ---

  function sendToBackend(email) {
    chrome.runtime.sendMessage(
      { type: 'CAPTURE_EMAIL', payload: email },
      (response) => {
        if (chrome.runtime.lastError) {
          showError('Extension error: ' + chrome.runtime.lastError.message);
          return;
        }
        renderResponse(response, email);
      }
    );
  }

  // --- Sidebar rendering ---

  function showLoading() {
    document.getElementById('ba-sidebar-body').innerHTML =
      '<div class="ba-loading">Loading booking context...</div>';
  }

  function showError(msg) {
    document.getElementById('ba-sidebar-body').innerHTML =
      `<div class="ba-error">${msg}</div>`;
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
      } else {
        const url = response?.url || '(unknown)';
        showError(`Can't reach backend at ${url}. Is it running?`);
      }
      return;
    }

    const body = document.getElementById('ba-sidebar-body');
    let html = '';

    if (response.linkedBookings && response.linkedBookings.length > 0) {
      html += '<div class="ba-section">';
      html += '<div class="ba-section-title">\u2705 Linked Booking</div>';
      response.linkedBookings.forEach(b => {
        html += renderBookingCard(b);
      });
      html += '</div>';
    } else if (response.suggestedBookings && response.suggestedBookings.length > 0) {
      html += '<div class="ba-section">';
      html += '<div class="ba-section-title">\uD83D\uDD0D Possible Match</div>';
      response.suggestedBookings.forEach(b => {
        html += renderBookingCard(b, true);
      });
      html += '</div>';
    } else {
      html += '<div class="ba-section">';
      html += '<div class="ba-empty">No booking linked.</div>';
      html += '</div>';
    }

    // Manual link search
    html += '<div class="ba-section">';
    html += '<div class="ba-section-title">\uD83D\uDD17 Manual Link</div>';
    html += `<button class="ba-link-btn secondary" onclick="window.open('http://localhost:5000', '_blank')">Open Dashboard \u2192</button>`;
    html += '</div>';

    // Handle with AI (placeholder for Phase 2)
    html += `<button class="ba-handle-btn" disabled title="Coming in Phase 2">\u2728 Handle with AI</button>`;

    body.innerHTML = html;
  }

  function renderBookingCard(booking, isSuggested) {
    isSuggested = isSuggested || false;
    const status = (booking.status || '').toLowerCase();
    const statusClass = `ba-status-${status}`;
    const start = booking.startDate ? new Date(booking.startDate).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' }) : '';
    const end = booking.endDate ? new Date(booking.endDate).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' }) : '';

    return `
      <div class="ba-booking-card">
        <div><span class="ba-booking-ref">#${booking.osmBookingId}</span> \u00B7 <span class="ba-booking-name">${booking.customerName}</span></div>
        <div class="ba-booking-dates">${start}${end ? ' \u2013 ' + end : ''}</div>
        <div><span class="ba-booking-status ${statusClass}">${booking.status}</span></div>
        ${isSuggested ? '<div style="font-size:11px;color:#666;margin-top:4px">Matched by sender email</div>' : ''}
      </div>
    `;
  }

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
    injectSidebar();
    startObserver();
    // Initial check in case an email is already open
    setTimeout(checkForEmailChange, 1000);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
