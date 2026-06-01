document.addEventListener('DOMContentLoaded', () => {
  const input = document.getElementById('backendUrl');
  const tokenInput = document.getElementById('apiToken');
  const status = document.getElementById('status');

  // Load saved values
  chrome.storage.sync.get(['backendUrl', 'apiToken'], (result) => {
    if (result.backendUrl) input.value = result.backendUrl;
    if (result.apiToken) tokenInput.value = result.apiToken;
  });

  document.getElementById('save').addEventListener('click', () => {
    const url = input.value.trim().replace(/\/$/, ''); // strip trailing slash
    if (!url.startsWith('http')) {
      status.textContent = 'URL must start with http:// or https://';
      status.style.color = 'red';
      return;
    }
    chrome.storage.sync.set({ backendUrl: url, apiToken: tokenInput.value.trim() }, () => {
      status.textContent = 'Saved!';
      status.style.color = 'green';
      setTimeout(() => status.textContent = '', 2000);
    });
  });
});
