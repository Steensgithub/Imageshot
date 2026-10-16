// Shared helpers for the Playwright tests.
const { chromium } = require('playwright');
const { execFileSync } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');

const EXT = path.resolve(__dirname, '..');
const SITE = process.env.TEST_SITE || 'http://127.0.0.1:8765';
const OUT = process.env.TEST_OUT || path.join(os.tmpdir(), 'imageshot-tests');
fs.mkdirSync(OUT, { recursive: true });

// Chromium binary: CHROME_PATH, or the first CLI argument, or Playwright's bundled browser.
const CHROME = process.env.CHROME_PATH || process.argv[2] || undefined;

const failures = [];
function check(cond, label, detail) {
  const line = `${cond ? 'ok  ' : 'FAIL'} ${label}${detail !== undefined ? ' – ' + (typeof detail === 'string' ? detail : JSON.stringify(detail)) : ''}`;
  console.log(line);
  if (!cond) failures.push(label);
  return cond;
}

function finish() {
  if (failures.length) {
    console.log(`\n${failures.length} assertion(s) failed: ${failures.join('; ')}`);
    process.exit(1);
  }
  console.log('\nall assertions passed');
  process.exit(0);
}

// Default UI language is English so assertions do not depend on the machine's locale.
async function launch({ lang = 'en', windowSize = '1000,700', env } = {}) {
  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'imageshot-prof-'));
  const args = [`--window-size=${windowSize}`, `--disable-extensions-except=${EXT}`, `--load-extension=${EXT}`];
  args.push(`--lang=${lang}`);
  // On Linux, Chromium also reads the UI language from $LANGUAGE.
  const ctx = await chromium.launchPersistentContext(profile, {
    headless: false, executablePath: CHROME, viewport: null, args,
    env: { ...process.env, LANGUAGE: lang, ...env }
  });
  const pageErrors = [];
  const watch = p => {
    p.on('pageerror', e => pageErrors.push(`${p.url()}: ${e}`));
    // Console errors count on extension pages; test-site noise (e.g. a missing favicon) does not.
    p.on('console', m => { if (m.type() === 'error' && p.url().startsWith('chrome-extension://')) pageErrors.push(`${p.url()}: ${m.text()}`); });
  };
  ctx.pages().forEach(watch);
  ctx.on('page', watch);
  const sw = await serviceWorker(ctx);
  const extId = sw.url().split('/')[2];
  const close = async () => {
    await ctx.close();
    fs.rmSync(profile, { recursive: true, force: true });
  };
  return { ctx, sw, extId, pageErrors, close };
}

// The extension's service worker, once its scripts (settings.js, store.js, background.js) have run.
async function serviceWorker(ctx) {
  const sw = ctx.serviceWorkers()[0] || await ctx.waitForEvent('serviceworker');
  const ready = await poll(() => sw.evaluate(() => typeof skbStore === 'object' && typeof run === 'function').catch(() => false), { timeout: 10000 });
  if (!ready) throw new Error('extension service worker did not start');
  return sw;
}

// Sends a real X11 key combination (XTest) to the focused window. Synthetic CDP key events
// never reach Chrome's extension shortcuts, so this is how a test produces a real user gesture.
function xkeys(combo) {
  execFileSync('python3', [path.join(__dirname, 'xkeys.py'), combo]);
}

async function poll(fn, { timeout = 5000, interval = 100 } = {}) {
  const end = Date.now() + timeout;
  let v;
  while (Date.now() < end) {
    v = await fn();
    if (v) return v;
    await new Promise(r => setTimeout(r, interval));
  }
  return v;
}

// Grants activeTab for `page` the way a user does: by pressing the extension's shortcut
// (Alt+Shift+S). The service worker's run() is stubbed meanwhile, so nothing is captured.
async function grantActiveTab(sw, page) {
  await page.bringToFront();
  await sw.evaluate(() => {
    self.__origRun = self.__origRun || run;
    self.__gesture = null;
    run = async (mode, tab) => { self.__gesture = { mode, tabId: tab && tab.id }; };
  });
  xkeys('Alt_L+Shift_L+s');
  const hit = await poll(() => sw.evaluate(() => self.__gesture), { timeout: 4000 });
  await sw.evaluate(() => { run = self.__origRun; });
  if (!hit) throw new Error('shortcut gesture did not reach the extension (is the window focused?)');
  return hit;
}

// Pixel statistics of an ImageBitmap/OffscreenCanvas, computed inside the service worker.
// Installs self.__stats(image, opts) -> { w, h, gapRows, blackRows, lines, colorRows }.
async function installStats(sw) {
  await sw.evaluate(() => {
    self.__stats = (img, { lineX, line = [153, 153, 153], color, colorX } = {}) => {
      const w = img.width, h = img.height;
      const c = new OffscreenCanvas(w, Math.min(h, 1000));
      const g = c.getContext('2d', { willReadFrequently: true });
      const near = (d, i, rgb, tol = 4) => Math.abs(d[i] - rgb[0]) <= tol && Math.abs(d[i + 1] - rgb[1]) <= tol && Math.abs(d[i + 2] - rgb[2]) <= tol;
      let gapRows = 0, blackRows = 0, colorRows = 0;
      const lines = [];
      const lx = Math.round(lineX ?? w / 2), cx = Math.round(colorX ?? w / 2);
      for (let y0 = 0; y0 < h; y0 += 1000) {
        const ch = Math.min(1000, h - y0);
        g.clearRect(0, 0, w, c.height);
        g.drawImage(img, 0, y0, w, ch, 0, 0, w, ch);
        const d = g.getImageData(0, 0, w, ch).data;
        for (let y = 0; y < ch; y++) {
          let transparent = true, black = true;
          for (let x = 0; x < w; x += 7) {
            const i = (y * w + x) * 4;
            if (d[i + 3] !== 0) transparent = false;
            if (d[i] > 8 || d[i + 1] > 8 || d[i + 2] > 8) black = false;
            if (!transparent && !black) break;
          }
          if (transparent) gapRows++;
          else if (black) blackRows++;
          if (near(d, (y * w + lx) * 4, line)) lines.push(y0 + y);
          if (color && near(d, (y * w + cx) * 4, color, 6)) colorRows++;
        }
      }
      return { w, h, gapRows, blackRows, lines, colorRows };
    };
  });
}

// Distances between consecutive separator lines (runs of adjacent rows count as one line).
function spacings(lines) {
  const starts = lines.filter((y, i) => i === 0 || y !== lines[i - 1] + 1);
  return starts.slice(1).map((y, i) => y - starts[i]);
}

module.exports = { EXT, SITE, OUT, CHROME, check, finish, launch, serviceWorker, xkeys, poll, grantActiveTab, installStats, spacings, path, fs };
