using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ImageShot
{
    // Kører de vigtigste funktioner i det rigtige program på rigtig Windows (bruges af CI).
    // ImageShot.exe --selftest <mappe>  →  <mappe>\report.txt + billeder. Afslutter med 0 = alt ok.
    static class SelfTest
    {
        static StringBuilder rep;
        static int fails;

        static void Check(string name, Func<string> f)
        {
            try
            {
                string r = f();
                rep.AppendLine("OK    " + name + (string.IsNullOrEmpty(r) ? "" : "  –  " + r));
            }
            catch (Exception ex)
            {
                fails++;
                rep.AppendLine("FEJL  " + name + Environment.NewLine + ex);
            }
        }

        static void Shoot(Form f, string file)
        {
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(20, 20);
            f.Show();
            for (int i = 0; i < 5; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(30); }
            using (Bitmap b = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(b, new Rectangle(0, 0, f.Width, f.Height));
                b.Save(file, ImageFormat.Png);
            }
            f.Close();
            f.Dispose();
        }

        static Bitmap SampleImage()
        {
            Bitmap b = new Bitmap(640, 400, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.White);
                g.FillRectangle(new SolidBrush(Color.FromArgb(0x1F, 0x6F, 0xEB)), 0, 0, 640, 48);
                using (Font f = new Font("Segoe UI", 14f)) g.DrawString("Eksempel – Example", f, Brushes.Black, 24, 80);
                g.FillRectangle(Brushes.LightGray, 24, 130, 400, 14);
                g.FillRectangle(Brushes.LightGray, 24, 160, 340, 14);
            }
            return b;
        }

        public static int Run(string outDir)
        {
            Directory.CreateDirectory(outDir);
            rep = new StringBuilder();
            fails = 0;
            rep.AppendLine("ImageShot selvtest  " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + "  Windows " + Native.WindowsVersion());

            Check("Oversættelser komplette", delegate
            {
                int n = 0;
                foreach (KeyValuePair<string, string[]> kv in L.All)
                {
                    if (kv.Value.Length != L.Languages.Length) throw new Exception("forkert antal sprog: " + kv.Key);
                    foreach (string v in kv.Value) if (string.IsNullOrEmpty(v)) throw new Exception("mangler tekst: " + kv.Key);
                    n++;
                }
                foreach (string lang in L.Languages) { L.Init(lang); L.T("toast.saved", "x"); L.T("q.detail", "PNG", 87, "x"); }
                L.Init("auto");
                return n + " nøgler × " + L.Languages.Length + " sprog, auto = " + L.Current;
            });

            Check("Skærme", delegate
            {
                StringBuilder b = new StringBuilder();
                foreach (Screen s in Screen.AllScreens) b.Append(s.Bounds + (Dxgi.IsHdr(s.Bounds) ? " HDR" : "") + "; ");
                return b.ToString();
            });

            Bitmap frozen = null;
            Check("Optagelse af hele skrivebordet", delegate
            {
                frozen = Capture.Freeze(false);
                frozen.Save(Path.Combine(outDir, "freeze.png"), ImageFormat.Png);
                return frozen.Width + "×" + frozen.Height + ", sorte skærme: " + Capture.BlackScreens + ", sort samlet: " + Capture.IsBlack(frozen, new Rectangle(0, 0, frozen.Width, frozen.Height));
            });

            Check("DirectX-optagelse (primær skærm)", delegate
            {
                Bitmap dx = Dxgi.CaptureMonitor(Screen.PrimaryScreen.Bounds);
                string t = Dxgi.LastTrace;
                if (dx == null) return "intet billede (ok på maskiner uden GPU) – " + t;
                using (dx) dx.Save(Path.Combine(outDir, "dxgi.png"), ImageFormat.Png);
                return "ok – " + t;
            });

            Check("DirectX-forbindelse til hver skærm (bruges under scroll)", delegate
            {
                StringBuilder b = new StringBuilder();
                foreach (Screen s in Screen.AllScreens)
                {
                    using (Dxgi.Session ses = Dxgi.OpenSession(s.Bounds))
                    {
                        if (ses == null || !ses.Ok) { b.Append(s.DeviceName + ": ingen (" + Dxgi.LastTrace + "); "); continue; }
                        Rectangle crop = new Rectangle(s.Bounds.X, s.Bounds.Y, Math.Min(300, s.Bounds.Width), Math.Min(200, s.Bounds.Height));
                        Bitmap f = ses.Next(500, crop);
                        b.Append(s.DeviceName + ": format " + ses.Format + (f == null ? ", intet billede" : ", " + f.Width + "×" + f.Height) + "; ");
                        if (f != null) f.Dispose();
                    }
                }
                return b.ToString();
            });

            Check("Ingen hukommelseslæk ved gentagne optagelser", delegate
            {
                Memory.Trim();
                long before = Memory.PrivateMB();
                for (int i = 0; i < 8; i++)
                {
                    using (Bitmap f = Capture.Freeze(false))
                    using (Bitmap c = Capture.Crop(f, SystemInformation.VirtualScreen, new Rectangle(SystemInformation.VirtualScreen.Location, new Size(200, 200)))) { }
                    Memory.Trim();
                }
                long after = Memory.PrivateMB();
                if (after - before > 60) throw new Exception("hukommelse voksede " + before + " → " + after + " MB");
                return before + " → " + after + " MB efter 8 optagelser";
            });

            Check("Beskær, skalér, gem PNG og JPEG", delegate
            {
                using (Bitmap src = new Bitmap(400, 300, PixelFormat.Format32bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(src)) { g.Clear(Color.SteelBlue); g.FillRectangle(Brushes.White, 50, 50, 100, 80); }
                    Rectangle vs = new Rectangle(0, 0, 400, 300);
                    using (Bitmap crop = Capture.Crop(src, vs, new Rectangle(40, 40, 200, 150)))
                    {
                        Bitmap scaled = Capture.Scale(crop, 0.87);
                        Settings st = new Settings();
                        st.Folder = Path.Combine(outDir, "gemte filer");
                        Shot shot = new Shot(scaled, "Test – ÆØÅ äöü ñ");
                        string png = Output.Save(shot, st, null, true);
                        st.Format = "jpeg";
                        string jpg = Output.Save(shot, st, null, false);
                        if (!File.Exists(png) || !File.Exists(jpg)) throw new Exception("filer mangler");
                        string r = Path.GetFileName(png) + ", " + Path.GetFileName(jpg) + ", " + scaled.Width + "×" + scaled.Height;
                        if (!ReferenceEquals(scaled, crop)) scaled.Dispose();
                        return r;
                    }
                }
            });

            Check("Udklipsholder", delegate
            {
                using (Bitmap b = new Bitmap(64, 32, PixelFormat.Format32bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(b)) g.Clear(Color.OrangeRed);
                    Output.Copy(b);
                    IDataObject d = Clipboard.GetDataObject();
                    bool ok = d != null && (d.GetDataPresent(DataFormats.Bitmap) || d.GetDataPresent("PNG"));
                    if (!ok) throw new Exception("billedet kom ikke på udklipsholderen");
                    return "billede og PNG lagt på";
                }
            });

            Check("Sammensyning (syntetisk scroll)", delegate
            {
                Random r = new Random(3);
                using (Bitmap doc = new Bitmap(300, 3000, PixelFormat.Format32bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(doc))
                    {
                        g.Clear(Color.White);
                        for (int y = 0; y < 3000; y += 18)
                            using (SolidBrush br = new SolidBrush(Color.FromArgb(r.Next(256), r.Next(256), r.Next(256)))) g.FillRectangle(br, r.Next(10, 80), y, r.Next(40, 200), 10);
                    }
                    Func<int, Bitmap> frame = delegate (int pos)
                    {
                        Bitmap f = new Bitmap(300, 400, PixelFormat.Format32bppRgb);
                        using (Graphics g = Graphics.FromImage(f)) g.DrawImage(doc, new Rectangle(0, 0, 300, 400), new Rectangle(0, pos, 300, 400), GraphicsUnit.Pixel);
                        return f;
                    };
                    Stitcher st;
                    using (Bitmap f0 = frame(0)) st = new Stitcher(f0);
                    using (st)
                    {
                        for (int pos = 60; pos <= 1800; pos += 60) using (Bitmap f = frame(pos)) st.Add(f, 1);
                        if (st.S != 1800) throw new Exception("forskydning " + st.S + " i stedet for 1800");
                        return "S = " + st.S;
                    }
                }
            });

            Check("Indstillinger: kvalitet og sprog", delegate
            {
                Settings s = new Settings();
                s.Resolution = 70;
                Quality q = s.Q;
                if (Math.Abs(q.Scale - 0.87) > 0.01) throw new Exception("skalering " + q.Scale);
                return q.Detail;
            });

            Check("Vinduer kan åbnes og lukkes", delegate
            {
                App app = new App();
                app.St = new Settings();
                using (Bitmap b = new Bitmap(320, 200, PixelFormat.Format32bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(b)) g.Clear(Color.White);
                    PreviewForm p = new PreviewForm(new Shot((Bitmap)b.Clone(), "test"), app);
                    p.Show(); Application.DoEvents(); p.Close();
                }
                SettingsForm sf = new SettingsForm(app, true);
                sf.Show(); Application.DoEvents(); sf.Close();
                Toast.ShowText("selvtest", false); Application.DoEvents(); Toast.CloseCurrent();
                AboutForm af = new AboutForm(app);
                af.Show(); Application.DoEvents(); af.Close();
                return "udklipsholder, indstillinger, besked, om";
            });

            Check("Skærmbilleder af vinduerne (lyst/mørkt, dansk/tysk)", delegate
            {
                string dir = Path.Combine(outDir, "ui");
                Directory.CreateDirectory(dir);
                int n = 0;
                foreach (string combo in new[] { "da-light", "da-dark", "de-light", "es-dark" })
                {
                    string lang = combo.Substring(0, 2), th = combo.Substring(3);
                    L.Init(lang); Theme.Init(th);
                    App app = new App();
                    app.St = new Settings();
                    Shoot(new SettingsForm(app, true), Path.Combine(dir, "settings-" + combo + ".png")); n++;
                    Shoot(new AboutForm(app), Path.Combine(dir, "about-" + combo + ".png")); n++;
                    using (Bitmap b = SampleImage())
                        Shoot(new PreviewForm(new Shot((Bitmap)b.Clone(), "test"), app), Path.Combine(dir, "preview-" + combo + ".png"));
                    n++;
                }
                L.Init("auto"); Theme.Init("auto");
                return n + " billeder i ui\\";
            });

            Check("Tastaturhook kan installeres og fjernes", delegate
            {
                using (Control c = new Control())
                {
                    c.CreateControl();
                    KeyboardHook kh = new KeyboardHook(c, delegate (int m) { });
                    bool ok = kh.Start();
                    kh.Dispose();
                    if (!ok) throw new Exception("SetWindowsHookEx fejlede");
                    return "ok";
                }
            });

            if (frozen != null) frozen.Dispose();
            rep.AppendLine(fails == 0 ? "RESULTAT: alt ok" : "RESULTAT: " + fails + " fejl");
            File.WriteAllText(Path.Combine(outDir, "report.txt"), rep.ToString(), new UTF8Encoding(true));
            Console.WriteLine(rep.ToString());
            return fails == 0 ? 0 : 1;
        }
    }
}
