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
    class PreviewForm : Form
    {
        readonly Shot shot;
        readonly App app;
        readonly ImageView view;
        readonly Label status;
        readonly CheckBox auto;
        readonly Button cropBtn, undoBtn;
        readonly List<Bitmap> undo = new List<Bitmap>();
        DateTime lastCrop = DateTime.MinValue;

        public PreviewForm(Shot shot, App app)
        {
            this.shot = shot;
            this.app = app;
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96f, 96f);
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = L.T("preview.title");
            Icon = Program.AppIcon;
            KeyPreview = true;
            StartPosition = FormStartPosition.Manual;
            MinimumSize = new Size(600, 400);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Padding = new Padding(10);

            view = new ImageView();
            view.Dock = DockStyle.Fill;
            view.Image = shot.Image;
            view.Margin = new Padding(0, 0, 0, 8);
            view.SelectionChanged += delegate { UpdateStatus(null); };
            view.ApplyRequested += delegate { ApplyCrop(); };
            root.Controls.Add(view, 0, 0);

            status = new Label();
            status.AutoSize = true;
            status.Margin = new Padding(0, 0, 0, 8);
            root.Controls.Add(status, 0, 1);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.Dock = DockStyle.Fill;
            buttons.WrapContents = true;
            buttons.Margin = Padding.Empty;

            Button copy = Btn(L.T("preview.copy") + "  (Ctrl+C)", delegate { DoCopy(); });
            cropBtn = Btn(L.T("preview.crop") + "  (T)", delegate { if (view.Cropping) ApplyCrop(); else StartCrop(); });
            undoBtn = Btn(L.T("preview.undo") + "  (Ctrl+Z)", delegate { Undo(); });
            undoBtn.Visible = false;
            Button save = Btn(L.T("preview.save") + "  (Ctrl+S)", delegate { DoSave(null); });
            Button saveAs = Btn(L.T("preview.saveAs"), delegate { DoSaveAs(); });
            Button folder = Btn(L.T("preview.openFolder"), delegate { Output.OpenFolder(app.St.Folder, null); });
            Button close = Btn(L.T("preview.close") + "  (Esc)", delegate { Close(); });
            buttons.Controls.AddRange(new Control[] { copy, cropBtn, undoBtn, save, saveAs, folder, close });

            auto = new CheckBox();
            auto.AutoSize = true;
            auto.Text = L.T("preview.autoClose");
            auto.Checked = app.St.AutoClose;
            auto.Margin = new Padding(12, 7, 0, 0);
            auto.CheckedChanged += delegate { app.St.AutoClose = auto.Checked; app.St.Save(); };
            buttons.Controls.Add(auto);
            root.Controls.Add(buttons, 0, 2);

            Controls.Add(root);
            AcceptButton = copy;
            CancelButton = close;
            UpdateStatus(null);
            ResumeLayout(false);
            PerformLayout();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            Size sz = new Size(Math.Max(MinimumSize.Width, wa.Width * 3 / 4), Math.Max(MinimumSize.Height, wa.Height * 3 / 4));
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

        void UpdateStatus(string message)
        {
            if (message != null) { status.Text = message; return; }
            if (view.Cropping)
            {
                Rectangle r = view.Selection;
                status.Text = r.IsEmpty
                    ? L.T("preview.cropHint")
                    : L.T("preview.cropSel", r.Width, r.Height);
                return;
            }
            status.Text = L.T("pixels", shot.Image.Width, shot.Image.Height) + "  ·  " + app.St.Q.Detail +
                          (undo.Count > 0 ? "  ·  " + L.T("preview.edited") : "");
        }

        void StartCrop()
        {
            view.Cropping = true;
            cropBtn.Text = L.T("preview.applyCrop") + "  (Enter)";
            view.Focus();
            UpdateStatus(null);
        }

        void EndCrop()
        {
            view.Cropping = false;
            cropBtn.Text = L.T("preview.crop") + "  (T)";
            UpdateStatus(null);
        }

        void ApplyCrop()
        {
            Rectangle r = view.Selection;
            r.Intersect(new Rectangle(0, 0, shot.Image.Width, shot.Image.Height));
            if (r.Width < 3 || r.Height < 3) { EndCrop(); return; }
            Bitmap cropped = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(cropped))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(shot.Image, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
            }
            lastCrop = DateTime.Now;
            undo.Add(shot.Image);
            shot.Image = cropped;
            view.Image = cropped;
            undoBtn.Visible = true;
            EndCrop();
        }

        void Undo()
        {
            if (view.Cropping) EndCrop();
            if (undo.Count == 0) return;
            Bitmap prev = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            Bitmap current = shot.Image;
            shot.Image = prev;
            view.Image = prev;
            current.Dispose();
            undoBtn.Visible = undo.Count > 0;
            UpdateStatus(null);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Holdes Enter nede efter tilskæring, må gentagelsen ikke også kopiere.
            if (keyData == Keys.Enter && (msg.LParam.ToInt64() & 0x40000000) != 0 &&
                (DateTime.Now - lastCrop).TotalMilliseconds < 1500) return true;
            if (view.Cropping)
            {
                if (keyData == Keys.Enter) { ApplyCrop(); return true; }
                if (keyData == Keys.Escape) { EndCrop(); return true; }
            }
            else if (keyData == Keys.T) { StartCrop(); return true; }
            if (keyData == (Keys.Control | Keys.Z)) { Undo(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C) { DoCopy(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.S) { DoSave(null); e.Handled = true; }
            base.OnKeyDown(e);
        }

        // En markeret ramme anvendes automatisk, før der kopieres eller gemmes.
        void FinishCrop()
        {
            if (view.Cropping) ApplyCrop();
        }

        void DoCopy()
        {
            FinishCrop();
            try
            {
                Output.Copy(shot.Image);
                if (app.St.AutoClose) { Toast.ShowText(L.T("toast.copied"), false); Close(); }
                else UpdateStatus(L.T("preview.copied") + "  (" + L.T("pixels", shot.Image.Width, shot.Image.Height) + ")");
            }
            catch (Exception ex) { UpdateStatus(L.T("preview.copyFailed", ex.Message)); }
        }

        void DoSave(string path) { DoSave(path, app.St.Q.Png); }

        void DoSave(string path, bool png)
        {
            FinishCrop();
            try
            {
                string saved = Output.Save(shot, app.St, path, png);
                if (app.St.AutoClose)
                {
                    string folder = app.St.Folder;
                    Toast.ShowText(L.T("toast.saved", Path.GetFileName(saved)), false, delegate { Output.OpenFolder(folder, saved); });
                    Close();
                }
                else UpdateStatus(L.T("preview.saved", saved));
            }
            catch (Exception ex) { UpdateStatus(L.T("preview.saveFailed", ex.Message)); }
        }

        void DoSaveAs()
        {
            FinishCrop();
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = L.T("dialog.filter");
                d.FilterIndex = app.St.Q.Png ? 1 : 2;
                d.DefaultExt = app.St.Q.Png ? "png" : "jpg";
                d.FileName = Output.Clean(shot.Name) + (app.St.Q.Png ? ".png" : ".jpg");
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            view.Image = null;
            shot.Image.Dispose();
            foreach (Bitmap b in undo) b.Dispose();
            undo.Clear();
            base.OnFormClosed(e);
        }
    }
}
