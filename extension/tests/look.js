// Look setting: "Vivid" sets <html data-theme="vivid"> on every extension page (live, and on later page loads),
// uses the bundled font, keeps contrast, and "Standard" restores the light/dark look.
const { check, finish, launch, poll } = require('./lib');

const lum = rgb => {
  const v = rgb.match(/[\d.]+/g).slice(0, 3).map(n => n / 255).map(c => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
  return 0.2126 * v[0] + 0.7152 * v[1] + 0.0722 * v[2];
};
const contrast = (a, b) => { const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); };

(async () => {
  const { ctx, extId, pageErrors, close } = await launch();
  const url = f => `chrome-extension://${extId}/${f}`;
  const theme = p => p.evaluate(() => document.documentElement.dataset.theme || '');

  const popup = await ctx.newPage();
  await popup.goto(url('popup.html'));
  await popup.waitForSelector('#resDetail:not(:empty)');
  check(await theme(popup) === '', 'standard look by default');
  check(await popup.evaluate(() => document.querySelector('label[for=look]').textContent) === 'Look', 'look setting has a visible label');
  const before = await popup.evaluate(() => getComputedStyle(document.body).backgroundColor);

  const other = await ctx.newPage();
  await other.goto(url('permission.html'));

  await popup.selectOption('#look', 'vivid');
  check(await poll(async () => await theme(popup) === 'vivid'), 'popup switches to vivid at once');
  check(await poll(async () => await theme(other) === 'vivid'), 'an already open page switches too');

  const v = await popup.evaluate(async () => {
    await document.fonts.load('700 17px "Atkinson Hyperlegible Next"');
    await document.fonts.ready;
    const cs = (sel, prop) => getComputedStyle(document.querySelector(sel))[prop];
    return {
      bg: cs('body', 'backgroundColor'), text: cs('body', 'color'), font: cs('body', 'fontFamily'),
      loaded: document.fonts.check('15px "Atkinson Hyperlegible Next"'),
      faces: [...document.fonts].filter(f => f.status === 'loaded').map(f => f.family),
      muted: cs('.tip', 'color'), mutedSize: parseFloat(cs('.tip', 'fontSize')),
      card: cs('.card', 'backgroundColor'), radius: cs('.card', 'borderTopLeftRadius'),
      ico: cs('.cap .ico', 'backgroundColor'), icoInk: cs('.cap .ico', 'color'),
      fieldBorder: cs('#look', 'borderTopColor'), field: cs('#look', 'backgroundColor'),
      scroll: document.documentElement.scrollWidth > document.documentElement.clientWidth
    };
  });
  console.log('vivid popup:', JSON.stringify(v));
  check(v.bg === 'rgb(255, 246, 236)' && v.bg !== before, 'vivid background applied', v.bg);
  check(v.font.startsWith('"Atkinson Hyperlegible Next"') && v.loaded && v.faces.length > 0, 'bundled font loaded from the extension', v.faces);
  check(contrast(v.text, v.bg) >= 4.5 && contrast(v.muted, v.bg) >= 4.5, 'text and muted text ≥ 4.5:1 on the page', [contrast(v.text, v.bg), contrast(v.muted, v.bg)]);
  check(contrast(v.icoInk, v.ico) >= 4.5, 'capture glyph ≥ 4.5:1 on its chip', contrast(v.icoInk, v.ico));
  check(contrast(v.fieldBorder, v.card) >= 3 && contrast(v.fieldBorder, v.field) >= 3, 'field border ≥ 3:1', contrast(v.fieldBorder, v.field));
  check(v.mutedSize >= 14 && v.radius === '20px', 'larger hint text and rounder cards', [v.mutedSize, v.radius]);
  check(!v.scroll, 'no sideways scroll in the popup');

  // Keyboard: the new select is reachable and shows a focus indication.
  await popup.focus('#look');
  const focus = await popup.evaluate(() => { const c = getComputedStyle(document.activeElement); return { id: document.activeElement.id, ring: c.boxShadow, border: c.borderTopColor }; });
  check(focus.id === 'look' && focus.ring !== 'none', 'look select shows a focus ring', focus);

  const clip = await ctx.newPage();
  await clip.goto(url('clipboard.html'));
  check(await poll(async () => await theme(clip) === 'vivid'), 'a page opened later starts in vivid');
  const stored = await popup.evaluate(() => chrome.storage.sync.get('look'));
  check(stored.look === 'vivid', 'choice is saved', stored);

  await popup.selectOption('#look', 'standard');
  check(await poll(async () => await theme(popup) === '' && await theme(clip) === '' && await theme(other) === ''), 'standard restores every page');
  check(await popup.evaluate(() => getComputedStyle(document.body).backgroundColor) === before, 'standard background is back');

  check(pageErrors.length === 0, 'no page errors', pageErrors);
  await close();
  finish();
})().catch(e => { console.error(e); process.exit(1); });
