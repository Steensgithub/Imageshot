// Full-page capture (window scrolling, inner scroll box, scroll-snap, fixed/sticky elements),
// the end-to-end shortcut flow through IndexedDB to the clipboard window, "save directly" and purging.
const { SITE, OUT, check, finish, launch, xkeys, poll, grantActiveTab, installStats, spacings, path, fs } = require('./lib');

const PAGES = (process.env.PAGES || 'long,app,body,fixed').split(',');

(async () => {
  const { ctx, sw, pageErrors, close } = await launch();
  await installStats(sw);

  // Without a gesture there is no host access at all (no <all_urls> in host_permissions).
  {
    const page = await ctx.newPage();
    await page.goto(`${SITE}/long.html`);
    await page.bringToFront();
    const denied = await sw.evaluate(async () => {
      const [tab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
      try { await chrome.tabs.captureVisibleTab(tab.windowId, { format: 'png' }); return 'captured'; } catch (e) { return e.message; }
    });
    check(/activeTab/.test(denied), 'no capture without a user gesture', denied);
    await page.close();
  }

  for (const name of PAGES) {
    const page = await ctx.newPage();
    await page.goto(`${SITE}/${name}.html`);
    await page.bringToFront();
    await page.waitForTimeout(400);
    if (name === 'app') await page.evaluate(() => { document.querySelector('.main').scrollTop = 300; });
    if (name === 'body') await page.evaluate(() => document.body.scrollTo({ top: 140, behavior: 'instant' }));
    const before = await page.evaluate(() => document.querySelector('.main') ? document.querySelector('.main').scrollTop : document.body.scrollTop);
    await grantActiveTab(sw, page);
    const vh = await page.evaluate(() => innerHeight * devicePixelRatio);

    const out = await sw.evaluate(async ({ name }) => {
      const [tab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
      const shot = await captureFull(tab);
      const k = shot.image.width / (await chrome.scripting.executeScript({ target: { tabId: tab.id }, func: () => innerWidth }))[0].result;
      const stats = self.__stats(shot.image, name === 'fixed' ? { lineX: shot.image.width - 30, color: [192, 57, 43], colorX: shot.image.width - 30 } : { lineX: shot.image.width - 30 });
      const blob = await shot.image.convertToBlob({ type: 'image/png' });
      return { ...stats, k, png: await blobToDataUrl(blob) };
    }, { name });
    fs.writeFileSync(path.join(OUT, `full-${name}.png`), Buffer.from(out.png.split(',')[1], 'base64'));

    const after = await page.evaluate(() => ({
      scrollY,
      inner: document.querySelector('.main') ? document.querySelector('.main').scrollTop : document.body.scrollTop,
      marker: !!document.querySelector('[data-skb-scroll]'),
      hidden: [...document.querySelectorAll('body *')].filter(e => e.style.visibility === 'hidden').length,
      snap: document.body.style.scrollSnapType
    }));
    const tag = `full/${name}`;
    console.log(`${tag}: ${out.w}×${out.h} (viewport ${vh}) after=${JSON.stringify(after)}`);
    check(out.h > vh * 1.5, `${tag}: image taller than the viewport`, `${out.h} > ${vh * 1.5}`);
    check(out.gapRows === 0, `${tag}: no empty (transparent) rows`, out.gapRows);
    check(out.blackRows === 0, `${tag}: no black rows`, out.blackRows);
    check(!after.marker && after.hidden === 0, `${tag}: page markers and hidden elements restored`);
    if (name === 'long') {
      check(Math.abs(out.h - 3240 * out.k) <= 1, `${tag}: exact page height`, out.h);
      const sp = spacings(out.lines);
      check(sp.length >= 38 && sp.every(d => Math.abs(d - 81 * out.k) <= 1), `${tag}: uniform row spacing (stitching)`, [...new Set(sp)]);
      check(after.scrollY === 0, `${tag}: scroll position restored`, after.scrollY);
    }
    if (name === 'fixed') {
      check(Math.abs(out.h - 3290 * out.k) <= 1, `${tag}: exact page height`, out.h);
      check(Math.abs(out.colorRows - 50 * out.k) <= 1, `${tag}: fixed header appears only once`, out.colorRows);
      const sp = spacings(out.lines);
      check(sp.length >= 38 && sp.every(d => Math.abs(d - 81 * out.k) <= 1), `${tag}: uniform row spacing (stitching)`, [...new Set(sp)]);
    }
    if (name === 'app') check(after.inner === 300, `${tag}: inner box scroll restored`, after.inner);
    if (name === 'body') {
      check(after.inner === before, `${tag}: body scroll restored`, [before, after.inner]);
      check(after.snap === '', `${tag}: scroll-snap restored`, after.snap);
    }
    await page.close();
  }

  // ---- End to end: real shortcut -> capture -> IndexedDB -> clipboard window -> cleanup ----
  {
    await sw.evaluate(() => skbSaveSettings({ afterCapture: 'clipboard', autoClose: false, format: 'webp', resolution: 70 }));
    const page = await ctx.newPage();
    await page.goto(`${SITE}/long.html`);
    await page.bringToFront();
    await page.waitForTimeout(300);
    const opened = ctx.waitForEvent('page', { predicate: p => p.url().includes('clipboard.html'), timeout: 10000 });
    xkeys('Alt_L+Shift_L+s');
    const clip = await opened;
    await clip.waitForSelector('.shot img');
    const id = new URL(clip.url()).searchParams.get('id');
    const info = await poll(async () => {
      const r = await clip.$eval('.shot', el => ({ text: el.querySelector('.info span').textContent, name: el.querySelector('.info b').textContent, nat: el.querySelector('img').naturalWidth, src: el.querySelector('img').src }));
      return r.nat > 0 ? r : null;
    });
    console.log('clipboard window:', JSON.stringify(info));
    check(!!info && info.src.startsWith('blob:'), 'shortcut capture shown from an object URL');
    check(!!info && /^\d+ × \d+ px · WEBP · /.test(info.text), 'image info line', info && info.text);
    check(!!info && /^ImageShot \d{4}-\d\d-\d\d \d\d\.\d\d\.\d\d$/.test(info.name), 'file name "ImageShot YYYY-MM-DD HH.mm.ss"', info && info.name);
    const rec = await sw.evaluate(async id => {
      const r = await skbStore.get(id);
      return r && { n: r.images.length, blob: r.images[0].blob instanceof Blob, size: r.images[0].blob.size, keys: Object.keys(r.images[0]).sort() };
    }, id);
    check(!!rec && rec.n === 1 && rec.blob && rec.size > 0, 'capture record stored in IndexedDB as a Blob', rec);
    const winKey = await sw.evaluate(async () => Object.keys(await chrome.storage.session.get(null)).filter(k => k.startsWith('clipWin:')).length);
    check(winKey === 1, 'clipboard window tracked for cleanup', winKey);
    await clip.close();
    const gone = await poll(() => sw.evaluate(async id => !(await skbStore.ids()).includes(id), id), { timeout: 5000 });
    check(gone, 'record deleted when the clipboard window closes');
    await page.close();
  }

  // ---- "Save directly": data URL download from the service worker ----
  {
    await sw.evaluate(() => skbSaveSettings({ afterCapture: 'save', folder: 'ImageShot' }));
    const page = await ctx.newPage();
    await page.goto(`${SITE}/long.html`);
    await grantActiveTab(sw, page);
    const dl = await sw.evaluate(async () => {
      const [tab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
      const before = Date.now();
      await run('visible', tab);
      for (let i = 0; i < 50; i++) {
        const items = await chrome.downloads.search({ startedAfter: new Date(before - 1000).toISOString() });
        const d = items.find(x => x.state === 'complete' || x.state === 'interrupted');
        if (d) return { state: d.state, error: d.error, url: d.url.slice(0, 22), bytes: d.fileSize || d.bytesReceived, ids: (await skbStore.ids()).length };
        await new Promise(r => setTimeout(r, 100));
      }
      return null;
    });
    console.log('save directly:', JSON.stringify(dl));
    check(!!dl && dl.state === 'complete' && dl.bytes > 0, '"save directly" downloads the image', dl);
    check(!!dl && dl.url.startsWith('data:image/webp'), '"save directly" uses a data URL', dl && dl.url);
    check(!!dl && dl.ids === 0, '"save directly" stores nothing in IndexedDB', dl && dl.ids);
    await sw.evaluate(() => skbSaveSettings({ afterCapture: 'clipboard' }));
    await page.close();
  }

  // ---- Purging old records ----
  {
    const r = await sw.evaluate(async () => {
      const blob = new Blob(['x'], { type: 'image/png' });
      await skbStore.put({ id: 'old', createdAt: Date.now() - 2 * 3600e3, images: [{ blob }] });
      await skbStore.put({ id: 'fresh', createdAt: Date.now(), images: [{ blob }] });
      const removed = await purgeOld();
      const ids = await skbStore.ids();
      await skbStore.remove('fresh');
      return { removed, ids };
    });
    check(r.removed === 1 && !r.ids.includes('old') && r.ids.includes('fresh'), 'records older than 1 hour are purged', r);
  }

  check(pageErrors.length === 0, 'no errors on any page', pageErrors);
  await close();
  finish();
})().catch(e => { console.error(e); process.exit(1); });
