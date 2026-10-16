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
    // Windows-funktioner
    // ------------------------------------------------------------------
    static class Native
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int RegisterWindowMessage(string name);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] public static extern int GetWindowLong(IntPtr h, int index);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
        [DllImport("user32.dll")] static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPaths, out uint numModes);
        [DllImport("user32.dll")] static extern int QueryDisplayConfig(uint flags, ref uint numPaths, IntPtr paths, ref uint numModes, IntPtr modes, IntPtr topology);
        [DllImport("user32.dll")] static extern int DisplayConfigGetDeviceInfo(IntPtr packet);

        // Windows' lysstyrke for SDR-indhold på en HDR-skærm, som faktor af 80 nits (fx 2,5 = 200 nits).
        public static float SdrWhiteScale(string gdiDeviceName)
        {
            IntPtr paths = IntPtr.Zero, modes = IntPtr.Zero, pkt = IntPtr.Zero;
            try
            {
                uint np, nm;
                if (GetDisplayConfigBufferSizes(2, out np, out nm) != 0) return 2.5f; // QDC_ONLY_ACTIVE_PATHS
                paths = Marshal.AllocHGlobal((int)np * 72);
                modes = Marshal.AllocHGlobal((int)nm * 64);
                if (QueryDisplayConfig(2, ref np, paths, ref nm, modes, IntPtr.Zero) != 0) return 2.5f;
                pkt = Marshal.AllocHGlobal(128);
                for (int i = 0; i < np; i++)
                {
                    IntPtr p = paths + i * 72;
                    // Kildens navn (\\.\DISPLAYn): DISPLAYCONFIG_SOURCE_DEVICE_NAME, type 1, 84 byte
                    for (int k = 0; k < 128; k += 4) Marshal.WriteInt32(pkt, k, 0);
                    Marshal.WriteInt32(pkt, 0, 1);
                    Marshal.WriteInt32(pkt, 4, 84);
                    Marshal.WriteInt64(pkt, 8, Marshal.ReadInt64(p, 0));   // kildens adapter-LUID
                    Marshal.WriteInt32(pkt, 16, Marshal.ReadInt32(p, 8));  // kildens id
                    if (DisplayConfigGetDeviceInfo(pkt) != 0) continue;
                    string name = Marshal.PtrToStringUni(pkt + 20);
                    if (!string.Equals(name, gdiDeviceName, StringComparison.OrdinalIgnoreCase)) continue;
                    // SDR-hvidniveau: DISPLAYCONFIG_SDR_WHITE_LEVEL, type 11, 24 byte
                    for (int k = 0; k < 128; k += 4) Marshal.WriteInt32(pkt, k, 0);
                    Marshal.WriteInt32(pkt, 0, 11);
                    Marshal.WriteInt32(pkt, 4, 24);
                    Marshal.WriteInt64(pkt, 8, Marshal.ReadInt64(p, 20));  // målets adapter-LUID
                    Marshal.WriteInt32(pkt, 16, Marshal.ReadInt32(p, 28)); // målets id
                    if (DisplayConfigGetDeviceInfo(pkt) != 0) return 2.5f;
                    uint level = (uint)Marshal.ReadInt32(pkt, 20);
                    if (level < 500 || level > 20000) return 2.5f;
                    return level / 1000f;
                }
            }
            catch { }
            finally
            {
                if (paths != IntPtr.Zero) Marshal.FreeHGlobal(paths);
                if (modes != IntPtr.Zero) Marshal.FreeHGlobal(modes);
                if (pkt != IntPtr.Zero) Marshal.FreeHGlobal(pkt);
            }
            return 2.5f;
        }

        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);

        // Kopierer skærmområdet r ind i g. Returnerer null ved succes, ellers en fejltekst.
        public static string ScreenBlt(Graphics g, Rectangle r)
        {
            IntPtr dst = IntPtr.Zero, src = IntPtr.Zero;
            try
            {
                src = GetDC(IntPtr.Zero);
                if (src == IntPtr.Zero) return "GetDC fejlede";
                dst = g.GetHdc();
                if (!BitBlt(dst, 0, 0, r.Width, r.Height, src, r.Left, r.Top, 0x00CC0020 | 0x40000000)) // SRCCOPY | CAPTUREBLT
                    return "fejlkode " + Marshal.GetLastWin32Error();
                return null;
            }
            catch (Exception ex) { return ex.Message; }
            finally
            {
                if (dst != IntPtr.Zero) g.ReleaseHdc(dst);
                if (src != IntPtr.Zero) try { ReleaseDC(IntPtr.Zero, src); } catch { }
            }
        }
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] public static extern int SetWindowLong(IntPtr h, int index, int value);
        [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
        [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr h, uint affinity);
        [DllImport("user32.dll")] static extern IntPtr RealChildWindowFromPoint(IntPtr parent, POINT pt);
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref POINT pt);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);

        // Er venstre (primære) museknap nede lige nu? null hvis det ikke kan aflæses.
        public static bool? PrimaryButtonDown()
        {
            try
            {
                int vk = GetSystemMetrics(23) != 0 ? 0x02 : 0x01; // SM_SWAPBUTTON
                return (GetAsyncKeyState(vk) & 0x8000) != 0;
            }
            catch { return null; }
        }

        // Det inderste synlige felt i et vindue på et skærmpunkt (fx dokumentfeltet i Word).
        public static IntPtr DeepestChild(IntPtr top, Point screen)
        {
            IntPtr h = top;
            try
            {
                for (int i = 0; i < 32; i++)
                {
                    POINT pt = new POINT { X = screen.X, Y = screen.Y };
                    if (!ScreenToClient(h, ref pt)) break;
                    IntPtr c = RealChildWindowFromPoint(h, pt);
                    if (c == IntPtr.Zero || c == h) break;
                    h = c;
                }
            }
            catch { }
            return h;
        }
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT value, int size);
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("shcore.dll")] static extern int SetProcessDpiAwareness(int value);
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("shell32.dll")] static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);

        // Per-skærm DPI, så koordinater passer præcist på skærme med skalering (125 %, 150 % …).
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr mon, int type, out uint x, out uint y);

        // Skærmens egen DPI (96 = 100 %) ved et punkt i skærmkoordinater.
        public static int DpiAt(System.Drawing.Point p)
        {
            try
            {
                POINT pt; pt.X = p.X; pt.Y = p.Y;
                IntPtr mon = MonitorFromPoint(pt, 2);
                uint x, y;
                if (mon != IntPtr.Zero && GetDpiForMonitor(mon, 0, out x, out y) == 0 && x >= 48) return (int)x;
            }
            catch { }
            try { using (System.Drawing.Graphics g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero)) return (int)g.DpiX; } catch { }
            return 96;
        }

        public static void MakeDpiAware()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } catch { }
            try { if (SetProcessDpiAwareness(2) == 0) return; } catch { }
            try { SetProcessDPIAware(); } catch { }
        }

        [DllImport("ntdll.dll")] static extern int RtlGetVersion(ref OSVERSIONINFOEX v);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OSVERSIONINFOEX
        {
            public int Size, Major, Minor, Build, Platform;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string CsdVersion;
            public ushort SpMajor, SpMinor, SuiteMask; public byte ProductType, Reserved;
        }

        // Den rigtige Windows-version (Environment.OSVersion kan lyve uden manifest).
        public static string WindowsVersion()
        {
            try
            {
                OSVERSIONINFOEX v = new OSVERSIONINFOEX();
                v.Size = Marshal.SizeOf(typeof(OSVERSIONINFOEX));
                if (RtlGetVersion(ref v) == 0) return v.Major + "." + v.Minor + "." + v.Build + (v.Build >= 22000 ? " (Windows 11)" : " (Windows 10)");
            }
            catch { }
            return Environment.OSVersion.Version.ToString();
        }

        public static bool IsWindows11
        {
            get
            {
                try
                {
                    OSVERSIONINFOEX v = new OSVERSIONINFOEX();
                    v.Size = Marshal.SizeOf(typeof(OSVERSIONINFOEX));
                    return RtlGetVersion(ref v) == 0 && v.Build >= 22000;
                }
                catch { return false; }
            }
        }

        public static string DownloadsFolder()
        {
            try
            {
                Guid g = new Guid("374DE290-123F-4565-9164-39C4925E467B");
                IntPtr p;
                if (SHGetKnownFolderPath(ref g, 0, IntPtr.Zero, out p) == 0)
                {
                    string s = Marshal.PtrToStringUni(p);
                    Marshal.FreeCoTaskMem(p);
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            }
            catch { }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        public static string ClassOf(IntPtr h)
        {
            StringBuilder sb = new StringBuilder(256);
            GetClassName(h, sb, sb.Capacity);
            return sb.ToString();
        }

        public static string TitleOf(IntPtr h)
        {
            int n = GetWindowTextLength(h);
            if (n <= 0) return "";
            StringBuilder sb = new StringBuilder(n + 1);
            GetWindowText(h, sb, sb.Capacity);
            return sb.ToString();
        }

        // Vinduets synlige ramme (uden Windows 10's usynlige skyggekant).
        public static Rectangle WindowBounds(IntPtr h)
        {
            RECT r;
            if (DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf(typeof(RECT))) != 0)
                GetWindowRect(h, out r);
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }
    }
}
