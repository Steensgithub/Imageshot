using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ImageShot
{
    // "Om ImageShot": version, kort beskrivelse, hvor data ligger, søg efter opdateringer.
    class AboutForm : Form
    {
        public static string VersionText
        {
            get
            {
                Version v = Assembly.GetExecutingAssembly().GetName().Version;
                return v.Major + "." + v.Minor + "." + v.Build;
            }
        }

        public AboutForm(App app)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9.5f);
            Text = L.T("about.title");
            Icon = Program.AppIcon;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            TableLayoutPanel t = new TableLayoutPanel();
            t.AutoSize = true;
            t.ColumnCount = 2;
            t.Padding = new Padding(20, 18, 20, 14);

            PictureBox logo = new PictureBox();
            logo.Size = new Size(64, 64);
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            try { logo.Image = new Icon(Program.AppIcon, 64, 64).ToBitmap(); } catch { }
            logo.Margin = new Padding(0, 0, 16, 0);
            t.Controls.Add(logo, 0, 0);
            t.SetRowSpan(logo, 3);

            Label name = new Label();
            name.Text = "ImageShot";
            name.Font = new Font("Segoe UI Semibold", 16f);
            name.AutoSize = true;
            t.Controls.Add(name, 1, 0);

            Label ver = new Label();
            ver.Text = L.T("about.version", VersionText) + "   ·   Windows " + Native.WindowsVersion();
            ver.AutoSize = true;
            ver.ForeColor = SystemColors.GrayText;
            t.Controls.Add(ver, 1, 1);

            Label tag = new Label();
            tag.Text = L.T("about.tagline");
            tag.AutoSize = true;
            tag.MaximumSize = new Size(380, 0);
            tag.Margin = new Padding(3, 8, 3, 8);
            t.Controls.Add(tag, 1, 2);

            Label data = new Label();
            data.Text = L.T("about.data", AppPaths.DataDir);
            data.AutoSize = true;
            data.MaximumSize = new Size(380, 0);
            data.ForeColor = SystemColors.GrayText;
            t.Controls.Add(data, 1, 3);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Fill;
            buttons.Margin = new Padding(0, 14, 0, 0);
            Button close = new Button { Text = L.T("preview.close"), AutoSize = true };
            close.Click += delegate { Close(); };
            Button logs = new Button { Text = L.T("about.openLogs"), AutoSize = true };
            logs.Click += delegate
            {
                try { Directory.CreateDirectory(AppPaths.LogDir); Process.Start("explorer.exe", "\"" + AppPaths.LogDir + "\""); }
                catch (Exception ex) { ErrorLog.Write("Åbn logmappe", ex); }
            };
            buttons.Controls.Add(close);
            buttons.Controls.Add(logs);
            t.Controls.Add(buttons, 0, 4);
            t.SetColumnSpan(buttons, 2);

            Controls.Add(t);
            AcceptButton = close;
            CancelButton = close;
            Theme.Apply(this);
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
