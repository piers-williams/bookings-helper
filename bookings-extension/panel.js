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
      toggleBtn.textContent = '\u203a';
      toggleBtn.title = 'Expand';
    } else {
      sidebar.classList.remove('ba-compact');
      toggleBtn.textContent = '\u2039';
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

  // Listen for results relayed from background
  chrome.runtime.onMessage.addListener((message) => {
    if (message.type === 'EMAIL_RESPONSE') {
      renderResponse(message.response, message.email);
    }
  });

  // Tell background we're ready — it will reply with the last cached result
  chrome.runtime.sendMessage({ type: 'PANEL_READY' });

  // --- Rendering ---

  function escapeHtml(str) {
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  function setStatusChip(text) {
    document.getElementById('ba-status-chip').textContent = text;
  }

  function showLoading() {
    document.getElementById('ba-sidebar-body').innerHTML =
      '<div class="ba-loading">Loading booking context...</div>';
    setStatusChip('\u27f3');
  }

  function showError(msg) {
    document.getElementById('ba-sidebar-body').innerHTML =
      `<div class="ba-error">${msg}</div>`;
    setStatusChip('\u26a0');
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
        setStatusChip('\u2699');
      } else {
        const url = response?.url || '(unknown)';
        showError(`Can\u2019t reach backend at ${escapeHtml(url)}. Is it running?`);
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
    html += `<button class="ba-link-btn secondary" id="ba-open-dashboard">Open Dashboard \u2192</button>`;
    html += '</div>';

    html += `<button class="ba-handle-btn" disabled title="Coming in Phase 2">\u2728 Handle with AI</button>`;

    body.innerHTML = html;
    setStatusChip(statusChip);

    document.getElementById('ba-open-dashboard')?.addEventListener('click', () => {
      chrome.storage.sync.get(['backendUrl'], (result) => {
        const url = result.backendUrl || 'http://localhost:5000';
        window.open(url, '_blank');
      });
    });
  }

  // --- Date formatting and school holiday helpers ---

  // Essex school holidays 2025-2026.
  // Source: https://www.essex.gov.uk/schools-and-learning/schools/essex-school-terms-and-holidays/academic-year-2025-2026
  // Update each academic year.
  const ESSEX_HOLIDAYS = [
    { name: 'Autumn half term',       shortName: 'Autumn half term', start: new Date('2025-10-27'), end: new Date('2025-10-31') },
    { name: 'Christmas holiday',      shortName: 'Christmas hols',   start: new Date('2025-12-22'), end: new Date('2026-01-02') },
    { name: 'Spring half term',       shortName: 'Spring half term', start: new Date('2026-02-16'), end: new Date('2026-02-20') },
    { name: 'Easter holiday',         shortName: 'Easter hols',      start: new Date('2026-03-30'), end: new Date('2026-04-10') },
    { name: 'Early May bank holiday', shortName: 'May bank holiday', start: new Date('2026-05-04'), end: new Date('2026-05-04') },
    { name: 'Summer half term',       shortName: 'Summer half term', start: new Date('2026-05-25'), end: new Date('2026-05-29') },
    { name: 'Summer holiday',         shortName: 'Summer hols',      start: new Date('2026-07-21'), end: new Date('2026-08-31') },
  ];

  // Returns the holiday object if `date` falls within any Essex holiday period, otherwise null.
  function getHolidayForDate(date) {
    const d = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    for (const h of ESSEX_HOLIDAYS) {
      if (d >= h.start && d <= h.end) return h;
    }
    return null;
  }

  function formatDate(date, includeYear) {
    const opts = { weekday: 'short', day: 'numeric', month: 'short' };
    if (includeYear) opts.year = 'numeric';
    return date.toLocaleDateString('en-GB', opts);
  }

  function renderBookingCard(booking, isSuggested) {
    isSuggested = isSuggested || false;
    const status = (booking.status || '').toLowerCase();
    const statusClass = `ba-status-${escapeHtml(status)}`;

    const startDate = booking.startDate ? new Date(booking.startDate) : null;
    const endDate   = booking.endDate   ? new Date(booking.endDate)   : null;
    const start = startDate ? formatDate(startDate, false) : '';
    const end   = endDate   ? formatDate(endDate,   true)  : '';

    // Build the date range text, then optionally wrap it in a holiday tooltip.
    const dateText = start + (end ? ' \u2013 ' + end : '');
    const holiday = startDate ? getHolidayForDate(startDate) : null;

    let datesHtml;
    if (holiday) {
      const tipStart = holiday.start.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
      const tipEnd   = holiday.end.toLocaleDateString('en-GB',   { day: 'numeric', month: 'short', year: 'numeric' });
      const tipText  = escapeHtml(`${holiday.name}: ${tipStart} \u2013 ${tipEnd}`);
      datesHtml = `<span class="ba-date-tip" data-tip="${tipText}">${escapeHtml(dateText)} \u00b7 ${escapeHtml(holiday.shortName)}</span>`;
    } else {
      datesHtml = escapeHtml(dateText);
    }

    return `
      <div class="ba-booking-card">
        <div><span class="ba-booking-ref">#${escapeHtml(booking.osmBookingId)}</span> \u00b7 <span class="ba-booking-name">${escapeHtml(booking.customerName)}</span></div>
        <div class="ba-booking-dates">${datesHtml}</div>
        <div><span class="ba-booking-status ${statusClass}">${escapeHtml(booking.status)}</span></div>
        ${isSuggested ? '<div style="font-size:11px;color:#666;margin-top:4px">Possible match</div>' : ''}
      </div>
    `;
  }
})();
