using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using ImageShot;
using Xunit;

namespace ImageShot.Tests
{
    // Syntetisk "program": fast toplinje med snurrende ikon, fast sidepanel, dokument der scroller,
    // scrollbjælke og ensfarvet bundlinje. Sammensyningen skal genskabe dokumentet pixel for pixel.
    public class StitcherTests
    {
        const int W = 800, VH = 700, HEAD = 60, FOOT = 30, VIEW = VH - HEAD - FOOT, SIDE = 150, BAR = 15, CW = W - SIDE - BAR, DOCH = 5000;

        static Bitmap Doc(int seed, bool periodic)
        {
            var r = new Random(seed);
            var b = new Bitmap(CW, DOCH, PixelFormat.Format32bppRgb);
            using (var g = Graphics.FromImage(b))
            {
                g.Clear(Color.White);
                int y = 10;
                while (y < DOCH - 40)
                {
                    if (periodic && y > 1200 && y < 1900) { g.DrawRectangle(Pens.Gray, 10, y, CW - 20, 24); y += 24; continue; }
                    int kind = r.Next(10);
                    if (kind < 6)
                    {
                        int x = 20;
                        while (x < CW - 60)
                        {
                            int ww = r.Next(10, 60);
                            using (var br = new SolidBrush(Color.FromArgb(r.Next(30), r.Next(30), r.Next(30)))) g.FillRectangle(br, x, y + 3, ww, 11);
                            g.FillRectangle(Brushes.Blue, x + r.Next(ww), y + r.Next(3, 14), 2, 1);
                            x += ww + r.Next(5, 12);
                        }
                        y += 22;
                    }
                    else if (kind < 8) y += r.Next(10, 60);
                    else
                    {
                        int ih = r.Next(60, 200);
                        for (int k = 0; k < ih; k++)
                            using (var p = new Pen(Color.FromArgb(r.Next(255), r.Next(255), r.Next(255)))) g.DrawLine(p, 40, y + k, CW - 40, y + k);
                        y += ih + 10;
                    }
                }
            }
            return b;
        }

        static Bitmap Frame(Bitmap doc, int pos, int frameNo)
        {
            var f = new Bitmap(W, VH, PixelFormat.Format32bppRgb);
            using (var g = Graphics.FromImage(f))
            {
                g.FillRectangle(Brushes.DarkSlateBlue, 0, 0, W, HEAD);
                g.FillRectangle(Brushes.White, 10, 20, 120, 18);
                using (var br = new SolidBrush(Color.FromArgb((frameNo * 37) % 255, 200, 50))) g.FillRectangle(br, 700, 15, 30, 30);
                g.FillRectangle(Brushes.Gainsboro, 0, HEAD, SIDE, VIEW);
                for (int i = 0; i < 20; i++) g.FillRectangle(Brushes.Black, 12, HEAD + 15 + i * 30, 90 - (i * 7) % 50, 10);
                g.DrawImage(doc, new Rectangle(SIDE, HEAD, CW, VIEW), new Rectangle(0, pos, CW, VIEW), GraphicsUnit.Pixel);
                g.FillRectangle(Brushes.WhiteSmoke, W - BAR, HEAD, BAR, VIEW);
                int th = VIEW * VIEW / DOCH, ty = HEAD + (VIEW - th) * pos / (DOCH - VIEW);
                g.FillRectangle(Brushes.DarkGray, W - BAR + 3, ty, BAR - 6, th);
                g.FillRectangle(Brushes.DimGray, 0, VH - FOOT, W, FOOT);
            }
            return f;
        }

        static int WrongRows(Bitmap outImg, Bitmap doc, int height)
        {
            int bad = 0;
            for (int y = 0; y < Math.Min(outImg.Height, height); y++)
                for (int x = SIDE; x < W - BAR; x += 3)
                {
                    int o = outImg.GetPixel(x, y).ToArgb() & 0xFFFFFF, d = doc.GetPixel(x - SIDE, y).ToArgb() & 0xFFFFFF;
                    if (o != d) { bad++; break; }
                }
            return bad;
        }

        static void Run(int seed, IEnumerable<int> positions, out int s, out int expected, out int wrong, out int height, out int maxPos)
        {
            var doc = Doc(seed, false);
            var st = new Stitcher(Frame(doc, 0, 0));
            int pos = 0, fno = 1; maxPos = 0;
            foreach (int p in positions)
            {
                int dir = Math.Sign(p - pos); pos = p; maxPos = Math.Max(maxPos, p);
                using (var f = Frame(doc, pos, fno++)) st.Add(f, dir == 0 ? 1 : dir);
            }
            using (var outImg = st.Crop(HEAD, HEAD + maxPos + VIEW))
            {
                s = st.S; expected = pos;
                height = outImg == null ? 0 : outImg.Height;
                wrong = outImg == null ? -1 : WrongRows(outImg, doc, maxPos + VIEW);
            }
            st.Dispose();
        }

        [Fact]
        public void SmoothScrollDown_IsPixelExact()
        {
            var ps = new List<int>(); int p = 0;
            for (int n = 0; n < 30; n++) { for (int k = 0; k < 4; k++) { p += 25; ps.Add(p); } ps.Add(p); }
            int s, e, w, h, m; Run(1, ps, out s, out e, out w, out h, out m);
            Assert.Equal(e, s);
            Assert.Equal(0, w);
            Assert.True(h >= m + VIEW - 40, "for kort: " + h);
        }

        [Fact]
        public void BigJumps_ArePixelExact()
        {
            var ps = new List<int>(); int p = 0; var r = new Random(5);
            for (int i = 0; i < 40; i++) { p = Math.Min(DOCH - VIEW, p + r.Next(30, 350)); ps.Add(p); }
            int s, e, w, h, m; Run(2, ps, out s, out e, out w, out h, out m);
            Assert.Equal(e, s);
            Assert.Equal(0, w);
        }

        [Fact]
        public void UpAndDown_IsPixelExact()
        {
            var ps = new List<int>(); int p = 0;
            for (int i = 0; i < 25; i++) { p += 80; ps.Add(p); }
            for (int i = 0; i < 10; i++) { p -= 60; ps.Add(p); }
            for (int i = 0; i < 20; i++) { p += 90; ps.Add(p); }
            int s, e, w, h, m; Run(5, ps, out s, out e, out w, out h, out m);
            Assert.Equal(e, s);
            Assert.Equal(0, w);
        }

        [Fact]
        public void NoChange_ReturnsFalse()
        {
            var doc = Doc(9, false);
            using (var f0 = Frame(doc, 0, 0))
            using (var st = new Stitcher(f0))
            using (var f1 = Frame(doc, 0, 0))
                Assert.False(st.Add(f1, 1));
        }
    }
}
