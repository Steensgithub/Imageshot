// Drawing tools in the clipboard window: arrow, rectangle, ellipse, highlight, text, step number, blur, pixelate.
// skbDraw.open(blob) shows the editor and resolves with the finished <canvas> (or null when cancelled).
// Shapes are kept in image pixels, so the result is full resolution whatever the zoom.
const skbDraw = (() => {
  const $ = id => document.getElementById(id);
  const COLORS = ['#E5484D', '#F78C1E', '#FFD600', '#2FB34A', '#1F6FEB', '#1B1F24', '#FFFFFF'];
  const WIDTHS = [2, 4, 8];
  const TOOLS = ['arrow', 'rect', 'ellipse', 'highlight', 'text', 'step', 'blur', 'pixelate'];
  const state = { tool: 'arrow', color: 0, width: 1 };
  let ui = null;

  // ---------------- Drawing shapes ----------------
  function drawShape(g, s, src) {
    g.save();
    g.lineCap = 'round'; g.lineJoin = 'round';
    g.strokeStyle = g.fillStyle = s.color;
    g.lineWidth = s.width;
    const x = Math.min(s.x1, s.x2), y = Math.min(s.y1, s.y2), w = Math.abs(s.x2 - s.x1), h = Math.abs(s.y2 - s.y1);
    switch (s.type) {
      case 'arrow': arrow(g, s); break;
      case 'rect': g.strokeRect(x, y, w, h); break;
      case 'ellipse':
        g.beginPath(); g.ellipse(x + w / 2, y + h / 2, Math.max(1, w / 2), Math.max(1, h / 2), 0, 0, Math.PI * 2); g.stroke(); break;
      case 'highlight':
        g.globalAlpha = 0.38; g.fillRect(x, y, w, h); break;
      case 'text': text(g, s); break;
      case 'step': step(g, s); break;
      case 'blur': case 'pixelate': effect(g, s, src, x, y, w, h); break;
    }
    g.restore();
  }

  function arrow(g, s) {
    const dx = s.x2 - s.x1, dy = s.y2 - s.y1, len = Math.hypot(dx, dy);
    if (len < 1) return;
    const ux = dx / len, uy = dy / len;
    const head = Math.min(len * 0.6, s.width * 4.5 + 8), hw = head * 0.55;
    const bx = s.x2 - ux * head, by = s.y2 - uy * head;
    g.beginPath(); g.moveTo(s.x1, s.y1); g.lineTo(s.x2 - ux * head * 0.6, s.y2 - uy * head * 0.6); g.stroke();
    g.beginPath();
    g.moveTo(s.x2, s.y2);
    g.lineTo(bx - uy * hw, by + ux * hw);
    g.lineTo(s.x2 - ux * head * 0.8, s.y2 - uy * head * 0.8);
    g.lineTo(bx + uy * hw, by - ux * hw);
    g.closePath(); g.fill();
  }

  const font = size => `600 ${size}px "Segoe UI", system-ui, sans-serif`;
  const light = c => { const n = parseInt(c.slice(1), 16); return (((n >> 16) & 255) * 0.299 + ((n >> 8) & 255) * 0.587 + (n & 255) * 0.114) > 170; };

  function text(g, s) {
    g.font = font(s.size);
    g.textBaseline = 'top';
    // Contrasting outline so the text is readable on light and dark backgrounds.
    g.lineWidth = Math.max(2, s.size / 7);
    g.strokeStyle = light(s.color) ? 'rgba(20,20,20,.86)' : 'rgba(255,255,255,.9)';
    g.strokeText(s.text, s.x1, s.y1);
    g.fillText(s.text, s.x1, s.y1);
  }

  function step(g, s) {
    const r = s.r;
    g.beginPath(); g.arc(s.x1, s.y1, r, 0, Math.PI * 2); g.fill();
    g.lineWidth = Math.max(2, r / 8); g.strokeStyle = '#fff'; g.stroke();
    g.fillStyle = light(s.color) ? '#000' : '#fff';
    g.font = `700 ${Math.round(r * (s.n > 9 ? 0.95 : 1.15))}px "Segoe UI", system-ui, sans-serif`;
    g.textAlign = 'center'; g.textBaseline = 'middle';
    g.fillText(String(s.n), s.x1, s.y1 + r * 0.06);
  }

  // Blur / pixelate: irreversible – strong downscale and upscale of the area.
  function effect(g, s, src, x, y, w, h) {
    x = Math.max(0, Math.round(x)); y = Math.max(0, Math.round(y));
    w = Math.min(src.width - x, Math.round(w)); h = Math.min(src.height - y, Math.round(h));
    if (w < 2 || h < 2) return;
    const pix = s.type === 'pixelate';
    const block = Math.min(24, pix ? Math.max(8, Math.min(w, h) / 12) : Math.max(6, Math.min(w, h) / 6));
    const sw = Math.max(1, Math.round(w / block)), sh = Math.max(1, Math.round(h / block));
    const small = new OffscreenCanvas(sw, sh), sg = small.getContext('2d');
    sg.imageSmoothingQuality = 'high';
    sg.drawImage(src, x, y, w, h, 0, 0, sw, sh);
    g.imageSmoothingEnabled = !pix;
    g.imageSmoothingQuality = 'high';
    g.drawImage(small, 0, 0, sw, sh, x, y, w, h);
  }

  // ---------------- Editor ----------------
  function build() {
    const root = $('draw');
    const bar = $('drawTools');
    TOOLS.forEach((t, i) => {
      const b = document.createElement('button');
      b.className = 'tool'; b.dataset.tool = t;
      b.title = `${toolTitle(t)} (${i + 1})`;
      b.innerHTML = ICONS[t];
      b.addEventListener('click', () => setTool(t));
      bar.appendChild(b);
    });
    bar.appendChild(Object.assign(document.createElement('span'), { className: 'sep' }));
    COLORS.forEach((c, i) => {
      const b = document.createElement('button');
      b.className = 'swatch'; b.dataset.color = i; b.title = skbT('toolColor');
      b.style.setProperty('--c', c);
      b.addEventListener('click', () => { state.color = i; mark(); });
      bar.appendChild(b);
    });
    bar.appendChild(Object.assign(document.createElement('span'), { className: 'sep' }));
    WIDTHS.forEach((w, i) => {
      const b = document.createElement('button');
      b.className = 'width'; b.dataset.width = i; b.title = ['S', 'M', 'L'][i];
      b.innerHTML = `<i style="height:${w + 1}px"></i>`;
      b.addEventListener('click', () => { state.width = i; mark(); });
      bar.appendChild(b);
    });
    bar.appendChild(Object.assign(document.createElement('span'), { className: 'sep' }));
    for (const [id, title, fn] of [['drawUndo', skbT('btnUndoCrop') + ' (Ctrl+Z)', () => undo()], ['drawRedo', skbT('toolRedo') + ' (Ctrl+Y)', () => redo()]]) {
      const b = document.createElement('button');
      b.className = 'tool'; b.id = id; b.title = title;
      b.innerHTML = id === 'drawUndo' ? ICONS.undo : ICONS.redo;
      b.addEventListener('click', fn);
      bar.appendChild(b);
    }
    // Toolbar buttons must not take the focus (Enter/Esc/1–8 keep working).
    bar.addEventListener('mousedown', e => e.preventDefault());
    const stage = $('drawStage');
    stage.addEventListener('pointerdown', down);
    stage.addEventListener('pointermove', move);
    stage.addEventListener('pointerup', up);
    stage.addEventListener('pointercancel', up);
    $('drawCancel').addEventListener('click', () => finish(false));
    $('drawApply').addEventListener('click', () => finish(true));
    addEventListener('resize', () => { if (ui) layout(); });
    addEventListener('keydown', key, true);
    return root;
  }

  function toolTitle(t) {
    return {
      arrow: skbT('tool_arrow'), rect: skbT('tool_rect'), ellipse: skbT('tool_ellipse'), highlight: skbT('tool_highlight'),
      text: skbT('tool_text'), step: skbT('tool_step'), blur: skbT('tool_blur'), pixelate: skbT('tool_pixelate')
    }[t];
  }

  function setTool(t) { commitText(); state.tool = t; mark(); }

  function mark() {
    for (const b of document.querySelectorAll('#drawTools .tool[data-tool]')) b.classList.toggle('on', b.dataset.tool === state.tool);
    for (const b of document.querySelectorAll('#drawTools .swatch')) b.classList.toggle('on', +b.dataset.color === state.color);
    for (const b of document.querySelectorAll('#drawTools .width')) b.classList.toggle('on', +b.dataset.width === state.width);
    if (ui) {
      $('drawUndo').disabled = ui.shapes.length === 0;
      $('drawRedo').disabled = ui.redo.length === 0;
      $('drawStage').style.cursor = state.tool === 'text' ? 'text' : 'crosshair';
    }
  }

  function layout() {
    const st = $('drawStage').getBoundingClientRect();
    const s = Math.min(1, (st.width - 48) / ui.src.width, (st.height - 48) / ui.src.height);
    ui.s = Math.max(0.01, s);
    const w = Math.round(ui.src.width * ui.s), h = Math.round(ui.src.height * ui.s);
    Object.assign(ui.canvas.style, {
      width: w + 'px', height: h + 'px',
      left: Math.round((st.width - w) / 2) + 'px', top: Math.round((st.height - h) / 2) + 'px'
    });
  }

  function render() {
    const g = ui.ctx;
    g.clearRect(0, 0, ui.canvas.width, ui.canvas.height);
    g.drawImage(ui.src, 0, 0);
    for (const s of ui.shapes) drawShape(g, s, ui.src);
    if (ui.cur) drawShape(g, ui.cur, ui.src);
    mark();
  }

  function point(e) {
    const r = ui.canvas.getBoundingClientRect();
    return { x: (e.clientX - r.left) / ui.s, y: (e.clientY - r.top) / ui.s };
  }

  function newShape(p) {
    const color = COLORS[state.color], width = WIDTHS[state.width];
    const s = { type: state.tool, color, width, x1: p.x, y1: p.y, x2: p.x, y2: p.y };
    if (s.type === 'highlight' && !light(color) && color !== '#E5484D') s.color = '#FFD600';
    return s;
  }

  function add(s) { ui.shapes.push(s); ui.redo = []; }

  function down(e) {
    if (!ui || e.button !== 0) return;
    if (e.target === ui.input) return;
    e.preventDefault();
    commitText();
    const p = point(e);
    if (state.tool === 'text') { beginText(e, p); return; }
    if (state.tool === 'step') {
      const n = ui.shapes.filter(s => s.type === 'step').reduce((m, s) => Math.max(m, s.n), 0) + 1;
      add({ ...newShape(p), r: 10 + WIDTHS[state.width] * 2.5, n });
      render();
      return;
    }
    ui.cur = newShape(p);
    $('drawStage').setPointerCapture(e.pointerId);
  }

  function move(e) {
    if (!ui || !ui.cur) return;
    let p = point(e);
    const c = ui.cur;
    if (e.shiftKey) {
      const dx = p.x - c.x1, dy = p.y - c.y1;
      if (c.type === 'arrow') {
        const a = Math.round(Math.atan2(dy, dx) / (Math.PI / 4)) * (Math.PI / 4), l = Math.hypot(dx, dy);
        p = { x: c.x1 + Math.cos(a) * l, y: c.y1 + Math.sin(a) * l };
      } else {
        const m = Math.max(Math.abs(dx), Math.abs(dy));
        p = { x: c.x1 + Math.sign(dx) * m, y: c.y1 + Math.sign(dy) * m };
      }
    }
    c.x2 = p.x; c.y2 = p.y;
    render();
  }

  function up() {
    if (!ui || !ui.cur) return;
    const c = ui.cur;
    ui.cur = null;
    if (Math.hypot(c.x2 - c.x1, c.y2 - c.y1) >= 4) add(c);
    render();
  }

  const textSize = () => 12 + WIDTHS[state.width] * 3;

  function beginText(e, p) {
    const inp = document.createElement('input');
    inp.className = 'draw-text';
    const size = textSize();
    Object.assign(inp.style, {
      left: e.clientX + 'px', top: e.clientY + 'px',
      font: font(Math.max(11, size * ui.s)), color: COLORS[state.color]
    });
    inp.placeholder = skbT('toolTextPrompt');
    ui.input = inp; ui.textAt = p; ui.textSize = size; ui.textColor = COLORS[state.color];
    inp.addEventListener('keydown', ev => {
      if (ev.key === 'Enter') { ev.preventDefault(); ev.stopPropagation(); commitText(); }
      else if (ev.key === 'Escape') { ev.preventDefault(); ev.stopPropagation(); cancelText(); }
      else ev.stopPropagation();
    });
    inp.addEventListener('blur', () => setTimeout(commitText, 0));
    document.body.appendChild(inp);
    inp.focus(); // at once, so no keystroke is lost
  }

  function commitText() {
    if (!ui || !ui.input) return;
    const inp = ui.input; ui.input = null;
    const t = inp.value.trim();
    inp.remove();
    if (t) add({ type: 'text', text: t, size: ui.textSize, color: ui.textColor, width: 0, x1: ui.textAt.x, y1: ui.textAt.y, x2: ui.textAt.x, y2: ui.textAt.y });
    render();
    $('drawStage').focus();
  }

  function cancelText() { if (ui && ui.input) { ui.input.remove(); ui.input = null; $('drawStage').focus(); } }

  function undo() { if (ui && ui.shapes.length) { commitText(); ui.redo.push(ui.shapes.pop()); render(); } }
  function redo() { if (ui && ui.redo.length) { ui.shapes.push(ui.redo.pop()); render(); } }

  function key(e) {
    if (!ui || ui.input) return;
    const k = e.key.toLowerCase();
    if ((e.ctrlKey || e.metaKey) && k === 'z' && !e.shiftKey) { e.preventDefault(); e.stopPropagation(); undo(); return; }
    if ((e.ctrlKey || e.metaKey) && (k === 'y' || (k === 'z' && e.shiftKey))) { e.preventDefault(); e.stopPropagation(); redo(); return; }
    if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); finish(false); return; }
    if (e.key === 'Enter' && e.target.tagName !== 'BUTTON') { e.preventDefault(); e.stopPropagation(); finish(true); return; }
    const n = parseInt(e.key, 10);
    if (n >= 1 && n <= TOOLS.length && !e.ctrlKey && !e.altKey) { e.stopPropagation(); setTool(TOOLS[n - 1]); }
  }

  function finish(apply) {
    if (!ui) return;
    commitText();
    const done = ui.done;
    let result = null;
    if (apply && ui.shapes.length) {
      // Redraw without any shape in progress – this is the final image.
      ui.cur = null;
      render();
      result = ui.canvas;
    }
    ui.src.close?.();
    $('draw').hidden = true;
    ui.canvas.remove();
    ui = null;
    done(result);
  }

  async function open(blob) {
    if (ui) return null;
    if (!build.done) { build(); build.done = true; }
    const src = await createImageBitmap(blob);
    const canvas = document.createElement('canvas');
    canvas.width = src.width; canvas.height = src.height;
    canvas.className = 'draw-canvas';
    $('drawStage').appendChild(canvas);
    return new Promise(done => {
      ui = { src, canvas, ctx: canvas.getContext('2d'), shapes: [], redo: [], cur: null, s: 1, done };
      $('draw').hidden = false;
      layout();
      render();
      $('drawStage').focus();
    });
  }

  const svg = b => `<svg viewBox="0 0 20 20" width="20" height="20" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">${b}</svg>`;
  const ICONS = {
    arrow: svg('<path d="M4 16 14 6"/><path d="M17 3 16 11 9 4Z" fill="currentColor" stroke="none"/>'),
    rect: svg('<rect x="3" y="5" width="14" height="10"/>'),
    ellipse: svg('<ellipse cx="10" cy="10" rx="7.5" ry="5.5"/>'),
    highlight: svg('<rect x="2" y="8" width="16" height="7" fill="#FFD600" fill-opacity=".6" stroke="none"/><path d="M4 5h12M4 11.5h10"/>'),
    text: svg('<path d="M4 4h12M10 4v13M7.5 17h5"/>'),
    step: svg('<circle cx="10" cy="10" r="8" fill="currentColor" stroke="none"/><text x="10" y="14" text-anchor="middle" font-size="11" font-weight="700" fill="var(--card)" stroke="none" font-family="Segoe UI, sans-serif">1</text>'),
    blur: svg('<circle cx="10" cy="10" r="7" fill="currentColor" fill-opacity=".25" stroke="none"/><circle cx="10" cy="10" r="4.5" fill="currentColor" fill-opacity=".5" stroke="none"/><circle cx="10" cy="10" r="2" fill="currentColor" stroke="none"/>'),
    pixelate: svg('<g stroke="none" fill="currentColor"><rect x="3" y="3" width="4.5" height="4.5"/><rect x="13" y="3" width="4.5" height="4.5"/><rect x="8" y="8" width="4.5" height="4.5"/><rect x="3" y="13" width="4.5" height="4.5"/><rect x="13" y="13" width="4.5" height="4.5"/></g><g stroke="none" fill="currentColor" fill-opacity=".35"><rect x="8" y="3" width="4.5" height="4.5"/><rect x="3" y="8" width="4.5" height="4.5"/><rect x="13" y="8" width="4.5" height="4.5"/><rect x="8" y="13" width="4.5" height="4.5"/></g>'),
    undo: svg('<path d="M5 6h6.5a4.5 4.5 0 0 1 0 9H8"/><path d="M8 3 5 6l3 3"/>'),
    redo: svg('<path d="M15 6H8.5a4.5 4.5 0 0 0 0 9H12"/><path d="M12 3l3 3-3 3"/>')
  };

  return { open, drawShape, get active() { return !!ui; } };
})();
