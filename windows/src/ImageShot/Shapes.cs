using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace ImageShot
{
    // Tegninger oven på billedet. Alle koordinater er i billedets pixels, så resultatet er det samme
    // uanset zoom. Figurerne bliver først "brændt ind" i billedet, når der kopieres eller gemmes.
    enum Tool { Select, Crop, Arrow, Rect, Ellipse, Highlight, Text, Step, Blur, Pixelate }

    abstract class Shape
    {
        public Color Color;
        public float Width;          // stregbredde i pixels

        public abstract void Draw(Graphics g, Bitmap source);
        public abstract RectangleF Bounds { get; }
        public abstract void Offset(float dx, float dy);
        public virtual bool Hit(PointF p, float tol) { RectangleF b = Bounds; b.Inflate(tol, tol); return b.Contains(p); }
        // Fra træk-start til træk-slut (bruges, mens figuren tegnes).
        public virtual void SetEnds(PointF a, PointF b) { }
        public virtual bool TooSmall { get { RectangleF b = Bounds; return b.Width < 3 && b.Height < 3; } }
        public Shape Clone() { return (Shape)MemberwiseClone(); }

        protected static RectangleF Norm(PointF a, PointF b)
        {
            return RectangleF.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }

        protected static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }
    }

    abstract class BoxShape : Shape
    {
        public RectangleF Rect;
        public override RectangleF Bounds { get { return Rect; } }
        public override void Offset(float dx, float dy) { Rect.Offset(dx, dy); }
        public override void SetEnds(PointF a, PointF b) { Rect = Norm(a, b); }
    }

    sealed class ArrowShape : Shape
    {
        public PointF A, B;
        public override void SetEnds(PointF a, PointF b) { A = a; B = b; }
        public override RectangleF Bounds { get { RectangleF r = Norm(A, B); r.Inflate(Width * 2, Width * 2); return r; } }
        public override bool TooSmall { get { return Dist(A, B) < 6; } }
        public override void Offset(float dx, float dy) { A = new PointF(A.X + dx, A.Y + dy); B = new PointF(B.X + dx, B.Y + dy); }
        static float Dist(PointF a, PointF b) { float dx = a.X - b.X, dy = a.Y - b.Y; return (float)Math.Sqrt(dx * dx + dy * dy); }

        public override bool Hit(PointF p, float tol)
        {
            float vx = B.X - A.X, vy = B.Y - A.Y, len2 = vx * vx + vy * vy;
            float t = len2 == 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X - A.X) * vx + (p.Y - A.Y) * vy) / len2));
            return Dist(p, new PointF(A.X + t * vx, A.Y + t * vy)) <= tol + Width;
        }

        public override void Draw(Graphics g, Bitmap source)
        {
            Smooth(g);
            float len = Dist(A, B);
            if (len < 1) return;
            float head = Math.Min(len * 0.6f, Width * 4.5f + 8);
            float ux = (B.X - A.X) / len, uy = (B.Y - A.Y) / len;
            PointF baseC = new PointF(B.X - ux * head, B.Y - uy * head);
            float hw = head * 0.55f;
            PointF l = new PointF(baseC.X - uy * hw, baseC.Y + ux * hw), r = new PointF(baseC.X + uy * hw, baseC.Y - ux * hw);
            using (Pen p = new Pen(Color, Width))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Flat;
                // Stregen stopper inde i spidsen, så den ikke stikker ud.
                g.DrawLine(p, A, new PointF(B.X - ux * head * 0.6f, B.Y - uy * head * 0.6f));
            }
            using (SolidBrush b = new SolidBrush(Color))
                g.FillPolygon(b, new[] { B, l, new PointF(B.X - ux * head * 0.8f, B.Y - uy * head * 0.8f), r });
        }
    }

    sealed class RectShape : BoxShape
    {
        public override bool Hit(PointF p, float tol)
        {
            RectangleF o = Rect; o.Inflate(tol + Width, tol + Width);
            RectangleF i = Rect; i.Inflate(-(tol + Width), -(tol + Width));
            return o.Contains(p) && !(i.Width > 0 && i.Height > 0 && i.Contains(p));
        }

        public override void Draw(Graphics g, Bitmap source)
        {
            Smooth(g);
            using (Pen p = new Pen(Color, Width)) { p.LineJoin = LineJoin.Round; g.DrawRectangle(p, Rect.X, Rect.Y, Rect.Width, Rect.Height); }
        }
    }

    sealed class EllipseShape : BoxShape
    {
        public override bool Hit(PointF p, float tol)
        {
            float rx = Rect.Width / 2, ry = Rect.Height / 2;
            if (rx < 1 || ry < 1) return base.Hit(p, tol);
            float cx = Rect.X + rx, cy = Rect.Y + ry;
            double d = Math.Sqrt(Math.Pow((p.X - cx) / rx, 2) + Math.Pow((p.Y - cy) / ry, 2));
            return Math.Abs(d - 1) * Math.Min(rx, ry) <= tol + Width;
        }

        public override void Draw(Graphics g, Bitmap source)
        {
            Smooth(g);
            using (Pen p = new Pen(Color, Width)) g.DrawEllipse(p, Rect);
        }
    }

    // Overstregning: halvgennemsigtig farve, så teksten under stadig kan læses.
    sealed class HighlightShape : BoxShape
    {
        public override void Draw(Graphics g, Bitmap source)
        {
            g.SmoothingMode = SmoothingMode.None;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(96, Color))) g.FillRectangle(b, Rect);
        }
    }

    sealed class TextShape : Shape
    {
        public PointF At;
        public string Text = "";
        public float Size = 20f;      // skriftstørrelse i pixels
        RectangleF measured;

        public override RectangleF Bounds { get { return measured.IsEmpty ? new RectangleF(At, new SizeF(Size, Size)) : measured; } }
        public override void Offset(float dx, float dy) { At = new PointF(At.X + dx, At.Y + dy); measured.Offset(dx, dy); }
        public override bool TooSmall { get { return string.IsNullOrEmpty(Text); } }

        public static Font MakeFont(float size) { return new Font("Segoe UI Semibold", size, GraphicsUnit.Pixel); }

        public void Measure()
        {
            using (Bitmap b = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(b))
            using (Font f = MakeFont(Size))
            {
                SizeF s = g.MeasureString(Text.Length == 0 ? " " : Text, f, PointF.Empty, StringFormat.GenericTypographic);
                measured = new RectangleF(At.X - Size * 0.15f, At.Y - Size * 0.1f, s.Width + Size * 0.3f, s.Height + Size * 0.2f);
            }
        }

        public override void Draw(Graphics g, Bitmap source)
        {
            if (string.IsNullOrEmpty(Text)) return;
            Smooth(g);
            Measure();
            // Tekst med kontrastkant, så den kan læses på både lys og mørk baggrund.
            Color halo = Color.GetBrightness() > 0.6f ? Color.FromArgb(220, 20, 20, 20) : Color.FromArgb(230, 255, 255, 255);
            using (Font f = MakeFont(Size))
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddString(Text, f.FontFamily, (int)f.Style, Size, At, StringFormat.GenericTypographic);
                using (Pen p = new Pen(halo, Math.Max(2f, Size / 7f))) { p.LineJoin = LineJoin.Round; g.DrawPath(p, path); }
                using (SolidBrush b = new SolidBrush(Color)) g.FillPath(b, path);
            }
        }
    }

    // Nummereret trin (1, 2, 3 …) – til vejledninger.
    sealed class StepShape : Shape
    {
        public PointF Center;
        public int Number;
        public float Radius = 16;
        public override RectangleF Bounds { get { return new RectangleF(Center.X - Radius, Center.Y - Radius, Radius * 2, Radius * 2); } }
        public override void Offset(float dx, float dy) { Center = new PointF(Center.X + dx, Center.Y + dy); }
        public override bool TooSmall { get { return false; } }

        public override void Draw(Graphics g, Bitmap source)
        {
            Smooth(g);
            RectangleF r = Bounds;
            using (SolidBrush b = new SolidBrush(Color)) g.FillEllipse(b, r);
            using (Pen p = new Pen(Color.White, Math.Max(2f, Radius / 8f))) g.DrawEllipse(p, r);
            Color txt = Color.GetBrightness() > 0.7f ? Color.Black : Color.White;
            using (Font f = new Font("Segoe UI", Radius * (Number > 9 ? 0.95f : 1.15f), FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush tb = new SolidBrush(txt))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString(Number.ToString(), f, tb, new RectangleF(r.X, r.Y + Radius * 0.06f, r.Width, r.Height), sf);
            }
        }
    }

    // Slør eller pixelér et område (fx adgangskoder, navne, e-mails).
    sealed class BlurShape : BoxShape
    {
        public bool Pixelate;
        Bitmap cache, cacheSrc;      // deles med kopier (fortryd); frigives af GC
        Rectangle cacheRect;
        bool cachePix;

        public override void Draw(Graphics g, Bitmap source)
        {
            Rectangle r = Rectangle.Round(Rect);
            r.Intersect(new Rectangle(0, 0, source.Width, source.Height));
            if (r.Width < 2 || r.Height < 2) return;
            // Genbrug effekten, så længe området og billedet er det samme (skærmen tegnes ofte om).
            if (cache == null || cacheRect != r || !ReferenceEquals(cacheSrc, source) || cachePix != Pixelate)
            {
                cache = Effect(source, r, Pixelate);
                cacheRect = r; cacheSrc = source; cachePix = Pixelate;
            }
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(cache, r, new Rectangle(0, 0, cache.Width, cache.Height), GraphicsUnit.Pixel);
        }

        // Kan ikke føres tilbage: pixeleret = blokke på 12 px; slør = stærk nedskalering og blød opskalering.
        public static Bitmap Effect(Bitmap src, Rectangle r, bool pixelate)
        {
            int block = pixelate ? Math.Max(8, Math.Min(r.Width, r.Height) / 12) : Math.Max(6, Math.Min(r.Width, r.Height) / 6);
            block = Math.Min(block, 24);
            int sw = Math.Max(1, r.Width / block), sh = Math.Max(1, r.Height / block);
            Bitmap small = new Bitmap(sw, sh, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(small))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(src, new Rectangle(0, 0, sw, sh), r, GraphicsUnit.Pixel);
            }
            Bitmap outB = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(outB))
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY); // ingen mørke kanter
                g.InterpolationMode = pixelate ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(small, new Rectangle(0, 0, r.Width, r.Height), 0, 0, sw, sh, GraphicsUnit.Pixel, ia);
            }
            small.Dispose();
            return outB;
        }
    }

    // Billede + figurer. Fortryd/annullér fortryd gemmer hele tilstande (billederne deles, ikke kopieres).
    sealed class Doc : IDisposable
    {
        public Bitmap Image;
        public List<Shape> Shapes = new List<Shape>();
        readonly List<State> undo = new List<State>(), redo = new List<State>();
        readonly HashSet<Bitmap> owned = new HashSet<Bitmap>();

        sealed class State { public Bitmap Image; public List<Shape> Shapes; }

        public Doc(Bitmap image) { Image = image; owned.Add(image); }

        public bool CanUndo { get { return undo.Count > 0; } }
        public bool CanRedo { get { return redo.Count > 0; } }
        public bool Edited { get { return undo.Count > 0; } }

        State Snapshot()
        {
            List<Shape> copy = new List<Shape>(Shapes.Count);
            foreach (Shape s in Shapes) copy.Add(s.Clone());
            return new State { Image = Image, Shapes = copy };
        }

        // Kaldes før hver ændring.
        public void Checkpoint()
        {
            undo.Add(Snapshot());
            if (undo.Count > 100) undo.RemoveAt(0);
            redo.Clear();
        }

        public void Undo() { if (undo.Count == 0) return; redo.Add(Snapshot()); Restore(undo); }
        public void Redo() { if (redo.Count == 0) return; undo.Add(Snapshot()); Restore(redo); }

        void Restore(List<State> from)
        {
            State s = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            Image = s.Image;
            Shapes = s.Shapes;
        }

        public int NextStep()
        {
            int n = 0;
            foreach (Shape s in Shapes) if (s is StepShape) n = Math.Max(n, ((StepShape)s).Number);
            return n + 1;
        }

        // Tegn figurerne ind i et nyt billede (til kopiér, gem, fastgør, tekstgenkendelse).
        public Bitmap Flatten()
        {
            Bitmap b = new Bitmap(Image.Width, Image.Height, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImageUnscaled(Image, 0, 0);
                g.CompositingMode = CompositingMode.SourceOver;
                foreach (Shape s in Shapes) s.Draw(g, Image);
            }
            return b;
        }

        // Tilskær: figurer brændes ind først, så de flytter med.
        public void Crop(Rectangle r)
        {
            r.Intersect(new Rectangle(0, 0, Image.Width, Image.Height));
            if (r.Width < 1 || r.Height < 1) return;
            Checkpoint();
            Bitmap c = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppRgb);
            using (Bitmap flat = Flatten())
            using (Graphics g = Graphics.FromImage(c))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(flat, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
            }
            owned.Add(c);
            Image = c;
            Shapes = new List<Shape>();
        }

        public void Dispose()
        {
            foreach (Bitmap b in owned) b.Dispose();
            owned.Clear();
        }
    }
}
