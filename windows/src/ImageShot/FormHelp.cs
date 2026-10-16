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
    // Fælles vindueshjælp
    // ------------------------------------------------------------------
    static class FormHelp
    {
        public static void BringToFrontNow(this Form f)
        {
            if (f.WindowState == FormWindowState.Minimized) f.WindowState = FormWindowState.Normal;
            f.TopMost = true;
            f.TopMost = false;
            f.Activate();
            Native.SetForegroundWindow(f.Handle);
        }

        public static Label Heading(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Font = new Font("Segoe UI Semibold", 10.5f);
            l.Margin = new Padding(0, 14, 0, 4);
            return l;
        }

        public static Label Note(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = SystemColors.GrayText;
            l.Margin = new Padding(0, 0, 0, 6);
            return l;
        }
    }
}
