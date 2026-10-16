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
    // Ramme om markeringen, mens der scrolles. Kan ikke klikkes på og
    // tegnes uden for selve markeringen, så den ikke kommer med på billedet.
    // ------------------------------------------------------------------
    class FrameWindow : Form
    {
        readonly Rectangle area;
        readonly int selLeft, selRight;
        Rectangle sel;
        string text = "";
        bool excluded;
        readonly Font font = new Font("Segoe UI", 10f);
        readonly Color key = Color.FromArgb(255, 0, 255);
        readonly Color accent = Color.FromArgb(0, 120, 215);

        public FrameWindow(Rectangle band, int selLeft, int selRight)
        {
            area = Rectangle.Inflate(band, 6, 6);
            this.selLeft = selLeft;
            this.selRight = selRight;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = key; // WS_EX_TOPMOST sættes i CreateParams (TopMost = true kan stjæle fokus)
            TransparencyKey = key;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Bounds = area;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x20 | 0x80 | 0x08 | 0x08000000; // TRANSPARENT | TOOLWINDOW | TOPMOST | NOACTIVATE
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Windows 10 (2004+): hold rammen ude af skærmbilleder.
            try { excluded = Native.SetWindowDisplayAffinity(Handle, 0x11); } catch { excluded = false; }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try { Native.SetWindowPos(Handle, new IntPtr(-1), area.X, area.Y, area.Width, area.Height, 0x0010 | 0x0040); } catch { }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x02E0) { m.Result = IntPtr.Zero; return; } // WM_DPICHANGED
            base.WndProc(ref m);
        }

        public void SetSelection(Rectangle r, string label)
        {
            sel = r;
            text = label;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(key);
            if (sel.IsEmpty) return;
            // Til klientkoordinater; stregerne ligger lige uden for markeringen.
            int x0 = selLeft - area.X, x1 = selRight - area.X;
            int y0 = Math.Max(0, sel.Top - area.Y), y1 = Math.Min(ClientSize.Height, sel.Bottom - area.Y);
            using (SolidBrush b = new SolidBrush(accent))
            {
                g.FillRectangle(b, x0 - 3, y0 - 3, 3, y1 - y0 + 6);         // venstre
                g.FillRectangle(b, x1, y0 - 3, 3, y1 - y0 + 6);             // højre
                // Top og bund ligger inde i det fotograferede område – kun når rammen holdes ude af billedet.
                if (excluded && sel.Top - area.Y >= 3) g.FillRectangle(b, x0 - 3, y0 - 3, x1 - x0 + 6, 3);
                if (excluded && sel.Bottom - area.Y <= ClientSize.Height - 3) g.FillRectangle(b, x0 - 3, y1, x1 - x0 + 6, 3);
            }
            if (excluded && text.Length > 0)
            {
                // Vejledning nederst – kun når rammen ikke kommer med på billedet.
                Size t = TextRenderer.MeasureText(text, font);
                Rectangle lr = new Rectangle((ClientSize.Width - t.Width - 24) / 2, ClientSize.Height - t.Height - 34, t.Width + 24, t.Height + 12);
                using (SolidBrush pill = new SolidBrush(Color.FromArgb(32, 33, 36)))
                    g.FillRectangle(pill, lr);
                TextRenderer.DrawText(g, text, font, lr, Color.White, Color.FromArgb(32, 33, 36),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) font.Dispose();
            base.Dispose(disposing);
        }
    }
}
