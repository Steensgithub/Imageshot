// Opened from the context menu ("All tabs") when host access has not been granted yet.
// The click on "Allow" is the user gesture that chrome.permissions.request requires.
(() => {
  const $ = id => document.getElementById(id);
  const tabId = Number(new URLSearchParams(location.search).get('tabId')) || undefined;

  $('cancel').addEventListener('click', () => window.close());
  $('grant').addEventListener('click', () => {
    chrome.runtime.sendMessage({ type: 'armTabs', tabId });
    chrome.permissions.request(SKB_ALL_URLS).then(granted => {
      if (granted) {
        chrome.runtime.sendMessage({ type: 'runPendingTabs', tabId }).finally(() => window.close());
      } else {
        chrome.runtime.sendMessage({ type: 'disarmTabs' });
        $('denied').hidden = false;
      }
    }, () => { $('denied').hidden = false; });
  });
  $('grant').focus();
})();
