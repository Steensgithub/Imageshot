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
        readonly TextBox folder = new TextBox(), template = new TextBox();
        readonly Label example = new Label();
        readonly ComboBox language = new ComboBox(), theme = new ComboBox();

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

            // To kolonner, så vinduet også passer på en bærbar skærm.
            TableLayoutPanel outer = new TableLayoutPanel();
            outer.AutoSize = true;
            outer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            outer.ColumnCount = 2;
            outer.Padding = new Padding(16, 8, 16, 12);
            TableLayoutPanel t = Column(), r = Column();
            r.Margin = new Padding(28, 0, 0, 0);

            if (firstRun)
            {
                Label intro = new Label();
                intro.AutoSize = true;
                intro.MaximumSize = new Size(900, 0);
                intro.Margin = new Padding(0, 8, 0, 4);
                intro.Text = L.T("set.intro");
                outer.Controls.Add(intro, 0, 0);
                outer.SetColumnSpan(intro, 2);
            }
            outer.Controls.Add(t, 0, 1);
            outer.Controls.Add(r, 1, 1);

            // ---- venstre: genveje, billede, efter optagelse
            Span(t, FormHelp.Heading(L.T("set.hotkeys")));
            Row(t, L.T("set.hkRegion") + ":", hkRegion);
            Row(t, L.T("mode.window") + ":", hkWindow);
            Row(t, L.T("mode.screen") + ":", hkScreen);
            Row(t, L.T("mode.all") + ":", hkAll);
            Label hkNote = FormHelp.Note(L.T("set.hkNote"));
            hkNote.MaximumSize = new Size(420, 0);
            Span(t, hkNote);
            hkRegion.Value = s.HkRegion; hkWindow.Value = s.HkWindow; hkScreen.Value = s.HkScreen; hkAll.Value = s.HkAll;

            Span(t, FormHelp.Heading(L.T("set.image")));
            res.Minimum = 0; res.Maximum = 100; res.TickFrequency = 10; res.SmallChange = 5; res.LargeChange = 10;
            res.Width = 260;
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
            autoClose.MaximumSize = new Size(420, 0);
            autoClose.Checked = s.AutoClose;
            autoClose.Margin = new Padding(3, 8, 3, 4);
            Span(t, autoClose);

            // ---- højre: gem, udseende, andet
            Span(r, FormHelp.Heading(L.T("set.folder")));
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
            Span(r, fp);

            Label fnHead = new Label { Text = L.T("set.fileName") + ":", AutoSize = true, Margin = new Padding(3, 10, 3, 2) };
            Span(r, fnHead);
            template.Width = 300;
            template.Text = s.FileTemplate;
            template.TextChanged += delegate { UpdateExample(); };
            Span(r, template);
            example.AutoSize = true;
            example.ForeColor = SystemColors.GrayText;
            example.MaximumSize = new Size(400, 0);
            Span(r, example);
            Label fnNote = FormHelp.Note(L.T("set.fileNameNote"));
            fnNote.MaximumSize = new Size(400, 0);
            Span(r, fnNote);

            Span(r, FormHelp.Heading(L.T("set.theme")));
            language.DropDownStyle = ComboBoxStyle.DropDownList;
            language.Width = 200;
            language.Items.Add(L.T("set.langAuto"));
            language.Items.AddRange(L.LanguageNames);
            int li = Array.IndexOf(L.Languages, s.Language);
            language.SelectedIndex = li < 0 ? 0 : li + 1;
            Row(r, L.T("set.language") + ":", language);
            theme.DropDownStyle = ComboBoxStyle.DropDownList;
            theme.Width = 200;
            theme.Items.AddRange(new object[] { L.T("set.themeAuto"), L.T("set.themeLight"), L.T("set.themeDark") });
            theme.SelectedIndex = s.Theme == "light" ? 1 : s.Theme == "dark" ? 2 : 0;
            Row(r, L.T("set.theme") + ":", theme);

            Span(r, FormHelp.Heading(L.T("set.other")));
            startup.Text = L.T("set.startup");
            startup.AutoSize = true;
            startup.Checked = App.GetStartup();
            Span(r, startup);
            alwaysDx.Text = L.T("set.alwaysDx");
            alwaysDx.AutoSize = true;
            alwaysDx.Checked = s.AlwaysDx;
            Span(r, alwaysDx);
            Label dxNote = FormHelp.Note(L.T("set.alwaysDxNote"));
            dxNote.MaximumSize = new Size(400, 0);
            Span(r, dxNote);

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
            outer.Controls.Add(bp, 0, 2);
            outer.SetColumnSpan(bp, 2);

            Controls.Add(outer);
            AcceptButton = ok;
            CancelButton = cancel;
            UpdateRes();
            UpdateExample();
            Theme.Apply(this);
            ResumeLayout(false);
            PerformLayout();
        }

        static TableLayoutPanel Column()
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.ColumnCount = 2;
            t.Margin = Padding.Empty;
            t.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            return t;
        }

        void UpdateExample()
        {
            string n = Output.BuildName(template.Text, DateTime.Now, "Dokument1 - Word", "WINWORD").Replace("{counter}", "001");
            example.Text = "→ " + n + (format.SelectedIndex == 1 ? ".jpg" : ".png");
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
            s.FileTemplate = template.Text.Trim().Length == 0 ? Output.DefaultTemplate : template.Text.Trim();
            s.Language = language.SelectedIndex <= 0 ? "auto" : L.Languages[language.SelectedIndex - 1];
            s.Theme = theme.SelectedIndex == 1 ? "light" : theme.SelectedIndex == 2 ? "dark" : "auto";

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
