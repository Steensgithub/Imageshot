using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ImageShot
{
    // Lyst / mørkt udseende. "auto" følger Windows (Indstillinger → Farver → Appfarve).
    static class Theme
    {
        public static readonly Color Brand = Color.FromArgb(0x1F, 0x6F, 0xEB);
        public static readonly Color BrandDark = Color.FromArgb(0x18, 0x57, 0xC1);

        public static bool Dark;
        public static Color Back, Surface, Text, Muted, Border, Accent, Hover;

        static Theme() { Init("auto"); }

        public static void Init(string setting)
        {
            Dark = setting == "dark" || (setting != "light" && SystemDark());
            if (Dark)
            {
                Back = Color.FromArgb(0x1E, 0x1F, 0x22); Surface = Color.FromArgb(0x2B, 0x2D, 0x31);
                Text = Color.FromArgb(0xE8, 0xEA, 0xED); Muted = Color.FromArgb(0x9A, 0xA0, 0xA6);
                Border = Color.FromArgb(0x45, 0x48, 0x4F); Accent = Color.FromArgb(0x4C, 0x8D, 0xFF);
                Hover = Color.FromArgb(0x1B, 0x2A, 0x44);
            }
            else
            {
                Back = Color.FromArgb(0xFA, 0xFB, 0xFC); Surface = Color.White;
                Text = Color.FromArgb(0x1B, 0x1F, 0x24); Muted = Color.FromArgb(0x5F, 0x6B, 0x7A);
                Border = Color.FromArgb(0xC9, 0xCE, 0xD6); Accent = Brand;
                Hover = Color.FromArgb(0xE8, 0xF0, 0xFE);
            }
        }

        public static bool SystemDark()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch { return false; }
        }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);

        // Mørk titellinje (Windows 10 1809+ / 11).
        static void TitleBar(Form f)
        {
            try
            {
                int v = Dark ? 1 : 0;
                if (DwmSetWindowAttribute(f.Handle, 20, ref v, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref v, 4);
            }
            catch { }
        }

        public static void Apply(Form f)
        {
            f.BackColor = Back;
            f.ForeColor = Text;
            Walk(f, f.Controls);
            if (f.IsHandleCreated) TitleBar(f);
            else f.HandleCreated += delegate { TitleBar(f); };
        }

        static void Walk(Form f, Control.ControlCollection cs)
        {
            foreach (Control c in cs)
            {
                if (c is ImageView) continue; // har sin egen mørke baggrund
                if (c is Button)
                {
                    Button b = (Button)c;
                    bool primary = ReferenceEquals(b, f.AcceptButton);
                    b.FlatStyle = FlatStyle.Flat;
                    b.UseVisualStyleBackColor = false;
                    b.FlatAppearance.BorderColor = primary ? Brand : Border;
                    b.FlatAppearance.MouseOverBackColor = primary ? BrandDark : Hover;
                    b.BackColor = primary ? Brand : Surface;
                    b.ForeColor = primary ? Color.White : Text;
                    if (b.Padding.Horizontal == 0) b.Padding = new Padding(8, 2, 8, 2);
                }
                else if (c is TextBox || c is ComboBox || c is NumericUpDown)
                {
                    c.BackColor = Surface;
                    c.ForeColor = Text;
                    if (c is TextBox) ((TextBox)c).BorderStyle = BorderStyle.FixedSingle;
                    if (c is ComboBox && Dark) ((ComboBox)c).FlatStyle = FlatStyle.Flat;
                }
                else if (c is LinkLabel)
                {
                    LinkLabel l = (LinkLabel)c;
                    l.LinkColor = Accent; l.ActiveLinkColor = Accent; l.VisitedLinkColor = Accent;
                }
                else if (c is Label || c is CheckBox || c is RadioButton)
                {
                    if (c.ForeColor == SystemColors.GrayText || "muted".Equals(c.Tag)) { c.ForeColor = Muted; c.Tag = "muted"; }
                    else if (c.ForeColor != Color.White) c.ForeColor = Text;
                }
                else if (c is TrackBar || c is Panel)
                {
                    c.BackColor = Back;
                }
                if (c.HasChildren) Walk(f, c.Controls);
            }
        }

        // Systembakkens menu i samme udseende.
        public static void Apply(ToolStrip m)
        {
            m.Renderer = new ToolStripProfessionalRenderer(new MenuColors());
            m.BackColor = Surface;
            m.ForeColor = Text;
            foreach (ToolStripItem it in m.Items) it.ForeColor = Text;
        }

        sealed class MenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Surface; } }
            public override Color ImageMarginGradientBegin { get { return Surface; } }
            public override Color ImageMarginGradientMiddle { get { return Surface; } }
            public override Color ImageMarginGradientEnd { get { return Surface; } }
            public override Color MenuBorder { get { return Border; } }
            public override Color MenuItemBorder { get { return Hover; } }
            public override Color MenuItemSelected { get { return Hover; } }
            public override Color MenuItemSelectedGradientBegin { get { return Hover; } }
            public override Color MenuItemSelectedGradientEnd { get { return Hover; } }
            public override Color SeparatorDark { get { return Border; } }
            public override Color SeparatorLight { get { return Surface; } }
        }
    }
}
