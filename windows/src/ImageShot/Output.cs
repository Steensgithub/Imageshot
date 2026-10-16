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
    // Kopiér og gem
    // ------------------------------------------------------------------
    class Shot
    {
        public Bitmap Image;
        public string Name;
        public string SavedPath;
        public Shot(Bitmap image, string name) { Image = image; Name = name; }
    }

    static class Output
    {
        public static string Stamp(DateTime d)
        {
            return d.ToString("yyyy-MM-dd") + " kl. " + d.ToString("HH.mm.ss");
        }

        public const string DefaultTemplate = "{prefix} {date} {time} {title}";

        // Filnavn ud fra skabelonen i indstillingerne. "\\" giver undermapper; {counter} udfyldes ved gemning.
        public static string BuildName(string template, DateTime d, string title, string app)
        {
            string t = string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template.Trim();
            string ti = Clean(title);
            if (ti.Length > 40) ti = ti.Substring(0, 40).Trim();
            return t.Replace("{prefix}", L.T("file.prefix"))
                    .Replace("{date}", d.ToString("yyyy-MM-dd"))
                    .Replace("{time}", d.ToString("HH.mm.ss"))
                    .Replace("{title}", ti)
                    .Replace("{app}", Clean(app));
        }

        // Programnavnet (fx "WINWORD") for et vindue.
        public static string AppOf(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return "";
            try
            {
                uint pid;
                Native.GetWindowThreadProcessId(hwnd, out pid);
                using (Process p = Process.GetProcessById((int)pid)) return p.ProcessName;
            }
            catch { return ""; }
        }

        // Sidste del af navnet (uden mapper og tæller) – til "Gem som".
        public static string LeafName(string name)
        {
            string[] parts = (name ?? "").Split('\\', '/');
            string leaf = Clean(parts[parts.Length - 1].Replace("{counter}", ""));
            return leaf.Length > 0 ? leaf : L.T("file.prefix");
        }

        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder b = new StringBuilder();
            foreach (char c in s)
            {
                if (c < 32 || "<>:\"/\\|?*~".IndexOf(c) >= 0) continue;
                b.Append(char.IsWhiteSpace(c) ? ' ' : c);
            }
            string r = b.ToString();
            while (r.Contains("  ")) r = r.Replace("  ", " ");
            return r.Trim().Trim('.').Trim();
        }

        // Udklipsholderen får både et almindeligt billede og en PNG-version.
        public static void Copy(Bitmap img)
        {
            DataObject data = new DataObject();
            Bitmap opaque = new Bitmap(img.Width, img.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(opaque)) g.DrawImageUnscaled(img, 0, 0);
            data.SetData(DataFormats.Bitmap, true, opaque);
            MemoryStream png = new MemoryStream();
            img.Save(png, ImageFormat.Png);
            data.SetData("PNG", false, png);
            Clipboard.SetDataObject(data, true, 10, 100);
        }

        public static string Save(Shot shot, Settings st, string explicitPath, bool png)
        {
            string path = explicitPath;
            if (path == null)
            {
                string dir = st.Folder;
                string[] parts = (shot.Name ?? "").Split('\\', '/');
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    string sub = Clean(parts[i]);
                    if (sub.Length > 0) dir = Path.Combine(dir, sub.Length > 60 ? sub.Substring(0, 60).Trim() : sub);
                }
                Directory.CreateDirectory(dir);
                string name = parts[parts.Length - 1];
                string ext = png ? ".png" : ".jpg";
                if (name.Contains("{counter}"))
                {
                    for (int i = 1; ; i++)
                    {
                        string n = Fit(name.Replace("{counter}", i.ToString("000")));
                        path = Path.Combine(dir, n + ext);
                        if (!File.Exists(path)) break;
                    }
                }
                else
                {
                    name = Fit(name);
                    path = Path.Combine(dir, name + ext);
                    for (int i = 2; File.Exists(path); i++)
                        path = Path.Combine(dir, name + " (" + i + ")" + ext);
                }
            }
            if (png)
            {
                shot.Image.Save(path, ImageFormat.Png);
            }
            else
            {
                ImageCodecInfo jpeg = null;
                foreach (ImageCodecInfo c in ImageCodecInfo.GetImageEncoders())
                    if (c.FormatID == ImageFormat.Jpeg.Guid) jpeg = c;
                using (EncoderParameters p = new EncoderParameters(1))
                {
                    p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, st.Q.JpegQuality);
                    shot.Image.Save(path, jpeg, p);
                }
            }
            return path;
        }

        static string Fit(string name)
        {
            name = Clean(name);
            if (name.Length > 120) name = name.Substring(0, 120).Trim();
            return name.Length == 0 ? L.T("file.prefix") : name;
        }

        public static void OpenFolder(string folder, string selectFile)
        {
            try
            {
                if (selectFile != null && File.Exists(selectFile))
                {
                    Process.Start("explorer.exe", "/select,\"" + selectFile + "\"");
                    return;
                }
                Directory.CreateDirectory(folder);
                Process.Start("explorer.exe", "\"" + folder + "\"");
            }
            catch (Exception ex)
            {
                Toast.ShowText(L.T("toast.folderFailed", ex.Message), true);
            }
        }
    }
}
