// "Select area": plain selection, edge auto-scroll (window and inner scroll box), mouse wheel,
// fixed top bar – and that the stitched result is seamless.
const { SITE, OUT, check, finish, launch, grantActiveTab, installStats, spacings, path, fs } = require('./lib');

const CASES = [
  // [page, edge hold ms, use wheel]
  ['long', 1800, false],
  ['app', 1500, false],
  ['long', 0, true],
  ['app', 0, false],
  ['fixed', 1500, false]
];

(async () => {
  const { ctx, sw, pageErrors, close } = await launch();
  await installStats(sw);

  for (const [name, holdMs, useWheel] of CASES) {
    const tag = `region/${name}-${useWheel ? 'wheel' : holdMs ? 'edge' : 'short'}`;
    const page = await ctx.newPage();
    await page.goto(`${SITE}/${name}.html`);
    await page.waitForTimeout(400);
    await grantActiveTab(sw, page);
    const { vh, dpr } = await page.evaluate(() => ({ vh: innerHeight, dpr: devicePixelRatio }));
    const before = await page.evaluate(() => [scrollY, document.querySelector('.main')?.scrollTop ?? null]);

    const job = sw.evaluate(async ({ fixed }) => {
      const [tab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
      const shot = await captureRegion(tab);
      if (!shot) return null;
      const stats = self.__stats(shot.image, fixed ? { color: [192, 57, 43] } : {});
      const c = new OffscreenCanvas(shot.image.width, shot.image.height);
      c.getContext('2d').drawImage(shot.image, 0, 0);
      return { ...stats, png: await blobToDataUrl(await c.convertToBlob({ type: 'image/png' })) };
    }, { fixed: name === 'fixed' });

    await page.waitForTimeout(600);
    const y0 = name === 'app' ? 150 : name === 'fixed' ? 200 : 120;
    await page.mouse.move(300, y0); await page.mouse.down();
    await page.mouse.move(500, 300, { steps: 5 });
    if (useWheel) {
      for (let i = 0; i < 6; i++) { await page.mouse.wheel(0, 300); await page.waitForTimeout(80); }
      await page.mouse.move(520, 400, { steps: 3 });
    } else if (holdMs) {
      const edgeY = name === 'app' ? vh - 50 : vh - 10; // app: the box ends above the footer
      await page.mouse.move(500, edgeY, { steps: 5 });
      await page.waitForTimeout(holdMs);
      await page.mouse.move(501, edgeY - 60, { steps: 3 }); // move back a little so scrolling stops
    } else {
      await page.mouse.move(520, 380, { steps: 3 }); // no scrolling: ordinary selection
    }
    await page.waitForTimeout(150);
    await page.mouse.up();
    const out = await job;
    const after = await page.evaluate(() => [scrollY, document.querySelector('.main')?.scrollTop ?? null, !!document.querySelector('[data-skb-scroll]'), !!window.__skbSelecting]);
    if (out) fs.writeFileSync(path.join(OUT, `${tag.replace('/', '-')}.png`), Buffer.from(out.png.split(',')[1], 'base64'));
    console.log(`${tag}: ${out ? `${out.w}×${out.h}` : 'nothing'} | scroll before ${JSON.stringify(before)} after ${JSON.stringify(after)}`);

    if (!check(!!out, `${tag}: an image was captured`)) { await page.close(); continue; }
    check(out.gapRows === 0 && out.blackRows === 0, `${tag}: no empty or black rows`, [out.gapRows, out.blackRows]);
    check(!after[2] && !after[3], `${tag}: page cleaned up`);
    if (holdMs || useWheel) {
      const minH = (name === 'app' ? vh - 100 : vh) * dpr; // app: the scroll box is 100 px shorter than the window
      check(out.h > minH, `${tag}: stitched image taller than the scroll area`, `${out.h} > ${minH}`);
      check(name === 'app' ? after[1] > before[1] : after[0] > before[0], `${tag}: page auto-scrolled while selecting`);
    } else {
      check(Math.abs(out.w - 220 * dpr) <= 2 && Math.abs(out.h - 230 * dpr) <= 2, `${tag}: exact selection size`, [out.w, out.h]);
    }
    if (name === 'long' || name === 'fixed') {
      const sp = spacings(out.lines);
      check(sp.length >= 5 && sp.every(d => Math.abs(d - 81 * dpr) <= 1), `${tag}: uniform row spacing (no seams or overlaps)`, [...new Set(sp)]);
    }
    if (name === 'fixed') check(out.colorRows === 0, `${tag}: fixed top bar not repeated in the stitched image`, out.colorRows);
    await page.close();
  }

  check(pageErrors.length === 0, 'no errors on any page', pageErrors);
  await close();
  finish();
})().catch(e => { console.error(e); process.exit(1); });
