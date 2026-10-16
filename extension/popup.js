(async () => {
  const $ = id => document.getElementById(id);

  // Capture buttons are wired first and synchronously: "All tabs" must call
  // chrome.permissions.request directly inside the click (user gesture).
  for (const btn of document.querySelectorAll('.cap')) {
    btn.addEventListener('click', () => {
      if (btn.dataset.mode === 'tabs') requestAllTabs();
      else chrome.runtime.sendMessage({ type: 'capture', mode: btn.dataset.mode }).finally(() => window.close());
    });
  }

  function requestAllTabs() {
    // Arm first: if the permission prompt closes the popup, the service worker
    // starts the capture itself from permissions.onAdded.
    chrome.runtime.sendMessage({ type: 'armTabs' });
    chrome.permissions.request(SKB_ALL_URLS).then(granted => {
      if (granted) {
        chrome.runtime.sendMessage({ type: 'runPendingTabs' }).finally(() => window.close());
      } else {
        chrome.runtime.sendMessage({ type: 'disarmTabs' });
        $('permNote').hidden = false;
      }
    }, () => { $('permNote').hidden = false; });
  }

  const s = await skbGetSettings();

  const res = $('resolution');
  const fields = ['format', 'afterCapture', 'allTabsScope'];

  res.value = s.resolution;
  for (const f of fields) $(f).value = s[f];
  $('autoClose').checked = !!s.autoClose;
  $('folder').value = s.folder;

  function refresh() {
    const cur = { resolution: Number(res.value), format: $('format').value };
    const q = skbQuality(cur);
    $('resLabel').textContent = `${q.label} (${cur.resolution})`;
    $('resDetail').textContent = q.detail;
    res.style.setProperty('--fill', cur.resolution + '%');
  }
  refresh();

  res.addEventListener('input', refresh);
  res.addEventListener('change', () => skbSaveSettings({ resolution: Number(res.value) }));
  for (const f of fields) {
    $(f).addEventListener('change', () => { skbSaveSettings({ [f]: $(f).value }); refresh(); });
  }
  $('autoClose').addEventListener('change', e => skbSaveSettings({ autoClose: e.target.checked }));

  let t;
  $('folder').addEventListener('input', e => {
    clearTimeout(t);
    t = setTimeout(() => skbSaveSettings({ folder: e.target.value.trim() || SKB_DEFAULTS.folder }), 300);
  });
  $('folder').addEventListener('blur', e => {
    if (!e.target.value.trim()) e.target.value = SKB_DEFAULTS.folder;
    skbSaveSettings({ folder: e.target.value.trim() });
  });
})();
