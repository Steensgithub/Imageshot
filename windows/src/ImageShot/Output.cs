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
                Directory.CreateDirectory(st.Folder);
                string name = Clean(shot.Name);
                if (name.Length > 120) name = name.Substring(0, 120).Trim();
                if (name.Length == 0) name = L.T("file.prefix");
                string ext = png ? ".png" : ".jpg";
                path = Path.Combine(st.Folder, name + ext);
                for (int i = 2; File.Exists(path); i++)
                    path = Path.Combine(st.Folder, name + " (" + i + ")" + ext);
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
