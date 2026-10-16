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
    // Programmet ved uret
    // ------------------------------------------------------------------
    class App : IDisposable
    {
        public Settings St;
        NotifyIcon tray;
        HotkeyWindow hk;
        SettingsForm settingsForm;
        bool busy;
        ToolStripMenuItem[] modeItems = new ToolStripMenuItem[4];

        static readonly Mode[] Modes = { Mode.Region, Mode.Window, Mode.Screen, Mode.All };
        static string[] ModeNames { get { return new[] { L.T("mode.region"), L.T("mode.window"), L.T("mode.screen"), L.T("mode.all") }; } }

        public void Start()
        {
            St = Settings.Load();
            L.Init(St.Language);

            tray = new NotifyIcon();
            tray.Icon = Program.AppIcon;
            tray.Text = Program.AppName;
            tray.ContextMenuStrip = BuildMenu();
            tray.MouseUp += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (mi != null) mi.Invoke(tray, null);
                }
            };
            tray.Visible = true;

            hk = new HotkeyWindow(this);
            invoker = new Control();
            invoker.CreateControl(); // håndtag på UI-tråden, så tastaturhooken kan sende hertil
            keyHook = new KeyboardHook(invoker, OnHotkey);
            if (!keyHook.Start()) ErrorLog.Info("Tastaturhook ikke aktiv – PrtScn bruger RegisterHotKey");
            List<string> failed = RegisterHotkeys();

            if (St.FirstRun)
            {
                St.FirstRun = false;
                SetStartup(true);
                St.Save();
                OpenSettings(true);
            }
            else if (failed.Count > 0)
            {
                Toast.ShowText(L.T("toast.hotkeysTaken", string.Join(", ", failed.ToArray())), true);
            }
        }

        ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            for (int i = 0; i < Modes.Length; i++)
            {
                Mode mode = Modes[i];
                ToolStripMenuItem it = new ToolStripMenuItem(ModeNames[i]);
                it.Click += delegate { CaptureFromMenu(mode); };
                modeItems[i] = it;
                m.Items.Add(it);
            }
            if (modeItems[0] != null) modeItems[0].Font = new Font(modeItems[0].Font, FontStyle.Bold);
            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem folder = new ToolStripMenuItem(L.T("menu.folder"));
            folder.Click += delegate { Output.OpenFolder(St.Folder, null); };
            m.Items.Add(folder);
            ToolStripMenuItem log = new ToolStripMenuItem(L.T("menu.log"));
            log.Click += delegate
            {
                try
                {
                    if (!File.Exists(Log.FilePath)) { Log.Begin(); Log.Add("Ingen optagelser endnu."); Log.Save(); }
                    Process.Start("notepad.exe", "\"" + Log.FilePath + "\"");
                }
                catch (Exception ex) { Toast.ShowText(L.T("toast.logFailed", ex.Message), true); }
            };
            m.Items.Add(log);
            ToolStripMenuItem about = new ToolStripMenuItem(L.T("menu.about"));
            about.Click += delegate { using (AboutForm f = new AboutForm(this)) f.ShowDialog(); };
            m.Items.Add(about);
            ToolStripMenuItem settings = new ToolStripMenuItem(L.T("menu.settings"));
            settings.Click += delegate { OpenSettings(false); };
            m.Items.Add(settings);
            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem quit = new ToolStripMenuItem(L.T("menu.quit"));
            quit.Click += delegate { Application.Exit(); };
            m.Items.Add(quit);
            UpdateMenuShortcuts();
            return m;
        }

        void UpdateMenuShortcuts()
        {
            for (int i = 0; i < Modes.Length; i++)
            {
                if (modeItems[i] == null) continue;
                Keys k = St.Hotkey(Modes[i]);
                modeItems[i].ShortcutKeyDisplayString = (k & Keys.KeyCode) == Keys.None ? "" : Hk.ToText(k);
            }
        }

        KeyboardHook keyHook;
        Control invoker;

        public void UnregisterHotkeys()
        {
            for (int id = 1; id <= 4; id++) Native.UnregisterHotKey(hk.Handle, id);
            if (keyHook != null) keyHook.SetHotkeys(new Dictionary<Keys, int>());
        }

        // Returnerer navnene på de genveje, der ikke kunne registreres.
        public List<string> RegisterHotkeys()
        {
            UnregisterHotkeys();
            List<string> failed = new List<string>();
            Dictionary<Keys, int> hooked = new Dictionary<Keys, int>();
            for (int i = 0; i < Modes.Length; i++)
            {
                Keys k = St.Hotkey(Modes[i]);
                Keys key = k & Keys.KeyCode;
                if (key == Keys.None) continue;
                // PrtScn-genveje fanges af tastaturhooken (virker også, når Windows 11 bruger PrtScn til Skærmklip).
                if (key == Keys.PrintScreen && keyHook != null && keyHook.Active) { hooked[k] = i + 1; continue; }
                if (!Native.RegisterHotKey(hk.Handle, i + 1, Hk.Mods(k), (uint)key))
                    failed.Add(Hk.ToText(k) + " (" + ModeNames[i].ToLower() + ")");
            }
            if (keyHook != null) keyHook.SetHotkeys(hooked);
            UpdateMenuShortcuts();
            return failed;
        }

        public void OnHotkey(int id)
        {
            if (id >= 1 && id <= 4) DoCapture(Modes[id - 1], false);
        }

        void CaptureFromMenu(Mode mode)
        {
            // Vent lidt, så menuen er helt væk, før der fotograferes.
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 250;
            t.Tick += delegate { t.Stop(); t.Dispose(); DoCapture(mode, true); };
            t.Start();
        }

        public void DoCapture(Mode mode, bool fromMenu)
        {
            if (busy) return;
            busy = true;
            Bitmap frozen = null;
            try
            {
                Rectangle vs = SystemInformation.VirtualScreen;
                Rectangle target = Rectangle.Empty;
                string title = null;

                // Find målet, før skærmen fryses (fokus kan skifte bagefter).
                if (mode == Mode.Window)
                {
                    WinInfo w = fromMenu ? Capture.TopAppWindow() : Capture.ForegroundAppWindow();
                    if (w != null) { target = w.Bounds; title = w.Title; }
                    else mode = Mode.Screen;
                }
                if (mode == Mode.Screen) target = Screen.FromPoint(Cursor.Position).Bounds;
                if (mode == Mode.All) target = vs;
                List<WinInfo> wins = mode == Mode.Region ? Capture.Windows() : null;
                WinInfo fg = mode == Mode.Region ? (fromMenu ? Capture.TopAppWindow() : Capture.ForegroundAppWindow()) : null;

                // En åben besked må ikke komme med på billedet.
                if (Toast.CloseCurrent()) { Application.DoEvents(); Thread.Sleep(120); }

                frozen = Capture.Freeze(St.AlwaysDx);
                if (Capture.BlackScreens > 0)
                    Toast.ShowText(L.T("toast.blackScreen"), true);

                if (mode == Mode.Region)
                {
                    using (RegionOverlay ov = new RegionOverlay(frozen, vs, wins, fg))
                    {
                        if (ov.ShowDialog() != DialogResult.OK) return;
                        target = ov.Selection;
                        title = ov.SelectedTitle;
                        if (ov.ResultImage != null)
                        {
                            // Der blev scrollet: billedet er allerede syet sammen.
                            Bitmap stitched = ov.ResultImage;
                            ov.ResultImage = null;
                            Deliver(stitched, title);
                            return;
                        }
                    }
                }

                Bitmap crop = Capture.Crop(frozen, vs, target);
                if (crop == null) { Toast.ShowText(L.T("toast.emptyArea"), true); return; }
                Deliver(crop, title);
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Optagelse", ex);
                Toast.ShowText(L.T("toast.captureFailed", ex.Message), true);
            }
            finally
            {
                if (frozen != null) frozen.Dispose();
                busy = false;
                Memory.Trim();
            }
        }

        void Deliver(Bitmap crop, string title)
        {
            Quality q = St.Q;
            Bitmap img = Capture.Scale(crop, q.Scale);
            if (!ReferenceEquals(img, crop)) crop.Dispose();

            string name = L.T("file.prefix") + " " + Output.Stamp(DateTime.Now);
            string t = Output.Clean(title);
            if (t.Length > 40) t = t.Substring(0, 40).Trim();
            if (t.Length > 0) name += " " + t;
            Shot shot = new Shot(img, name);

            if (St.AfterCapture != "clipboard")
            {
                try
                {
                    if (St.AfterCapture == "copy") Output.Copy(img);
                    else shot.SavedPath = Output.Save(shot, St, null, q.Png);
                }
                catch { img.Dispose(); throw; }
            }

            if (St.AfterCapture == "copy")
            {
                Toast.ShowShot(shot, L.T("toast.copied"), delegate { OpenPreview(shot); }, true);
            }
            else if (St.AfterCapture == "save")
            {
                string path = shot.SavedPath;
                Toast.ShowShot(shot, L.T("toast.saved", Path.GetFileName(path)), delegate { Output.OpenFolder(St.Folder, path); }, false);
            }
            else
            {
                OpenPreview(shot);
            }
        }

        public void OpenPreview(Shot shot)
        {
            PreviewForm f = new PreviewForm(shot, this);
            f.Show();
            f.BringToFrontNow();
        }

        public void OpenSettings(bool firstRun)
        {
            if (settingsForm != null && !settingsForm.IsDisposed)
            {
                settingsForm.BringToFrontNow();
                return;
            }
            UnregisterHotkeys(); // så tasterne kan vælges i felterne
            settingsForm = new SettingsForm(this, firstRun);
            settingsForm.FormClosed += delegate
            {
                settingsForm = null;
                RegisterHotkeys();
            };
            settingsForm.Show();
            settingsForm.BringToFrontNow();
        }

        // Kaldes fra indstillingsvinduet. Returnerer genveje, der ikke kunne bruges.
        public List<string> Apply(Settings s, bool startup)
        {
            Settings old = St;
            St = s;
            List<string> failed = RegisterHotkeys();
            UnregisterHotkeys(); // vinduet er stadig åbent; registreres igen når det lukkes
            if (failed.Count > 0) { St = old; return failed; }
            St.Save();
            SetStartup(startup);
            return failed;
        }

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "ImageShot";

        public static bool GetStartup()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    object v = k == null ? null : k.GetValue(RunName);
                    return v != null && v.ToString().Trim('"').Equals(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }

        public static void SetStartup(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                    else if (k.GetValue(RunName) != null) k.DeleteValue(RunName);
                }
            }
            catch { }
        }

        public void Dispose()
        {
            if (hk != null) { UnregisterHotkeys(); hk.Dispose(); }
            if (keyHook != null) keyHook.Dispose();
            if (invoker != null) invoker.Dispose();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
        }
    }
}
