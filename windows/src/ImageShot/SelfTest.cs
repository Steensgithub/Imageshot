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

            Check("Markering: værktøjslinje, lup og piletaster", delegate
            {
                Rectangle vs = SystemInformation.VirtualScreen;
                using (Bitmap fr = Capture.Freeze(false))
                using (RegionOverlay ov = new RegionOverlay(fr, vs, Capture.Windows(), null))
                {
                    Rectangle pb = Screen.PrimaryScreen.Bounds;
                    Cursor.Position = new Point(pb.Left + pb.Width / 3, pb.Top + pb.Height / 2);
                    ov.Show();
                    for (int i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(30); }
                    Point before = Cursor.Position;
                    SendKeys.SendWait("{RIGHT}{RIGHT}+{DOWN}");
                    for (int i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(30); }
                    Point after = Cursor.Position;
                    using (Bitmap shot = Capture.Freeze(false)) shot.Save(Path.Combine(outDir, "ui", "overlay.png"), ImageFormat.Png);
                    ov.Close();
                    string moved = (after.X - before.X) + "," + (after.Y - before.Y);
                    if (moved != "2,10") throw new Exception("piletaster flyttede musen " + moved + " (forventet 2,10)");
                    return "mus flyttet " + moved;
                }
            });

            Check("Editor: figurer, slør, tilskær, fortryd", delegate
            {
                App app = new App();
                app.St = new Settings();
                Bitmap src = SampleImage();
                PreviewForm pf = new PreviewForm(new Shot(src, "editor"), app);
                pf.Show(); Application.DoEvents();
                Doc d = pf.Document;
                Color red = Color.FromArgb(0xE5, 0x48, 0x4D);
                d.Checkpoint(); d.Shapes.Add(new RectShape { Rect = new RectangleF(20, 60, 300, 60), Color = red, Width = 4 });
                d.Checkpoint(); d.Shapes.Add(new ArrowShape { A = new PointF(560, 300), B = new PointF(340, 120), Color = red, Width = 4 });
                d.Checkpoint(); d.Shapes.Add(new EllipseShape { Rect = new RectangleF(420, 200, 160, 90), Color = Theme.Brand, Width = 4 });
                d.Checkpoint(); d.Shapes.Add(new HighlightShape { Rect = new RectangleF(24, 156, 340, 22), Color = Color.Yellow });
                d.Checkpoint(); d.Shapes.Add(new StepShape { Center = new PointF(60, 260), Number = d.NextStep(), Radius = 20, Color = red });
                d.Checkpoint(); d.Shapes.Add(new StepShape { Center = new PointF(120, 260), Number = d.NextStep(), Radius = 20, Color = red });
                TextShape ts = new TextShape { At = new PointF(160, 240), Text = "Klik her – ÆØÅ", Size = 28, Color = red };
                d.Checkpoint(); d.Shapes.Add(ts);
                d.Checkpoint(); d.Shapes.Add(new BlurShape { Rect = new RectangleF(24, 120, 200, 30), Pixelate = true });
                d.Checkpoint(); d.Shapes.Add(new BlurShape { Rect = new RectangleF(400, 60, 220, 40) });
                pf.View.Invalidate();
                for (int i = 0; i < 5; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(30); }
                string dir = Path.Combine(outDir, "ui");
                Directory.CreateDirectory(dir);
                using (Bitmap win = new Bitmap(pf.Width, pf.Height)) { pf.DrawToBitmap(win, new Rectangle(0, 0, pf.Width, pf.Height)); win.Save(Path.Combine(dir, "editor.png"), ImageFormat.Png); }
                using (Bitmap flat = d.Flatten())
                {
                    flat.Save(Path.Combine(dir, "editor-flat.png"), ImageFormat.Png);
                    Color edge = flat.GetPixel(20, 90);
                    if (Math.Abs(edge.R - red.R) > 40 || edge.G > 120) throw new Exception("rammen blev ikke tegnet: " + edge);
                }
                if (d.NextStep() != 3) throw new Exception("trinnummer forkert: " + d.NextStep());
                int n = d.Shapes.Count;
                d.Undo(); d.Undo();
                if (d.Shapes.Count != n - 2) throw new Exception("fortryd virker ikke");
                d.Redo();
                if (d.Shapes.Count != n - 1) throw new Exception("annullér fortryd virker ikke");
                d.Crop(new Rectangle(10, 10, 300, 200));
                if (d.Image.Width != 300 || d.Image.Height != 200 || d.Shapes.Count != 0) throw new Exception("tilskæring forkert");
                d.Undo();
                if (d.Image.Width != 640 || d.Shapes.Count != n - 1) throw new Exception("fortryd af tilskæring forkert");
                pf.Close();
                return n + " figurer, fortryd/annullér, tilskær med figurer";
            });

            Check("Slør og pixelering skjuler indholdet", delegate
            {
                using (Bitmap b = new Bitmap(200, 60, PixelFormat.Format32bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.Clear(Color.White);
                        using (Font f = new Font("Consolas", 20f, GraphicsUnit.Pixel)) g.DrawString("hemmelig123", f, Brushes.Black, 4, 16);
                    }
                    foreach (bool pix in new[] { true, false })
                        using (Bitmap fx = BlurShape.Effect(b, new Rectangle(0, 0, 200, 60), pix))
                        {
                            // Ingen rent sorte pixels tilbage = teksten kan ikke læses.
                            int black = 0;
                            for (int y = 0; y < fx.Height; y++) for (int x = 0; x < fx.Width; x++) if (fx.GetPixel(x, y).R < 60) black++;
                            if (black > 0) throw new Exception((pix ? "pixelering" : "slør") + ": " + black + " mørke pixels");
                        }
                }
                return "ok";
            });

            Check("Tekstgenkendelse (OCR)", delegate
            {
                using (Bitmap b = new Bitmap(520, 120, PixelFormat.Format32bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.Clear(Color.White);
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                        using (Font f = new Font("Segoe UI", 28f, GraphicsUnit.Pixel)) g.DrawString("ImageShot reads text 2026", f, Brushes.Black, 12, 36);
                    }
                    System.Threading.Tasks.Task<string> t = Ocr.RecognizeAsync(b);
                    DateTime until = DateTime.Now.AddSeconds(20);
                    while (!t.IsCompleted && DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
                    if (!t.IsCompleted) throw new Exception("tidsgrænse");
                    string r = t.Result;
                    if (r == null) return "ikke tilgængelig på denne maskine";
                    if (!r.Contains("ImageShot") || !r.Contains("2026")) throw new Exception("læste: " + r);
                    return "\"" + r.Replace("\r", "").Replace("\n", " / ") + "\" (" + Ocr.Languages + ")";
                }
            });

            Check("Fastgør billede", delegate
            {
                PinWindow p = PinWindow.Open(SampleImage(), Screen.PrimaryScreen);
                for (int i = 0; i < 5; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
                bool top = p.TopMost && p.Visible;
                p.Close();
                if (!top) throw new Exception("vinduet var ikke øverst");
                return "ok";
            });

            Check("Historik: gem, vis, indlæs, ryd", delegate
            {
                History.Clear();
                using (Bitmap b = SampleImage())
                    for (int i = 0; i < History.MaxItems + 3; i++) History.Add(b, "test " + i, DateTime.Now.AddSeconds(i));
                List<History.Item> items = History.List();
                if (items.Count != History.MaxItems) throw new Exception("antal " + items.Count);
                if (items[0].Name != "test " + (History.MaxItems + 2)) throw new Exception("nyeste først? " + items[0].Name);
                using (Bitmap l = History.Load(items[0].Path)) if (l.Width != 640) throw new Exception("indlæst forkert");
                using (Bitmap th = History.Thumb(items[0].Path, 40)) if (th.Height != 40) throw new Exception("miniature forkert");
                History.Clear();
                if (History.List().Count != 0) throw new Exception("ikke ryddet");
                return History.MaxItems + " beholdt, ældste slettet";
            });

            Check("Filnavneskabelon", delegate
            {
                DateTime d = new DateTime(2026, 10, 16, 9, 5, 7);
                string n = Output.BuildName("{app}\\{date} {title} {counter}", d, "Dok: 1?", "WINWORD");
                if (n != "WINWORD\\2026-10-16 Dok 1 {counter}") throw new Exception(n);
                Settings st = new Settings(); st.Folder = Path.Combine(outDir, "skabelon");
                string p1, p2;
                using (Bitmap b = new Bitmap(10, 10)) { p1 = Output.Save(new Shot(b, n), st, null, true); p2 = Output.Save(new Shot(b, n), st, null, true); }
                if (!p1.EndsWith("WINWORD\\2026-10-16 Dok 1 001.png") || !p2.EndsWith("002.png")) throw new Exception(p1 + " / " + p2);
                return Path.GetFileName(p1) + ", " + Path.GetFileName(p2);
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
