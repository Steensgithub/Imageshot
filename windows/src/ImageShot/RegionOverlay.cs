using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ImageShot
{
    // ------------------------------------------------------------------
    // Markér område – fuldskærms-overlejring over det frosne billede
    // ------------------------------------------------------------------
    class RegionOverlay : Form
    {
        readonly Bitmap bright, dim;
        readonly Rectangle vs;
        readonly List<WinInfo> wins;
        Font font, bold, labelFont;  // skaleret til den enkelte skærms DPI
        float sc = 1f, lsc = 1f;     // skala for værktøjslinjen / størrelsesmærket
        int labelDpi;
        readonly Color accent = Theme.Brand;

        bool dragging;
        Point start;
        Rectangle sel;       // klientkoordinater
        Rectangle hover;     // klientkoordinater
        WinInfo hoverWin;
        Rectangle lastDeco;
        Rectangle hintRect;   // værktøjslinjen
        bool showHint = true;
        readonly WinInfo fgWin;
        readonly Rectangle[] toolRects = new Rectangle[5];
        int hoverTool = -1;
        static string[] ToolText { get { return new[] { "1   " + L.T("mode.region"), "2   " + L.T("mode.window"), "3   " + L.T("mode.screen"), "4   " + L.T("mode.all"), "Esc   " + L.T("mode.cancel") }; } }
        static string SubHint { get { return L.T("overlay.hint"); } }

        public Rectangle Selection;  // virtuelle skærmkoordinater
        public string SelectedTitle;
        public IntPtr SelectedHandle; // vinduet, billedet kommer fra (til {app} i filnavnet)
        public Bitmap ResultImage;   // sat, når der er scrollet (billedet er syet sammen)

        // Scroll ved at skubbe til kanten
        const int EdgeZone = 6;
        Point startScreen;
        WinInfo dragWin;             // programmet, der markeres i
        Rectangle band;              // programmets synlige del på skærmen
        readonly System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer();
        DateTime edgeSince = DateTime.MinValue;
        bool live;
        Stitcher stitcher;
        FrameWindow frame;
        Rectangle grabRect;
        int startC, endC;
        int skipTicks;
        int[] wheelOrder;
        int wheelIdx;
        bool wheelConfirmed, stuck;
        IntPtr wheelChild;
        DateTime lastWheel = DateTime.MinValue, lastChange;
        int wheelsSinceChange;
        int lastDir;                 // 1 = ned, -1 = op (bruges, når genkendelsen er i tvivl)
        Dxgi.Session dxs;            // DirectX-forbindelse på HDR-skærme (almindelig optagelse bliver sort)
        int dxsReopen;
        Rectangle monBounds;
        int pendingWheel;            // musehjul før udgangsbilledet er taget

        public RegionOverlay(Bitmap frozen, Rectangle vs, List<WinInfo> wins, WinInfo fgWin)
        {
            this.vs = vs;
            this.fgWin = fgWin;
            tick.Tick += delegate { Tick(); };
            this.wins = wins ?? new List<WinInfo>();

            bright = new Bitmap(frozen.Width, frozen.Height, PixelFormat.Format32bppPArgb);
            dim = new Bitmap(frozen.Width, frozen.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bright)) g.DrawImageUnscaled(frozen, 0, 0);
            using (Graphics g = Graphics.FromImage(dim))
            using (SolidBrush shade = new SolidBrush(Color.FromArgb(115, 0, 0, 0)))
            {
                g.DrawImageUnscaled(frozen, 0, 0);
                g.FillRectangle(shade, 0, 0, dim.Width, dim.Height);
            }

            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            Cursor = Cursors.Cross;
            Text = L.T("overlay.title");
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            Bounds = vs;
            SetToolbarDpi(Native.DpiAt(Cursor.Position));
            SetLabelDpi(Native.DpiAt(Cursor.Position));
        }

        // 10 punkt ved skærmens egen DPI – ellers bliver teksten forkert stor på en skærm med anden skalering.
        static Font Px(string face, int dpi) { return new Font(face, 10f * dpi / 72f, GraphicsUnit.Pixel); }
        int S(int v) { return (int)Math.Round(v * sc); }
        int LS(int v) { return (int)Math.Round(v * lsc); }

        void SetToolbarDpi(int dpi)
        {
            if (font != null) font.Dispose();
            if (bold != null) bold.Dispose();
            font = Px("Segoe UI", dpi);
            bold = Px("Segoe UI Semibold", dpi);
            sc = dpi / 96f;
        }

        void SetLabelDpi(int dpi)
        {
            if (dpi == labelDpi) return;
            if (labelFont != null) labelFont.Dispose();
            labelFont = Px("Segoe UI", dpi);
            lsc = dpi / 96f;
            labelDpi = dpi;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x02E0) { m.Result = IntPtr.Zero; return; } // WM_DPICHANGED: behold størrelsen
            base.WndProc(ref m);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try { Native.SetWindowPos(Handle, new IntPtr(-1), vs.X, vs.Y, vs.Width, vs.Height, 0x0040); } catch { }
            Activate();
            try { Native.SetForegroundWindow(Handle); } catch { }

            Point p = PointToClient(Cursor.Position);
            Rectangle mon = Screen.FromPoint(Cursor.Position).Bounds;
            mon.Offset(-vs.Left, -vs.Top);
            LayoutToolbar(mon);
            UpdateHover(p);
            Invalidate();
        }

        // Værktøjslinje øverst på den skærm, musen er på: vælg optagelsestype.
        void LayoutToolbar(Rectangle mon)
        {
            int pad = S(10), gap = S(6);
            Size[] sizes = new Size[ToolText.Length];
            int rowW = 0, rowH = 0;
            for (int i = 0; i < ToolText.Length; i++)
            {
                Size t = TextRenderer.MeasureText(ToolText[i], bold);
                sizes[i] = new Size(t.Width + S(26), t.Height + S(14));
                rowW += sizes[i].Width + (i > 0 ? gap : 0);
                rowH = Math.Max(rowH, sizes[i].Height);
            }
            Size sub = TextRenderer.MeasureText(SubHint, font);
            int w = Math.Max(rowW, sub.Width) + pad * 2;
            int h = pad + rowH + S(8) + sub.Height + pad;
            hintRect = new Rectangle(mon.Left + (mon.Width - w) / 2, mon.Top + S(24), w, h);
            int x = hintRect.Left + (w - rowW) / 2;
            for (int i = 0; i < ToolText.Length; i++)
            {
                toolRects[i] = new Rectangle(x, hintRect.Top + pad, sizes[i].Width, rowH);
                x += sizes[i].Width + gap;
            }
        }

        int ToolAt(Point p)
        {
            if (!showHint) return -1;
            for (int i = 0; i < toolRects.Length; i++) if (toolRects[i].Contains(p)) return i;
            return -1;
        }

        void Choose(int tool)
        {
            Rectangle r;
            string title = null;
            switch (tool)
            {
                case 0: return; // Markér område: træk bare en ramme
                case 1:
                    if (fgWin != null) { r = fgWin.Bounds; title = fgWin.Title; SelectedHandle = fgWin.Handle; }
                    else r = Screen.FromPoint(Cursor.Position).Bounds;
                    break;
                case 2: r = Screen.FromPoint(Cursor.Position).Bounds; break;
                case 3: r = vs; break;
                default: DialogResult = DialogResult.Cancel; return;
            }
            r.Intersect(vs);
            if (r.Width < 1 || r.Height < 1) return;
            Selection = r;
            SelectedTitle = title;
            DialogResult = DialogResult.OK;
        }

        void UpdateHover(Point client)
        {
            Point screen = new Point(client.X + vs.Left, client.Y + vs.Top);
            WinInfo found = null;
            foreach (WinInfo w in wins)
                if (w.Bounds.Contains(screen)) { found = w; break; }
            Rectangle r;
            if (found != null) r = found.Bounds;
            else r = Screen.FromPoint(screen).Bounds;
            r.Intersect(vs);
            r.Offset(-vs.Left, -vs.Top);
            hoverWin = found;
            hover = r;
            if (!dragging) SetLabelDpi(Native.DpiAt(screen));
        }

        Rectangle Highlight { get { return dragging ? sel : hover; } }

        Rectangle LabelRect(Rectangle hi)
        {
            string s = SizeText(hi);
            Size t = TextRenderer.MeasureText(s, labelFont);
            int w = t.Width + LS(12), h = t.Height + LS(6);
            int x = hi.Left, y = hi.Bottom + LS(6);
            if (y + h > ClientSize.Height) y = hi.Top + LS(6);
            if (x + w > ClientSize.Width) x = ClientSize.Width - w;
            return new Rectangle(Math.Max(0, x), Math.Max(0, y), w, h);
        }

        static string SizeText(Rectangle r) { return r.Width + " × " + r.Height; }

        Rectangle Deco()
        {
            Rectangle hi = Highlight;
            if (hi.IsEmpty) return Rectangle.Empty;
            Rectangle d = Rectangle.Inflate(hi, 4, 4);
            return Rectangle.Union(d, Rectangle.Inflate(LabelRect(hi), 2, 2));
        }

        void Refresh2()
        {
            Rectangle d = Deco();
            Invalidate(Rectangle.Union(lastDeco, d));
            lastDeco = d;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle clip = e.ClipRectangle;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(dim, clip, clip, GraphicsUnit.Pixel);

            Rectangle hi = Highlight;
            if (!hi.IsEmpty)
            {
                Rectangle c = Rectangle.Intersect(hi, clip);
                if (!c.IsEmpty) g.DrawImage(bright, c, c, GraphicsUnit.Pixel);
                g.CompositingMode = CompositingMode.SourceOver;
                using (Pen p = new Pen(accent, 2f))
                    g.DrawRectangle(p, hi.X - 1, hi.Y - 1, hi.Width + 1, hi.Height + 1);

                Rectangle lr = LabelRect(hi);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 32, 33, 36)))
                    g.FillRectangle(b, lr);
                TextRenderer.DrawText(g, SizeText(hi), labelFont, lr, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            if (showHint && !hintRect.IsEmpty)
            {
                g.CompositingMode = CompositingMode.SourceOver;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = Rounded(hintRect, S(10)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 32, 33, 36)))
                    g.FillPath(b, path);
                for (int i = 0; i < toolRects.Length; i++)
                {
                    Rectangle r = toolRects[i];
                    Color fill = i == hoverTool ? (i == 4 ? Color.FromArgb(196, 43, 28) : accent)
                               : i == 0 ? Theme.BrandDark : Color.FromArgb(62, 63, 67);
                    using (GraphicsPath bp = Rounded(r, S(6)))
                    using (SolidBrush b = new SolidBrush(fill))
                        g.FillPath(b, bp);
                    TextRenderer.DrawText(g, ToolText[i], bold, r, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                g.SmoothingMode = SmoothingMode.None;
                Rectangle sr = new Rectangle(hintRect.Left, toolRects[0].Bottom + 4, hintRect.Width, hintRect.Bottom - toolRects[0].Bottom - 6);
                TextRenderer.DrawText(g, SubHint, font, sr, Color.FromArgb(200, 200, 200),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        static GraphicsPath Rounded(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { DialogResult = DialogResult.Cancel; return; }
            if (e.Button != MouseButtons.Left) return;
            if (showHint && hintRect.Contains(e.Location))
            {
                int t = ToolAt(e.Location);
                if (t >= 0) Choose(t);
                return;
            }
            dragging = true;
            start = e.Location;
            sel = new Rectangle(start, Size.Empty);
            startScreen = new Point(e.X + vs.Left, e.Y + vs.Top);
            dragWin = null;
            foreach (WinInfo w in wins)
                if (w.Bounds.Contains(startScreen)) { if (!w.Class.StartsWith("Shell_")) dragWin = w; break; }
            band = dragWin != null ? Rectangle.Intersect(dragWin.Bounds, Screen.FromPoint(startScreen).Bounds) : Rectangle.Empty;
            if (band.Height < 120 || band.Width < 60) { dragWin = null; band = Rectangle.Empty; }
            tick.Interval = 30;
            tick.Start();
            if (showHint) { showHint = false; Invalidate(Rectangle.Inflate(hintRect, 2, 2)); }
            Refresh2();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (live) return;
            if (dragging)
            {
                int x1 = Math.Min(start.X, e.X), y1 = Math.Min(start.Y, e.Y);
                int x2 = Math.Max(start.X, e.X), y2 = Math.Max(start.Y, e.Y);
                sel = new Rectangle(x1, y1, x2 - x1 + 1, y2 - y1 + 1);
            }
            else
            {
                int t = ToolAt(e.Location);
                if (t != hoverTool) { hoverTool = t; Invalidate(Rectangle.Inflate(hintRect, 2, 2)); }
                if (showHint && hintRect.Contains(e.Location))
                {
                    Cursor = t >= 0 ? Cursors.Hand : Cursors.Default;
                    return;
                }
                Cursor = Cursors.Cross;
                UpdateHover(e.Location);
            }
            Refresh2();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !dragging) return;
            if (live) { FinishLive(); return; }
            dragging = false;
            tick.Stop();
            Rectangle chosen;
            if (sel.Width < 5 && sel.Height < 5)
            {
                // Et klik: tag vinduet (eller skærmen) under musen.
                UpdateHover(e.Location);
                chosen = hover;
                SelectedTitle = hoverWin != null ? hoverWin.Title : null;
                SelectedHandle = hoverWin != null ? hoverWin.Handle : IntPtr.Zero;
            }
            else
            {
                chosen = sel;
                if (dragWin != null) { SelectedTitle = dragWin.Title; SelectedHandle = dragWin.Handle; }
            }
            chosen.Offset(vs.Left, vs.Top);
            Selection = chosen;
            DialogResult = DialogResult.OK;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; return; }
            if (dragging) return;
            if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2) Choose(1);
            else if (e.KeyCode == Keys.D3 || e.KeyCode == Keys.NumPad3) Choose(2);
            else if (e.KeyCode == Keys.D4 || e.KeyCode == Keys.NumPad4) Choose(3);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!dragging || dragWin == null) return;
            if (!live) StartLive();
            if (!live) return;
            if (stitcher == null) pendingWheel += e.Delta;
            else Wheel(e.Delta);
        }

        // ---------- Scroll ved kanten ----------

        bool InEdge(Point p)
        {
            if (dragWin == null) return false;
            bool down = p.Y >= band.Bottom - EdgeZone && p.Y - startScreen.Y > 20;
            bool up = p.Y <= band.Top + EdgeZone && startScreen.Y - p.Y > 20;
            return (down || up) && sel.Width >= 8;
        }

        void Tick()
        {
            if (!dragging) { tick.Stop(); return; }
            Point cp = Cursor.Position;
            if (!live)
            {
                // Musen skal blive ved kanten et øjeblik, så en almindelig markering ikke starter scroll.
                if (InEdge(cp))
                {
                    if (edgeSince == DateTime.MinValue) edgeSince = DateTime.Now;
                    else if ((DateTime.Now - edgeSince).TotalMilliseconds >= 250) StartLive();
                }
                else edgeSince = DateTime.MinValue;
                return;
            }
            // Sikkerhed: hvis musen er sluppet uden at vi fik besked, afsluttes her.
            bool? down = Native.PrimaryButtonDown();
            if (down.HasValue && !down.Value) { FinishLive(); return; }
            if (skipTicks > 0) { skipTicks--; return; }

            if (stitcher == null)
            {
                // Udgangsbillede taget fra den levende skærm (programmet kan have ændret udseende,
                // fx grå titellinje, efter at det mistede fokus).
                using (Bitmap f = Grab()) if (f != null) stitcher = new Stitcher(f);
                if (stitcher == null) return;
                lastChange = DateTime.Now;
                if (pendingWheel != 0) { Wheel(pendingWheel); pendingWheel = 0; }
                UpdateFrame();
                return;
            }

            DateTime now = DateTime.Now;
            int dir = cp.Y >= band.Bottom - EdgeZone ? 1 : cp.Y <= band.Top + EdgeZone ? -1 : 0;
            if (dir != 0 && !stitcher.Lost)
            {
                int beyond = dir > 0 ? cp.Y - band.Bottom : band.Top - cp.Y;
                int interval = beyond > 30 ? 70 : 150; // længere ud = hurtigere
                if ((now - lastWheel).TotalMilliseconds >= interval) Wheel(dir > 0 ? -120 : 120);
            }

            using (Bitmap cur = Grab())
            {
                if (cur != null && stitcher.Add(cur, lastDir))
                {
                    lastChange = now;
                    wheelsSinceChange = 0;
                    wheelConfirmed = true;
                    stuck = false;
                }
            }
            if (!wheelConfirmed && wheelsSinceChange >= 4 && (now - lastChange).TotalMilliseconds > 700)
            {
                // Denne måde at scrolle på virker ikke i programmet – prøv den næste.
                if (wheelIdx < wheelOrder.Length - 1) { wheelIdx++; wheelsSinceChange = 0; lastChange = now; }
                else stuck = true;
            }
            if (stitcher.Lost) stuck = true;

            // Markeringens slutpunkt følger musen, men kun inden for den del, der scroller.
            int lo = band.Top + (stitcher.HasMoved ? stitcher.MovTop : 0);
            // (stitcher er sat her – Tick vender tilbage ovenfor, indtil den findes)
            int hi = band.Top + (stitcher.HasMoved ? stitcher.MovBot : band.Height) - 1;
            int cy = Math.Max(lo, Math.Min(hi, cp.Y));
            endC = cy - band.Top + stitcher.S;
            UpdateFrame();
        }

        void StartLive()
        {
            if (live || dragWin == null) return;
            int x0 = Math.Max(sel.Left + vs.Left, band.Left), x1 = Math.Min(sel.Right + vs.Left, band.Right);
            if (x1 - x0 < 8) return;
            grabRect = new Rectangle(x0, band.Top, x1 - x0, band.Height);

            stitcher = null; // udgangsbilledet tages i Tick, når programmet er synligt igen
            monBounds = Screen.FromPoint(startScreen).Bounds;
            if (Dxgi.IsHdr(monBounds) || ImageShot.Capture.BlackScreens > 0)
            {
                dxs = Dxgi.OpenSession(monBounds);
                Log.Add("Scroll på HDR-skærm: DirectX-forbindelse " + (dxs != null ? "åbnet" : "fejlede") + "  " + Dxgi.LastTrace);
                Log.Save();
            }
            pendingWheel = 0;
            startC = Math.Max(0, Math.Min(band.Height - 1, startScreen.Y - band.Top));
            endC = startC;
            live = true;

            // Gør overlejringen usynlig, men behold musen, så programmet nedenunder ikke får klik.
            try
            {
                int ex = Native.GetWindowLong(Handle, -20);
                Native.SetWindowLong(Handle, -20, ex | 0x80000 | 0x20); // WS_EX_LAYERED | WS_EX_TRANSPARENT
                Native.SetLayeredWindowAttributes(Handle, 0, 0, 2); // helt gennemsigtig
            }
            catch { }
            Capture = true;

            frame = new FrameWindow(band, grabRect.Left, grabRect.Right);
            frame.Show();
            Activate(); // Esc skal stadig gå til os

            bool chromium = dragWin.Class.StartsWith("Chrome_WidgetWin") || dragWin.Class == "MozillaWindowClass";
            wheelOrder = chromium ? new int[] { 1, 0, 2 } : new int[] { 0, 1, 2 };
            wheelIdx = 0;
            wheelChild = Native.DeepestChild(dragWin.Handle, new Point(grabRect.X + grabRect.Width / 2, band.Top + band.Height / 2));
            skipTicks = 4; // lad skærmen nå at vise programmet igen (ca. 120 ms)
            lastChange = DateTime.Now;
            UpdateFrame();
        }

        // Send et musehjuls-skridt til programmet (delta > 0 = op).
        void Wheel(int delta)
        {
            lastWheel = DateTime.Now;
            wheelsSinceChange++;
            lastDir = delta < 0 ? 1 : -1;
            Point c = new Point(grabRect.X + grabRect.Width / 2, band.Top + band.Height / 2);
            IntPtr wp = new IntPtr((delta & 0xFFFF) << 16);
            IntPtr lp = new IntPtr(((c.Y & 0xFFFF) << 16) | (c.X & 0xFFFF));
            try
            {
                switch (wheelOrder[wheelIdx])
                {
                    case 0: Native.PostMessage(wheelChild, 0x020A, wp, lp); break;              // WM_MOUSEWHEEL til feltet
                    case 1: Native.PostMessage(dragWin.Handle, 0x020A, wp, lp); break;         // … til programvinduet
                    default:
                        int steps = Math.Max(1, Math.Abs(delta) / 40);
                        for (int i = 0; i < steps; i++)                                          // WM_VSCROLL linje op/ned
                            Native.PostMessage(wheelChild, 0x0115, new IntPtr(delta < 0 ? 1 : 0), IntPtr.Zero);
                        break;
                }
            }
            catch { }
        }

        Bitmap Grab()
        {
            if (dxs != null)
            {
                if (dxs.Broken && dxsReopen < 3)
                {
                    // Forbindelsen mistes fx ved skærmskift – åbn en ny.
                    dxsReopen++;
                    dxs.Dispose();
                    dxs = Dxgi.OpenSession(monBounds);
                    Log.Add("DirectX-forbindelse genåbnet: " + (dxs != null ? "ja" : "nej") + "  " + Dxgi.LastTrace);
                    Log.Save();
                    if (dxs == null) return null;
                }
                if (!dxs.Broken)
                {
                    Rectangle c = grabRect;
                    c.Offset(-monBounds.Left, -monBounds.Top);
                    Bitmap b = dxs.Next(stitcher == null ? 300 : 15, c);
                    if (b != null) return b;
                }
                if (stitcher == null) { Log.Add("Intet DirectX-billede ved start af scroll"); Log.Save(); }
                return null; // GDI ville give sort på denne skærm – spring billedet over
            }
            try
            {
                Bitmap b = new Bitmap(grabRect.Width, grabRect.Height, PixelFormat.Format32bppRgb);
                using (Graphics g = Graphics.FromImage(b))
                    g.CopyFromScreen(grabRect.Left, grabRect.Top, 0, 0, grabRect.Size, CopyPixelOperation.SourceCopy);
                return b;
            }
            catch { return null; }
        }

        void UpdateFrame()
        {
            if (frame == null) return;
            int S = stitcher != null ? stitcher.S : 0;
            int c0 = Math.Min(startC, endC), c1 = Math.Max(startC, endC) + 1;
            Rectangle r = Rectangle.FromLTRB(grabRect.Left, band.Top + c0 - S, grabRect.Right, band.Top + c1 - S);
            string text = grabRect.Width + " × " + (c1 - c0) + "   ·   " +
                (stitcher != null && stitcher.Lost ? L.T("overlay.lost")
                 : stuck ? L.T("overlay.stuck") : L.T("overlay.scrollHint")) +
                "   ·   " + L.T("overlay.release") + "   ·   " + L.T("overlay.escCancels");
            frame.SetSelection(r, text);
        }

        void FinishLive()
        {
            dragging = false;
            tick.Stop();
            // Tag et sidste billede, så det nyeste indhold kommer med.
            using (Bitmap cur = Grab())
            {
                if (cur != null)
                {
                    if (stitcher == null) stitcher = new Stitcher(cur);
                    else stitcher.Add(cur, lastDir);
                }
            }
            int c0 = Math.Min(startC, endC), c1 = Math.Max(startC, endC) + 1;
            Log.Add("Scroll slut: udgangsbillede " + (stitcher != null ? "ja" : "nej") +
                    (stitcher != null ? "  scrollet " + stitcher.S + " px  flyttet: " + stitcher.HasMoved + (stitcher.Lost ? "  (mistet)" : "") : "") +
                    "  markering " + c0 + "–" + c1 + (dxs != null ? "  DirectX-format " + dxs.Format : "  almindelig optagelse"));
            if (stitcher == null || !stitcher.HasMoved || c1 - c0 < 4)
            {
                // Der blev ikke scrollet: brug den almindelige markering fra det frosne billede.
                Log.Add("  → bruger den almindelige markering");
                Log.Save();
                Rectangle r = sel;
                r.Offset(vs.Left, vs.Top);
                if (r.Width < 4 || r.Height < 4) { DialogResult = DialogResult.Cancel; return; }
                Selection = r;
                SelectedTitle = dragWin.Title;
                SelectedHandle = dragWin.Handle;
                DialogResult = DialogResult.OK;
                return;
            }
            Log.Save();
            ResultImage = stitcher.Crop(c0, c1);
            if (ResultImage == null) { DialogResult = DialogResult.Cancel; return; }
            Selection = new Rectangle(grabRect.Left, band.Top, ResultImage.Width, ResultImage.Height);
            SelectedTitle = dragWin.Title;
            SelectedHandle = dragWin.Handle;
            DialogResult = DialogResult.OK;
        }

        void StopLive()
        {
            tick.Stop();
            if (dxs != null) { dxs.Dispose(); dxs = null; }
            if (frame != null) { frame.Close(); frame.Dispose(); frame = null; }
            if (stitcher != null) { stitcher.Dispose(); stitcher = null; }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopLive();
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { StopLive(); tick.Dispose(); bright.Dispose(); dim.Dispose(); font.Dispose(); bold.Dispose(); labelFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
