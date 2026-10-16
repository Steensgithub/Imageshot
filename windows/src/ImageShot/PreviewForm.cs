using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace ImageShot
{
    // ------------------------------------------------------------------
    // Editoren: tegn, tilskær, slør – kopiér, gem, fastgør eller hent teksten.
    // ------------------------------------------------------------------
    class PreviewForm : Form
    {
        readonly Shot shot;
        readonly App app;
        readonly Doc doc;
        readonly ImageView view;
        readonly Label status;
        readonly CheckBox auto;
        readonly ToolStrip bar;
        readonly Dictionary<Tool, ToolStripButton> toolButtons = new Dictionary<Tool, ToolStripButton>();
        readonly List<ToolStripButton> colorButtons = new List<ToolStripButton>(), widthButtons = new List<ToolStripButton>();
        ToolStripButton undoBtn, redoBtn, delBtn;
        Button cropApply;
        DateTime lastCrop = DateTime.MinValue;
        int iconSize = 20;

        static readonly Tool[] ToolOrder = { Tool.Select, Tool.Crop, Tool.Arrow, Tool.Rect, Tool.Ellipse, Tool.Highlight, Tool.Text, Tool.Step, Tool.Blur, Tool.Pixelate };
        static readonly string[] ToolKeys = { "tool.select", "preview.crop", "tool.arrow", "tool.rect", "tool.ellipse", "tool.highlight", "tool.text", "tool.step", "tool.blur", "tool.pixelate" };
        static readonly Color[] Palette =
        {
            Color.FromArgb(0xE5, 0x48, 0x4D), Color.FromArgb(0xF7, 0x8C, 0x1E), Color.FromArgb(0xFF, 0xD6, 0x00),
            Color.FromArgb(0x2F, 0xB3, 0x4A), Theme.Brand, Color.FromArgb(0x1B, 0x1F, 0x24), Color.White
        };
        static readonly float[] Widths = { 2f, 4f, 8f };

        // Sidste valg huskes, mens programmet kører.
        static Tool lastTool = Tool.Arrow;
        static int lastColor = 0, lastWidth = 1;

        public PreviewForm(Shot shot, App app)
        {
            this.shot = shot;
            this.app = app;
            doc = new Doc(shot.Image);
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96f, 96f);
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = L.T("preview.title");
            Icon = Program.AppIcon;
            KeyPreview = true;
            StartPosition = FormStartPosition.Manual;
            MinimumSize = new Size(760, 460);
            iconSize = Math.Max(16, (int)Math.Round(20 * DeviceDpi / 96f));

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Padding = new Padding(10, 4, 10, 10);

            bar = new ToolStrip();
            bar.GripStyle = ToolStripGripStyle.Hidden;
            bar.Dock = DockStyle.Fill;
            bar.Padding = new Padding(0, 2, 0, 4);
            bar.ImageScalingSize = new Size(iconSize, iconSize);
            bar.ShowItemToolTips = true;
            BuildBar();
            root.Controls.Add(bar, 0, 0);

            view = new ImageView();
            view.Dock = DockStyle.Fill;
            view.Doc = doc;
            view.Margin = new Padding(0, 0, 0, 8);
            view.SelectionChanged += delegate { UpdateState(); };
            view.DocChanged += delegate { UpdateState(); };
            view.ApplyRequested += delegate { ApplyCrop(); };
            root.Controls.Add(view, 0, 1);

            status = new Label();
            status.AutoSize = true;
            status.Margin = new Padding(0, 0, 0, 8);
            root.Controls.Add(status, 0, 2);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.Dock = DockStyle.Fill;
            buttons.WrapContents = true;
            buttons.Margin = Padding.Empty;

            Button copy = Btn(L.T("preview.copy") + "  (Ctrl+C)", delegate { DoCopy(); });
            cropApply = Btn(L.T("preview.applyCrop") + "  (Enter)", delegate { ApplyCrop(); });
            cropApply.Visible = false;
            Button save = Btn(L.T("preview.save") + "  (Ctrl+S)", delegate { DoSave(null); });
            Button saveAs = Btn(L.T("preview.saveAs"), delegate { DoSaveAs(); });
            Button text = Btn(L.T("preview.copyText"), delegate { DoCopyText(); });
            Button pin = Btn(L.T("preview.pin"), delegate { DoPin(); });
            Button folder = Btn(L.T("preview.openFolder"), delegate { Output.OpenFolder(app.St.Folder, null); });
            Button close = Btn(L.T("preview.close") + "  (Esc)", delegate { Close(); });
            buttons.Controls.AddRange(new Control[] { copy, cropApply, save, saveAs, text, pin, folder, close });

            auto = new CheckBox();
            auto.AutoSize = true;
            auto.Text = L.T("preview.autoClose");
            auto.Checked = app.St.AutoClose;
            auto.Margin = new Padding(12, 7, 0, 0);
            auto.CheckedChanged += delegate { app.St.AutoClose = auto.Checked; app.St.Save(); };
            buttons.Controls.Add(auto);
            root.Controls.Add(buttons, 0, 3);

            Controls.Add(root);
            AcceptButton = copy;
            CancelButton = close;
            Theme.Apply(this);
            ThemeBar();
            SetTool(lastTool);
            SetColor(lastColor);
            SetWidth(lastWidth);
            ResumeLayout(false);
            PerformLayout();
        }

        // Til selvtesten: editorens dokument.
        internal Doc Document { get { return doc; } }
        internal ImageView View { get { return view; } }

        void BuildBar()
        {
            for (int i = 0; i < ToolOrder.Length; i++)
            {
                Tool t = ToolOrder[i];
                ToolStripButton b = new ToolStripButton();
                b.DisplayStyle = ToolStripItemDisplayStyle.Image;
                b.ToolTipText = L.T(ToolKeys[i]) + "  (" + ((i + 1) % 10) + (t == Tool.Crop ? ", T" : "") + ")";
                b.Click += delegate { SetTool(t); };
                b.Margin = new Padding(1, 0, 1, 0);
                toolButtons[t] = b;
                bar.Items.Add(b);
                if (t == Tool.Crop) bar.Items.Add(new ToolStripSeparator());
            }
            bar.Items.Add(new ToolStripSeparator());
            for (int i = 0; i < Palette.Length; i++)
            {
                int ci = i;
                ToolStripButton b = new ToolStripButton { DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = L.T("tool.color") };
                b.Click += delegate { SetColor(ci); };
                colorButtons.Add(b);
                bar.Items.Add(b);
            }
            bar.Items.Add(new ToolStripSeparator());
            for (int i = 0; i < Widths.Length; i++)
            {
                int wi = i;
                ToolStripButton b = new ToolStripButton { DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = new[] { "S", "M", "L" }[i] };
                b.Click += delegate { SetWidth(wi); };
                widthButtons.Add(b);
                bar.Items.Add(b);
            }
            bar.Items.Add(new ToolStripSeparator());
            undoBtn = new ToolStripButton { DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = L.T("preview.undo") + "  (Ctrl+Z)" };
            undoBtn.Click += delegate { Undo(); };
            redoBtn = new ToolStripButton { DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = L.T("preview.redo") + "  (Ctrl+Y)" };
            redoBtn.Click += delegate { Redo(); };
            delBtn = new ToolStripButton { DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = L.T("tool.delete") + "  (Delete)" };
            delBtn.Click += delegate { view.DeleteSelected(); };
            bar.Items.Add(undoBtn);
            bar.Items.Add(redoBtn);
            bar.Items.Add(delBtn);
        }

        void ThemeBar()
        {
            Theme.Apply(bar);
            bar.BackColor = Theme.Back;
            Color fg = Theme.Text;
            foreach (KeyValuePair<Tool, ToolStripButton> kv in toolButtons) { Swap(kv.Value, Icons.Tool(kv.Key, iconSize, fg)); }
            Swap(undoBtn, Icons.Glyph("undo", iconSize, fg));
            Swap(redoBtn, Icons.Glyph("redo", iconSize, fg));
            Swap(delBtn, Icons.Glyph("delete", iconSize, fg));
        }

        static void Swap(ToolStripItem b, Image img)
        {
            Image old = b.Image;
            b.Image = img;
            if (old != null) old.Dispose();
        }

        void SetTool(Tool t)
        {
            if (view.Cropping && t != Tool.Crop) EndCropUi();
            view.Tool = t;
            if (t != Tool.Select && t != Tool.Crop) lastTool = t;
            foreach (KeyValuePair<Tool, ToolStripButton> kv in toolButtons) kv.Value.Checked = kv.Key == t;
            cropApply.Visible = t == Tool.Crop;
            view.Focus();
            UpdateState();
        }

        void SetColor(int i)
        {
            lastColor = i;
            view.PenColor = Palette[i];
            for (int k = 0; k < colorButtons.Count; k++) Swap(colorButtons[k], Icons.Swatch(Palette[k], iconSize, k == i, Theme.Accent));
            Recolor();
        }

        void SetWidth(int i)
        {
            lastWidth = i;
            view.PenWidth = Widths[i];
            for (int k = 0; k < widthButtons.Count; k++) Swap(widthButtons[k], Icons.Line(Widths[k], iconSize, Theme.Text, k == i, Theme.Accent));
        }

        // Skift farve på en valgt figur.
        void Recolor()
        {
            Shape s = view.SelectedShape;
            if (s == null || view.Tool != Tool.Select || s is BlurShape) return;
            doc.Checkpoint();
            s.Color = view.PenColor;
            view.Invalidate();
            UpdateState();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            Size sz = new Size(Math.Max(MinimumSize.Width, wa.Width * 3 / 4), Math.Max(MinimumSize.Height, wa.Height * 3 / 4));
            sz = new Size(Math.Min(sz.Width, wa.Width), Math.Min(sz.Height, wa.Height));
            Bounds = new Rectangle(wa.Left + (wa.Width - sz.Width) / 2, wa.Top + (wa.Height - sz.Height) / 2, sz.Width, sz.Height);
        }

        static Button Btn(string text, EventHandler click)
        {
            Button b = new Button();
            b.Text = text;
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.MinimumSize = new Size(0, 30);
            b.Padding = new Padding(8, 3, 8, 3);
            b.Margin = new Padding(0, 0, 8, 0);
            b.Click += click;
            return b;
        }

        void UpdateState(string message = null)
        {
            undoBtn.Enabled = doc.CanUndo;
            redoBtn.Enabled = doc.CanRedo;
            delBtn.Enabled = view.SelectedShape != null;
            if (message != null) { status.Text = message; return; }
            if (view.Cropping)
            {
                Rectangle r = view.Selection;
                status.Text = r.IsEmpty ? L.T("preview.cropHint") : L.T("preview.cropSel", r.Width, r.Height);
                return;
            }
            string s = L.T("pixels", doc.Image.Width, doc.Image.Height) + "  ·  " + app.St.Q.Detail;
            if (doc.Edited) s += "  ·  " + L.T("preview.edited");
            if (view.Tool == Tool.Text) s += "  ·  " + L.T("tool.textPrompt");
            status.Text = s;
        }

        void EndCropUi()
        {
            cropApply.Visible = false;
        }

        void ApplyCrop()
        {
            Rectangle r = view.Selection;
            if (r.Width >= 3 && r.Height >= 3)
            {
                doc.Crop(r);
                lastCrop = DateTime.Now;
                view.Doc = doc;
            }
            SetTool(Tool.Select);
        }

        void Undo() { view.CommitText(); doc.Undo(); view.Doc = doc; UpdateState(); }
        void Redo() { view.CommitText(); doc.Redo(); view.Doc = doc; UpdateState(); }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (view.Editing) return base.ProcessCmdKey(ref msg, keyData); // tekstfeltet får tasterne
            // Holdes Enter nede efter tilskæring, må gentagelsen ikke også kopiere.
            if (keyData == Keys.Enter && (msg.LParam.ToInt64() & 0x40000000) != 0 &&
                (DateTime.Now - lastCrop).TotalMilliseconds < 1500) return true;
            if (view.Cropping)
            {
                if (keyData == Keys.Enter) { ApplyCrop(); return true; }
                if (keyData == Keys.Escape) { SetTool(Tool.Select); return true; }
            }
            if (keyData == (Keys.Control | Keys.Z)) { Undo(); return true; }
            if (keyData == (Keys.Control | Keys.Y) || keyData == (Keys.Control | Keys.Shift | Keys.Z)) { Redo(); return true; }
            if (keyData == (Keys.Control | Keys.C)) { DoCopy(); return true; }
            if (keyData == (Keys.Control | Keys.S)) { DoSave(null); return true; }
            if (keyData == Keys.Delete && view.DeleteSelected()) return true;
            if (keyData == Keys.T) { SetTool(Tool.Crop); return true; }
            if (keyData >= Keys.D0 && keyData <= Keys.D9)
            {
                int n = keyData - Keys.D0;
                SetTool(ToolOrder[n == 0 ? 9 : n - 1]);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // Billedet med tegningerne – det, der kopieres og gemmes.
        Bitmap Result()
        {
            view.CommitText();
            if (view.Cropping && view.Selection.Width >= 3) ApplyCrop();
            return doc.Flatten();
        }

        void DoCopy()
        {
            try
            {
                using (Bitmap b = Result()) Output.Copy(b);
                if (app.St.AutoClose) { Toast.ShowText(L.T("toast.copied"), false); Close(); }
                else UpdateState(L.T("preview.copied") + "  (" + L.T("pixels", doc.Image.Width, doc.Image.Height) + ")");
            }
            catch (Exception ex) { UpdateState(L.T("preview.copyFailed", ex.Message)); }
        }

        void DoSave(string path) { DoSave(path, app.St.Q.Png); }

        void DoSave(string path, bool png)
        {
            try
            {
                string saved;
                using (Bitmap b = Result()) saved = Output.Save(new Shot(b, shot.Name), app.St, path, png);
                shot.SavedPath = saved;
                if (app.St.AutoClose)
                {
                    string folder = app.St.Folder;
                    Toast.ShowText(L.T("toast.saved", Path.GetFileName(saved)), false, delegate { Output.OpenFolder(folder, saved); });
                    Close();
                }
                else UpdateState(L.T("preview.saved", saved));
            }
            catch (Exception ex) { UpdateState(L.T("preview.saveFailed", ex.Message)); }
        }

        void DoSaveAs()
        {
            view.CommitText();
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = L.T("dialog.filter");
                d.FilterIndex = app.St.Q.Png ? 1 : 2;
                d.DefaultExt = app.St.Q.Png ? "png" : "jpg";
                d.FileName = Output.LeafName(shot.Name) + (app.St.Q.Png ? ".png" : ".jpg");
                try { Directory.CreateDirectory(app.St.Folder); d.InitialDirectory = app.St.Folder; } catch { }
                if (d.ShowDialog(this) != DialogResult.OK) return;
                bool png = d.FilterIndex != 2;
                string path = d.FileName;
                string lower = path.ToLowerInvariant();
                if (!lower.EndsWith(".png") && !lower.EndsWith(".jpg") && !lower.EndsWith(".jpeg"))
                    path += png ? ".png" : ".jpg";
                DoSave(path, png);
            }
        }

        async void DoCopyText()
        {
            UpdateState("…");
            try
            {
                string text;
                using (Bitmap b = Result()) text = await Ocr.RecognizeAsync(b);
                if (IsDisposed) return;
                if (text == null) { UpdateState(L.T("ocr.unavailable")); return; }
                if (text.Trim().Length == 0) { UpdateState(L.T("toast.noText")); return; }
                Clipboard.SetText(text);
                UpdateState(L.T("toast.textCopied", text.Length));
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Tekstgenkendelse", ex);
                if (!IsDisposed) UpdateState(L.T("ocr.failed", ex.Message));
            }
        }

        void DoPin()
        {
            Bitmap b = Result();
            PinWindow.Open(b, Screen.FromControl(this));
            UpdateState(L.T("pin.opened"));
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            view.Doc = null;
            doc.Dispose();
            foreach (ToolStripItem it in bar.Items) if (it.Image != null) it.Image.Dispose();
            base.OnFormClosed(e);
        }
    }
}
