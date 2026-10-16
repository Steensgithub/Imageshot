(async () => {
  const $ = id => document.getElementById(id);
  const params = new URLSearchParams(location.search);
  const id = params.get('id');
  const autoCopy = params.get('auto') === 'copy';

  const settings = await skbGetSettings();
  $('autoClose').checked = !!settings.autoClose;
  $('autoClose').addEventListener('change', e => {
    settings.autoClose = e.target.checked;
    skbSaveSettings({ autoClose: e.target.checked });
  });
  const folder = skbFolder(settings.folder);
  $('where').textContent = folder ? skbT('savedIn', folder) : skbT('savedInRoot');
  $('closeBtn').addEventListener('click', () => window.close());

  let data = null;
  try {
    data = id ? await skbStore.get(id) : null;
  } catch (e) {
    console.warn('ImageShot: could not read capture', e);
  }
  if (!data || !data.images?.length) {
    const p = document.createElement('p');
    p.className = 'empty';
    p.textContent = skbT('captureGone');
    $('shots').replaceChildren(p);
    $('copyMain').disabled = $('saveMain').disabled = $('cropMain').disabled = $('drawMain').disabled = true;
    return;
  }

  const images = data.images;
  const multi = images.length > 1;
  $('shots').classList.add(multi ? 'multi' : 'single');
  if (data.errors?.length) {
    $('warn').hidden = false;
    $('warn').textContent = skbT('tabsFailed', data.errors.length);
  }

  // The Blobs stay readable in this page; the stored record is no longer needed once the window closes.
  // (The service worker also deletes it from windows.onRemoved, and old records are purged on startup.)
  const urls = new Set();
  const objectUrl = blob => { const u = URL.createObjectURL(blob); urls.add(u); return u; };
  addEventListener('pagehide', () => {
    skbStore.remove(id).catch(() => {});
    for (const u of urls) URL.revokeObjectURL(u);
  });

  const kb = n => n > 1048576 ? skbNumber(n / 1048576, 1) + ' MB' : skbNumber(Math.round(n / 1024)) + ' KB';

  const cards = new Map(); // image -> card
  images.forEach((img, i) => {
    img.orig = img.blob;  // cropping always starts from the original (no extra quality loss)
    img.url = objectUrl(img.blob);
    img.crop = null;
    const el = document.createElement('article');
    el.className = 'shot';
    el.innerHTML = `
      <div class="frame"><img alt=""></div>
      <div class="meta">
        <div class="info"><b></b><span></span></div>
      </div>`;
    if (multi) {
      for (const [act, key] of [['copy', 'btnCopy'], ['draw', 'btnDraw'], ['crop', 'btnCrop'], ['save', 'btnSave']]) {
        const b = document.createElement('button');
        b.className = 'btn';
        b.dataset.act = act;
        b.textContent = skbT(key);
        el.querySelector('.meta').appendChild(b);
      }
    }
    el.querySelector('b').textContent = multi ? (img.title || skbT('imageN', i + 1)) : img.name;
    el.querySelectorAll('[data-act]').forEach(b => b.addEventListener('click', async () => {
      if (b.dataset.act === 'copy') await copy(img, false);
      else if (b.dataset.act === 'crop') openCrop(img);
      else if (b.dataset.act === 'draw') await openDraw(img);
      else await save([img], false);
    }));
    cards.set(img, el);
    render(img);
    $('shots').appendChild(el);
  });

  function render(img) {
    const el = cards.get(img);
    el.querySelector('img').src = img.url;
    el.querySelector('.info span').textContent =
      `${img.width} × ${img.height} px · ${img.ext.toUpperCase()} · ${kb(img.size)}${img.crop ? ' · ' + skbT('cropped') : ''}${img.drawn ? ' · ' + skbT('drawn') : ''}`;
  }

  if (multi) {
    $('copyMain').hidden = true;
    $('cropMain').hidden = true;
    $('drawMain').hidden = true;
    $('saveMain').textContent = skbT('btnSaveAll', images.length);
    $('saveMain').classList.add('primary');
  }
  $('copyMain').addEventListener('click', () => copy(images[0], true));
  $('cropMain').addEventListener('click', () => openCrop(images[0]));
  $('drawMain').addEventListener('click', () => openDraw(images[0]));
  $('undoMain').addEventListener('click', undo);
  $('saveMain').addEventListener('click', () => save(images, true));

  // ---- Handlinger ----

  async function toPng(img) {
    if (img.mime === 'image/png') return img.blob;
    const bmp = await createImageBitmap(img.blob);
    const c = document.createElement('canvas');
    c.width = bmp.width; c.height = bmp.height;
    c.getContext('2d').drawImage(bmp, 0, 0);
    return new Promise(r => c.toBlob(r, 'image/png'));
  }

  async function copy(img, closeAfter) {
    if (busy) return false;
    try {
      // The clipboard only accepts PNG – the image is converted when copying.
      await navigator.clipboard.write([new ClipboardItem({ 'image/png': toPng(img) })]);
      toast(skbT('toastCopied'));
      if (closeAfter || !multi) maybeClose();
      return true;
    } catch (e) {
      toast(skbT('toastCopyFail'), true);
      return false;
    }
  }

  async function save(list, closeAfter) {
    if (busy) return;
    const pending = [];
    try {
      for (const img of list) {
        // Extension pages may pass their own blob: URLs to chrome.downloads.
        const url = URL.createObjectURL(img.blob);
        const downloadId = await chrome.downloads.download({
          url,
          filename: skbFilePath(settings.folder, img.name, img.ext),
          conflictAction: 'uniquify',
          saveAs: false
        });
        pending.push(downloadFinished(downloadId).finally(() => URL.revokeObjectURL(url)));
      }
      toast(list.length > 1 ? skbT('toastSavedN', list.length) : skbT('toastSaved'));
      // Keep the page (and its blob: URLs) alive until the files are written.
      if ((closeAfter || !multi) && settings.autoClose) {
        await Promise.all(pending);
        maybeClose();
      }
    } catch (e) {
      toast(skbT('toastSaveFail', e.message || e), true);
    }
  }

  // Resolves when the download is no longer in progress (or after a timeout).
  function downloadFinished(downloadId, timeout = 30000) {
    return new Promise(resolve => {
      const done = () => { clearTimeout(timer); chrome.downloads.onChanged.removeListener(onChanged); resolve(); };
      const onChanged = d => { if (d.id === downloadId && d.state && d.state.current !== 'in_progress') done(); };
      const timer = setTimeout(done, timeout);
      chrome.downloads.onChanged.addListener(onChanged);
      chrome.downloads.search({ id: downloadId }).then(([d]) => { if (!d || d.state !== 'in_progress') done(); }, done);
    });
  }

  function maybeClose() {
    if (settings.autoClose) setTimeout(() => window.close(), 700);
  }

  let tt;
  function toast(text, err) {
    const t = $('toast');
    t.textContent = text;
    t.classList.toggle('err', !!err);
    t.classList.add('show');
    clearTimeout(tt);
    tt = setTimeout(() => t.classList.remove('show'), 1800);
  }

  // ---- Crop ----

  const history = []; // for Undo (Ctrl+Z)
  const crop = { img: null, W: 0, H: 0, s: 1, sel: null, drag: null };

  let busy = false; // while a cropped image is being encoded

  function openCrop(img) {
    if (busy) return;
    crop.img = img;
    crop.W = img.width;
    crop.H = img.height;
    crop.sel = null;
    $('cropImg').src = img.url;
    $('crop').hidden = false;
    layoutCrop();
    drawSel();
    $('cropStage').focus();
  }

  function closeCrop() {
    $('crop').hidden = true;
    $('cropImg').removeAttribute('src'); // frigiv billedet
    crop.img = null;
    crop.drag = null;
  }

  function layoutCrop() {
    const st = $('cropStage').getBoundingClientRect();
    const s = Math.min(1, (st.width - 48) / crop.W, (st.height - 48) / crop.H);
    crop.s = Math.max(0.01, s);
    const w = Math.round(crop.W * crop.s), h = Math.round(crop.H * crop.s);
    Object.assign($('cropCanvas').style, {
      width: w + 'px', height: h + 'px',
      left: Math.round((st.width - w) / 2) + 'px', top: Math.round((st.height - h) / 2) + 'px'
    });
  }

  function drawSel() {
    const sel = crop.sel, box = $('cropSel');
    if (!sel || sel.w < 1 || sel.h < 1) {
      box.hidden = true;
      $('cropInfo').textContent = skbT('cropHint');
      return;
    }
    box.hidden = false;
    const s = crop.s;
    Object.assign(box.style, {
      left: sel.x * s + 'px', top: sel.y * s + 'px', width: sel.w * s + 'px', height: sel.h * s + 'px'
    });
    $('cropInfo').textContent = skbT('cropSize', [Math.round(sel.w), Math.round(sel.h)]);
  }

  // Mouse position in image pixels (clamped to the image).
  function point(e) {
    const r = $('cropCanvas').getBoundingClientRect();
    return {
      x: Math.max(0, Math.min(crop.W, (e.clientX - r.left) / crop.s)),
      y: Math.max(0, Math.min(crop.H, (e.clientY - r.top) / crop.s))
    };
  }

  $('cropStage').tabIndex = -1;
  $('cropStage').addEventListener('pointerdown', e => {
    if (e.button !== 0 || !crop.img) return;
    e.preventDefault();
    const p = point(e);
    const handle = e.target.dataset?.h;
    if (handle && crop.sel) {
      crop.drag = { mode: 'resize', h: handle, start: { ...crop.sel } };
    } else if (e.target === $('cropSel') && crop.sel) {
      crop.drag = { mode: 'move', dx: p.x - crop.sel.x, dy: p.y - crop.sel.y };
    } else {
      crop.drag = { mode: 'new', ax: p.x, ay: p.y };
      crop.sel = { x: p.x, y: p.y, w: 0, h: 0 };
    }
    $('cropStage').setPointerCapture(e.pointerId);
    drawSel();
  });

  $('cropStage').addEventListener('pointermove', e => {
    const d = crop.drag;
    if (!d) return;
    const p = point(e);
    if (d.mode === 'new') {
      crop.sel = { x: Math.min(d.ax, p.x), y: Math.min(d.ay, p.y), w: Math.abs(p.x - d.ax), h: Math.abs(p.y - d.ay) };
    } else if (d.mode === 'move') {
      const s = crop.sel;
      s.x = Math.max(0, Math.min(crop.W - s.w, p.x - d.dx));
      s.y = Math.max(0, Math.min(crop.H - s.h, p.y - d.dy));
    } else {
      const o = d.start;
      let x1 = o.x, y1 = o.y, x2 = o.x + o.w, y2 = o.y + o.h;
      if (d.h.includes('w')) x1 = p.x;
      if (d.h.includes('e')) x2 = p.x;
      if (d.h.includes('n')) y1 = p.y;
      if (d.h.includes('s')) y2 = p.y;
      crop.sel = { x: Math.min(x1, x2), y: Math.min(y1, y2), w: Math.abs(x2 - x1), h: Math.abs(y2 - y1) };
    }
    drawSel();
  });

  const endDrag = () => {
    if (!crop.drag) return;
    crop.drag = null;
    if (crop.sel && (crop.sel.w < 3 || crop.sel.h < 3)) crop.sel = null; // a click removes the frame
    drawSel();
  };
  $('cropStage').addEventListener('pointerup', endDrag);
  $('cropStage').addEventListener('pointercancel', endDrag);
  $('cropStage').addEventListener('dblclick', e => {
    const sel = crop.sel;
    if (!sel) return;
    const p = point(e);
    if (p.x >= sel.x && p.x <= sel.x + sel.w && p.y >= sel.y && p.y <= sel.y + sel.h) applyCrop();
  });

  $('cropCancel').addEventListener('click', closeCrop);
  $('cropApply').addEventListener('click', applyCrop);
  addEventListener('resize', () => { if (crop.img) { layoutCrop(); drawSel(); } });

  async function applyCrop() {
    const img = crop.img, sel = crop.sel;
    if (!img) return;
    if (!sel || sel.w < 3 || sel.h < 3) { closeCrop(); return; }
    // The frame is in pixels of the displayed image – convert to the original.
    const base = img.crop || { x: 0, y: 0 };
    const x = Math.round(sel.x), y = Math.round(sel.y);
    const r = {
      x: base.x + x,
      y: base.y + y,
      w: Math.max(1, Math.min(Math.round(sel.w), crop.W - x)),
      h: Math.max(1, Math.min(Math.round(sel.h), crop.H - y))
    };
    closeCrop();
    busy = true;
    try {
      const before = snapshot(img);
      await encodeCrop(img, r);
      history.push(before);
      $('undoMain').hidden = false;
      render(img);
      toast(skbT('toastCropped'));
    } catch (e) {
      toast(skbT('toastCropFail', e.message || e), true);
    } finally {
      busy = false;
    }
  }

  async function encodeCrop(img, r) {
    const bmp = await createImageBitmap(img.orig);
    const c = document.createElement('canvas');
    c.width = r.w; c.height = r.h;
    const ctx = c.getContext('2d');
    if (img.mime === 'image/jpeg') { ctx.fillStyle = '#ffffff'; ctx.fillRect(0, 0, r.w, r.h); }
    ctx.drawImage(bmp, r.x, r.y, r.w, r.h, 0, 0, r.w, r.h);
    bmp.close();
    const q = skbQuality(settings);
    const blob = await new Promise(res => c.toBlob(res, img.mime, q.quality));
    if (!blob) throw new Error(skbT('encodeFail'));
    img.blob = blob;
    img.url = objectUrl(blob);
    img.width = r.w;
    img.height = r.h;
    img.size = blob.size;
    img.crop = r;
  }

  const snapshot = img => ({ img, orig: img.orig, crop: img.crop, drawn: img.drawn, blob: img.blob, url: img.url, width: img.width, height: img.height, size: img.size });

  // ---- Draw ----
  async function openDraw(img) {
    if (busy || skbDraw.active) return;
    const canvas = await skbDraw.open(img.blob);
    if (!canvas) return;
    busy = true;
    try {
      const before = snapshot(img);
      const c = canvas;
      if (img.mime === 'image/jpeg') {
        // JPEG has no transparency – put a white background underneath.
        const g = c.getContext('2d'); g.globalCompositeOperation = 'destination-over'; g.fillStyle = '#fff'; g.fillRect(0, 0, c.width, c.height);
      }
      const q = skbQuality(settings);
      const blob = await new Promise(res => c.toBlob(res, img.mime, q.quality));
      if (!blob) throw new Error(skbT('encodeFail'));
      // The drawing becomes the new base image, so a later crop keeps it.
      Object.assign(img, { blob, orig: blob, crop: null, url: objectUrl(blob), size: blob.size, width: c.width, height: c.height, drawn: true });
      history.push(before);
      $('undoMain').hidden = false;
      render(img);
      toast(skbT('toastDrawn'));
    } catch (e) {
      toast(skbT('toastCropFail', e.message || e), true);
    } finally {
      busy = false;
    }
  }

  function undo() {
    if (busy) return;
    const h = history.pop();
    if (!h) return;
    Object.assign(h.img, { orig: h.orig, crop: h.crop, drawn: h.drawn, blob: h.blob, url: h.url, width: h.width, height: h.height, size: h.size });
    render(h.img);
    $('undoMain').hidden = history.length === 0;
    toast(skbT('toastUndone'));
  }

  addEventListener('keydown', e => {
    if (crop.img) {
      if (e.key === 'Enter' && e.target.tagName === 'BUTTON') return; // e.g. Enter on "Cancel"
      if (e.key === 'Enter') { e.preventDefault(); applyCrop(); }
      else if (e.key === 'Escape') { e.preventDefault(); closeCrop(); }
      return;
    }
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') { e.preventDefault(); undo(); }
  });

  // "Copy directly": copy right away; always close afterwards if it worked.
  if (autoCopy) {
    window.focus();
    await new Promise(r => setTimeout(r, 120));
    try {
      await navigator.clipboard.write([new ClipboardItem({ 'image/png': toPng(images[0]) })]);
      toast(skbT('toastCopied'));
      setTimeout(() => window.close(), 600);
    } catch (e) {
      toast(skbT('toastClickCopy'), true);
    }
  }
})();
