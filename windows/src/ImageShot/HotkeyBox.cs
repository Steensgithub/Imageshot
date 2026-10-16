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
    // Indstillinger
    // ------------------------------------------------------------------
    class HotkeyBox : TextBox
    {
        Keys value;
        public Keys Value
        {
            get { return value; }
            set { this.value = value; Text = Hk.ToText(value); }
        }

        public HotkeyBox()
        {
            ReadOnly = true;
            BackColor = SystemColors.Window;
            ShortcutsEnabled = false;
            Width = 200;
            Cursor = Cursors.Hand;
        }

        static bool IsModifier(Keys k)
        {
            return k == Keys.ShiftKey || k == Keys.ControlKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin ||
                   k == Keys.LShiftKey || k == Keys.RShiftKey || k == Keys.LControlKey || k == Keys.RControlKey ||
                   k == Keys.LMenu || k == Keys.RMenu;
        }

        bool Take(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            Keys mods = keyData & Keys.Modifiers;
            if ((key == Keys.Tab || key == Keys.Escape || key == Keys.Enter) && mods == Keys.None) return false;
            if ((key == Keys.Delete || key == Keys.Back) && mods == Keys.None)
            {
                Value = Keys.None;
                return true;
            }
            if (IsModifier(key))
            {
                string t = "";
                if ((mods & Keys.Control) != 0) t += "Ctrl+";
                if ((mods & Keys.Alt) != 0) t += "Alt+";
                if ((mods & Keys.Shift) != 0) t += "Shift+";
                Text = t.Length > 0 ? t + "…" : Hk.ToText(value);
                return true;
            }
            if (mods == Keys.None && !Hk.AllowedWithoutModifier(key))
            {
                Text = L.T("hk.needModifier");
                return true;
            }
            Value = keyData;
            return true;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (Take(keyData)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            // PrtScn sender kun "tast sluppet" til programmer.
            if (e.KeyCode == Keys.PrintScreen) Value = e.KeyData;
            else if (e.Modifiers == Keys.None && (Text.EndsWith("…") || Text == L.T("hk.needModifier"))) Text = Hk.ToText(value);
            e.Handled = true;
        }

        protected override void OnLeave(EventArgs e)
        {
            Text = Hk.ToText(value);
            base.OnLeave(e);
        }
    }
}
