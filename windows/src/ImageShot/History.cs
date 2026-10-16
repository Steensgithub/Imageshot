using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;

namespace ImageShot
{
    // De seneste skærmbilleder gemmes i %APPDATA%\ImageShot\history, så et billede, man glemte at gemme,
    // kan åbnes igen fra menuen "Seneste". Højst MaxItems billeder; de ældste slettes.
    static class History
    {
        public const int MaxItems = 30;
        static readonly object gate = new object();

        public sealed class Item
        {
            public string Path;
            public string Name;
            public DateTime Time;
        }

        static string Index { get { return System.IO.Path.Combine(AppPaths.HistoryDir, "index.txt"); } }

        // Gemmes i baggrunden, så optagelsen ikke venter på PNG-kodningen.
        public static Task AddAsync(Bitmap image, string name)
        {
            Bitmap copy;
            try { copy = new Bitmap(image); } catch (Exception ex) { ErrorLog.Write("Historik", ex); return Task.FromResult(0); }
            return Task.Run(delegate
            {
                try { using (copy) Add(copy, name, DateTime.Now); }
                catch (Exception ex) { ErrorLog.Write("Historik", ex); }
            });
        }

        public static void Add(Bitmap image, string name, DateTime time)
        {
            lock (gate)
            {
                Directory.CreateDirectory(AppPaths.HistoryDir);
                string file = System.IO.Path.Combine(AppPaths.HistoryDir, time.ToString("yyyyMMdd-HHmmss-fff") + ".png");
                image.Save(file, ImageFormat.Png);
                File.AppendAllText(Index, System.IO.Path.GetFileName(file) + "|" + (name ?? "").Replace("|", " ").Replace("\n", " ") + Environment.NewLine);
                Trim();
            }
        }

        public static List<Item> List()
        {
            lock (gate)
            {
                List<Item> items = new List<Item>();
                Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    if (File.Exists(Index))
                        foreach (string line in File.ReadAllLines(Index))
                        {
                            int i = line.IndexOf('|');
                            if (i > 0) names[line.Substring(0, i)] = line.Substring(i + 1);
                        }
                    if (!Directory.Exists(AppPaths.HistoryDir)) return items;
                    foreach (string f in Directory.GetFiles(AppPaths.HistoryDir, "*.png"))
                    {
                        string fn = System.IO.Path.GetFileName(f);
                        DateTime t;
                        if (!DateTime.TryParseExact(fn.Substring(0, Math.Min(19, fn.Length)), "yyyyMMdd-HHmmss-fff", null, System.Globalization.DateTimeStyles.None, out t))
                            t = File.GetLastWriteTime(f);
                        string n;
                        items.Add(new Item { Path = f, Time = t, Name = names.TryGetValue(fn, out n) ? n : fn });
                    }
                }
                catch (Exception ex) { ErrorLog.Write("Historik", ex); }
                items.Sort((a, b) => b.Time.CompareTo(a.Time));
                return items;
            }
        }

        static void Trim()
        {
            List<Item> all = new List<Item>();
            foreach (string f in Directory.GetFiles(AppPaths.HistoryDir, "*.png"))
                all.Add(new Item { Path = f, Time = File.GetLastWriteTime(f) });
            all.Sort((a, b) => b.Time.CompareTo(a.Time));
            for (int i = MaxItems; i < all.Count; i++) { try { File.Delete(all[i].Path); } catch { } }
            if (all.Count > MaxItems * 2) RewriteIndex();
        }

        static void RewriteIndex()
        {
            try
            {
                List<string> keep = new List<string>();
                foreach (string line in File.ReadAllLines(Index))
                {
                    int i = line.IndexOf('|');
                    if (i > 0 && File.Exists(System.IO.Path.Combine(AppPaths.HistoryDir, line.Substring(0, i)))) keep.Add(line);
                }
                File.WriteAllLines(Index, keep.ToArray());
            }
            catch { }
        }

        public static void Clear()
        {
            lock (gate)
            {
                try
                {
                    if (!Directory.Exists(AppPaths.HistoryDir)) return;
                    foreach (string f in Directory.GetFiles(AppPaths.HistoryDir)) { try { File.Delete(f); } catch { } }
                }
                catch { }
            }
        }

        // Indlæs uden at låse filen.
        public static Bitmap Load(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            using (MemoryStream ms = new MemoryStream(data))
            using (Image img = Image.FromStream(ms))
            {
                Bitmap b = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppRgb);
                using (Graphics g = Graphics.FromImage(b)) g.DrawImage(img, 0, 0, img.Width, img.Height);
                return b;
            }
        }

        public static Bitmap Thumb(string path, int height)
        {
            using (Bitmap full = Load(path))
            {
                int h = height, w = Math.Max(1, Math.Min(height * 3, full.Width * height / Math.Max(1, full.Height)));
                Bitmap t = new Bitmap(w, h);
                using (Graphics g = Graphics.FromImage(t))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    float s = Math.Max(w / (float)full.Width, h / (float)full.Height);
                    g.DrawImage(full, (w - full.Width * s) / 2, 0, full.Width * s, full.Height * s);
                }
                return t;
            }
        }
    }
}
