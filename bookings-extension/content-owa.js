(function () {
  'use strict';

  let lastEmailKey = null;
  let debounceTimer = null;

  // --- Email extraction from OWA DOM ---
  // Selectors verified against outlook.cloud.microsoft (Feb 2026).
  // IDs like CONV_xxx_SUBJECT and MSG_xxx_FROM are dynamic but the suffix is stable.

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
    chrome.runtime.sendMessage({ type: 'CAPTURE_EMAIL', payload: email }).catch(() => {});
  }

  // --- Message listener (refresh triggered from panel) ---

  chrome.runtime.onMessage.addListener((message) => {
    if (message.type === 'REFRESH') {
      lastEmailKey = null;
      checkForEmailChange();
    }
    return false;
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
