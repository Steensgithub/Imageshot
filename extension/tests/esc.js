// Esc while selecting (after edge auto-scroll) cancels and restores the page exactly.
const { SITE, check, finish, launch, grantActiveTab } = require('./lib');

(async () => {
  const { ctx, sw, pageErrors, close } = await launch();
  const page = await ctx.newPage();
  await page.goto(`${SITE}/long.html`);
  await page.waitForTimeout(400);
  await page.evaluate(() => scrollTo(0, 300));
  await grantActiveTab(sw, page);

  const job = sw.evaluate(async () => {
    const [tab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
    return await captureRegion(tab);
  });
  await page.waitForTimeout(500);
  const vh = await page.evaluate(() => innerHeight);
  await page.mouse.move(300, 150); await page.mouse.down();
  await page.mouse.move(400, vh - 8, { steps: 6 });
  await page.waitForTimeout(900);
  const during = await page.evaluate(() => scrollY);
  await page.keyboard.press('Escape'); await page.mouse.up();
  const res = await job;
  const after = await page.evaluate(() => ({
    scrollY,
    marker: !!document.querySelector('[data-skb-scroll]'),
    behavior: document.documentElement.style.scrollBehavior,
    snapStyle: document.documentElement.style.scrollSnapType,
    overlay: [...document.documentElement.children].some(e => e.tagName === 'DIV'),
    selecting: !!window.__skbSelecting
  }));
  console.log('scroll during selection:', during, '| result:', res, '| after Esc:', JSON.stringify(after));

  check(during > 300, 'edge auto-scroll happened before Esc', during);
  check(res === null, 'Esc returns no image');
  check(after.scrollY === 300, 'scroll position restored', after.scrollY);
  check(!after.marker && after.behavior === '' && after.snapStyle === '', 'inline styles and markers restored', after);
  check(!after.overlay && !after.selecting, 'overlay removed');

  // A new selection can be started afterwards (the "selecting" guard was reset).
  const again = sw.evaluate(async () => {
    const [tab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
    return await captureRegion(tab);
  });
  await page.waitForTimeout(400);
  await page.keyboard.press('Escape');
  check((await again) === null && (await page.evaluate(() => scrollY)) === 300, 'second selection can start and be cancelled');

  check(pageErrors.length === 0, 'no errors on any page', pageErrors);
  await close();
  finish();
})().catch(e => { console.error(e); process.exit(1); });
