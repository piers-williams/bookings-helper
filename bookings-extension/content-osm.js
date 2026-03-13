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

  // --- Date annotation ---

  const MONTHS = {
    jan: 0, january: 0, feb: 1, february: 1, mar: 2, march: 2,
    apr: 3, april: 3, may: 4, jun: 5, june: 5, jul: 6, july: 6,
    aug: 7, august: 7, sep: 8, september: 8, oct: 9, october: 9,
    nov: 10, november: 10, dec: 11, december: 11
  };

  const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

  // Essex school holidays 2025-2026 (same as panel.js)
  const ESSEX_HOLIDAYS = [
    { name: 'Autumn half term',       start: new Date(2025,  9, 27), end: new Date(2025,  9, 31) },
    { name: 'Christmas holiday',      start: new Date(2025, 11, 22), end: new Date(2026,  0,  2) },
    { name: 'Spring half term',       start: new Date(2026,  1, 16), end: new Date(2026,  1, 20) },
    { name: 'Easter holiday',         start: new Date(2026,  2, 30), end: new Date(2026,  3, 10) },
    { name: 'Early May bank holiday', start: new Date(2026,  4,  4), end: new Date(2026,  4,  4) },
    { name: 'Summer half term',       start: new Date(2026,  4, 25), end: new Date(2026,  4, 29) },
    { name: 'Summer holiday',         start: new Date(2026,  6, 21), end: new Date(2026,  7, 31) },
  ];

  function getHolidayForDate(date) {
    const d = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    for (const h of ESSEX_HOLIDAYS) {
      if (d >= h.start && d <= h.end) return h;
    }
    return null;
  }

  // Matches: DD/MM/YYYY, DD-MM-YYYY, DD Month YYYY, DD Mon YYYY
  const DATE_RE = /\b(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})\b|\b(\d{1,2})\s+(Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|June?|July?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+(\d{4})\b/gi;

  const MARKER = 'ba-date-annotated';

  function injectStyles() {
    if (document.getElementById('ba-date-styles')) return;
    const style = document.createElement('style');
    style.id = 'ba-date-styles';
    style.textContent = `
      .ba-weekday {
        display: inline-block;
        font-size: 0.85em;
        font-weight: 600;
        color: #7a4500;
        background: #fff4ce;
        border-radius: 3px;
        padding: 0 3px;
        margin-left: 2px;
        vertical-align: baseline;
        line-height: inherit;
      }
      .ba-weekday[data-holiday]::after {
        content: ' \u00b7 ' attr(data-holiday);
        font-weight: 400;
      }
      .ba-weekday.ba-friday {
        color: #7a4500;
        background: #fff9c4;
      }
      .ba-weekday.ba-weekend {
        color: #107c10;
        background: #dff6dd;
      }
    `;
    document.head.appendChild(style);
  }

  function parseDate(match) {
    // match groups: 1,2,3 = numeric DD/MM/YYYY; 4,5,6 = DD Month YYYY
    const day   = match[1] || match[4];
    const month = match[2] ? parseInt(match[2], 10) - 1 : MONTHS[match[5].toLowerCase()];
    const year  = match[3] || match[6];
    if (month === undefined) return null;
    const d = new Date(parseInt(year, 10), month, parseInt(day, 10));
    // Validate the date is real (e.g. not 31 Feb)
    if (d.getDate() !== parseInt(day, 10)) return null;
    return d;
  }

  function buildBadge(date) {
    const dow = WEEKDAYS[date.getDay()];
    const span = document.createElement('span');
    span.className = 'ba-weekday';
    const day = date.getDay();
    if (day === 0 || day === 6) {
      span.classList.add('ba-weekend');
    } else if (day === 5) {
      span.classList.add('ba-friday');
    }
    span.textContent = dow;
    const holiday = getHolidayForDate(date);
    if (holiday) {
      span.setAttribute('data-holiday', holiday.name);
    }
    return span;
  }

  function annotateTextNode(textNode) {
    const text = textNode.nodeValue;
    DATE_RE.lastIndex = 0;
    const matches = [];
    let m;
    while ((m = DATE_RE.exec(text)) !== null) {
      const date = parseDate(m);
      if (date) matches.push({ index: m.index, length: m[0].length, date });
    }
    if (matches.length === 0) return;

    // Build a document fragment replacing the text node.
    // Wrap each date in a <span class="ba-date-done"> so re-scans skip it.
    const frag = document.createDocumentFragment();
    let cursor = 0;
    for (const { index, length, date } of matches) {
      if (index > cursor) {
        frag.appendChild(document.createTextNode(text.slice(cursor, index)));
      }
      const wrapper = document.createElement('span');
      wrapper.className = MARKER;
      wrapper.appendChild(document.createTextNode(text.slice(index, index + length)));
      wrapper.appendChild(buildBadge(date));
      frag.appendChild(wrapper);
      cursor = index + length;
    }
    if (cursor < text.length) {
      frag.appendChild(document.createTextNode(text.slice(cursor)));
    }
    textNode.parentNode.replaceChild(frag, textNode);
  }

  function annotateDates(root) {
    injectStyles();
    // Walk text nodes, skip scripts/styles/inputs and already-annotated spans
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
      acceptNode(node) {
        const parent = node.parentElement;
        if (!parent) return NodeFilter.FILTER_REJECT;
        const tag = parent.tagName;
        if (tag === 'SCRIPT' || tag === 'STYLE' || tag === 'TEXTAREA' || tag === 'INPUT') {
          return NodeFilter.FILTER_REJECT;
        }
        // Skip text inside an already-processed date wrapper or badge
        if (parent.closest('.' + MARKER) || parent.classList.contains('ba-weekday')) {
          return NodeFilter.FILTER_REJECT;
        }
        return NodeFilter.FILTER_ACCEPT;
      }
    });

    const textNodes = [];
    while (walker.nextNode()) textNodes.push(walker.currentNode);
    // Process in reverse so DOM indices stay valid
    for (let i = textNodes.length - 1; i >= 0; i--) {
      annotateTextNode(textNodes[i]);
    }

    // Annotate form inputs whose value contains a date.
    // Badge is inserted after the input, not inside it, so the form stays intact.
    annotateInputs(root);
  }

  const INPUT_MARKER = 'baInputAnnotated';

  function annotateInputs(root) {
    root.querySelectorAll('input').forEach(input => {
      if (input.dataset[INPUT_MARKER]) return;
      if (input.type === 'hidden') return;
      const val = input.value;
      if (!val) return;

      DATE_RE.lastIndex = 0;
      const m = DATE_RE.exec(val);
      if (!m) return;
      const date = parseDate(m);
      if (!date) return;

      input.dataset[INPUT_MARKER] = '1';
      const badge = buildBadge(date);
      input.parentNode.insertBefore(badge, input.nextSibling);
    });
  }

  let annotatePending = false;
  function scheduleAnnotation() {
    if (annotatePending) return;
    annotatePending = true;
    requestAnimationFrame(() => {
      annotatePending = false;
      annotateDates(document.body);
    });
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
      // Annotate any new dates that appeared in the DOM
      scheduleAnnotation();
    });

    observer.observe(document.body, { childList: true, subtree: true });
  }

  // --- Init ---

  function init() {
    startObserver();
    setTimeout(checkForBookingChange, 1000);
    // Initial annotation pass
    annotateDates(document.body);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
