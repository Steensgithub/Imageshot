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
    // Lille besked nederst til højre (stjæler ikke fokus fra spil o.l.)
    // ------------------------------------------------------------------
    class Toast : Form
    {
        static Toast current;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly EventHandler onClick;
        readonly Shot shot;
        readonly bool clickTakesImage;
        PictureBox thumb;
        bool clicked;

        public static void ShowText(string text, bool error) { ShowText(text, error, null); }

        public static void ShowText(string text, bool error, EventHandler click)
        {
            Show(new Toast(null, text, error, click, false));
        }

        public static void ShowShot(Shot shot, string text, EventHandler click, bool clickTakesImage)
        {
            Show(new Toast(shot, text + "\n" + L.T("toast.clickToOpen"), false, click, clickTakesImage));
        }

        public static bool CloseCurrent()
        {
            if (current == null || current.IsDisposed) return false;
            current.Close();
            return true;
        }

        static void Show(Toast t)
        {
            if (current != null && !current.IsDisposed) current.Close();
            current = t;
            t.Show();
        }

        Toast(Shot shot, string text, bool error, EventHandler click, bool clickTakesImage)
        {
            this.shot = shot;
            this.clickTakesImage = clickTakesImage;
            onClick = click;
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96f, 96f);
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(32, 33, 36);
            ForeColor = Color.White;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(error ? 4 : 0, 0, 0, 0);
            if (error) BackColor = Color.FromArgb(196, 43, 28);
            Cursor = click != null ? Cursors.Hand : Cursors.Default;

            FlowLayoutPanel p = new FlowLayoutPanel();
            p.AutoSize = true;
            p.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            p.Padding = new Padding(12);
            p.BackColor = Color.FromArgb(32, 33, 36);
            p.WrapContents = false;
            p.Margin = Padding.Empty;

            if (shot != null)
            {
                PictureBox pb = new PictureBox();
                pb.Size = new Size(112, 70);
                pb.SizeMode = PictureBoxSizeMode.Zoom;
                pb.Image = shot.Image;
                pb.Margin = new Padding(0, 0, 12, 0);
                p.Controls.Add(pb);
                thumb = pb;
            }
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.MaximumSize = new Size(320, 0);
            l.ForeColor = Color.White;
            l.Margin = new Padding(0, 4, 0, 0);
            p.Controls.Add(l);
            Controls.Add(p);

            foreach (Control c in new Control[] { this, p, l })
                c.Click += delegate { Clicked(); };
            foreach (Control c in p.Controls) { c.Click += delegate { Clicked(); }; c.Cursor = Cursor; }

            timer.Interval = error ? 7000 : (shot != null ? 4000 : 2500);
            timer.Tick += delegate { Close(); };
            ResumeLayout(false);
            PerformLayout();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x08 | 0x08000000; // TOOLWINDOW | TOPMOST | NOACTIVATE
                return cp;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            PerformLayout();
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(wa.Right - Width - 16, wa.Bottom - Height - 16);
            timer.Start();
        }

        void Clicked()
        {
            if (clicked) return;
            clicked = true;
            if (onClick != null) onClick(this, EventArgs.Empty);
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            timer.Dispose();
            if (current == this) current = null;
            if (thumb != null) thumb.Image = null;
            // Billedet frigives her, medmindre klikket gav det videre til udklipsholdervinduet.
            if (shot != null && !(clicked && clickTakesImage)) shot.Image.Dispose();
            base.OnFormClosed(e);
        }
    }
}
