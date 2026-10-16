// Fills in localized text on extension pages.
//   data-i18n="key"              -> textContent
//   data-i18n-bold="text"        -> $1 in the message is shown as <b>text</b>
//   data-i18n-title="key"        -> title attribute
//   <html data-i18n-doc="key">    -> document title
function skbApplyI18n(root = document) {
  document.documentElement.lang = skbLocale();
  for (const el of root.querySelectorAll('[data-i18n]')) {
    const bold = el.getAttribute('data-i18n-bold');
    if (bold === null) {
      el.textContent = skbT(el.dataset.i18n);
      continue;
    }
    // Build nodes instead of using innerHTML.
    const marker = '\uE000';
    const [before, after = ''] = skbT(el.dataset.i18n, marker).split(marker);
    const b = document.createElement('b');
    b.textContent = bold;
    el.replaceChildren(before, b, after);
  }
  for (const el of root.querySelectorAll('[data-i18n-title]')) el.title = skbT(el.dataset.i18nTitle);
  const titleKey = document.documentElement.dataset.i18nDoc;
  if (titleKey) document.title = skbT(titleKey);
}

skbApplyI18n();
