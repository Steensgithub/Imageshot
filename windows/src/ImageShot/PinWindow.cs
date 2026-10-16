using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ImageShot
{
    // Fastgjort billede: svæver øverst over alle vinduer – til at sammenligne eller skrive af.
    // Træk for at flytte, musehjul zoomer, dobbeltklik eller Esc lukker, højreklik for menu.
    sealed class PinWindow : Form
    {
        readonly Bitmap image;
        float zoom = 1f;
        readonly Color border = Theme.Brand;

        public static PinWindow Open(Bitmap image, Screen near)
        {
            PinWindow p = new PinWindow(image, near);
            p.Show();
            return p;
        }

        PinWindow(Bitmap image, Screen near)
        {
            this.image = image;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            Text = "ImageShot – " + L.T("preview.pin");
            DoubleBuffered = true;
            KeyPreview = true;
            Rectangle wa = near.WorkingArea;
            zoom = Math.Min(1f, Math.Min(wa.Width * 0.8f / image.Width, wa.Height * 0.8f / image.Height));
            Size sz = Scaled();
            Location = new Point(wa.Left + (wa.Width - sz.Width) / 2 + 30, wa.Top + (wa.Height - sz.Height) / 2 + 30);
            ClientSize = sz;

            ContextMenuStrip m = new ContextMenuStrip();
            m.Items.Add(L.T("preview.copy"), null, delegate { try { Output.Copy(image); } catch { } });
            m.Items.Add("100 %", null, delegate { SetZoom(1f); });
            ToolStripMenuItem op = new ToolStripMenuItem(L.T("pin.opacity"));
            foreach (int pct in new[] { 100, 80, 60, 40 })
            {
                int v = pct;
                op.DropDownItems.Add(pct + " %", null, delegate { Opacity = v / 100.0; });
            }
            m.Items.Add(op);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(L.T("preview.close") + "  (Esc)", null, delegate { Close(); });
            Theme.Apply(m);
            ContextMenuStrip = m;
        }

        Size Scaled() { return new Size(Math.Max(24, (int)(image.Width * zoom)) + 2, Math.Max(24, (int)(image.Height * zoom)) + 2); }

        void SetZoom(float z)
        {
            z = Math.Max(0.1f, Math.Min(4f, z));
            Point c = new Point(Left + Width / 2, Top + Height / 2);
            zoom = z;
            Size sz = Scaled();
            Bounds = new Rectangle(c.X - sz.Width / 2, c.Y - sz.Height / 2, sz.Width, sz.Height);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.InterpolationMode = zoom < 1 ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(image, new Rectangle(1, 1, ClientSize.Width - 2, ClientSize.Height - 2));
            using (Pen p = new Pen(border, 1f)) g.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        Point grab;
        bool dragging;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) { dragging = true; grab = e.Location; }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) Location = new Point(Left + e.X - grab.X, Top + e.Y - grab.Y);
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = false; }

        protected override void OnMouseDoubleClick(MouseEventArgs e) { base.OnMouseDoubleClick(e); Close(); }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); SetZoom(zoom * (e.Delta > 0 ? 1.1f : 1 / 1.1f)); }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            if (keyData == (Keys.Control | Keys.C)) { try { Output.Copy(image); } catch { } return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            image.Dispose();
            if (ContextMenuStrip != null) ContextMenuStrip.Dispose();
            base.OnFormClosed(e);
        }
    }
}
