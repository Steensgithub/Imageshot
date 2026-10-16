// Crop editor in the clipboard window: drag / resize / move, Enter, double-click, Esc, Enter on
// "Cancel", Ctrl+Z, cropping from the original, saving via an object URL, cleanup on close.
const { OUT, check, finish, launch, poll, path } = require('./lib');

const RED = [204, 51, 51], GREEN = [51, 204, 51], BLUE = [51, 51, 204], YELLOW = [204, 204, 51];
const near = (a, b) => a && a.every((v, i) => Math.abs(v - b[i]) <= 12);

(async () => {
  const { ctx, sw, extId, pageErrors, close } = await launch({ windowSize: '1100,800' });

  // Seed a capture through the storage API: an 800×500 image with four coloured quadrants.
  await sw.evaluate(async () => {
    await skbSaveSettings({ autoClose: false, folder: 'ImageShot', resolution: 70, format: 'webp' });
    const c = new OffscreenCanvas(800, 500); const g = c.getContext('2d');
    g.fillStyle = '#c33'; g.fillRect(0, 0, 400, 250); g.fillStyle = '#3c3'; g.fillRect(400, 0, 400, 250);
    g.fillStyle = '#33c'; g.fillRect(0, 250, 400, 250); g.fillStyle = '#cc3'; g.fillRect(400, 250, 400, 250);
    const img = await encode({ image: c, title: 'Test' }, skbQuality(await skbGetSettings()), 'ImageShot test');
    await skbStore.put({ id: 'test1', createdAt: Date.now(), errors: [], images: [img] });
  });

  const page = await ctx.newPage();
  await page.goto(`chrome-extension://${extId}/clipboard.html?id=test1`);
  await page.waitForSelector('.shot img');
  const info = () => page.$eval('.info span', e => e.textContent);
  const W0 = await poll(() => page.$eval('.shot img', e => e.naturalWidth));
  const H0 = await page.$eval('.shot img', e => e.naturalHeight);
  console.log('before:', await info());
  check(W0 === 699 && H0 === 437, 'seeded image decoded from IndexedDB Blob (70 % scale)', [W0, H0]);
  check((await info()).startsWith(`${W0} × ${H0} px · WEBP`), 'info line shows size and format');

  const waitCropped = async () => poll(async () => (await info()).includes('cropped'), { timeout: 4000 });
  const pixels = () => page.evaluate(async () => {
    const im = document.querySelector('.shot img'); await im.decode();
    const c = document.createElement('canvas'); c.width = im.naturalWidth; c.height = im.naturalHeight;
    const g = c.getContext('2d'); g.drawImage(im, 0, 0);
    const at = (x, y) => Array.from(g.getImageData(x, y, 1, 1).data.slice(0, 3));
    return { size: [c.width, c.height], tl: at(5, 5), br: at(c.width - 5, c.height - 5) };
  });
  async function openEditor() {
    await page.click('#cropMain');
    const cb = await page.$eval('#cropCanvas', e => { const r = e.getBoundingClientRect(); return { x: r.left, y: r.top, w: r.width }; });
    const nat = await page.$eval('.shot img', e => e.naturalWidth);
    const s = cb.w / nat;
    return (x, y) => [cb.x + x * s, cb.y + y * s]; // image pixels -> screen
  }
  async function drag(from, to, steps = 5) {
    await page.mouse.move(...from); await page.mouse.down(); await page.mouse.move(...to, { steps }); await page.mouse.up();
  }

  // 1) New frame (100,50)-(500,300), corner to (600,350), move 50 px left -> 500×300 at (50,50).
  let P = await openEditor();
  await drag(P(100, 50), P(500, 300), 8);
  console.log('frame:', await page.$eval('#cropInfo', e => e.textContent));
  check(/^400 × 250 px/.test(await page.$eval('#cropInfo', e => e.textContent)), 'new frame size');
  const se = await page.$eval('#cropSel i[data-h="se"]', e => { const r = e.getBoundingClientRect(); return [r.left + r.width / 2, r.top + r.height / 2]; });
  await drag(se, P(600, 350));
  check(/^500 × 300 px/.test(await page.$eval('#cropInfo', e => e.textContent)), 'resize with corner handle');
  await drag(P(300, 200), P(250, 200));
  await page.screenshot({ path: path.join(OUT, 'crop-editor.png') });
  await page.keyboard.press('Enter');
  check(await waitCropped(), 'Enter applies the crop');
  let px = await pixels();
  console.log('after Enter:', await info(), JSON.stringify(px));
  check(px.size[0] === 500 && px.size[1] === 300, 'cropped size is exact', px.size);
  check(near(px.tl, RED) && near(px.br, YELLOW), 'moved frame keeps the right content (red top-left, yellow bottom-right)', px);
  check(await page.isVisible('#undoMain'), 'undo button visible');
  await page.screenshot({ path: path.join(OUT, 'crop-after.png') });

  // 2) Crop the cropped image again: taken from the original, so offsets add up.
  P = await openEditor();
  await drag(P(400, 10), P(480, 290));                 // inside the crop: original x 450..530, y 60..340
  await page.keyboard.press('Enter');
  await poll(async () => (await pixels()).size[0] === 80, { timeout: 4000 });
  px = await pixels();
  check(px.size[0] === 80 && px.size[1] === 280, 'second crop size is exact', px.size);
  check(near(px.tl, GREEN) && near(px.br, YELLOW), 'second crop is taken from the original pixels', px);

  // 3) Ctrl+Z twice -> back to the original.
  await page.keyboard.press('Control+z');
  await poll(async () => (await pixels()).size[0] === 500, { timeout: 2000 });
  await page.keyboard.press('Control+z');
  await poll(async () => (await pixels()).size[0] === W0, { timeout: 2000 });
  px = await pixels();
  console.log('after 2× Ctrl+Z:', await info());
  check(px.size[0] === W0 && px.size[1] === H0 && !(await info()).includes('cropped'), 'Ctrl+Z restores the original', px.size);
  check(!(await page.isVisible('#undoMain')), 'undo button hidden when nothing to undo');

  // 4) Double-click inside the frame applies.
  P = await openEditor();
  await drag(P(10, 10), P(210, 110), 4);
  await page.mouse.dblclick(...P(100, 60));
  await waitCropped();
  px = await pixels();
  check(px.size[0] === 200 && px.size[1] === 100 && near(px.tl, RED), 'double-click applies (200×100)', px);
  check(await page.$eval('#crop', e => e.hidden), 'editor closed after double-click');

  // 5) Esc cancels; Enter on the focused "Cancel" button must not apply.
  P = await openEditor();
  await drag(P(10, 10), P(100, 50), 3);
  await page.keyboard.press('Escape');
  check(await page.$eval('#crop', e => e.hidden) && (await pixels()).size[0] === 200, 'Esc cancels the editor');
  P = await openEditor();
  await drag(P(10, 10), P(100, 50), 3);
  await page.focus('#cropCancel'); await page.keyboard.press('Enter');
  await page.waitForTimeout(300);
  check(await page.$eval('#crop', e => e.hidden) && (await pixels()).size[0] === 200, 'Enter on "Cancel" does not apply');

  // 6) Copy: the (cropped, WebP) Blob is converted to PNG and written to the system clipboard.
  await page.click('#copyMain');
  const toast = await poll(() => page.$eval('#toast', t => (t.classList.contains('show') && /cop/i.test(t.textContent)) ? { text: t.textContent, err: t.classList.contains('err') } : null), { timeout: 4000 });
  check(!!toast && !toast.err && toast.text === 'Copied to the clipboard', 'Copy writes the image to the clipboard', toast);

  // 7) Save from the page with an object URL (the cropped 200×100 image).
  // Playwright renames downloaded files, so record the file name the page asks for.
  await page.evaluate(() => {
    const orig = chrome.downloads.download.bind(chrome.downloads);
    chrome.downloads.download = opts => { window.__dlName = opts.filename; return orig(opts); };
  });
  const started = Date.now();
  await page.click('#saveMain');
  const dl = await poll(() => sw.evaluate(async t => {
    const [d] = await chrome.downloads.search({ startedAfter: new Date(t - 1000).toISOString(), state: 'complete' });
    return d ? { url: d.url.slice(0, 5), bytes: d.fileSize || d.bytesReceived } : null;
  }, started), { timeout: 8000 });
  if (dl) dl.file = await page.evaluate(() => window.__dlName);
  console.log('download:', JSON.stringify(dl));
  check(!!dl && dl.url === 'blob:' && dl.bytes > 0, 'save downloads from an object URL', dl);
  check(!!dl && dl.file === 'ImageShot/ImageShot test.webp', 'download goes to Downloads/ImageShot/', dl && dl.file);

  // 8) Closing the window deletes the stored capture.
  await page.close();
  const gone = await poll(() => sw.evaluate(async () => !(await skbStore.ids()).includes('test1')), { timeout: 4000 });
  check(gone, 'record removed from IndexedDB on close');

  // 9) Unknown id -> friendly empty state, buttons disabled.
  const p2 = await ctx.newPage();
  await p2.goto(`chrome-extension://${extId}/clipboard.html?id=missing`);
  await p2.waitForSelector('.empty');
  check(await p2.$eval('#copyMain', b => b.disabled), 'missing capture shows the empty state', await p2.$eval('.empty', e => e.textContent));
  await p2.close();

  check(pageErrors.length === 0, 'no errors on any page', pageErrors);
  await close();
  finish();
})().catch(e => { console.error(e); process.exit(1); });
