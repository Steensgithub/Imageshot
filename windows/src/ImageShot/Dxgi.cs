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
    // DirectX-optagelse (DXGI Desktop Duplication) til spil i fuldskærm.
    // Kaldes via COM-funktionstabeller, så der ikke kræves ekstra biblioteker.
    // ------------------------------------------------------------------
    static class Dxgi
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int QIFn(IntPtr self, ref Guid iid, out IntPtr ppv);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint ReleaseFn(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnumFn(IntPtr self, uint index, out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetDescFn(IntPtr self, IntPtr desc);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DuplicateFn(IntPtr self, IntPtr device, out IntPtr dup);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int AcquireFn(IntPtr self, uint timeoutMs, IntPtr info, out IntPtr resource);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int ReleaseFrameFn(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Duplicate1Fn(IntPtr self, IntPtr device, uint flags, uint count, IntPtr formats, out IntPtr dup);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateTexFn(IntPtr self, IntPtr desc, IntPtr init, out IntPtr tex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void TexDescFn(IntPtr self, IntPtr desc);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void CopyResFn(IntPtr self, IntPtr dst, IntPtr src);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int MapFn(IntPtr self, IntPtr res, uint sub, int type, uint flags, IntPtr mapped);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void UnmapFn(IntPtr self, IntPtr res, uint sub);

        // Funktionsnumre i COM-tabellerne (kontrolleret mod Windows SDK-headerne).
        const int QI = 0, RELEASE = 2;
        const int FACTORY_ENUMADAPTERS = 7, ADAPTER_ENUMOUTPUTS = 7, OUTPUT_GETDESC = 7, OUTPUT1_DUPLICATE = 22;
        const int DUP_ACQUIRE = 8, DUP_RELEASEFRAME = 14;
        const int DEVICE_CREATETEXTURE2D = 5, TEXTURE_GETDESC = 10;
        const int CTX_MAP = 14, CTX_UNMAP = 15, CTX_COPYRESOURCE = 47;
        const int OUTPUT5_DUPLICATE1 = 26, OUTPUT6_GETDESC1 = 27;
        static readonly Guid IID_IDXGIOutput5 = new Guid("80a07424-ab52-42eb-833c-0c42fd282d98");
        static readonly Guid IID_IDXGIOutput6 = new Guid("068346e8-aaec-4b84-add7-137f513f77a1");

        // Kopierer området c fra et kortlagt DirectX-billede til en 32-bit bitmap.
        // format 87/91 = B8G8R8A8 (8-bit), format 10 = R16G16B16A16_FLOAT (HDR, scRGB).
        internal static Bitmap CopyMapped(IntPtr data, int pitch, int format, Rectangle c, byte[] hdrLut)
        {
            Bitmap bmp = new Bitmap(c.Width, c.Height, PixelFormat.Format32bppRgb);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, c.Width, c.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try
            {
                if (format == 87 || format == 91)
                {
                    UIntPtr row = new UIntPtr((uint)(c.Width * 4));
                    for (int y = 0; y < c.Height; y++)
                        CopyMemory(bd.Scan0 + y * bd.Stride, data + (c.Y + y) * pitch + c.X * 4, row);
                }
                else
                {
                    // HDR: 4 halve kommatal pr. pixel (R, G, B, A) → omregnes via tabel til sRGB.
                    short[] src = new short[c.Width * 4];
                    int[] dst = new int[c.Width];
                    for (int y = 0; y < c.Height; y++)
                    {
                        Marshal.Copy(data + (c.Y + y) * pitch + c.X * 8, src, 0, src.Length);
                        for (int x = 0; x < c.Width; x++)
                        {
                            int r = hdrLut[(ushort)src[x * 4]], g = hdrLut[(ushort)src[x * 4 + 1]], b = hdrLut[(ushort)src[x * 4 + 2]];
                            dst[x] = (r << 16) | (g << 8) | b;
                        }
                        Marshal.Copy(dst, 0, bd.Scan0 + y * bd.Stride, c.Width);
                    }
                }
            }
            finally { bmp.UnlockBits(bd); }
            return bmp;
        }

        // Tabel: halvt kommatal (scRGB, 1.0 = 80 nits) → 8-bit sRGB, hvor SDR-hvid = sdrScale.
        internal static byte[] BuildHdrLut(float sdrScale)
        {
            byte[] lut = new byte[65536];
            for (int h = 0; h < 65536; h++)
            {
                int sign = h >> 15, exp = (h >> 10) & 0x1F, mant = h & 0x3FF;
                double f;
                if (exp == 0) f = mant * Math.Pow(2, -24);
                else if (exp == 31) f = mant == 0 ? 1e9 : 0; // uendelig → hvid, NaN → sort
                else f = (1 + mant / 1024.0) * Math.Pow(2, exp - 15);
                if (sign == 1) f = 0;
                double v = Math.Max(0, Math.Min(1, f / sdrScale));
                double srgb = v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;
                lut[h] = (byte)Math.Round(Math.Max(0, Math.Min(1, srgb)) * 255);
            }
            return lut;
        }

        static readonly Dictionary<string, byte[]> lutCache = new Dictionary<string, byte[]>();
        static byte[] LutFor(Rectangle bounds)
        {
            string dev = "";
            foreach (Screen sc in Screen.AllScreens) if (sc.Bounds == bounds) dev = sc.DeviceName;
            byte[] lut;
            if (lutCache.TryGetValue(dev, out lut)) return lut;
            float scale = Native.SdrWhiteScale(dev);
            T("SDR-hvid " + scale.ToString("0.00"));
            lut = BuildHdrLut(scale);
            lutCache[dev] = lut;
            return lut;
        }

        public static string LastTrace = "";
        static void T(string s) { LastTrace += s + " "; }
        static string Hr(int hr) { return "0x" + hr.ToString("X8"); }

        static readonly Guid IID_IDXGIFactory1 = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
        static readonly Guid IID_IDXGIOutput1 = new Guid("00cddea8-939b-4b83-a340-a685226666cc");
        static readonly Guid IID_ID3D11Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

        [DllImport("dxgi.dll")]
        static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr factory);

        [DllImport("d3d11.dll")]
        static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags,
            IntPtr featureLevels, uint numFeatureLevels, uint sdkVersion,
            out IntPtr device, out int featureLevel, out IntPtr context);

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        static extern void CopyMemory(IntPtr dest, IntPtr src, UIntPtr count);

        static T F<T>(IntPtr obj, int slot) where T : class
        {
            IntPtr vtbl = Marshal.ReadIntPtr(obj);
            IntPtr fn = Marshal.ReadIntPtr(vtbl, slot * IntPtr.Size);
            return Marshal.GetDelegateForFunctionPointer(fn, typeof(T)) as T;
        }

        static void Rel(ref IntPtr p)
        {
            if (p != IntPtr.Zero) { F<ReleaseFn>(p, RELEASE)(p); p = IntPtr.Zero; }
        }

        [HandleProcessCorruptedStateExceptions, SecurityCritical]
        public static Bitmap CaptureMonitor(Rectangle bounds)
        {
            LastTrace = "";
            try { return Find(bounds, 0) as Bitmap; }
            catch (Exception ex) { T("fejl: " + ex.Message); return null; }
        }

        // Kører skærmen i HDR? (IDXGIOutput6 → farverum BT.2020 PQ)
        [HandleProcessCorruptedStateExceptions, SecurityCritical]
        public static bool IsHdr(Rectangle bounds)
        {
            try { object r = Find(bounds, 1); return r is bool && (bool)r; }
            catch { return false; }
        }

        static object HdrOf(IntPtr output)
        {
            IntPtr o6 = IntPtr.Zero;
            IntPtr desc = Marshal.AllocHGlobal(256);
            try
            {
                Guid g6 = IID_IDXGIOutput6;
                if (F<QIFn>(output, QI)(output, ref g6, out o6) < 0) return false; // ældre Windows: ingen HDR
                if (F<GetDescFn>(o6, OUTPUT6_GETDESC1)(o6, desc) < 0) return false;
                // DXGI_OUTPUT_DESC1: … HMONITOR (offset 88), BitsPerColor, ColorSpace
                int colorSpace = Marshal.ReadInt32(desc, 88 + IntPtr.Size + 4);
                return colorSpace == 12; // DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020
            }
            finally
            {
                Marshal.FreeHGlobal(desc);
                Rel(ref o6);
            }
        }

        // mode: 0 = ét billede, 1 = er skærmen HDR?, 2 = vedvarende forbindelse (Session)
        static Rectangle curBounds;

        static object Find(Rectangle bounds, int mode)
        {
            curBounds = bounds;
            IntPtr factory;
            Guid gf = IID_IDXGIFactory1;
            if (CreateDXGIFactory1(ref gf, out factory) < 0) return null;
            IntPtr desc = Marshal.AllocHGlobal(128);
            try
            {
                for (uint a = 0; a < 16; a++)
                {
                    IntPtr adapter;
                    if (F<EnumFn>(factory, FACTORY_ENUMADAPTERS)(factory, a, out adapter) < 0) break;
                    try
                    {
                        for (uint o = 0; o < 16; o++)
                        {
                            IntPtr output;
                            if (F<EnumFn>(adapter, ADAPTER_ENUMOUTPUTS)(adapter, o, out output) < 0) break;
                            try
                            {
                                if (F<GetDescFn>(output, OUTPUT_GETDESC)(output, desc) < 0) continue;
                                // DXGI_OUTPUT_DESC: DeviceName[32] (64 byte), RECT, AttachedToDesktop, Rotation, HMONITOR
                                int l = Marshal.ReadInt32(desc, 64), t = Marshal.ReadInt32(desc, 68);
                                int r = Marshal.ReadInt32(desc, 72), b = Marshal.ReadInt32(desc, 76);
                                int rotation = Marshal.ReadInt32(desc, 84);
                                if (new Rectangle(l, t, r - l, b - t) != bounds) continue;
                                if (mode == 1) return HdrOf(output);
                                if (rotation > 1) { T("drejet skærm"); return null; } // ikke understøttet
                                if (mode == 2) { Session ses = new Session(adapter, output, bounds); if (ses.Ok) return ses; ses.Dispose(); return null; }
                                return Read(adapter, output);
                            }
                            finally { Rel(ref output); }
                        }
                    }
                    finally { Rel(ref adapter); }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(desc);
                Rel(ref factory);
            }
            return null;
        }

        static Bitmap Read(IntPtr adapter, IntPtr output)
        {
            IntPtr out1 = IntPtr.Zero, dev = IntPtr.Zero, ctx = IntPtr.Zero, dup = IntPtr.Zero;
            IntPtr res = IntPtr.Zero, tex = IntPtr.Zero, staging = IntPtr.Zero;
            IntPtr info = Marshal.AllocHGlobal(64), tdesc = Marshal.AllocHGlobal(64), mapped = Marshal.AllocHGlobal(32);
            bool frameHeld = false;
            try
            {
                Guid g1 = IID_IDXGIOutput1;
                if (F<QIFn>(output, QI)(output, ref g1, out out1) < 0) return null;

                int level;
                // D3D_DRIVER_TYPE_UNKNOWN (kræves når adapteren angives), D3D11_CREATE_DEVICE_BGRA_SUPPORT, SDK 7
                int hr = D3D11CreateDevice(adapter, 0, IntPtr.Zero, 0x20, IntPtr.Zero, 0, 7, out dev, out level, out ctx);
                if (hr < 0) { T("D3D11CreateDevice " + Hr(hr)); return null; }

                // Nyere metode (Windows 10 1703+): beder om almindelige 8-bit farver, så Windows
                // selv omregner fra HDR. Den gamle metode afvises på HDR-skærme.
                IntPtr o5;
                Guid g5 = IID_IDXGIOutput5;
                if (F<QIFn>(output, QI)(output, ref g5, out o5) >= 0)
                {
                    IntPtr fmts = Marshal.AllocHGlobal(4);
                    try
                    {
                        Marshal.WriteInt32(fmts, 87); // DXGI_FORMAT_B8G8R8A8_UNORM
                        hr = F<Duplicate1Fn>(o5, OUTPUT5_DUPLICATE1)(o5, dev, 0, 1, fmts, out dup);
                        T("DuplicateOutput1 " + Hr(hr));
                        if (hr < 0) dup = IntPtr.Zero;
                    }
                    finally { Marshal.FreeHGlobal(fmts); Rel(ref o5); }
                }
                if (dup == IntPtr.Zero)
                {
                    hr = F<DuplicateFn>(out1, OUTPUT1_DUPLICATE)(out1, dev, out dup);
                    T("DuplicateOutput " + Hr(hr));
                    if (hr < 0) { dup = IntPtr.Zero; return null; }
                }

                // Det første billede efter DuplicateOutput indeholder hele skærmen.
                for (int i = 0; i < 6 && !frameHeld; i++)
                {
                    hr = F<AcquireFn>(dup, DUP_ACQUIRE)(dup, 200, info, out res);
                    if (hr == unchecked((int)0x887A0027)) continue; // DXGI_ERROR_WAIT_TIMEOUT
                    if (hr < 0) { T("AcquireNextFrame " + Hr(hr)); return null; }
                    frameHeld = true;
                    // Et billede med kun museændring (LastPresentTime = 0) kan være tomt – tag det næste.
                    if (Marshal.ReadInt64(info, 0) == 0 && i < 5)
                    {
                        Rel(ref res);
                        F<ReleaseFrameFn>(dup, DUP_RELEASEFRAME)(dup);
                        frameHeld = false;
                    }
                }
                if (!frameHeld || res == IntPtr.Zero) return null;

                Guid gt = IID_ID3D11Texture2D;
                if (F<QIFn>(res, QI)(res, ref gt, out tex) < 0) return null;

                // D3D11_TEXTURE2D_DESC: Width, Height, MipLevels, ArraySize, Format, SampleCount, SampleQuality,
                //                       Usage, BindFlags, CPUAccessFlags, MiscFlags
                F<TexDescFn>(tex, TEXTURE_GETDESC)(tex, tdesc);
                int w = Marshal.ReadInt32(tdesc, 0), h = Marshal.ReadInt32(tdesc, 4);
                int format = Marshal.ReadInt32(tdesc, 16);
                T("format " + format);
                if (format != 87 && format != 91 && format != 10) return null; // 8-bit eller HDR (FP16)
                byte[] lut = format == 10 ? LutFor(curBounds) : null;
                Marshal.WriteInt32(tdesc, 8, 1);
                Marshal.WriteInt32(tdesc, 12, 1);
                Marshal.WriteInt32(tdesc, 20, 1);
                Marshal.WriteInt32(tdesc, 24, 0);
                Marshal.WriteInt32(tdesc, 28, 3);        // D3D11_USAGE_STAGING
                Marshal.WriteInt32(tdesc, 32, 0);
                Marshal.WriteInt32(tdesc, 36, 0x20000);  // D3D11_CPU_ACCESS_READ
                Marshal.WriteInt32(tdesc, 40, 0);
                if (F<CreateTexFn>(dev, DEVICE_CREATETEXTURE2D)(dev, tdesc, IntPtr.Zero, out staging) < 0) return null;

                F<CopyResFn>(ctx, CTX_COPYRESOURCE)(ctx, staging, tex);
                if (F<MapFn>(ctx, CTX_MAP)(ctx, staging, 0, 1 /* D3D11_MAP_READ */, 0, mapped) < 0) return null;
                try
                {
                    IntPtr data = Marshal.ReadIntPtr(mapped, 0);
                    int pitch = Marshal.ReadInt32(mapped, IntPtr.Size);
                    return CopyMapped(data, pitch, format, new Rectangle(0, 0, w, h), lut);
                }
                finally { F<UnmapFn>(ctx, CTX_UNMAP)(ctx, staging, 0); }
            }
            finally
            {
                Rel(ref staging);
                Rel(ref tex);
                Rel(ref res);
                if (frameHeld && dup != IntPtr.Zero) F<ReleaseFrameFn>(dup, DUP_RELEASEFRAME)(dup);
                Rel(ref dup);
                Rel(ref out1);
                Rel(ref ctx);
                Rel(ref dev);
                Marshal.FreeHGlobal(info);
                Marshal.FreeHGlobal(tdesc);
                Marshal.FreeHGlobal(mapped);
            }
        }

        // Opretter en skærmkopi-forbindelse (helst med 8-bit farver, så HDR omregnes af Windows).
        static bool CreateDuplication(IntPtr adapter, IntPtr output, out IntPtr dev, out IntPtr ctx, out IntPtr dup)
        {
            dev = IntPtr.Zero; ctx = IntPtr.Zero; dup = IntPtr.Zero;
            int level;
            int hr = D3D11CreateDevice(adapter, 0, IntPtr.Zero, 0x20, IntPtr.Zero, 0, 7, out dev, out level, out ctx);
            if (hr < 0) { T("D3D11CreateDevice " + Hr(hr)); return false; }
            IntPtr o5;
            Guid g5 = IID_IDXGIOutput5;
            if (F<QIFn>(output, QI)(output, ref g5, out o5) >= 0)
            {
                IntPtr fmts = Marshal.AllocHGlobal(4);
                try
                {
                    Marshal.WriteInt32(fmts, 87);
                    hr = F<Duplicate1Fn>(o5, OUTPUT5_DUPLICATE1)(o5, dev, 0, 1, fmts, out dup);
                    T("DuplicateOutput1 " + Hr(hr));
                    if (hr < 0) dup = IntPtr.Zero;
                }
                finally { Marshal.FreeHGlobal(fmts); Rel(ref o5); }
            }
            if (dup == IntPtr.Zero)
            {
                IntPtr out1;
                Guid g1 = IID_IDXGIOutput1;
                if (F<QIFn>(output, QI)(output, ref g1, out out1) >= 0)
                {
                    hr = F<DuplicateFn>(out1, OUTPUT1_DUPLICATE)(out1, dev, out dup);
                    T("DuplicateOutput " + Hr(hr));
                    if (hr < 0) dup = IntPtr.Zero;
                    Rel(ref out1);
                }
            }
            if (dup == IntPtr.Zero) { Rel(ref ctx); Rel(ref dev); return false; }
            return true;
        }

        [HandleProcessCorruptedStateExceptions, SecurityCritical]
        public static Session OpenSession(Rectangle bounds)
        {
            LastTrace = "";
            try { return Find(bounds, 2) as Session; }
            catch (Exception ex) { T("fejl: " + ex.Message); return null; }
        }

        // Vedvarende forbindelse til én skærm: hurtige billeder under scroll (virker også med HDR).
        public sealed class Session : IDisposable
        {
            IntPtr dev, ctx, dup, staging;
            readonly IntPtr info = Marshal.AllocHGlobal(64), tdesc = Marshal.AllocHGlobal(64), mapped = Marshal.AllocHGlobal(32);
            Bitmap last;                 // seneste billede af beskæringen
            Rectangle lastCrop;
            bool disposed;
            public bool Ok, Broken;

            readonly Rectangle bounds;
            byte[] lut;
            public int Format;

            internal Session(IntPtr adapter, IntPtr output, Rectangle bounds)
            {
                this.bounds = bounds;
                Ok = CreateDuplication(adapter, output, out dev, out ctx, out dup);
            }

            // Næste billede (eller det seneste, hvis skærmen ikke har ændret sig), beskåret til crop.
            [HandleProcessCorruptedStateExceptions, SecurityCritical]
            public Bitmap Next(int timeoutMs, Rectangle crop)
            {
                if (!Ok || Broken) return null;
                IntPtr res = IntPtr.Zero, tex = IntPtr.Zero;
                bool held = false;
                try
                {
                    int hr = F<AcquireFn>(dup, DUP_ACQUIRE)(dup, (uint)timeoutMs, info, out res);
                    if (hr == unchecked((int)0x887A0027)) return Cut(crop);          // intet nyt
                    if (hr < 0) { Broken = true; return null; }                    // fx skærmskift
                    held = true;
                    if (Marshal.ReadInt64(info, 0) == 0 && last != null && crop == lastCrop) return Cut(crop); // kun musen flyttede sig
                    Guid gt = IID_ID3D11Texture2D;
                    if (F<QIFn>(res, QI)(res, ref gt, out tex) < 0) return Cut(crop);
                    F<TexDescFn>(tex, TEXTURE_GETDESC)(tex, tdesc);
                    int w = Marshal.ReadInt32(tdesc, 0), h = Marshal.ReadInt32(tdesc, 4);
                    int format = Marshal.ReadInt32(tdesc, 16);
                    Format = format;
                    if (format != 87 && format != 91 && format != 10) { Broken = true; return null; }
                    if (format == 10 && lut == null) lut = LutFor(bounds);
                    if (staging == IntPtr.Zero)
                    {
                        Marshal.WriteInt32(tdesc, 8, 1);
                        Marshal.WriteInt32(tdesc, 12, 1);
                        Marshal.WriteInt32(tdesc, 20, 1);
                        Marshal.WriteInt32(tdesc, 24, 0);
                        Marshal.WriteInt32(tdesc, 28, 3);
                        Marshal.WriteInt32(tdesc, 32, 0);
                        Marshal.WriteInt32(tdesc, 36, 0x20000);
                        Marshal.WriteInt32(tdesc, 40, 0);
                        if (F<CreateTexFn>(dev, DEVICE_CREATETEXTURE2D)(dev, tdesc, IntPtr.Zero, out staging) < 0)
                        { staging = IntPtr.Zero; Broken = true; return null; }
                    }
                    F<CopyResFn>(ctx, CTX_COPYRESOURCE)(ctx, staging, tex);
                    if (F<MapFn>(ctx, CTX_MAP)(ctx, staging, 0, 1, 0, mapped) < 0) return Cut(crop);
                    try
                    {
                        IntPtr data = Marshal.ReadIntPtr(mapped, 0);
                        int pitch = Marshal.ReadInt32(mapped, IntPtr.Size);
                        // Kopiér kun det markerede område (meget mindre end hele skærmen).
                        Rectangle c = crop;
                        c.Intersect(new Rectangle(0, 0, w, h));
                        if (c.Width < 1 || c.Height < 1) return null;
                        Bitmap bmp = CopyMapped(data, pitch, format, c, lut);
                        if (last != null) last.Dispose();
                        last = bmp;
                        lastCrop = crop;
                    }
                    finally { F<UnmapFn>(ctx, CTX_UNMAP)(ctx, staging, 0); }
                    return Cut(crop);
                }
                catch { Broken = true; return null; }
                finally
                {
                    Rel(ref tex);
                    Rel(ref res);
                    if (held) F<ReleaseFrameFn>(dup, DUP_RELEASEFRAME)(dup);
                }
            }

            Bitmap Cut(Rectangle crop)
            {
                if (last == null || crop != lastCrop) return null;
                return last.Clone(new Rectangle(0, 0, last.Width, last.Height), PixelFormat.Format32bppRgb);
            }

            [HandleProcessCorruptedStateExceptions, SecurityCritical]
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                try
                {
                    Rel(ref staging);
                    Rel(ref dup);
                    Rel(ref ctx);
                    Rel(ref dev);
                }
                catch { }
                if (last != null) { last.Dispose(); last = null; }
                Marshal.FreeHGlobal(info);
                Marshal.FreeHGlobal(tdesc);
                Marshal.FreeHGlobal(mapped);
            }
        }
    }
}
