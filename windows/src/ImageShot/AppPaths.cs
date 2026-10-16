using System;
using System.IO;
using Microsoft.Win32;

namespace ImageShot
{
    // Hvor ImageShot gemmer sine filer.
    static class AppPaths
    {
        public static string DataDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ImageShot"); }
        }

        public static string LogDir { get { return Path.Combine(DataDir, "logs"); } }
        public static string HistoryDir { get { return Path.Combine(DataDir, "history"); } }
        public static string SettingsFile { get { return Path.Combine(DataDir, "settings.ini"); } }

        // Den tidligere version hed "Skærmbillede" og gemte i %APPDATA%\Skaermbillede.
        public static string OldSettingsFile
        {
            get { return Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Skaermbillede"), "indstillinger.ini"); }
        }

        public static void MigrateFromOldVersion()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                if (!File.Exists(SettingsFile) && File.Exists(OldSettingsFile))
                {
                    File.Copy(OldSettingsFile, SettingsFile);
                    ErrorLog.Info("Indstillinger overført fra Skærmbillede");
                }
                // Den gamle version startede selv med Windows – fjern den post.
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (k != null && k.GetValue("Skaermbillede") != null)
                    {
                        k.DeleteValue("Skaermbillede");
                        ErrorLog.Info("Gammel autostart (Skærmbillede) fjernet");
                    }
                }
            }
            catch (Exception ex) { ErrorLog.Write("Overførsel fra gammel version", ex); }
        }
    }
}
