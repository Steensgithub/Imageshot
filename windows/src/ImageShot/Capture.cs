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
    // Optagelse
    // ------------------------------------------------------------------
    class WinInfo
    {
        public IntPtr Handle;
        public Rectangle Bounds;
        public string Title;
        public string Class;
        public bool Tool;
    }

    static class Capture
    {
        // Fotograferer hele det virtuelle skrivebord (alle skærme) med det samme.
        // Skærme, der kommer ud sorte (typisk spil i ægte fuldskærm), hentes igen via DirectX.
        public static Bitmap Freeze(bool alwaysDx)
        {
            Rectangle vs = SystemInformation.VirtualScreen;
            Log.Begin();
            Log.Add("Virtuel skærm: " + vs + "  (DirectX altid: " + alwaysDx + ")");
            Bitmap bmp = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                // .NET's CopyFromScreen afviser SourceCopy | CaptureBlt, så BitBlt kaldes direkte
                // (CaptureBlt tager også menuer, tooltips og andre "lag"-vinduer med).
                string err = Native.ScreenBlt(g, vs);
                if (err != null)
                {
                    Log.Add("BitBlt: " + err + " – prøver CopyFromScreen");
                    try { g.CopyFromScreen(vs.Left, vs.Top, 0, 0, vs.Size, CopyPixelOperation.SourceCopy); }
                    catch (Exception ex) { Log.Add("Almindelig optagelse fejlede: " + ex.Message); }
                }
            }

            BlackScreens = 0;
            int no = 0;
            foreach (Screen s in Screen.AllScreens)
            {
                no++;
                Rectangle r = s.Bounds;
                r.Offset(-vs.Left, -vs.Top);
                bool black = IsBlack(bmp, r);
                bool hdr = Dxgi.IsHdr(s.Bounds);
                Log.Add("Skærm " + no + ": " + s.Bounds + (s.Primary ? " (primær)" : "") + "  HDR: " + (hdr ? "ja" : "nej") +
                        "  almindelig optagelse sort: " + (black ? "ja" : "nej"));
                // HDR-skærme: Windows' almindelige optagelse kan blive sort eller forkert, så brug DirectX.
                if (!alwaysDx && !hdr && !black) continue;
                Bitmap dx = Dxgi.CaptureMonitor(s.Bounds);
                Log.Add("  DirectX: " + (dx == null ? "intet billede" : dx.Width + "×" + dx.Height) + "  " + Dxgi.LastTrace);
                bool used = false;
                if (dx != null)
                {
                    using (dx)
                    {
                        if (!IsBlack(dx, new Rectangle(0, 0, dx.Width, dx.Height)))
                        {
                            using (Graphics g = Graphics.FromImage(bmp))
                            {
                                g.CompositingMode = CompositingMode.SourceCopy;
                                g.DrawImage(dx, new Rectangle(r.Left, r.Top, dx.Width, dx.Height), 0, 0, dx.Width, dx.Height, GraphicsUnit.Pixel);
                            }
                            used = true;
                        }
                        else Log.Add("  DirectX-billedet var også sort");
                    }
                }
                if (!used && black) BlackScreens++;
            }
            Log.Add("Sorte skærme efter alle forsøg: " + BlackScreens);
            Log.Save();
            return bmp;
        }

        public static int BlackScreens; // skærme, der stadig var sorte efter sidste Freeze

        // Næsten alle prøvepunkter sorte?
        public static bool IsBlack(Bitmap bmp, Rectangle r)
        {
            r.Intersect(new Rectangle(0, 0, bmp.Width, bmp.Height));
            if (r.Width <= 0 || r.Height <= 0) return false;
            BitmapData d = bmp.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
            try
            {
                const int N = 48;
                int dark = 0, total = 0;
                for (int yi = 0; yi < N; yi++)
                {
                    int y = (int)((yi + 0.5) * r.Height / N);
                    for (int xi = 0; xi < N; xi++)
                    {
                        int x = (int)((xi + 0.5) * r.Width / N);
                        int px = Marshal.ReadInt32(d.Scan0, y * d.Stride + x * 4);
                        int b = px & 0xFF, gg = (px >> 8) & 0xFF, rr = (px >> 16) & 0xFF;
                        if (b < 12 && gg < 12 && rr < 12) dark++;
                        total++;
                    }
                }
                return dark >= total * 0.995;
            }
            finally { bmp.UnlockBits(d); }
        }

        public static Bitmap Crop(Bitmap src, Rectangle vs, Rectangle target)
        {
            Rectangle r = target;
            r.Offset(-vs.Left, -vs.Top);
            r.Intersect(new Rectangle(0, 0, src.Width, src.Height));
            if (r.Width < 1 || r.Height < 1) return null;
            Bitmap b = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(src, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
            }
            return b;
        }

        public static Bitmap Scale(Bitmap src, double scale)
        {
            if (scale >= 0.999) return src;
            int w = Math.Max(1, (int)Math.Round(src.Width * scale));
            int h = Math.Max(1, (int)Math.Round(src.Height * scale));
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(b))
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY); // ingen mørk kant
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
            }
            return b;
        }

        // Synlige vinduer, øverste først.
        public static List<WinInfo> Windows()
        {
            List<WinInfo> list = new List<WinInfo>();
            uint me = (uint)Process.GetCurrentProcess().Id;
            Native.EnumWindowsProc proc = delegate(IntPtr h, IntPtr l)
            {
                if (!Native.IsWindowVisible(h) || Native.IsIconic(h)) return true;
                uint pid;
                Native.GetWindowThreadProcessId(h, out pid);
                if (pid == me) return true;
                int cloaked;
                if (Native.DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true;

                string cls = Native.ClassOf(h);
                if (cls == "Progman" || cls == "WorkerW") return true;

                int ex = Native.GetWindowLong(h, -20);
                if ((ex & 0x20) != 0) return true; // WS_EX_TRANSPARENT: usynlige overlejringer
                bool tool = (ex & 0x80) != 0;
                string title = Native.TitleOf(h);
                bool taskbar = cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd";
                if (tool && title.Length == 0 && !taskbar) return true;

                Rectangle r = Native.WindowBounds(h);
                if (r.Width <= 1 || r.Height <= 1) return true;

                WinInfo w = new WinInfo();
                w.Handle = h; w.Bounds = r; w.Title = title; w.Class = cls; w.Tool = tool || taskbar;
                list.Add(w);
                return true;
            };
            Native.EnumWindows(proc, IntPtr.Zero);
            GC.KeepAlive(proc);
            return list;
        }

        static bool IsApp(WinInfo w)
        {
            return !w.Tool && w.Title.Length > 0;
        }

        // Det vindue, man arbejder i lige nu.
        public static WinInfo ForegroundAppWindow()
        {
            IntPtr fg = Native.GetAncestor(Native.GetForegroundWindow(), 2);
            List<WinInfo> wins = Windows();
            foreach (WinInfo w in wins)
                if (w.Handle == fg && !w.Class.StartsWith("Shell_")) return w;
            return TopApp(wins);
        }

        // Det øverste programvindue (bruges fra menuen ved uret, hvor proceslinjen har fokus).
        public static WinInfo TopAppWindow() { return TopApp(Windows()); }

        static WinInfo TopApp(List<WinInfo> wins)
        {
            foreach (WinInfo w in wins) if (IsApp(w)) return w;
            return null;
        }
    }
}
