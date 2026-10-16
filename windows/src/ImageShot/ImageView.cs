using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ImageShot
{
    // ------------------------------------------------------------------
    // Tegnefladen i editoren: viser billedet + figurer, tilskæring, tegneværktøjer.
    // ------------------------------------------------------------------
    class ImageView : Control
    {
        Doc doc;
        Tool tool = Tool.Select;
        Rectangle sel;          // tilskæringsramme i billedets pixels
        Rectangle dest;         // hvor billedet tegnes
        float scale = 1f;
        string drag;            // tilskæring: null, "new", "move" eller håndtag (nw, n, …)
        Point anchor, moveOffset;
        Rectangle startSel;
        readonly Color accent = Theme.Brand;
        static readonly string[] Handles = { "nw", "n", "ne", "e", "se", "s", "sw", "w" };

        // Tegning
        Shape drawing;          // figuren, der tegnes lige nu
        PointF drawStart;
        Shape selected;         // valgt figur (Vælg-værktøjet)
        PointF lastMove;
        bool moving, moved;
        TextBox editor;         // tekstfelt, mens der skrives
        PointF editAt;

        public Color PenColor = Color.FromArgb(0xE5, 0x48, 0x4D);
        public float PenWidth = 4f;

        public event EventHandler SelectionChanged;   // tilskæringsrammen ændret
        public event EventHandler ApplyRequested;     // dobbeltklik i rammen
        public event EventHandler DocChanged;         // figurer/billede ændret

        public ImageView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Color.FromArgb(45, 45, 48);
        }

        public Doc Doc
        {
            get { return doc; }
            set { doc = value; sel = Rectangle.Empty; selected = null; Invalidate(); }
        }

        Bitmap image { get { return doc == null ? null : doc.Image; } }

        public Tool Tool
        {
            get { return tool; }
            set
            {
                CommitText();
                tool = value;
                sel = Rectangle.Empty;
                drag = null;
                if (value != Tool.Select) selected = null;
                Cursor = value == Tool.Select ? Cursors.Default : value == Tool.Text ? Cursors.IBeam : Cursors.Cross;
                Invalidate();
                Changed();
            }
        }

        public bool Cropping { get { return tool == Tool.Crop; } }
        public Rectangle Selection { get { return sel; } }
        public Shape SelectedShape { get { return selected; } }

        void Changed() { if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty); }
        void DocChange() { Invalidate(); if (DocChanged != null) DocChanged(this, EventArgs.Empty); }

        // Skærmens skalering, hvor vinduet står (100 % = 96).
        float Dpi
        {
            get
            {
                try { uint d = Native.GetDpiForWindow(Handle); if (d > 0) return d; } catch { }
                return 96f;
            }
        }

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

        Rectangle ToScreen(RectangleF r)
        {
            return Rectangle.FromLTRB(dest.X + (int)(r.Left * scale), dest.Y + (int)(r.Top * scale),
                                      dest.X + (int)Math.Ceiling(r.Right * scale), dest.Y + (int)Math.Ceiling(r.Bottom * scale));
        }

        Point ToImage(Point p)
        {
            int x = (int)Math.Round((p.X - dest.X) / scale), y = (int)Math.Round((p.Y - dest.Y) / scale);
            return new Point(Math.Max(0, Math.Min(image.Width, x)), Math.Max(0, Math.Min(image.Height, y)));
        }

        PointF ToImageF(Point p) { return new PointF((p.X - dest.X) / scale, (p.Y - dest.Y) / scale); }

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
            int tol = Math.Max(7, (int)(7 * Dpi / 96f));
            foreach (string h in Handles)
            {
                Point c = HandlePos(r, h);
                if (Math.Abs(p.X - c.X) <= tol && Math.Abs(p.Y - c.Y) <= tol) return h;
            }
            return null;
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

        Shape HitShape(Point screen)
        {
            PointF p = ToImageF(screen);
            float tol = 6f / scale;
            for (int i = doc.Shapes.Count - 1; i >= 0; i--)
                if (doc.Shapes[i].Hit(p, tol)) return doc.Shapes[i];
            return null;
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

            // Figurerne tegnes i billedets koordinater.
            GraphicsState st = g.Save();
            g.SetClip(dest);
            g.TranslateTransform(dest.X, dest.Y);
            g.ScaleTransform(scale, scale);
            foreach (Shape s in doc.Shapes) s.Draw(g, image);
            if (drawing != null) drawing.Draw(g, image);
            g.Restore(st);
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Default;

            if (selected != null && tool == Tool.Select)
            {
                Rectangle r = ToScreen(selected.Bounds);
                r.Inflate(4, 4);
                using (Pen p = new Pen(accent, 1.5f) { DashStyle = DashStyle.Dash }) g.DrawRectangle(p, r);
            }

            if (tool != Tool.Crop) return;
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(sel.IsEmpty ? 70 : 140, 0, 0, 0)))
            {
                if (sel.IsEmpty) { g.FillRectangle(dim, dest); return; }
                Rectangle r = ToScreen(sel);
                using (Region outside = new Region(dest))
                {
                    outside.Exclude(r);
                    g.FillRegion(dim, outside);
                }
                using (Pen white = new Pen(Color.White, 1f))
                    g.DrawRectangle(white, r.X, r.Y, r.Width - 1, r.Height - 1);
                int hs = Math.Max(4, (int)(4 * Dpi / 96f));
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

        Shape NewShape(PointF at)
        {
            Shape s;
            switch (tool)
            {
                case Tool.Arrow: s = new ArrowShape(); break;
                case Tool.Rect: s = new RectShape(); break;
                case Tool.Ellipse: s = new EllipseShape(); break;
                case Tool.Highlight: s = new HighlightShape(); break;
                case Tool.Blur: s = new BlurShape(); break;
                case Tool.Pixelate: s = new BlurShape { Pixelate = true }; break;
                case Tool.Step: s = new StepShape { Center = at, Number = doc.NextStep(), Radius = 10 + PenWidth * 2.5f }; break;
                default: return null;
            }
            s.Color = tool == Tool.Highlight && PenColor.GetBrightness() < 0.25f ? Color.Yellow : PenColor;
            s.Width = PenWidth;
            s.SetEnds(at, at);
            return s;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (image == null || e.Button != MouseButtons.Left) return;
            Fit();
            CommitText();
            PointF pf = ToImageF(e.Location);

            if (tool == Tool.Crop)
            {
                string h = HitHandle(e.Location);
                Point p = ToImage(e.Location);
                if (h != null) { drag = h; startSel = sel; }
                else if (!sel.IsEmpty && ToScreen(sel).Contains(e.Location))
                {
                    drag = "move";
                    moveOffset = new Point(p.X - sel.X, p.Y - sel.Y);
                }
                else { drag = "new"; anchor = p; sel = Rectangle.Empty; }
                Invalidate();
                Changed();
                return;
            }

            if (tool == Tool.Select)
            {
                selected = HitShape(e.Location);
                if (selected != null)
                {
                    moving = true;
                    moved = false;
                    lastMove = pf;
                }
                Invalidate();
                Changed();
                return;
            }

            if (tool == Tool.Text) { BeginText(e.Location); return; }

            drawing = NewShape(pf);
            drawStart = pf;
            if (drawing is StepShape)
            {
                doc.Checkpoint();
                doc.Shapes.Add(drawing);
                drawing = null;
                DocChange();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (image == null) return;
            if (tool == Tool.Crop) { CropMove(e); return; }
            if (tool == Tool.Select)
            {
                if (moving && selected != null)
                {
                    PointF p = ToImageF(e.Location);
                    if (!moved) { doc.Checkpoint(); moved = true; } // fortryd kun, hvis der faktisk flyttes
                    selected.Offset(p.X - lastMove.X, p.Y - lastMove.Y);
                    lastMove = p;
                    Invalidate();
                }
                else Cursor = HitShape(e.Location) != null ? Cursors.SizeAll : Cursors.Default;
                return;
            }
            if (drawing != null)
            {
                PointF p = ToImageF(e.Location);
                if ((ModifierKeys & Keys.Shift) != 0) p = Constrain(drawStart, p, drawing is ArrowShape);
                drawing.SetEnds(drawStart, p);
                Invalidate();
            }
        }

        // Shift: lige pile (45°-trin) og kvadrater/cirkler.
        static PointF Constrain(PointF a, PointF b, bool line)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            if (line)
            {
                double ang = Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4)) * (Math.PI / 4);
                double len = Math.Sqrt(dx * dx + dy * dy);
                return new PointF(a.X + (float)(Math.Cos(ang) * len), a.Y + (float)(Math.Sin(ang) * len));
            }
            float m = Math.Max(Math.Abs(dx), Math.Abs(dy));
            return new PointF(a.X + Math.Sign(dx) * m, a.Y + Math.Sign(dy) * m);
        }

        void CropMove(MouseEventArgs e)
        {
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
                sel = Rectangle.FromLTRB(Math.Min(anchor.X, p.X), Math.Min(anchor.Y, p.Y), Math.Max(anchor.X, p.X), Math.Max(anchor.Y, p.Y));
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
            if (tool == Tool.Crop)
            {
                if (drag == null) return;
                drag = null;
                if (sel.Width < 3 || sel.Height < 3) sel = Rectangle.Empty; // et klik fjerner rammen
                Invalidate();
                Changed();
                return;
            }
            if (moving) { moving = false; if (moved) DocChange(); return; }
            if (drawing != null)
            {
                Shape s = drawing;
                drawing = null;
                if (!s.TooSmall)
                {
                    doc.Checkpoint();
                    doc.Shapes.Add(s);
                }
                DocChange();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (tool == Tool.Crop && !sel.IsEmpty && ToScreen(sel).Contains(e.Location) && ApplyRequested != null)
                ApplyRequested(this, EventArgs.Empty);
            else if (tool == Tool.Select && selected is TextShape)
            {
                // Dobbeltklik på en tekst: ret den.
                TextShape t = (TextShape)selected;
                doc.Checkpoint();
                doc.Shapes.Remove(t);
                selected = null;
                Invalidate();
                BeginText(ToScreen(new RectangleF(t.At, SizeF.Empty)).Location, t.Text, t.Color);
            }
        }

        public bool DeleteSelected()
        {
            if (selected == null) return false;
            doc.Checkpoint();
            doc.Shapes.Remove(selected);
            selected = null;
            DocChange();
            return true;
        }

        // ---- Tekst
        float TextSize { get { return 12f + PenWidth * 3f; } }

        void BeginText(Point screen, string initial = "", Color? color = null)
        {
            editAt = ToImageF(screen);
            editor = new TextBox();
            editor.BorderStyle = BorderStyle.FixedSingle;
            editor.Font = new Font("Segoe UI Semibold", Math.Max(9f, TextSize * scale), GraphicsUnit.Pixel);
            editor.ForeColor = color ?? PenColor;
            editor.BackColor = Color.FromArgb(250, 250, 250);
            editor.Text = initial;
            editor.Location = screen;
            editor.Width = Math.Max(160, Width - screen.X - 12);
            editor.Tag = color ?? PenColor;
            editor.KeyDown += delegate (object s, KeyEventArgs k)
            {
                if (k.KeyCode == Keys.Enter) { k.SuppressKeyPress = true; CommitText(); }
                else if (k.KeyCode == Keys.Escape) { k.SuppressKeyPress = true; CancelText(); }
            };
            editor.LostFocus += delegate { BeginInvoke(new Action(CommitText)); };
            Controls.Add(editor);
            editor.Focus();
            editor.SelectionStart = editor.Text.Length;
        }

        public bool Editing { get { return editor != null; } }

        public void CommitText()
        {
            if (editor == null) return;
            TextBox ed = editor;
            editor = null;
            string text = ed.Text.Trim();
            Color c = (Color)ed.Tag;
            Controls.Remove(ed);
            ed.Dispose();
            if (text.Length > 0)
            {
                doc.Checkpoint();
                TextShape t = new TextShape { At = editAt, Text = text, Size = TextSize, Color = c, Width = PenWidth };
                t.Measure();
                doc.Shapes.Add(t);
            }
            Focus();
            DocChange();
        }

        void CancelText()
        {
            if (editor == null) return;
            TextBox ed = editor;
            editor = null;
            Controls.Remove(ed);
            ed.Dispose();
            Focus();
            Invalidate();
        }
    }
}
