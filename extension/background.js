importScripts('settings.js', 'store.js');

const MAX_PAGE_HEIGHT = 30000;          // pixels – limit for "Full page"
const CAPTURE_MAX_AGE = 60 * 60 * 1000; // stored captures older than this are purged on startup
const WIN_KEY = 'clipWin:';             // storage.session: clipboard window id -> capture id

const MENU = [
  { id: 'visible', title: 'modeVisible' },
  { id: 'region',  title: 'modeRegion' },
  { id: 'full',    title: 'modeFull' },
  { id: 'tabs',    title: 'modeTabs' }
];

function buildMenus() {
  chrome.contextMenus.removeAll(() => {
    const contexts = ['page', 'frame', 'selection', 'link', 'image', 'video', 'audio', 'editable'];
    chrome.contextMenus.create({ id: 'skb-root', title: skbT('menuRoot'), contexts });
    for (const m of MENU) {
      chrome.contextMenus.create({ id: 'skb-' + m.id, parentId: 'skb-root', title: skbT(m.title), contexts });
    }
  });
}

function purgeOld() {
  return skbStore.purgeOlderThan(CAPTURE_MAX_AGE).catch(e => console.warn('ImageShot: purge failed', e));
}

chrome.runtime.onInstalled.addListener(buildMenus);
chrome.runtime.onStartup.addListener(buildMenus);

// The clipboard window is gone: its capture is no longer needed.
chrome.windows.onRemoved.addListener(async windowId => {
  const key = WIN_KEY + windowId;
  const { [key]: id } = await chrome.storage.session.get(key);
  if (!id) return;
  await chrome.storage.session.remove(key);
  await skbStore.remove(id).catch(() => {});
});

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  if (typeof info.menuItemId !== 'string' || !info.menuItemId.startsWith('skb-') || info.menuItemId === 'skb-root') return;
  const mode = info.menuItemId.slice(4);
  // "All tabs" needs host access to every tab. A service worker cannot ask for it,
  // so a small extension page asks the user (and gets the required user gesture).
  if (mode === 'tabs' && !(await chrome.permissions.contains(SKB_ALL_URLS))) {
    openPermissionWindow(tab);
    return;
  }
  run(mode, tab);
});

chrome.commands.onCommand.addListener(async (command, tab) => {
  const mode = { 'capture-visible': 'visible', 'capture-region': 'region' }[command];
  if (mode) run(mode, tab || await activeTab());
});

// "All tabs" waiting for the permission prompt. It is started either by the page that asked
// (popup / permission window) or by permissions.onAdded – whichever comes first.
let pendingTabs = null;

function armTabs(tabId) {
  pendingTabs = (async () => ({
    at: Date.now(),
    tab: tabId ? await chrome.tabs.get(tabId).catch(() => null) : await activeTab()
  }))();
}

async function runPendingTabs(delay) {
  const p = pendingTabs;
  if (!p) return;
  pendingTabs = null;
  const { at, tab } = await p;
  if (!tab || Date.now() - at > 5 * 60 * 1000) return;
  if (!(await chrome.permissions.contains(SKB_ALL_URLS))) return;
  setTimeout(() => run('tabs', tab), delay);
}

chrome.permissions.onAdded.addListener(perms => {
  if (perms.origins?.includes('<all_urls>')) runPendingTabs(400);
});

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (msg?.type === 'capture') {
    sendResponse({ ok: true });
    // Short pause so the popup is closed before capturing.
    setTimeout(async () => run(msg.mode, await activeTab()), 150);
    return false;
  }
  if (msg?.type === 'armTabs') {
    armTabs(msg.tabId);
    sendResponse({ ok: true });
    return false;
  }
  if (msg?.type === 'runPendingTabs') {
    sendResponse({ ok: true });
    runPendingTabs(msg.tabId ? 400 : 150);
    return false;
  }
  if (msg?.type === 'disarmTabs') {
    pendingTabs = null;
    return false;
  }
  return false;
});

async function activeTab() {
  const [tab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
  return tab;
}

const sleep = ms => new Promise(r => setTimeout(r, ms));

// captureVisibleTab may be called at most about twice per second.
let lastGrab = 0;
async function grab(windowId) {
  const wait = lastGrab + 550 - Date.now();
  if (wait > 0) await sleep(wait);
  lastGrab = Date.now();
  return chrome.tabs.captureVisibleTab(windowId, { format: 'png' });
}

// Stop if the user switches tabs in the middle of a long capture.
async function ensureActive(tab) {
  const t = await chrome.tabs.get(tab.id);
  if (!t.active) throw new Error(skbT('errTabSwitched'));
}

async function dataUrlToBlob(url) {
  return (await fetch(url)).blob();
}

// ---------- Main flow ----------

async function run(mode, tab) {
  if (!tab) return;
  const settings = await skbGetSettings();
  const errors = [];
  try {
    let shots = [];
    if (mode === 'visible') shots = [await captureVisible(tab)];
    else if (mode === 'region') shots = [await captureRegion(tab)];
    else if (mode === 'full') shots = [await captureFull(tab)];
    else if (mode === 'tabs') shots = await captureTabs(tab, settings, errors);
    shots = shots.filter(Boolean);
    if (!shots.length) {
      if (errors.length) flagError(skbT('errNoTabs'));
      return;
    }

    const q = skbQuality(settings);
    const stamp = skbStamp();
    const images = [];
    for (let i = 0; i < shots.length; i++) {
      const base = shots.length > 1
        ? `${SKB_BRAND} ${stamp} (${i + 1}) ${skbClean(shots[i].title).slice(0, 40)}`
        : `${SKB_BRAND} ${stamp}`;
      images.push(await encode(shots[i], q, base));
    }
    await deliver(images, settings, errors);
  } catch (e) {
    console.error(e);
    flagError(friendlyError(e));
  }
}

function friendlyError(e) {
  const m = String(e?.message || e);
  if (/cannot be scripted|Cannot access|chrome:\/\/|extensions gallery|webstore|permission is required/i.test(m)) {
    return skbT('errProtected');
  }
  return skbT('errFailed', m);
}

function flagError(message) {
  chrome.action.setBadgeBackgroundColor({ color: '#c0392b' });
  chrome.action.setBadgeText({ text: '!' });
  chrome.action.setTitle({ title: `${SKB_BRAND} – ${message}` });
  setTimeout(() => {
    chrome.action.setBadgeText({ text: '' });
    chrome.action.setTitle({ title: SKB_BRAND });
  }, 8000);
}

function flagOk() {
  chrome.action.setBadgeBackgroundColor({ color: '#1F6FEB' });
  chrome.action.setBadgeText({ text: '✓' });
  setTimeout(() => chrome.action.setBadgeText({ text: '' }), 2500);
}

async function openPermissionWindow(tab) {
  const width = 460, height = 340;
  const pos = await centeredOn(width, height);
  const url = chrome.runtime.getURL(`permission.html?tabId=${tab?.id ?? ''}`);
  await chrome.windows.create({ url, type: 'popup', width, height, ...pos, focused: true });
}

async function centeredOn(width, height) {
  try {
    const cur = await chrome.windows.getLastFocused();
    return {
      left: Math.max(0, Math.round(cur.left + (cur.width - width) / 2)),
      top: Math.max(0, Math.round(cur.top + (cur.height - height) / 2))
    };
  } catch (e) {
    return {}; // default placement
  }
}

// ---------- Capture modes ----------

async function captureVisible(tab) {
  const url = await grab(tab.windowId);
  return { image: await createImageBitmap(await dataUrlToBlob(url)), title: tab.title };
}

async function captureRegion(tab) {
  const target = { tabId: tab.id };
  const [res] = await chrome.scripting.executeScript({ target, func: selectRegion, args: [skbT('regionHint')] });
  const r = res?.result;
  if (!r) return null;
  try {
    // The selection is in content coordinates: content row c is shown in the window at c - scrollTop + boxTop.
    const visStart = r.scrollTop + (r.vTop - r.boxTop);
    const visEnd = visStart + (r.vBot - r.vTop);
    if (r.y0 >= visStart - 0.5 && r.y1 <= visEnd + 0.5) {
      // Everything is visible at once: a single image.
      const blob = await dataUrlToBlob(await grab(tab.windowId));
      const whole = await createImageBitmap(blob);
      const k = whole.width / r.vw;
      const vy = r.y0 - r.scrollTop + r.boxTop;
      const sx = Math.max(0, Math.round(r.x * k));
      const sy = Math.max(0, Math.round(vy * k));
      const sw = Math.min(whole.width - sx, Math.round(r.w * k));
      const sh = Math.min(whole.height - sy, Math.round((r.y1 - r.y0) * k));
      whole.close();
      return { image: await createImageBitmap(blob, sx, sy, sw, sh), title: tab.title };
    }
    return await captureRegionScrolled(tab, target, r);
  } finally {
    await chrome.scripting.executeScript({ target, func: restorePage, args: [r.restore] }).catch(() => {});
  }
}

// The selection is taller than the window: scroll through it and stitch the pieces together.
async function captureRegionScrolled(tab, target, r) {
  const off = r.vTop - r.boxTop;   // content row at the top of the visible area = scrollTop + off
  const visH = r.vBot - r.vTop;
  const inset = r.inset || 0;      // fixed top bar / sticky header at the top of the area
  const H = r.y1 - r.y0;
  let canvas = null, ctx = null, k = 1, pos = r.y0, prev = null;
  for (let i = 0; i < 400 && pos < r.y1 - 0.5; i++) {
    // Place the next piece just below a fixed top bar, if any.
    const want = Math.max(0, pos - off - inset);
    const [m] = await chrome.scripting.executeScript({
      target, func: r.kind === 'box' ? scrollBoxStep : scrollStep, args: [want, i]
    });
    const actual = m.result;
    if (actual == null) break;
    if (prev !== null && actual <= prev && i > 0 && actual + off + visH <= pos) break; // cannot scroll any further
    prev = actual;
    await sleep(i === 0 ? 150 : 250);
    await ensureActive(tab);
    const bmp = await createImageBitmap(await dataUrlToBlob(await grab(tab.windowId)));
    if (!canvas) {
      k = bmp.width / r.vw;
      canvas = new OffscreenCanvas(Math.max(1, Math.round(r.w * k)), Math.max(1, Math.min(Math.round(H * k), MAX_PAGE_HEIGHT)));
      ctx = canvas.getContext('2d');
    }
    // At the top of the page the top bar is part of the content; further down it covers the content.
    const top = actual + off + (actual > 0.5 ? inset : 0);
    const s0 = Math.max(pos, top), s1 = Math.min(r.y1, actual + off + visH);
    if (s1 > s0 + 0.5) {
      const sy = Math.round((s0 - actual + r.boxTop) * k);
      const sh = Math.round((s1 - actual + r.boxTop) * k) - sy;
      const dy = Math.round((s0 - r.y0) * k);
      const dh = Math.round((s1 - r.y0) * k) - dy;   // same rounding as the next piece: no seams
      ctx.drawImage(bmp, Math.round(r.x * k), sy, canvas.width, sh, 0, dy, canvas.width, dh);
      if (dy + dh >= canvas.height) { bmp.close(); break; }
      pos = s1;
    } else {
      bmp.close();
      break;
    }
    bmp.close();
  }
  return canvas ? { image: canvas, title: tab.title } : null;
}

async function captureFull(tab) {
  const target = { tabId: tab.id };
  const [info] = await chrome.scripting.executeScript({ target, func: pageInfo });
  const p = info.result;
  let canvas = null, ctx = null, k = 1;

  try {
    // Web apps (Gmail, Saxo, Notion …) scroll inside a box instead of the window.
    if (p.box) return await captureBox(tab, target, p);

    let prevY = -1;
    for (let y = 0, i = 0; y < p.h; y += p.vh, i++) {
      const [moved] = await chrome.scripting.executeScript({ target, func: scrollStep, args: [y, i] });
      const actualY = moved.result;
      if (i > 0 && actualY <= prevY) break; // the page cannot scroll any further
      prevY = actualY;
      await sleep(120);
      await ensureActive(tab);
      const bmp = await createImageBitmap(await dataUrlToBlob(await grab(tab.windowId)));
      if (!canvas) {
        k = bmp.width / p.vw;
        canvas = new OffscreenCanvas(bmp.width, Math.min(Math.round(p.h * k), MAX_PAGE_HEIGHT));
        ctx = canvas.getContext('2d');
      }
      ctx.drawImage(bmp, 0, Math.round(actualY * k));
      bmp.close();
      if ((actualY + p.vh) * k >= canvas.height || actualY + p.vh >= p.h) break;
    }
  } finally {
    await chrome.scripting.executeScript({ target, func: restorePage, args: [p] }).catch(() => {});
  }
  return canvas ? { image: canvas, title: tab.title } : null;
}

// Scrolls through the box and stitches the images so the page is "unrolled":
// everything above the box, the box's entire content and everything below it.
async function captureBox(tab, target, p) {
  const b = p.box;
  const extra = b.sh - b.ch;            // how far the box can scroll
  let canvas = null, ctx = null, k = 1, last = null;

  try {
    let prev = -1;
    for (let top = 0, i = 0; i < 400; i++) {
      const [moved] = await chrome.scripting.executeScript({ target, func: scrollBoxStep, args: [top, i] });
      const actual = moved.result;
      if (actual == null) break;
      await sleep(i === 0 ? 200 : 300); // give the web app time to paint its content
      await ensureActive(tab);
      const bmp = await createImageBitmap(await dataUrlToBlob(await grab(tab.windowId)));

      if (!canvas) {
        k = bmp.width / p.vw;
        canvas = new OffscreenCanvas(bmp.width, Math.min(Math.round((p.vh + extra) * k), MAX_PAGE_HEIGHT));
        ctx = canvas.getContext('2d');
        ctx.fillStyle = b.bg;
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        // First image: everything from the top to the bottom of the box (what is below the box is taken last).
        const keep = Math.min(bmp.height, Math.round((b.y + b.vis) * k));
        ctx.drawImage(bmp, 0, 0, bmp.width, keep, 0, 0, bmp.width, keep);
      } else {
        // Only the visible part of the box, placed according to the scroll position.
        const sx = Math.round(b.x * k), sy = Math.round(b.y * k);
        const sw = Math.round((b.x + b.w) * k) - sx, sh = Math.round((b.y + b.vis) * k) - sy;
        const dy = Math.round((b.y + actual) * k), dh = Math.round((b.y + actual + b.vis) * k) - dy;
        ctx.drawImage(bmp, sx, sy, sw, sh, sx, dy, sw, dh);
      }
      if (last) last.close();
      last = bmp;
      const done = actual >= extra - 1 ||                 // reached the bottom
                   (i > 0 && actual <= prev + 0.5) ||     // the box no longer moves
                   (b.y + actual + b.vis) * k >= canvas.height;
      prev = actual;
      top = actual + b.vis;
      if (done) {
        // Whatever is below the box (e.g. a footer) is taken from the last image.
        const below = p.vh - (b.y + b.vis);
        if (below > 0) {
          const y0 = Math.round((b.y + b.vis) * k), hh = Math.round(below * k);
          ctx.drawImage(last, 0, y0, last.width, hh, 0, Math.round((b.y + b.vis + actual) * k), last.width, hh);
        }
        break;
      }
    }
  } finally {
    if (last) last.close();
  }
  return canvas ? { image: canvas, title: tab.title } : null;
}

async function captureTabs(startTab, settings, errors) {
  const query = settings.allTabsScope === 'all' ? { windowType: 'normal' } : { windowId: startTab.windowId };
  const tabs = await chrome.tabs.query(query);
  const originalActive = new Map();
  for (const t of tabs) if (t.active) originalActive.set(t.windowId, t.id);
  const focusedWindow = startTab.windowId;
  const shots = [];
  await chrome.windows.update(focusedWindow, { focused: true }).catch(() => {});

  for (const t of tabs) {
    try {
      if (settings.allTabsScope === 'all') await chrome.windows.update(t.windowId, { focused: true });
      await chrome.tabs.update(t.id, { active: true });
      await waitForComplete(t.id, 4000);
      await sleep(350);
      const url = await grab(t.windowId);
      shots.push({ image: await createImageBitmap(await dataUrlToBlob(url)), title: t.title });
    } catch (e) {
      errors.push(t.title || t.url || skbT('unknownTab'));
    }
  }

  for (const [winId, tabId] of originalActive) {
    await chrome.tabs.update(tabId, { active: true }).catch(() => {});
  }
  await chrome.windows.update(focusedWindow, { focused: true }).catch(() => {});
  return shots;
}

async function waitForComplete(tabId, timeout) {
  const end = Date.now() + timeout;
  while (Date.now() < end) {
    const t = await chrome.tabs.get(tabId);
    if (t.status === 'complete') return;
    await sleep(150);
  }
}

// ---------- Encoding and delivery ----------

// Returns { blob, mime, ext, name, width, height, size, title } – the record format kept in IndexedDB.
async function encode(shot, q, baseName) {
  const src = shot.image;
  const w = Math.max(1, Math.round(src.width * q.scale));
  const h = Math.max(1, Math.round(src.height * q.scale));
  const c = new OffscreenCanvas(w, h);
  const ctx = c.getContext('2d');
  if (q.mime === 'image/jpeg') {
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, w, h);
  }
  ctx.imageSmoothingEnabled = true;
  ctx.imageSmoothingQuality = 'high';
  ctx.drawImage(src, 0, 0, w, h);
  if (src.close) src.close();
  // WebP is limited to 16383 px per side – very long pages are saved as PNG.
  const mime = q.mime === 'image/webp' && (w > 16383 || h > 16383) ? 'image/png' : q.mime;
  const blob = await c.convertToBlob({ type: mime, quality: q.quality });
  return {
    blob,
    mime: blob.type,
    name: baseName,
    ext: blob.type === 'image/png' ? 'png' : blob.type === 'image/jpeg' ? 'jpg' : blob.type === 'image/webp' ? 'webp' : q.ext,
    width: w,
    height: h,
    size: blob.size,
    title: shot.title || ''
  };
}

// Service workers cannot hand blob: URLs to chrome.downloads, so "save directly" uses data URLs.
async function blobToDataUrl(blob) {
  const bytes = new Uint8Array(await blob.arrayBuffer());
  let bin = '';
  const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) {
    bin += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
  }
  return `data:${blob.type};base64,${btoa(bin)}`;
}

async function deliver(images, settings, errors) {
  if (settings.afterCapture === 'save') {
    for (const img of images) {
      await chrome.downloads.download({
        url: await blobToDataUrl(img.blob),
        filename: skbFilePath(settings.folder, img.name, img.ext),
        conflictAction: 'uniquify',
        saveAs: false
      });
    }
    if (errors.length) flagError(skbT('errTabsSome', errors.length));
    else flagOk();
    return;
  }

  const id = crypto.randomUUID();
  await skbStore.put({ id, createdAt: Date.now(), errors, images });

  // "Copy directly" only works with one image – otherwise the clipboard window is shown.
  const auto = settings.afterCapture === 'copy' && images.length === 1;
  try {
    await openClipboardWindow(id, auto);
  } catch (e) {
    await skbStore.remove(id).catch(() => {});
    throw e;
  }
}

async function openClipboardWindow(id, autoCopy) {
  const width = 780, height = 660;
  const pos = await centeredOn(width, height);
  const url = chrome.runtime.getURL(`clipboard.html?id=${id}${autoCopy ? '&auto=copy' : ''}`);
  const win = await chrome.windows.create({ url, type: 'popup', width, height, ...pos, focused: true });
  // Lets windows.onRemoved delete the capture even if the page could not do it itself.
  await chrome.storage.session.set({ [WIN_KEY + win.id]: id });
}

// ---------- Functions injected into the page ----------

function selectRegion(hintText) {
  if (window.__skbSelecting) return null;
  window.__skbSelecting = true;
  return new Promise(resolve => {
    const de = document.documentElement;
    const root = document.createElement('div');
    root.style.cssText = 'position:fixed;inset:0;z-index:2147483647;cursor:crosshair;background:rgba(0,0,0,.35);user-select:none;touch-action:none;';
    const box = document.createElement('div');
    box.style.cssText = 'position:fixed;display:none;pointer-events:none;border:2px solid #fff;outline:1px solid #1F6FEB;box-shadow:0 0 0 100vmax rgba(0,0,0,.35);';
    const size = document.createElement('div');
    size.style.cssText = 'position:fixed;display:none;pointer-events:none;background:#1F6FEB;color:#fff;font:600 11px system-ui,sans-serif;padding:3px 7px;border-radius:6px;';
    const hint = document.createElement('div');
    hint.textContent = hintText;
    hint.style.cssText = 'position:fixed;top:16px;left:50%;transform:translateX(-50%);pointer-events:none;background:#1F6FEB;color:#fff;font:600 13px system-ui,sans-serif;padding:8px 16px;border-radius:999px;box-shadow:0 4px 14px rgba(0,0,0,.25);white-space:nowrap;';
    root.append(box, size, hint);
    de.appendChild(root);

    // Remember what is changed so it can be restored.
    const restore = {
      x: window.scrollX, y: window.scrollY,
      behavior: de.style.scrollBehavior || '',
      snap: de.style.scrollSnapType || '',
      bodySnap: document.body ? (document.body.style.scrollSnapType || '') : '',
      box: null
    };

    const isScroller = el => {
      const oy = getComputedStyle(el).overflowY;
      return (oy === 'auto' || oy === 'scroll' || oy === 'overlay') && el.scrollHeight - el.clientHeight > 2;
    };
    const windowScrolls = () => {
      // Scroll snapping would pull the page back and make it look non-scrollable.
      de.style.scrollSnapType = 'none';
      if (document.body) document.body.style.scrollSnapType = 'none';
      const y0 = window.scrollY;
      const go = top => window.scrollTo({ left: window.scrollX, top, behavior: 'instant' });
      go(y0 + 1);
      let moves = window.scrollY !== y0;
      if (!moves && y0 > 0) { go(y0 - 1); moves = window.scrollY !== y0; }
      go(y0);
      return moves;
    };

    // What should scroll? A large scroll box under the mouse (web apps) or the window itself.
    let sc = null;
    function pickScroller(cx, cy) {
      let el = document.elementsFromPoint(cx, cy).find(e => !root.contains(e) && e !== root);
      let found = null;
      for (; el && el !== de; el = el.parentElement) if (isScroller(el)) { found = el; break; }
      const winOk = windowScrolls();
      if (found && (found.clientHeight >= window.innerHeight * 0.4 || !winOk)) {
        de.style.scrollSnapType = restore.snap;
        if (document.body && found !== document.body) document.body.style.scrollSnapType = restore.bodySnap;
        found.setAttribute('data-skb-scroll', '1');
        restore.box = {
          scrollTop: found.scrollTop,
          behavior: found.style.scrollBehavior || '',
          anchor: found.style.overflowAnchor || '',
          snap: found === document.body ? restore.bodySnap : (found.style.scrollSnapType || '')
        };
        found.style.scrollSnapType = 'none';
        found.style.scrollBehavior = 'auto';
        return { kind: 'box', el: found, orig: found.scrollTop };
      }
      if (winOk) {
        de.style.scrollBehavior = 'auto';
        return { kind: 'window', el: null, orig: window.scrollY };
      }
      return { kind: 'none', el: null };
    }
    // Release a previously chosen box (e.g. picked with the mouse wheel over another area).
    function releaseScroller() {
      if (sc && sc.el) {
        sc.el.style.scrollSnapType = restore.box ? restore.box.snap : '';
        sc.el.style.scrollBehavior = restore.box ? restore.box.behavior : '';
        sc.el.removeAttribute('data-skb-scroll');
      }
      restore.box = null;
      sc = null;
    }
    // Height of fixed/sticky elements at the top of the area (top bars, table headers).
    function topInset(g, x0, x1) {
      let inset = 0;
      const limit = g.vTop + (g.vBot - g.vTop) * 0.4;
      const scope = sc.kind === 'box' ? sc.el.querySelectorAll('*') : document.querySelectorAll('body *');
      for (const el of scope) {
        if (root.contains(el)) continue;
        const pos = getComputedStyle(el).position;
        if (pos !== 'fixed' && pos !== 'sticky') continue;
        const r = el.getBoundingClientRect();
        if (!r.width || !r.height || r.top > g.vTop + 1 || r.bottom <= g.vTop || r.bottom > limit) continue;
        if (r.right <= x0 || r.left >= x1) continue;
        inset = Math.max(inset, r.bottom - g.vTop);
      }
      return inset;
    }
    function geom() {
      if (sc.kind === 'box') {
        const r = sc.el.getBoundingClientRect();
        const boxTop = r.top + sc.el.clientTop;
        return { boxTop, vTop: Math.max(0, boxTop), vBot: Math.min(window.innerHeight, boxTop + sc.el.clientHeight) };
      }
      return { boxTop: 0, vTop: 0, vBot: window.innerHeight };
    }
    const scrollTop = () => sc.kind === 'box' ? sc.el.scrollTop : sc.kind === 'window' ? window.scrollY : 0;
    const scrollBy = v => {
      if (sc.kind === 'box') sc.el.scrollTop += v;
      else if (sc.kind === 'window') window.scrollTo({ left: window.scrollX, top: window.scrollY + v, behavior: 'instant' });
    };

    let dragging = false, moved = false, sx = 0, startC = 0, lastX = 0, lastY = 0, startY = 0, edgeSince = 0;
    const toContent = (cy, g) => Math.max(g.vTop, Math.min(g.vBot, cy)) - g.boxTop + scrollTop();
    const rectNow = () => {
      const g = geom();
      const endC = toContent(lastY, g);
      const x2 = Math.max(0, Math.min(window.innerWidth, lastX));
      return { g, x: Math.min(sx, x2), w: Math.abs(x2 - sx), y0: Math.min(startC, endC), y1: Math.max(startC, endC) };
    };
    const draw = () => {
      const r = rectNow();
      const top = r.y0 - scrollTop() + r.g.boxTop;
      Object.assign(box.style, { left: r.x + 'px', top: top + 'px', width: r.w + 'px', height: (r.y1 - r.y0) + 'px' });
      size.textContent = `${Math.round(r.w)} × ${Math.round(r.y1 - r.y0)}`;
      const ly = Math.min(window.innerHeight - 24, Math.max(0, top - 24));
      Object.assign(size.style, { left: r.x + 'px', top: ly + 'px' });
    };

    // Push against the edge: scroll while the mouse stays there (faster the further out).
    const EDGE = 40;
    const loop = () => {
      if (!dragging) return;
      if (moved && sc.kind !== 'none') {
        const g = geom();
        let v = 0;
        // Only when the mouse has moved at least 20 px towards the edge – not if the selection merely starts close to it.
        if (lastY > g.vBot - EDGE && lastY - startY > 20) v = Math.min(60, 4 + (lastY - (g.vBot - EDGE)) * 0.6);
        else if (lastY < g.vTop + EDGE && startY - lastY > 20) v = -Math.min(60, 4 + ((g.vTop + EDGE) - lastY) * 0.6);
        if (v) {
          if (!edgeSince) edgeSince = performance.now();
          if (performance.now() - edgeSince >= 150) scrollBy(v); // short pause at the edge
        } else edgeSince = 0;
      }
      draw();
      requestAnimationFrame(loop);
    };

    const cleanupPage = () => {
      // Cancelled: scroll back to where the page was.
      if (sc && sc.kind === 'box' && sc.el) sc.el.scrollTop = restore.box ? restore.box.scrollTop : sc.el.scrollTop;
      window.scrollTo({ left: restore.x, top: restore.y, behavior: 'instant' });
      releaseScroller();
      de.style.scrollSnapType = restore.snap;
      if (document.body) document.body.style.scrollSnapType = restore.bodySnap;
      de.style.scrollBehavior = restore.behavior;
    };
    const onKey = e => {
      if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); cleanupPage(); finish(null); }
    };
    const finish = result => {
      dragging = false;
      root.remove();
      window.removeEventListener('keydown', onKey, true);
      window.__skbSelecting = false;
      // Wait until the overlay is off the screen before capturing.
      requestAnimationFrame(() => requestAnimationFrame(() => setTimeout(() => resolve(result), 40)));
    };

    window.addEventListener('keydown', onKey, true);
    root.addEventListener('pointerdown', e => {
      if (e.button !== 0) return;
      e.preventDefault();
      root.setPointerCapture(e.pointerId);
      const picked = sc;
      if (picked && picked.kind === 'box') {
        // Keep the box if it is still under the mouse; otherwise pick again.
        const r = picked.el.getBoundingClientRect();
        if (!(e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom)) releaseScroller();
      }
      if (!sc) sc = pickScroller(e.clientX, e.clientY);
      dragging = true; moved = false; edgeSince = 0;
      sx = lastX = e.clientX; lastY = startY = e.clientY;
      startC = toContent(e.clientY, geom());
      root.style.background = 'transparent';
      hint.style.display = 'none';
      box.style.display = 'block';
      size.style.display = 'block';
      requestAnimationFrame(loop);
    });
    root.addEventListener('pointermove', e => {
      if (!dragging) return;
      lastX = e.clientX; lastY = e.clientY;
      if (Math.abs(e.clientY - (startC - scrollTop() + geom().boxTop)) > 8 || Math.abs(e.clientX - sx) > 8) moved = true;
    });
    // The mouse wheel scrolls too – both before and during the selection.
    root.addEventListener('wheel', e => {
      e.preventDefault();
      if (!sc) sc = pickScroller(e.clientX, e.clientY);
      scrollBy(e.deltaMode === 1 ? e.deltaY * 16 : e.deltaY);
      if (dragging) draw();
    }, { passive: false });
    const endDrag = () => {
      const r = rectNow();
      dragging = false;
      if (r.w < 4 || r.y1 - r.y0 < 4) { cleanupPage(); finish(null); return; }
      // The scroll state is restored by the service worker after capturing (the page stays where it is).
      restore.x = window.scrollX;
      restore.y = window.scrollY;
      if (restore.box) restore.box.scrollTop = sc.el.scrollTop;
      finish({
        kind: sc.kind, x: r.x, w: r.w, y0: r.y0, y1: r.y1,
        boxTop: r.g.boxTop, vTop: r.g.vTop, vBot: r.g.vBot,
        scrollTop: scrollTop(), vw: window.innerWidth, vh: window.innerHeight,
        inset: sc.kind === 'none' ? 0 : topInset(r.g, r.x, r.x + r.w),
        restore
      });
    };
    root.addEventListener('pointerup', e => {
      if (!dragging || e.button !== 0) return;
      lastX = e.clientX; lastY = e.clientY;
      endDrag();
    });
    root.addEventListener('lostpointercapture', () => { if (dragging) endDrag(); });
    root.addEventListener('pointercancel', () => { if (dragging) { dragging = false; cleanupPage(); finish(null); } });
    root.addEventListener('contextmenu', e => { e.preventDefault(); cleanupPage(); finish(null); });
  });
}

function pageInfo() {
  const de = document.documentElement;
  const vw = window.innerWidth, vh = window.innerHeight;
  const info = {
    h: Math.max(de.scrollHeight, document.body ? document.body.scrollHeight : 0),
    vh, vw,
    x: window.scrollX,
    y: window.scrollY,
    behavior: de.style.scrollBehavior || '',
    snap: de.style.scrollSnapType || '',
    box: null
  };
  // Scroll snapping makes the browser jump away from the positions we scroll to.
  de.style.scrollSnapType = 'none';
  if (document.body) { info.bodySnap = document.body.style.scrollSnapType || ''; document.body.style.scrollSnapType = 'none'; }

  // Can the window itself scroll? Try moving it by one pixel.
  const go = top => window.scrollTo({ left: info.x, top, behavior: 'instant' });
  const y0 = window.scrollY;
  go(y0 + 1);
  let moves = window.scrollY !== y0;
  if (!moves && y0 > 0) { go(y0 - 1); moves = window.scrollY !== y0; }
  go(y0);
  const winRange = moves ? Math.max(0, info.h - vh) : 0;
  if (winRange > vh * 0.1) return info;

  // Otherwise: find the largest visible box that scrolls vertically.
  let best = null, bestArea = 0;
  for (const el of document.querySelectorAll('body, body *')) {
    if (el.scrollHeight - el.clientHeight < 40 || el.clientHeight < 120 || el.clientWidth < 200) continue;
    const cs = getComputedStyle(el);
    const oy = cs.overflowY;
    if (oy !== 'auto' && oy !== 'scroll' && oy !== 'overlay') continue;
    if (cs.visibility !== 'visible') continue;
    const r = el.getBoundingClientRect();
    const x1 = Math.max(r.left, 0), x2 = Math.min(r.right, vw), y1 = Math.max(r.top, 0), y2 = Math.min(r.bottom, vh);
    const area = Math.max(0, x2 - x1) * Math.max(0, y2 - y1);
    if (area <= bestArea) continue;
    // The box must actually be visible (not hidden behind something else).
    const hit = document.elementFromPoint((x1 + x2) / 2, (y1 + y2) / 2);
    if (hit && !el.contains(hit) && !hit.contains(el)) continue;
    best = el; bestArea = area;
  }
  if (!best || bestArea < vw * vh * 0.15) return info;

  const r = best.getBoundingClientRect();
  const top = r.top + best.clientTop, left = r.left + best.clientLeft;
  const y = Math.max(0, top), x = Math.max(0, left);
  const vis = Math.min(top + best.clientHeight, vh) - y;
  const w = Math.min(left + best.clientWidth, vw) - x;
  if (vis < 60 || w < 100) return info;

  // Background colour for empty areas next to the box.
  let bg = '#ffffff';
  for (let el = best; el; el = el.parentElement) {
    const c = getComputedStyle(el).backgroundColor;
    if (c && c !== 'transparent' && !/rgba\(.*,\s*0\)$/.test(c)) { bg = c; break; }
  }

  document.querySelectorAll('[data-skb-scroll]').forEach(el => el.removeAttribute('data-skb-scroll'));
  best.setAttribute('data-skb-scroll', '1');
  info.box = {
    x, y, w, vis,
    sh: best.scrollHeight,
    ch: vis,
    scrollTop: best.scrollTop,
    behavior: best.style.scrollBehavior || '',
    anchor: best.style.overflowAnchor || '',
    snap: best === document.body ? info.bodySnap : (best.style.scrollSnapType || ''),
    bg
  };
  // The box may be taller than the window: then only the visible part counts.
  info.box.sh = best.scrollHeight - (best.clientHeight - vis);
  best.style.scrollSnapType = 'none';
  return info;
}

function scrollBoxStep(top, index) {
  const el = document.querySelector('[data-skb-scroll]');
  if (!el) return null;
  el.style.scrollBehavior = 'auto';
  el.style.overflowAnchor = 'none'; // the browser must not shift the content while scrolling
  if (index >= 1) {
    const list = window.__skbHidden || (window.__skbHidden = []);
    const seen = window.__skbSeen || (window.__skbSeen = new WeakSet());
    const hide = node => {
      if (seen.has(node)) return;
      seen.add(node);
      list.push([node, node.style.getPropertyValue('visibility'), node.style.getPropertyPriority('visibility')]);
      node.style.setProperty('visibility', 'hidden', 'important');
    };
    // Sticky headers inside the box would otherwise repeat in every piece.
    // Sticky columns (e.g. the ticker column of a table) are kept.
    for (const child of el.querySelectorAll('*')) {
      const cs = getComputedStyle(child);
      if (cs.position === 'fixed' || (cs.position === 'sticky' && (cs.top !== 'auto' || cs.bottom !== 'auto'))) hide(child);
    }
    if (index === 1) {
      // Fixed elements outside the box that overlap it (chat bubbles, cookie bars).
      const br = el.getBoundingClientRect();
      for (const node of document.querySelectorAll('body *')) {
        if (el.contains(node) || node.contains(el)) continue;
        if (getComputedStyle(node).position !== 'fixed') continue;
        const r = node.getBoundingClientRect();
        if (r.width && r.height && r.right > br.left && r.left < br.right && r.bottom > br.top && r.top < br.bottom) hide(node);
      }
    }
  }
  el.scrollTo({ top, behavior: 'instant' });
  return new Promise(r => requestAnimationFrame(() => requestAnimationFrame(() => r(el.scrollTop))));
}

function scrollStep(y, index) {
  document.documentElement.style.scrollBehavior = 'auto';
  if (index === 1 && !window.__skbHidden) {
    // Hide fixed/sticky elements (e.g. top bars) after the first image so they are not repeated.
    const list = [];
    for (const el of document.querySelectorAll('body *')) {
      const pos = getComputedStyle(el).position;
      if (pos === 'fixed' || pos === 'sticky') {
        list.push([el, el.style.getPropertyValue('visibility'), el.style.getPropertyPriority('visibility')]);
        el.style.setProperty('visibility', 'hidden', 'important');
      }
    }
    window.__skbHidden = list;
  }
  window.scrollTo({ left: 0, top: y, behavior: 'instant' });
  return new Promise(r => requestAnimationFrame(() => requestAnimationFrame(() => r(window.scrollY))));
}

function restorePage(p) {
  for (const [el, value, prio] of (window.__skbHidden || [])) {
    if (value) el.style.setProperty('visibility', value, prio);
    else el.style.removeProperty('visibility');
  }
  window.__skbHidden = null;
  window.__skbSeen = null;
  const box = document.querySelector('[data-skb-scroll]');
  if (box && p.box) {
    box.scrollTo({ top: p.box.scrollTop, behavior: 'instant' });
    box.style.scrollBehavior = p.box.behavior;
    box.style.overflowAnchor = p.box.anchor;
  }
  window.scrollTo({ left: p.x, top: p.y, behavior: 'instant' });
  // Snapping is re-enabled once everything is back in place.
  document.documentElement.style.scrollSnapType = p.snap || '';
  if (document.body) document.body.style.scrollSnapType = p.bodySnap || '';
  if (box && p.box && box !== document.body) box.style.scrollSnapType = p.box.snap;
  if (box) box.removeAttribute('data-skb-scroll');
  // Scroll once more: re-enabled snapping may have moved things.
  if (box && p.box) box.scrollTo({ top: p.box.scrollTop, behavior: 'instant' });
  window.scrollTo({ left: p.x, top: p.y, behavior: 'instant' });
  document.documentElement.style.scrollBehavior = p.behavior;
}

// Runs every time the service worker starts (browser start, after being stopped, after an update).
purgeOld();
