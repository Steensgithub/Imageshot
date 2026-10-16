// Drawing tools in the clipboard window: rectangle, pixelate, step, text, undo/redo inside the editor,
// Enter applies, the drawing survives a later crop, and the window's Undo removes it again.
const { OUT, check, finish, launch, poll, path } = require('./lib');

const RED = [204, 51, 51], GREEN = [51, 204, 51], BLUE_PEN = [31, 111, 235];
const near = (a, b, t = 14) => a && a.every((v, i) => Math.abs(v - b[i]) <= t);

(async () => {
  const { ctx, sw, extId, pageErrors, close } = await launch({ windowSize: '1100,800' });
  await sw.evaluate(async () => {
    await skbSaveSettings({ autoClose: false, folder: 'ImageShot', resolution: 100, format: 'png' });
    const c = new OffscreenCanvas(800, 500); const g = c.getContext('2d');
    g.fillStyle = '#c33'; g.fillRect(0, 0, 400, 250); g.fillStyle = '#3c3'; g.fillRect(400, 0, 400, 250);
    g.fillStyle = '#33c'; g.fillRect(0, 250, 400, 250); g.fillStyle = '#cc3'; g.fillRect(400, 250, 400, 250);
    const img = await encode({ image: c, title: 'Test' }, skbQuality(await skbGetSettings()), 'ImageShot draw');
    await skbStore.put({ id: 'draw1', createdAt: Date.now(), errors: [], images: [img] });
  });

  const page = await ctx.newPage();
  await page.goto(`chrome-extension://${extId}/clipboard.html?id=draw1`);
  await page.waitForSelector('.shot img');
  const info = () => page.$eval('.info span', e => e.textContent);
  const W0 = await poll(() => page.$eval('.shot img', e => e.naturalWidth));
  const k = W0 / 800; // image pixels per seeded pixel
  console.log('image', W0, await info());

  const pixels = pts => page.evaluate(async pts => {
    let im;
    for (let i = 0; ; i++) {
      im = document.querySelector('.shot img');
      try { await im.decode(); break; } catch (e) { if (i >= 20) throw e; await new Promise(r => setTimeout(r, 100)); }
    }
    const c = document.createElement('canvas'); c.width = im.naturalWidth; c.height = im.naturalHeight;
    const g = c.getContext('2d'); g.drawImage(im, 0, 0);
    return { size: [c.width, c.height], px: pts.map(([x, y]) => Array.from(g.getImageData(Math.round(x), Math.round(y), 1, 1).data.slice(0, 3))) };
  }, pts);

  async function openDraw() {
    await page.click('#drawMain');
    await page.waitForSelector('#draw:not([hidden]) canvas.draw-canvas');
    const cb = await page.$eval('#drawStage canvas', e => { const r = e.getBoundingClientRect(); return { x: r.left, y: r.top, w: r.width, n: e.width }; });
    const s = cb.w / cb.n;
    return (x, y) => [cb.x + x * k * s, cb.y + y * k * s]; // seeded pixels -> screen
  }
  async function drag(from, to, steps = 6) {
    await page.mouse.move(...from); await page.mouse.down(); await page.mouse.move(...to, { steps }); await page.mouse.up();
  }

  let P = await openDraw();
  check(await page.isVisible('#drawTools .tool.on'), 'editor opens with a tool selected');
  await page.click('#drawTools .swatch[data-color="4"]');
  await page.keyboard.press('2');                        // rectangle
  await drag(P(100, 50), P(300, 200));
  check(!(await page.$eval('#drawUndo', b => b.disabled)), 'undo enabled after drawing');
  await page.keyboard.press('8');                        // pixelate
  await drag(P(380, 230), P(420, 270));
  await page.keyboard.press('6');                        // step number
  await page.mouse.click(...P(600, 100));
  await page.mouse.click(...P(660, 100));
  await page.keyboard.press('5');                        // text
  await page.mouse.click(...P(480, 320));
  await page.keyboard.type('Hej ÆØÅ');
  await page.keyboard.press('Enter');
  await page.waitForTimeout(150);
  await page.screenshot({ path: path.join(OUT, 'draw-editor.png') });
  const shapes = () => page.evaluate(() => document.querySelectorAll('#drawUndo').length && !document.querySelector('#drawUndo').disabled);
  await page.keyboard.press('Control+z');
  await page.keyboard.press('Control+y');
  check(await shapes(), 'Ctrl+Z / Ctrl+Y inside the editor');
  await page.keyboard.press('Enter');
  const applied = await poll(async () => (await info()).includes('drawn on'), { timeout: 4000 });
  check(applied, 'Enter applies the drawing', await info());
  check(await page.$eval('#draw', e => e.hidden), 'editor closed after applying');

  let r = await pixels([[100 * k, 120 * k], [200 * k, 120 * k], [400 * k, 240 * k], [10 * k, 10 * k]]);
  console.log('after drawing:', JSON.stringify(r));
  check(r.size[0] === W0, 'same size after drawing', r.size);
  check(near(r.px[0], BLUE_PEN, 40), 'rectangle edge is drawn in the chosen colour', r.px[0]);
  check(near(r.px[1], RED), 'inside the rectangle is untouched', r.px[1]);
  check(!near(r.px[2], RED) && !near(r.px[2], GREEN), 'pixelated area is mixed (not readable)', r.px[2]);
  check(near(r.px[3], RED), 'rest of the image untouched', r.px[3]);

  // Crop afterwards: the drawing is part of the new base image.
  await page.click('#cropMain');
  const cb = await page.$eval('#cropCanvas', e => { const r = e.getBoundingClientRect(); return { x: r.left, y: r.top, w: r.width }; });
  const s = cb.w / W0;
  const C = (x, y) => [cb.x + x * k * s, cb.y + y * k * s];
  await drag(C(50, 20), C(350, 230));
  await page.keyboard.press('Enter');
  await poll(async () => (await info()).includes('cropped'), { timeout: 4000 });
  r = await pixels([[50 * k, 100 * k]]);
  check(near(r.px[0], BLUE_PEN, 40), 'drawing kept after a later crop', r);

  // Window Undo: crop, then drawing.
  await page.keyboard.press('Control+z');
  await poll(async () => !(await info()).includes('cropped'), { timeout: 3000 });
  await page.keyboard.press('Control+z');
  check(await poll(async () => !(await info()).includes('drawn on'), { timeout: 3000 }), 'info line no longer says "drawn on"');
  r = await pixels([[100 * k, 120 * k]]);
  check(near(r.px[0], RED), 'Undo removes the drawing again', r.px[0]);

  // Esc cancels without changes.
  P = await openDraw();
  await page.keyboard.press('3');
  await drag(P(100, 100), P(200, 200));
  await page.keyboard.press('Escape');
  await page.waitForTimeout(200);
  const esc = { hidden: await page.$eval('#draw', e => e.hidden), info: await info() };
  check(esc.hidden && !esc.info.includes('drawn on'), 'Esc cancels the drawing', esc);

  check(pageErrors.length === 0, 'no page errors', pageErrors);
  await close();
  finish();
})().catch(e => { console.error(e); process.exit(1); });
