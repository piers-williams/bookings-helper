document.addEventListener('DOMContentLoaded', () => {
  const input = document.getElementById('backendUrl');
  const status = document.getElementById('status');

  // Load saved value
  chrome.storage.sync.get(['backendUrl'], (result) => {
    if (result.backendUrl) input.value = result.backendUrl;
  });

  document.getElementById('save').addEventListener('click', () => {
    const url = input.value.trim().replace(/\/$/, ''); // strip trailing slash
    if (!url.startsWith('http')) {
      status.textContent = 'URL must start with http:// or https://';
      status.style.color = 'red';
      return;
    }
    chrome.storage.sync.set({ backendUrl: url }, () => {
      status.textContent = 'Saved!';
      status.style.color = 'green';
      setTimeout(() => status.textContent = '', 2000);
    });
  });
});
