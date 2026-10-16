// UI language: launches Chromium with --lang=<xx> and checks that the popup, the clipboard window,
// the permission window and the context-menu titles use that language.
// LANGS=de node locale.js  (default: de,da,es,en)
const { EXT, check, finish, launch, path, fs } = require('./lib');

const LANGS = (process.env.LANGS || 'de,da,es,en').split(',');
const messages = lang => JSON.parse(fs.readFileSync(path.join(EXT, '_locales', lang, 'messages.json'), 'utf8'));
const msg = (m, key) => m[key].message.replace(/\$\w+\$/g, '');

(async () => {
  for (const lang of LANGS) {
    const m = messages(lang);
    const { ctx, sw, extId, pageErrors, close } = await launch({ lang });

    const popup = await ctx.newPage();
    await popup.goto(`chrome-extension://${extId}/popup.html`);
    await popup.waitForSelector('#resDetail:not(:empty)');
    const ui = await popup.evaluate(() => ({
      lang: document.documentElement.lang,
      title: document.title,
      caps: [...document.querySelectorAll('.cap span[data-i18n]')].map(e => e.textContent),
      resolution: document.querySelector('label[for=resolution]').textContent,
      detail: document.querySelector('#resDetail').textContent,
      tip: document.querySelector('.tip').innerHTML,
      options: [...document.querySelectorAll('#afterCapture option')].map(o => o.textContent),
      untranslated: [...document.querySelectorAll('[data-i18n]')].filter(e => !e.textContent.trim()).length
    }));
    console.log(`[${lang}] popup:`, JSON.stringify(ui));
    check(ui.lang === lang, `[${lang}] <html lang>`, ui.lang);
    check(ui.caps.join('|') === ['modeVisible', 'modeRegion', 'modeFull', 'modeTabs'].map(k => m[k].message).join('|'), `[${lang}] capture buttons translated`, ui.caps);
    check(ui.resolution === m.resolution.message, `[${lang}] settings labels translated`);
    check(ui.options[0] === m.acClipboard.message, `[${lang}] select options translated`);
    check(ui.detail.includes(msg(m, 'qDetail').split('·')[1].trim()), `[${lang}] quality detail line translated`, ui.detail);
    check(ui.tip.includes('<b>ImageShot</b>') && ui.tip.startsWith(m.tip.message.split('$name$')[0].replace(/&/g, '&amp;')), `[${lang}] tip with bold menu name`, ui.tip);
    check(ui.untranslated === 0, `[${lang}] no empty data-i18n elements`, ui.untranslated);
    if (lang === 'de') {
      check(ui.caps[0] === 'Sichtbarer Bereich' && ui.caps[3] === 'Alle Tabs', '[de] popup shows German text', ui.caps);
      check(ui.resolution === 'Auflösung', '[de] "Auflösung" label');
    }
    if (lang === 'da') check(ui.caps[0] === 'Synligt område', '[da] popup shows Danish text', ui.caps);

    const clip = await ctx.newPage();
    await clip.goto(`chrome-extension://${extId}/clipboard.html?id=none`);
    await clip.waitForSelector('.empty');
    const c = await clip.evaluate(() => ({ title: document.title, empty: document.querySelector('.empty').textContent, copy: document.querySelector('#copyMain').textContent }));
    check(c.empty === m.captureGone.message && c.copy === m.btnCopy.message && c.title === m.clipTitle.message, `[${lang}] clipboard window translated`, c);

    const perm = await ctx.newPage();
    await perm.goto(`chrome-extension://${extId}/permission.html`);
    const p = await perm.evaluate(() => ({ h: document.querySelector('h1').textContent, grant: document.querySelector('#grant').textContent }));
    check(p.h === m.permTitle.message && p.grant === m.permGrant.message, `[${lang}] permission window translated`, p);

    const sw_ = await sw.evaluate(() => ({ menu: ['modeVisible', 'modeRegion', 'modeFull', 'modeTabs'].map(k => skbT(k)), hint: skbT('regionHint'), ui: chrome.i18n.getUILanguage() }));
    check(sw_.menu[0] === m.modeVisible.message && sw_.hint === m.regionHint.message, `[${lang}] context-menu titles / overlay hint (service worker)`, sw_);

    check(pageErrors.length === 0, `[${lang}] no errors on any page`, pageErrors);
    await close();
  }
  finish();
})().catch(e => { console.error(e); process.exit(1); });
