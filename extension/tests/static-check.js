// Checks without a browser: locale files, message keys used in the code, manifest permissions.
const fs = require('fs');
const path = require('path');

const EXT = path.resolve(__dirname, '..');
const LANGS = ['en', 'da', 'de', 'es'];
const failures = [];
const check = (cond, label, detail) => {
  console.log(`${cond ? 'ok  ' : 'FAIL'} ${label}${detail !== undefined ? ' – ' + JSON.stringify(detail) : ''}`);
  if (!cond) failures.push(label);
};

const read = f => fs.readFileSync(path.join(EXT, f), 'utf8');
const manifest = JSON.parse(read('manifest.json'));
const bundles = Object.fromEntries(LANGS.map(l => [l, JSON.parse(read(`_locales/${l}/messages.json`))]));

// Locale files: same keys, same placeholders, placeholders actually used.
const keys = Object.keys(bundles.en).sort();
for (const l of LANGS) {
  const k = Object.keys(bundles[l]).sort();
  check(JSON.stringify(k) === JSON.stringify(keys), `[${l}] same message keys as en`, k.filter(x => !keys.includes(x)).concat(keys.filter(x => !k.includes(x))));
  for (const key of keys) {
    const m = bundles[l][key];
    if (!m) continue;
    const ph = Object.keys(m.placeholders || {}).sort();
    const enPh = Object.keys(bundles.en[key].placeholders || {}).sort();
    const used = [...m.message.matchAll(/\$(\w+)\$/g)].map(x => x[1].toLowerCase());
    if (JSON.stringify(ph) !== JSON.stringify(enPh) || !ph.every(p => used.includes(p.toLowerCase())) || !used.every(u => ph.map(p => p.toLowerCase()).includes(u))) {
      check(false, `[${l}] placeholders of "${key}"`, { ph, enPh, used });
    }
    if (!m.message.trim()) check(false, `[${l}] "${key}" is empty`);
  }
}
check(bundles.da.modeVisible.message === 'Synligt område', 'Danish wording kept from the original UI');

// Every key referenced by the code / HTML / manifest exists.
const used = new Set();
for (const f of fs.readdirSync(EXT).filter(f => /\.(js|html)$/.test(f))) {
  const src = read(f).split('\n').filter(line => !/^\s*\/\//.test(line)).join('\n'); // ignore comment lines
  for (const m of src.matchAll(/skbT\('(\w+)'/g)) used.add(m[1]);
  for (const m of src.matchAll(/data-i18n(?:-doc|-title)?="(\w+)"/g)) used.add(m[1]);
  for (const m of src.matchAll(/\{ id: '\w+',\s+title: '(\w+)' \}/g)) used.add(m[1]); // context menu table
  for (const m of src.matchAll(/\['\w+', '(btn\w+)'\]/g)) used.add(m[1]);
}
for (const m of read('manifest.json').matchAll(/__MSG_(\w+)__/g)) used.add(m[1]);
used.add('locale');
const missing = [...used].filter(k => !keys.includes(k));
check(missing.length === 0, 'all message keys used in the code exist', missing);
const unused = keys.filter(k => !used.has(k));
check(unused.length === 0, 'no unused message keys', unused);

// No hard-coded Danish left in user-facing code.
for (const f of ['background.js', 'clipboard.js', 'popup.js', 'settings.js', 'popup.html', 'clipboard.html', 'permission.html', 'permission.js']) {
  const hits = read(f).split('\n').filter(line => /[æøåÆØÅ]/.test(line));
  check(hits.length === 0, `${f}: no hard-coded Danish text`, hits.slice(0, 3));
}

// Manifest.
check(manifest.version === '2.0.0', 'version 2.0.0');
check(manifest.default_locale === 'en', 'default_locale en');
check(manifest.name === '__MSG_extName__' && manifest.description === '__MSG_extDescription__', 'localized name/description');
check(!(manifest.host_permissions || []).length, 'no host_permissions (activeTab instead)', manifest.host_permissions);
check(JSON.stringify(manifest.optional_host_permissions) === '["<all_urls>"]', '<all_urls> is optional');
check(manifest.permissions.includes('activeTab') && !manifest.permissions.includes('tabs'), 'activeTab used, "tabs" not requested', manifest.permissions);
const doc = read('PERMISSIONS.md');
for (const p of [...manifest.permissions, ...manifest.optional_host_permissions]) {
  check(doc.includes('`' + p + '`'), `PERMISSIONS.md documents ${p}`);
}
for (const f of ['icons/icon16.png', 'icons/icon32.png', 'icons/icon48.png', 'icons/icon128.png']) {
  check(fs.existsSync(path.join(EXT, f)), `${f} exists`);
}
check(!fs.existsSync(path.join(EXT, 'LÆS MIG.txt')), '"LÆS MIG.txt" removed');
for (const r of ['README.md', 'README.da.md', 'README.de.md', 'README.es.md']) check(fs.existsSync(path.join(EXT, r)), `${r} exists`);

if (failures.length) { console.log(`\n${failures.length} check(s) failed`); process.exit(1); }
console.log('\nall checks passed');
