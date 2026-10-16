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
    // Fejlfinding: beskriver sidste optagelse i %APPDATA%\Skaermbillede\log.txt
    // ------------------------------------------------------------------
    static class Log
    {
        static StringBuilder b = new StringBuilder();
        public static string FilePath
        {
            get { return Path.Combine(AppPaths.LogDir, "last-capture.txt"); }
        }
        public static void Begin()
        {
            b = new StringBuilder();
            b.AppendLine("ImageShot " + Assembly.GetExecutingAssembly().GetName().Version + "  –  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            b.AppendLine("Windows " + Environment.OSVersion.Version + (Environment.Is64BitProcess ? " (64-bit)" : " (32-bit)"));
        }
        public static void Add(string line) { b.AppendLine(line); }
        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, b.ToString(), new UTF8Encoding(true));
            }
            catch { }
        }
    }
}
