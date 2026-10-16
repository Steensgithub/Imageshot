using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageShot
{
    // Løbende log over hændelser og fejl: %APPDATA%\ImageShot\logs\imageshot-ÅÅÅÅMMDD.log (gemmes 14 dage).
    static class ErrorLog
    {
        static readonly object gate = new object();
        static bool installed;

        public static string TodayFile
        {
            get { return Path.Combine(AppPaths.LogDir, "imageshot-" + DateTime.Now.ToString("yyyyMMdd") + ".log"); }
        }

        public static void Install()
        {
            if (installed) return;
            installed = true;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e)
            {
                Write("Ubehandlet fejl (UI)", e.Exception);
                try { Toast.ShowText(L.T("error.generic", e.Exception.Message), true); } catch { }
            };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            {
                Write("Ubehandlet fejl (afslutter)", e.ExceptionObject as Exception);
            };
            TaskScheduler.UnobservedTaskException += delegate (object s, UnobservedTaskExceptionEventArgs e)
            {
                Write("Ubehandlet fejl (baggrund)", e.Exception);
                e.SetObserved();
            };
            Cleanup();
            Info("Start " + Assembly.GetExecutingAssembly().GetName().Version + " på Windows " + Native.WindowsVersion());
        }

        public static void Info(string message) { Append("INFO  " + message); }

        public static void Write(string context, Exception ex)
        {
            Append("FEJL  " + context + Environment.NewLine + (ex == null ? "(ukendt)" : ex.ToString()));
        }

        static void Append(string text)
        {
            try
            {
                lock (gate)
                {
                    Directory.CreateDirectory(AppPaths.LogDir);
                    File.AppendAllText(TodayFile, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + text + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch { }
        }

        static void Cleanup()
        {
            try
            {
                if (!Directory.Exists(AppPaths.LogDir)) return;
                foreach (string f in Directory.GetFiles(AppPaths.LogDir, "imageshot-*.log"))
                    if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-14)) File.Delete(f);
            }
            catch { }
        }
    }
}
