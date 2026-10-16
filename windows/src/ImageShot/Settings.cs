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
    // Indstillinger (gemmes i %APPDATA%\Skaermbillede\indstillinger.ini)
    // ------------------------------------------------------------------
    class Settings
    {
        public int Resolution = 70;            // 0 = mindste fil, 100 = højeste opløsning
        public string Format = "png";          // png | jpeg
        public string AfterCapture = "clipboard"; // clipboard | copy | save
        public bool AutoClose = true;
        public string Folder;
        public bool AlwaysDx = false;
        public bool FirstRun = true;
        public string Language = "auto";       // auto | da | en | de | es
        public string Theme = "auto";          // auto | light | dark
        public bool CheckUpdates = true;
        public bool KeepHistory = true;
        public string FileTemplate = "{prefix} {date} {time} {title}";
        public Keys HkRegion = Keys.PrintScreen;
        public Keys HkWindow = Keys.Alt | Keys.PrintScreen;
        public Keys HkScreen = Keys.Control | Keys.PrintScreen;
        public Keys HkAll = Keys.Control | Keys.Shift | Keys.PrintScreen;

        public Settings()
        {
            Folder = Path.Combine(Native.DownloadsFolder(), "ImageShot");
        }


        static string FilePath { get { return AppPaths.SettingsFile; } }

        public Settings Clone() { return (Settings)MemberwiseClone(); }

        public Keys Hotkey(Mode m)
        {
            switch (m)
            {
                case Mode.Region: return HkRegion;
                case Mode.Window: return HkWindow;
                case Mode.Screen: return HkScreen;
                default: return HkAll;
            }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i).Trim();
                    string v = line.Substring(i + 1).Trim();
                    int n;
                    switch (k)
                    {
                        case "opløsning": if (int.TryParse(v, out n)) s.Resolution = Math.Max(0, Math.Min(100, n)); break;
                        case "format": if (v == "png" || v == "jpeg") s.Format = v; break;
                        case "efterOptagelse": if (v == "clipboard" || v == "copy" || v == "save") s.AfterCapture = v; break;
                        case "lukAutomatisk": s.AutoClose = v == "1"; break;
                        case "mappe": if (v.Length > 0) s.Folder = v; break;
                        case "altidDirectX": s.AlwaysDx = v == "1"; break;
                        case "førsteStart": s.FirstRun = v == "1"; break;
                        case "sprog": if (v.Length > 0) s.Language = v; break;
                        case "tema": if (v == "auto" || v == "light" || v == "dark") s.Theme = v; break;
                        case "opdateringer": s.CheckUpdates = v == "1"; break;
                        case "historik": s.KeepHistory = v == "1"; break;
                        case "filnavn": if (v.Length > 0) s.FileTemplate = v; break;
                        case "genvejOmråde": if (int.TryParse(v, out n)) s.HkRegion = (Keys)n; break;
                        case "genvejVindue": if (int.TryParse(v, out n)) s.HkWindow = (Keys)n; break;
                        case "genvejSkærm": if (int.TryParse(v, out n)) s.HkScreen = (Keys)n; break;
                        case "genvejAlle": if (int.TryParse(v, out n)) s.HkAll = (Keys)n; break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppPaths.DataDir);
                StringBuilder b = new StringBuilder();
                b.AppendLine("opløsning=" + Resolution);
                b.AppendLine("format=" + Format);
                b.AppendLine("efterOptagelse=" + AfterCapture);
                b.AppendLine("lukAutomatisk=" + (AutoClose ? "1" : "0"));
                b.AppendLine("mappe=" + Folder);
                b.AppendLine("altidDirectX=" + (AlwaysDx ? "1" : "0"));
                b.AppendLine("førsteStart=" + (FirstRun ? "1" : "0"));
                b.AppendLine("sprog=" + Language);
                b.AppendLine("tema=" + Theme);
                b.AppendLine("opdateringer=" + (CheckUpdates ? "1" : "0"));
                b.AppendLine("historik=" + (KeepHistory ? "1" : "0"));
                b.AppendLine("filnavn=" + FileTemplate);
                b.AppendLine("genvejOmråde=" + (int)HkRegion);
                b.AppendLine("genvejVindue=" + (int)HkWindow);
                b.AppendLine("genvejSkærm=" + (int)HkScreen);
                b.AppendLine("genvejAlle=" + (int)HkAll);
                File.WriteAllText(FilePath, b.ToString(), new UTF8Encoding(true));
            }
            catch { }
        }

        // Samme omregning som browserudvidelsen: skyder -> skalering og kvalitet.
        public Quality Q
        {
            get
            {
                double t = Math.Max(0, Math.Min(100, Resolution)) / 100.0;
                Quality q = new Quality();
                q.Scale = Resolution >= 100 ? 1 : 0.5 + 0.5 * Math.Pow(t, 0.82);
                q.JpegQuality = (long)Math.Round(50 + 50 * t);
                q.Png = Format != "jpeg";
                q.Ext = q.Png ? "png" : "jpg";
                if (Resolution <= 15) q.Label = L.T("q.smallest");
                else if (Resolution <= 40) q.Label = L.T("q.small");
                else if (Resolution <= 80) q.Label = L.T("q.balanced");
                else if (Resolution < 100) q.Label = L.T("q.high");
                else q.Label = L.T("q.highest");
                q.Detail = L.T("q.detail", q.Png ? "PNG" : "JPEG", (int)Math.Round(q.Scale * 100), q.Png ? L.T("q.lossless") : L.T("q.quality", q.JpegQuality));
                return q;
            }
        }
    }

    class Quality
    {
        public double Scale;
        public long JpegQuality;
        public bool Png;
        public string Ext;
        public string Label;
        public string Detail;
    }
}
