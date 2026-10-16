// "All tabs" after the optional <all_urls> permission has been granted.
// Chrome's permission prompt cannot be clicked from automation, so this test loads a temporary copy of
// the extension whose manifest grants <all_urls> up front – the same state as after the user allowed it.
// Checks: every tab captured, tab titles readable without the "tabs" permission, original tab re-activated,
// and the armTabs/runPendingTabs hand-over used by the popup and the permission window.
const os = require('os');
const { SITE, EXT, check, finish, poll, path, fs } = require('./lib');
const lib = require('./lib');

(async () => {
  const copy = fs.mkdtempSync(path.join(os.tmpdir(), 'imageshot-ext-'));
  fs.cpSync(EXT, copy, { recursive: true, filter: src => !/[\\/](node_modules|tests)([\\/]|$)/.test(src.slice(EXT.length)) });
  const manifest = JSON.parse(fs.readFileSync(path.join(copy, 'manifest.json'), 'utf8'));
  manifest.host_permissions = ['<all_urls>'];
  fs.writeFileSync(path.join(copy, 'manifest.json'), JSON.stringify(manifest, null, 2));
  check(!manifest.permissions.includes('tabs'), 'extension does not request the "tabs" permission');

  const { chromium } = require('playwright');
  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'imageshot-prof-'));
  const ctx = await chromium.launchPersistentContext(profile, {
    headless: false, executablePath: lib.CHROME, viewport: null,
    args: ['--window-size=1000,700', `--disable-extensions-except=${copy}`, `--load-extension=${copy}`]
  });
  const errors = [];
  ctx.on('page', p => {
    p.on('pageerror', e => errors.push(String(e)));
    p.on('console', m => { if (m.type() === 'error' && p.url().startsWith('chrome-extension://')) errors.push(m.text()); });
  });
  const sw = await lib.serviceWorker(ctx);

  await sw.evaluate(() => skbSaveSettings({ afterCapture: 'clipboard', autoClose: false, allTabsScope: 'window' }));
  const initial = ctx.pages();
  const names = ['Tab A', 'Tab B', 'Tab C'];
  const pages = [];
  for (const [i, n] of names.entries()) {
    const p = await ctx.newPage();
    await p.goto(`${SITE}/${['long', 'fixed', 'app'][i]}.html`);
    await p.evaluate(t => { document.title = t; }, n);
    pages.push(p);
  }
  for (const p of initial) await p.close().catch(() => {}); // only the three test tabs remain
  await pages[1].bringToFront(); // start from the middle tab
  await pages[1].waitForTimeout(300);

  check(await sw.evaluate(() => chrome.permissions.contains(SKB_ALL_URLS)), 'host access granted');
  const opened = ctx.waitForEvent('page', { predicate: p => p.url().includes('clipboard.html'), timeout: 20000 });
  // Same hand-over as the popup: arm (inside the click), then run once the permission is there.
  await sw.evaluate(() => { armTabs(); return runPendingTabs(0); });
  const clip = await opened;
  await clip.waitForSelector('.shot');
  const titles = await poll(async () => {
    const t = await clip.$$eval('.shot .info b', els => els.map(e => e.textContent));
    return t.length >= 3 ? t : null;
  }, { timeout: 5000 });
  console.log('cards:', JSON.stringify(titles));
  check(!!titles && names.every(n => titles.includes(n)), 'all tabs captured, titles readable without "tabs" permission', titles);
  check(await clip.$eval('#saveMain', b => b.textContent.includes('(3)')), '"Save all (3)" button');
  const imgsOk = await poll(() => clip.$$eval('.shot img', els => els.length === 3 && els.every(e => e.naturalWidth > 0)));
  check(imgsOk, 'all images decoded from IndexedDB');
  const active = await sw.evaluate(async () => (await chrome.tabs.query({ active: true, windowType: 'normal' })).map(t => t.title));
  check(active.includes('Tab B'), 'original tab re-activated', active);
  const pendingAgain = await sw.evaluate(() => pendingTabs === null);
  check(pendingAgain, 'pending "All tabs" request consumed exactly once');

  check(errors.length === 0, 'no errors on any page', errors);
  await ctx.close();
  fs.rmSync(profile, { recursive: true, force: true });
  fs.rmSync(copy, { recursive: true, force: true });
  finish();
})().catch(e => { console.error(e); process.exit(1); });
