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
    // Genvejstaster
    // ------------------------------------------------------------------
    static class Hk
    {
        public static string ToText(Keys k)
        {
            if ((k & Keys.KeyCode) == Keys.None) return L.T("hk.none");
            StringBuilder b = new StringBuilder();
            if ((k & Keys.Control) != 0) b.Append("Ctrl+");
            if ((k & Keys.Alt) != 0) b.Append("Alt+");
            if ((k & Keys.Shift) != 0) b.Append("Shift+");
            b.Append(KeyName(k & Keys.KeyCode));
            return b.ToString();
        }

        public static string KeyName(Keys key)
        {
            if (key == Keys.PrintScreen) return "PrtScn";
            if (key >= Keys.D0 && key <= Keys.D9) return ((int)(key - Keys.D0)).ToString();
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return "Num " + (int)(key - Keys.NumPad0);
            if (key == Keys.Pause) return "Pause";
            if (key == Keys.Scroll) return "ScrollLock";
            if (key == Keys.Insert) return "Insert";
            if (key == Keys.Home) return "Home";
            if (key == Keys.End) return "End";
            if (key == Keys.PageUp) return "PageUp";
            if (key == Keys.Next) return "PageDown";
            return key.ToString();
        }

        public static uint Mods(Keys k)
        {
            uint m = 0x4000; // MOD_NOREPEAT
            if ((k & Keys.Alt) != 0) m |= 1;
            if ((k & Keys.Control) != 0) m |= 2;
            if ((k & Keys.Shift) != 0) m |= 4;
            return m;
        }

        public static bool AllowedWithoutModifier(Keys key)
        {
            return key == Keys.PrintScreen || key == Keys.Pause || key == Keys.Scroll ||
                   (key >= Keys.F1 && key <= Keys.F24);
        }
    }

    class HotkeyWindow : NativeWindow, IDisposable
    {
        readonly App app;
        public HotkeyWindow(App app)
        {
            this.app = app;
            CreateParams cp = new CreateParams();
            cp.Caption = "ImageShot.Hotkeys";
            CreateHandle(cp);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312) // WM_HOTKEY
            {
                app.OnHotkey(m.WParam.ToInt32());
                return;
            }
            if (m.Msg == Program.ShowSettingsMessage && Program.ShowSettingsMessage != 0)
            {
                app.OpenSettings(false);
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose() { DestroyHandle(); }
    }
}
