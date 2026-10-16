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
    class SettingsForm : Form
    {
        readonly App app;
        readonly HotkeyBox hkRegion = new HotkeyBox(), hkWindow = new HotkeyBox(), hkScreen = new HotkeyBox(), hkAll = new HotkeyBox();
        readonly TrackBar res = new TrackBar();
        readonly Label resLabel = new Label();
        readonly ComboBox format = new ComboBox();
        readonly RadioButton rbShow = new RadioButton(), rbCopy = new RadioButton(), rbSave = new RadioButton();
        readonly CheckBox autoClose = new CheckBox(), startup = new CheckBox(), alwaysDx = new CheckBox();
        readonly TextBox folder = new TextBox();

        public SettingsForm(App app, bool firstRun)
        {
            this.app = app;
            Settings s = app.St;
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96f, 96f);
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = L.T("set.title");
            Icon = Program.AppIcon;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            TableLayoutPanel t = new TableLayoutPanel();
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.ColumnCount = 2;
            t.Padding = new Padding(16, 8, 16, 12);
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            if (firstRun)
            {
                Label intro = new Label();
                intro.AutoSize = true;
                intro.MaximumSize = new Size(520, 0);
                intro.Margin = new Padding(0, 8, 0, 4);
                intro.Text = L.T("set.intro");
                Span(t, intro);
            }

            Span(t, FormHelp.Heading(L.T("set.hotkeys")));
            Row(t, L.T("set.hkRegion") + ":", hkRegion);
            Row(t, L.T("mode.window") + ":", hkWindow);
            Row(t, L.T("mode.screen") + ":", hkScreen);
            Row(t, L.T("mode.all") + ":", hkAll);
            Span(t, FormHelp.Note(L.T("set.hkNote")));
            hkRegion.Value = s.HkRegion; hkWindow.Value = s.HkWindow; hkScreen.Value = s.HkScreen; hkAll.Value = s.HkAll;

            Span(t, FormHelp.Heading(L.T("set.image")));
            res.Minimum = 0; res.Maximum = 100; res.TickFrequency = 10; res.SmallChange = 5; res.LargeChange = 10;
            res.Width = 300;
            res.Value = s.Resolution;
            res.ValueChanged += delegate { UpdateRes(); };
            Row(t, L.T("set.resolution") + ":", res);
            resLabel.AutoSize = true;
            resLabel.ForeColor = SystemColors.GrayText;
            resLabel.Margin = new Padding(3, 0, 3, 6);
            t.Controls.Add(new Label(), 0, t.RowCount);
            t.Controls.Add(resLabel, 1, t.RowCount);
            t.RowCount++;
            format.DropDownStyle = ComboBoxStyle.DropDownList;
            format.Items.AddRange(new object[] { L.T("set.png"), L.T("set.jpeg") });
            format.SelectedIndex = s.Format == "jpeg" ? 1 : 0;
            format.Width = 220;
            format.SelectedIndexChanged += delegate { UpdateRes(); };
            Row(t, L.T("set.format") + ":", format);

            Span(t, FormHelp.Heading(L.T("set.after")));
            rbShow.Text = L.T("set.afterShow");
            rbCopy.Text = L.T("set.afterCopy");
            rbSave.Text = L.T("set.afterSave");
            foreach (RadioButton rb in new RadioButton[] { rbShow, rbCopy, rbSave }) { rb.AutoSize = true; rb.Margin = new Padding(3, 2, 3, 2); Span(t, rb); }
            rbShow.Checked = s.AfterCapture == "clipboard";
            rbCopy.Checked = s.AfterCapture == "copy";
            rbSave.Checked = s.AfterCapture == "save";
            autoClose.Text = L.T("set.autoClose");
            autoClose.AutoSize = true;
            autoClose.Checked = s.AutoClose;
            autoClose.Margin = new Padding(3, 8, 3, 4);
            Span(t, autoClose);

            FlowLayoutPanel fp = new FlowLayoutPanel();
            fp.AutoSize = true;
            fp.WrapContents = false;
            fp.Margin = Padding.Empty;
            folder.Width = 300;
            folder.Text = s.Folder;
            Button browse = new Button();
            browse.Text = L.T("set.browse");
            browse.AutoSize = true;
            browse.Click += delegate
            {
                using (FolderBrowserDialog d = new FolderBrowserDialog())
                {
                    d.Description = L.T("set.browseTitle");
                    try { Directory.CreateDirectory(folder.Text); d.SelectedPath = folder.Text; } catch { }
                    if (d.ShowDialog(this) == DialogResult.OK) folder.Text = d.SelectedPath;
                }
            };
            fp.Controls.Add(folder);
            fp.Controls.Add(browse);
            Row(t, L.T("set.folder") + ":", fp);

            Span(t, FormHelp.Heading(L.T("set.other")));
            startup.Text = L.T("set.startup");
            startup.AutoSize = true;
            startup.Checked = App.GetStartup();
            Span(t, startup);
            alwaysDx.Text = L.T("set.alwaysDx");
            alwaysDx.AutoSize = true;
            alwaysDx.Checked = s.AlwaysDx;
            Span(t, alwaysDx);
            Span(t, FormHelp.Note(L.T("set.alwaysDxNote")));

            FlowLayoutPanel bp = new FlowLayoutPanel();
            bp.AutoSize = true;
            bp.FlowDirection = FlowDirection.RightToLeft;
            bp.Dock = DockStyle.Fill;
            bp.Margin = new Padding(0, 14, 0, 0);
            Button cancel = new Button();
            cancel.Text = L.T("set.cancel");
            cancel.AutoSize = true;
            cancel.Click += delegate { Close(); };
            Button ok = new Button();
            ok.Text = L.T("set.save");
            ok.AutoSize = true;
            ok.Click += delegate { SaveAndClose(); };
            bp.Controls.Add(cancel);
            bp.Controls.Add(ok);
            Span(t, bp);

            Controls.Add(t);
            AcceptButton = ok;
            CancelButton = cancel;
            UpdateRes();
            ResumeLayout(false);
            PerformLayout();
        }

        static void Row(TableLayoutPanel t, string label, Control c)
        {
            Label l = new Label();
            l.Text = label;
            l.AutoSize = true;
            l.Anchor = AnchorStyles.Left;
            l.Margin = new Padding(3, 6, 12, 3);
            c.Anchor = AnchorStyles.Left;
            t.Controls.Add(l, 0, t.RowCount);
            t.Controls.Add(c, 1, t.RowCount);
            t.RowCount++;
        }

        static void Span(TableLayoutPanel t, Control c)
        {
            t.Controls.Add(c, 0, t.RowCount);
            t.SetColumnSpan(c, 2);
            t.RowCount++;
        }

        void UpdateRes()
        {
            Settings tmp = app.St.Clone();
            tmp.Resolution = res.Value;
            tmp.Format = format.SelectedIndex == 1 ? "jpeg" : "png";
            Quality q = tmp.Q;
            resLabel.Text = res.Value + " – " + q.Label + "  ·  " + q.Detail;
        }

        void SaveAndClose()
        {
            Keys[] keys = { hkRegion.Value, hkWindow.Value, hkScreen.Value, hkAll.Value };
            for (int i = 0; i < keys.Length; i++)
                for (int j = i + 1; j < keys.Length; j++)
                    if ((keys[i] & Keys.KeyCode) != Keys.None && keys[i] == keys[j])
                    {
                        MessageBox.Show(this, L.T("set.dupHotkey", Hk.ToText(keys[i])), Text,
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

            string dir = folder.Text.Trim();
            try
            {
                if (dir.Length == 0 || !Path.IsPathRooted(dir)) throw new Exception(L.T("set.fullPath"));
                Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L.T("set.badFolder", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Settings s = app.St.Clone();
            s.HkRegion = keys[0]; s.HkWindow = keys[1]; s.HkScreen = keys[2]; s.HkAll = keys[3];
            s.Resolution = res.Value;
            s.Format = format.SelectedIndex == 1 ? "jpeg" : "png";
            s.AfterCapture = rbCopy.Checked ? "copy" : rbSave.Checked ? "save" : "clipboard";
            s.AutoClose = autoClose.Checked;
            s.Folder = dir;
            s.AlwaysDx = alwaysDx.Checked;

            List<string> failed = app.Apply(s, startup.Checked);
            if (failed.Count > 0)
            {
                MessageBox.Show(this,
                    L.T("set.hotkeysTaken", string.Join("\n", failed.ToArray())),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Close();
        }
    }
}
