using System;
using System.Drawing;
using System.Runtime.InteropServices;
using ImageShot;
using Xunit;

namespace ImageShot.Tests
{
    public class HdrTests
    {
        // Uafhængig reference for halve kommatal (IEEE 754 binary16).
        static double Half(int h)
        {
            int s = (h >> 15) & 1, e = (h >> 10) & 31, m = h & 1023;
            double v = e == 0 ? m / 1024.0 * Math.Pow(2, -14) : e == 31 ? (m == 0 ? double.PositiveInfinity : double.NaN) : (1 + m / 1024.0) * Math.Pow(2, e - 15);
            return s == 1 ? -v : v;
        }

        [Fact]
        public void Lut_MatchesReference_ForAllHalfValues()
        {
            const float scale = 2.5f;
            byte[] lut = Dxgi.BuildHdrLut(scale);
            int bad = 0;
            for (int h = 0; h < 65536; h++)
            {
                double f = Half(h);
                if (double.IsNaN(f) || f < 0) f = 0;
                if (double.IsPositiveInfinity(f)) f = 1e9;
                double v = Math.Max(0, Math.Min(1, f / scale));
                double srgb = v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;
                byte want = (byte)Math.Round(Math.Max(0, Math.Min(1, srgb)) * 255);
                if (lut[h] != want) bad++;
            }
            Assert.Equal(0, bad);
        }

        [Fact]
        public void CopyMapped_ConvertsFp16Row()
        {
            byte[] lut = Dxgi.BuildHdrLut(2.5f);
            short[] px = { 0x4100, 0x4100, 0x4100, 0x3C00, 0, 0, 0, 0x3C00, 0x3C00, 0, 0, 0x3C00, 0x4880, 0x4880, 0x4880, 0x3C00 };
            IntPtr mem = Marshal.AllocHGlobal(px.Length * 2);
            try
            {
                Marshal.Copy(px, 0, mem, px.Length);
                using (Bitmap b = Dxgi.CopyMapped(mem, 32, 10, new Rectangle(0, 0, 4, 1), lut))
                {
                    Assert.Equal(Color.FromArgb(255, 255, 255, 255).ToArgb(), b.GetPixel(0, 0).ToArgb()); // SDR-hvid
                    Assert.Equal(Color.FromArgb(255, 0, 0, 0).ToArgb(), b.GetPixel(1, 0).ToArgb());       // sort
                    Assert.Equal(170, b.GetPixel(2, 0).R);                                                // 1.0 rød
                    Assert.Equal(255, b.GetPixel(3, 0).R);                                                // højlys klippes
                }
            }
            finally { Marshal.FreeHGlobal(mem); }
        }
    }
}
