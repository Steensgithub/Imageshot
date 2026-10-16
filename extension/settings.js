// Shared settings and helpers (used by the service worker, the popup and the clipboard window).

const SKB_BRAND = 'ImageShot';

const SKB_DEFAULTS = {
  resolution: 70,            // 0 = smallest file, 100 = highest resolution
  format: 'webp',            // webp | png | jpeg
  afterCapture: 'clipboard', // clipboard | copy | save
  autoClose: true,           // close the clipboard window automatically after copy/save
  allTabsScope: 'window',    // window | all
  folder: SKB_BRAND          // subfolder in Downloads
};

const SKB_FORMATS = {
  webp: { mime: 'image/webp', ext: 'webp', label: 'WebP' },
  png:  { mime: 'image/png',  ext: 'png',  label: 'PNG'  },
  jpeg: { mime: 'image/jpeg', ext: 'jpg',  label: 'JPEG' }
};

// Host access needed only for "All tabs" (requested at runtime).
const SKB_ALL_URLS = { origins: ['<all_urls>'] };

// Localized string; `subs` may be a single value or an array ($1, $2 ...).
function skbT(key, subs) {
  const list = subs === undefined ? undefined : (Array.isArray(subs) ? subs : [subs]).map(String);
  return chrome.i18n.getMessage(key, list) || key;
}

// The locale whose messages are actually used (en, da, de or es).
function skbLocale() {
  return chrome.i18n.getMessage('locale') || 'en';
}

function skbNumber(n, digits = 0) {
  return new Intl.NumberFormat(skbLocale(), { maximumFractionDigits: digits, minimumFractionDigits: digits }).format(n);
}

async function skbGetSettings() {
  const stored = await chrome.storage.sync.get(SKB_DEFAULTS);
  return { ...SKB_DEFAULTS, ...stored };
}

function skbSaveSettings(patch) {
  return chrome.storage.sync.set(patch);
}

// Maps the slider value to scale and quality.
function skbQuality(settings) {
  const v = Math.max(0, Math.min(100, Number(settings.resolution)));
  const t = v / 100;
  const scale = v >= 100 ? 1 : 0.5 + 0.5 * Math.pow(t, 0.82);
  const quality = Math.round(50 + 50 * t) / 100;
  const fmt = SKB_FORMATS[settings.format] || SKB_FORMATS.webp;

  let label;
  if (v <= 15) label = skbT('qSmallest');
  else if (v <= 40) label = skbT('qSmall');
  else if (v <= 80) label = skbT('qBalanced');
  else if (v < 100) label = skbT('qHigh');
  else label = skbT('qMax');

  const pct = Math.round(scale * 100);
  const qText = fmt.mime === 'image/png' ? skbT('qLossless') : skbT('qQuality', Math.round(quality * 100));
  return {
    scale,
    quality,
    mime: fmt.mime,
    ext: fmt.ext,
    label,
    detail: skbT('qDetail', [fmt.label, pct, qText])
  };
}

// YYYY-MM-DD HH.mm.ss (dots instead of colons: valid in file names everywhere).
function skbStamp(d = new Date()) {
  const p = n => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}.${p(d.getMinutes())}.${p(d.getSeconds())}`;
}

function skbClean(part) {
  return String(part || '')
    .replace(/[<>:"/\\|?*\u0000-\u001f~]/g, '')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/^\.+|\.+$/g, '');
}

function skbFolder(folder) {
  return String(folder || '')
    .split(/[\\/]+/)
    .map(skbClean)
    .filter(part => part && part !== '..')
    .join('/');
}

function skbFilePath(folder, baseName, ext) {
  const dir = skbFolder(folder);
  const name = skbClean(baseName).slice(0, 120) || SKB_BRAND;
  return (dir ? dir + '/' : '') + name + '.' + ext;
}
