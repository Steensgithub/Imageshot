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
    // Udklipsholder – vis billedet og vælg kopiér eller gem
    // ------------------------------------------------------------------
    // Billedvisning med tilskæring: træk en ramme, justér kanter/hjørner, flyt den.
    class ImageView : Control
    {
        Bitmap image;
        bool cropping;
        Rectangle sel;          // i billedets pixels
        Rectangle dest;         // hvor billedet tegnes
        float scale = 1f;
        string drag;            // null, "new", "move" eller et håndtag (nw, n, ne, e, se, s, sw, w)
        Point anchor, moveOffset;
        Rectangle startSel;
        readonly Color accent = Theme.Brand;
        static readonly string[] Handles = { "nw", "n", "ne", "e", "se", "s", "sw", "w" };

        public event EventHandler SelectionChanged;
        public event EventHandler ApplyRequested;

        public ImageView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(45, 45, 48);
        }

        public Bitmap Image
        {
            get { return image; }
            set { image = value; sel = Rectangle.Empty; Invalidate(); }
        }

        public bool Cropping
        {
            get { return cropping; }
            set
            {
                cropping = value;
                sel = Rectangle.Empty;
                drag = null;
                Cursor = value ? Cursors.Cross : Cursors.Default;
                Invalidate();
                Changed();
            }
        }

        public Rectangle Selection { get { return sel; } }

        void Changed() { if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty); }

        void Fit()
        {
            if (image == null) return;
            float s = Math.Min(1f, Math.Min((Width - 16f) / image.Width, (Height - 16f) / image.Height));
            if (s <= 0.001f) s = 0.001f;
            scale = s;
            int w = Math.Max(1, (int)Math.Round(image.Width * s)), h = Math.Max(1, (int)Math.Round(image.Height * s));
            dest = new Rectangle((Width - w) / 2, (Height - h) / 2, w, h);
        }

        Rectangle ToScreen(Rectangle r)
        {
            int x1 = dest.X + (int)Math.Round(r.Left * scale), y1 = dest.Y + (int)Math.Round(r.Top * scale);
            int x2 = dest.X + (int)Math.Round(r.Right * scale), y2 = dest.Y + (int)Math.Round(r.Bottom * scale);
            return Rectangle.FromLTRB(x1, y1, x2, y2);
        }

        Point ToImage(Point p)
        {
            int x = (int)Math.Round((p.X - dest.X) / scale), y = (int)Math.Round((p.Y - dest.Y) / scale);
            return new Point(Math.Max(0, Math.Min(image.Width, x)), Math.Max(0, Math.Min(image.Height, y)));
        }

        Point HandlePos(Rectangle r, string h)
        {
            int x = h.Contains("w") ? r.Left : h.Contains("e") ? r.Right : (r.Left + r.Right) / 2;
            int y = h.Contains("n") ? r.Top : h.Contains("s") ? r.Bottom : (r.Top + r.Bottom) / 2;
            return new Point(x, y);
        }

        string HitHandle(Point p)
        {
            if (sel.IsEmpty) return null;
            Rectangle r = ToScreen(sel);
            int tol = Math.Max(7, (int)(7 * ScreenDpi() / 96f));
            foreach (string h in Handles)
            {
                Point c = HandlePos(r, h);
                if (Math.Abs(p.X - c.X) <= tol && Math.Abs(p.Y - c.Y) <= tol) return h;
            }
            return null;
        }

        // Skærmens skalering, hvor vinduet står (100 % = 96).
        float ScreenDpi()
        {
            try
            {
                uint d = Native.GetDpiForWindow(Handle);
                if (d > 0) return d;
            }
            catch { }
            return 96f;
        }

        static Cursor CursorFor(string h)
        {
            switch (h)
            {
                case "nw": case "se": return Cursors.SizeNWSE;
                case "ne": case "sw": return Cursors.SizeNESW;
                case "n": case "s": return Cursors.SizeNS;
                default: return Cursors.SizeWE;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            if (image == null) return;
            Fit();
            g.InterpolationMode = scale < 1f ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(image, dest, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
            if (!cropping) return;

            g.PixelOffsetMode = PixelOffsetMode.Default;
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(sel.IsEmpty ? 70 : 140, 0, 0, 0)))
            {
                if (sel.IsEmpty)
                {
                    g.FillRectangle(dim, dest);
                    return;
                }
                Rectangle r = ToScreen(sel);
                using (Region outside = new Region(dest))
                {
                    outside.Exclude(r);
                    g.FillRegion(dim, outside);
                }
                using (Pen white = new Pen(Color.White, 1f))
                    g.DrawRectangle(white, r.X, r.Y, r.Width - 1, r.Height - 1);
                int hs = Math.Max(4, (int)(4 * ScreenDpi() / 96f));
                using (SolidBrush wb = new SolidBrush(Color.White))
                using (Pen ap = new Pen(accent, 1.5f))
                {
                    foreach (string h in Handles)
                    {
                        Point c = HandlePos(r, h);
                        Rectangle hr = new Rectangle(c.X - hs, c.Y - hs, hs * 2, hs * 2);
                        g.FillRectangle(wb, hr);
                        g.DrawRectangle(ap, hr);
                    }
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (!cropping || image == null || e.Button != MouseButtons.Left) return;
            Fit();
            string h = HitHandle(e.Location);
            Point p = ToImage(e.Location);
            if (h != null) { drag = h; startSel = sel; }
            else if (!sel.IsEmpty && ToScreen(sel).Contains(e.Location))
            {
                drag = "move";
                moveOffset = new Point(p.X - sel.X, p.Y - sel.Y);
            }
            else
            {
                drag = "new";
                anchor = p;
                sel = Rectangle.Empty;
            }
            Invalidate();
            Changed();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!cropping || image == null) return;
            if (drag == null)
            {
                string h = HitHandle(e.Location);
                if (h != null) Cursor = CursorFor(h);
                else if (!sel.IsEmpty && ToScreen(sel).Contains(e.Location)) Cursor = Cursors.SizeAll;
                else Cursor = Cursors.Cross;
                return;
            }
            Point p = ToImage(e.Location);
            if (drag == "new")
            {
                sel = Rectangle.FromLTRB(Math.Min(anchor.X, p.X), Math.Min(anchor.Y, p.Y), Math.Max(anchor.X, p.X), Math.Max(anchor.Y, p.Y));
            }
            else if (drag == "move")
            {
                int x = Math.Max(0, Math.Min(image.Width - sel.Width, p.X - moveOffset.X));
                int y = Math.Max(0, Math.Min(image.Height - sel.Height, p.Y - moveOffset.Y));
                sel = new Rectangle(x, y, sel.Width, sel.Height);
            }
            else
            {
                int x1 = startSel.Left, y1 = startSel.Top, x2 = startSel.Right, y2 = startSel.Bottom;
                if (drag.Contains("w")) x1 = p.X;
                if (drag.Contains("e")) x2 = p.X;
                if (drag.Contains("n")) y1 = p.Y;
                if (drag.Contains("s")) y2 = p.Y;
                sel = Rectangle.FromLTRB(Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(x1, x2), Math.Max(y1, y2));
            }
            Invalidate();
            Changed();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (drag == null) return;
            drag = null;
            if (sel.Width < 3 || sel.Height < 3) sel = Rectangle.Empty; // et klik fjerner rammen
            Invalidate();
            Changed();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (cropping && !sel.IsEmpty && ToScreen(sel).Contains(e.Location) && ApplyRequested != null)
                ApplyRequested(this, EventArgs.Empty);
        }
    }
}
