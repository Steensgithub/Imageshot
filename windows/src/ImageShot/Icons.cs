using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ImageShot
{
    // Små værktøjsikoner tegnet i kode – skarpe i alle skaleringer og uden ekstra filer.
    static class Icons
    {
        public static Bitmap Tool(Tool t, int size, Color fg)
        {
            Bitmap b = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float s = size / 20f;
                g.ScaleTransform(s, s);
                using (Pen p = new Pen(fg, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                using (SolidBrush br = new SolidBrush(fg))
                {
                    switch (t)
                    {
                        case ImageShot.Tool.Select:
                            g.FillPolygon(br, new[] { new PointF(5, 3), new PointF(5, 16), new PointF(8.5f, 12.5f), new PointF(11, 17.5f), new PointF(13, 16.5f), new PointF(10.5f, 11.5f), new PointF(15, 11.5f) });
                            break;
                        case ImageShot.Tool.Crop:
                            g.DrawLines(p, new[] { new PointF(6, 2), new PointF(6, 14), new PointF(18, 14) });
                            g.DrawLines(p, new[] { new PointF(2, 6), new PointF(14, 6), new PointF(14, 18) });
                            break;
                        case ImageShot.Tool.Arrow:
                            g.DrawLine(p, 4, 16, 14, 6);
                            g.FillPolygon(br, new[] { new PointF(17, 3), new PointF(16, 11), new PointF(9, 4) });
                            break;
                        case ImageShot.Tool.Rect:
                            g.DrawRectangle(p, 3, 5, 14, 10);
                            break;
                        case ImageShot.Tool.Ellipse:
                            g.DrawEllipse(p, 2.5f, 4.5f, 15, 11);
                            break;
                        case ImageShot.Tool.Highlight:
                            using (SolidBrush y = new SolidBrush(Color.FromArgb(150, 255, 214, 0))) g.FillRectangle(y, 2, 8, 16, 7);
                            g.DrawLine(p, 4, 5, 16, 5);
                            g.DrawLine(p, 4, 11.5f, 14, 11.5f);
                            break;
                        case ImageShot.Tool.Text:
                            g.DrawLine(p, 4, 4, 16, 4);
                            g.DrawLine(p, 10, 4, 10, 17);
                            g.DrawLine(p, 7.5f, 17, 12.5f, 17);
                            break;
                        case ImageShot.Tool.Step:
                            g.FillEllipse(br, 2, 2, 16, 16);
                            using (Font f = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Pixel))
                            using (SolidBrush w = new SolidBrush(fg.GetBrightness() > 0.5f ? Color.Black : Color.White))
                            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                                g.DrawString("1", f, w, new RectangleF(2, 2.5f, 16, 16), sf);
                            break;
                        case ImageShot.Tool.Blur:
                            for (int i = 0; i < 3; i++)
                                using (SolidBrush c = new SolidBrush(Color.FromArgb(70 + i * 60, fg))) g.FillEllipse(c, 3 + i * 2.5f, 3 + i * 2.5f, 14 - i * 5, 14 - i * 5);
                            break;
                        case ImageShot.Tool.Pixelate:
                            for (int y = 0; y < 3; y++)
                                for (int x = 0; x < 3; x++)
                                    using (SolidBrush c = new SolidBrush(Color.FromArgb((x + y) % 2 == 0 ? 230 : 90, fg))) g.FillRectangle(c, 3 + x * 5, 3 + y * 5, 4.5f, 4.5f);
                            break;
                    }
                }
            }
            return b;
        }

        public static Bitmap Swatch(Color c, int size, bool selected, Color ring)
        {
            Bitmap b = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float m = size * 0.18f;
                if (selected) using (Pen p = new Pen(ring, size / 10f)) g.DrawEllipse(p, size * 0.06f, size * 0.06f, size * 0.88f, size * 0.88f);
                using (SolidBrush br = new SolidBrush(c)) g.FillEllipse(br, m, m, size - 2 * m, size - 2 * m);
                using (Pen p = new Pen(Color.FromArgb(90, 128, 128, 128), 1f)) g.DrawEllipse(p, m, m, size - 2 * m, size - 2 * m);
            }
            return b;
        }

        public static Bitmap Line(float width, int size, Color fg, bool selected, Color ring)
        {
            Bitmap b = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (selected) using (SolidBrush r = new SolidBrush(Color.FromArgb(60, ring))) g.FillRectangle(r, 0, 0, size, size);
                using (Pen p = new Pen(fg, width * size / 20f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(p, size * 0.2f, size * 0.5f, size * 0.8f, size * 0.5f);
            }
            return b;
        }

        public static Bitmap Glyph(string kind, int size, Color fg)
        {
            Bitmap b = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float s = size / 20f;
                g.ScaleTransform(s, s);
                using (Pen p = new Pen(fg, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    if (kind == "undo" || kind == "redo")
                    {
                        if (kind == "redo") { g.TranslateTransform(20, 0); g.ScaleTransform(-1, 1); }
                        g.DrawArc(p, 5, 6, 11, 10, 270, 200);
                        g.DrawLine(p, 10.5f, 6, 5, 6);
                        g.DrawLines(p, new[] { new PointF(8, 3), new PointF(5, 6), new PointF(8, 9) });
                    }
                    else if (kind == "delete")
                    {
                        g.DrawLine(p, 4, 5, 16, 5);
                        g.DrawLines(p, new[] { new PointF(6, 5), new PointF(7, 17), new PointF(13, 17), new PointF(14, 5) });
                        g.DrawLine(p, 8.5f, 3, 11.5f, 3);
                    }
                }
            }
            return b;
        }
    }
}
