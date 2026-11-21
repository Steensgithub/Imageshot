// Applies the chosen look to an extension page: "vivid" sets <html data-theme="vivid"> (styled in ui.css),
// "standard" removes it so the page follows the browser's light/dark setting. Updates live when the setting changes.
(() => {
  const apply = look => {
    if (look === 'vivid') document.documentElement.dataset.theme = 'vivid';
    else delete document.documentElement.dataset.theme;
  };
  chrome.storage.sync.get({ look: SKB_DEFAULTS.look }).then(s => apply(s.look), () => {});
  chrome.storage.onChanged.addListener((changes, area) => {
    if (area === 'sync' && changes.look) apply(changes.look.newValue);
  });
})();
