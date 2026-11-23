using System;
using System.Drawing;
using System.Windows.Forms;

namespace SoundCloudDesktop
{
    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        private readonly Color _bg;
        private readonly Color _bgHover;
        private readonly Color _border;
        private readonly Color _accent;
        private readonly Color _checkBg;
        private readonly Color _checkSelectedBg;

        public DarkColorTable() : this(ThemeCatalog.GetById("original"))
        {
        }

        public DarkColorTable(ThemeDefinition theme)
        {
            ThemeDefinition resolved = theme != null ? theme : ThemeCatalog.GetById("original");
            _bg = resolved.MenuBg;
            _bgHover = resolved.MenuHover;
            _border = resolved.MenuBorder;
            _accent = resolved.AccentColor;
            _checkBg = Blend(_bg, _accent, 0.22f);
            _checkSelectedBg = Blend(_bg, _accent, 0.34f);
        }

        public override Color MenuItemSelected { get { return _bgHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return _bgHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return _bgHover; } }
        public override Color MenuItemPressedGradientBegin { get { return _bgHover; } }
        public override Color MenuItemPressedGradientMiddle { get { return _bgHover; } }
        public override Color MenuItemPressedGradientEnd { get { return _bg; } }
        public override Color MenuItemBorder { get { return _accent; } }
        public override Color MenuBorder { get { return _border; } }
        public override Color ToolStripDropDownBackground { get { return _bg; } }
        public override Color ImageMarginGradientBegin { get { return _bg; } }
        public override Color ImageMarginGradientMiddle { get { return _bg; } }
        public override Color ImageMarginGradientEnd { get { return _bg; } }
        public override Color SeparatorDark { get { return _border; } }
        public override Color SeparatorLight { get { return _border; } }
        public override Color CheckBackground { get { return _checkBg; } }
        public override Color CheckSelectedBackground { get { return _checkSelectedBg; } }
        public override Color CheckPressedBackground { get { return _accent; } }

        private static Color Blend(Color baseColor, Color overlayColor, float amount)
        {
            int r = (int)Math.Round(baseColor.R + (overlayColor.R - baseColor.R) * amount);
            int g = (int)Math.Round(baseColor.G + (overlayColor.G - baseColor.G) * amount);
            int b = (int)Math.Round(baseColor.B + (overlayColor.B - baseColor.B) * amount);
            return Color.FromArgb(
                Math.Max(0, Math.Min(255, r)),
                Math.Max(0, Math.Min(255, g)),
                Math.Max(0, Math.Min(255, b)));
        }
    }

    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly ThemeDefinition _theme;

        public DarkMenuRenderer() : this(ThemeCatalog.GetById("original"))
        {
        }

        public DarkMenuRenderer(ThemeDefinition theme) : base(new DarkColorTable(theme))
        {
            _theme = theme != null ? theme : ThemeCatalog.GetById("original");
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? _theme.MenuText : _theme.MenuMuted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = (e.Item != null && e.Item.Enabled) ? _theme.MenuText : _theme.MenuMuted;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle r = e.ImageRectangle;
            Color checkMarkColor = GetContrastingForeColor(_theme.AccentColor);
            using (SolidBrush b = new SolidBrush(_theme.AccentColor))
            using (Pen p = new Pen(checkMarkColor, 2f))
            {
                e.Graphics.FillRectangle(b, r);
                e.Graphics.DrawLine(p, r.Left + 3, r.Top + r.Height / 2, r.Left + r.Width / 2 - 1, r.Bottom - 4);
                e.Graphics.DrawLine(p, r.Left + r.Width / 2 - 1, r.Bottom - 4, r.Right - 3, r.Top + 4);
            }
        }

        private static Color GetContrastingForeColor(Color bg)
        {
            double luminance = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B;
            return luminance > 186.0 ? Color.Black : Color.White;
        }
    }
}
