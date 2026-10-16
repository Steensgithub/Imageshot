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
    // Start
    // ------------------------------------------------------------------
    static class Program
    {
        public const string AppName = "ImageShot";
        public static int ShowSettingsMessage;
        static Mutex mutex;

        [STAThread]
        static int Main(string[] args)
        {
            Native.MakeDpiAware();
            ErrorLog.Install();

            // Selvtest (bruges af den automatiske bygning på Windows): ImageShot.exe --selftest <mappe>
            if (args.Length >= 1 && args[0] == "--selftest")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                return SelfTest.Run(args.Length >= 2 ? args[1] : Path.Combine(Path.GetTempPath(), "imageshot-selftest"));
            }

            ShowSettingsMessage = Native.RegisterWindowMessage("ImageShot.ShowSettings");

            bool created;
            mutex = new Mutex(true, "Local\\ImageShot.Running", out created);
            if (!created)
            {
                // Kører allerede: bed den kørende kopi om at vise indstillingerne.
                Native.PostMessage(new IntPtr(0xFFFF), ShowSettingsMessage, IntPtr.Zero, IntPtr.Zero);
                return 0;
            }

            AppPaths.MigrateFromOldVersion();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (App app = new App())
            {
                app.Start();
                Application.Run();
            }
            GC.KeepAlive(mutex);
            return 0;
        }

        static Icon icon;
        public static Icon AppIcon
        {
            get
            {
                if (icon == null)
                {
                    try
                    {
                        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
                            icon = new Icon(s, SystemInformation.SmallIconSize);
                    }
                    catch { icon = SystemIcons.Application; }
                }
                return icon;
            }
        }
    }

    enum Mode { Region, Window, Screen, All }
}
