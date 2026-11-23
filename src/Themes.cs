using System;
using System.Drawing;

namespace SoundCloudDesktop
{
    internal sealed class ThemeDefinition
    {
        public string Id;
        public string DisplayName;
        public bool DarkTitleBar;
        public Color ToolbarBg;
        public Color BodyBg;
        public Color BorderColor;
        public Color AccentColor;
        public Color TextPrimary;
        public Color TextMuted;
        public Color ButtonHover;
        public Color ButtonDown;
        public Color MenuBg;
        public Color MenuHover;
        public Color MenuBorder;
        public Color MenuText;
        public Color MenuMuted;
    }

    internal static class ThemeCatalog
    {
        public static readonly ThemeDefinition[] All = new ThemeDefinition[]
        {
            Create(
                "original",
                "Original Web (Dark Shell)",
                true,
                0x141414, 0x121212, 0x282828, 0xFF5500,
                0xE8E8E8, 0x9C9C9C, 0x2C2C2C, 0x3E3E3E,
                0x181818, 0x2E2E2E, 0x343434, 0xEEEEEE, 0x787878),
            Create(
                "dark",
                "Dark Obsidian",
                true,
                0x161618, 0x111113, 0x2D2D32, 0xFF5500,
                0xECECEF, 0x9A9AA2, 0x2B2B30, 0x3A3A42,
                0x1B1B1E, 0x2E2E35, 0x383840, 0xF0F0F4, 0x888892),
            Create(
                "amoled",
                "Midnight OLED",
                true,
                0x050505, 0x000000, 0x1E1E1E, 0xFF5500,
                0xF2F2F2, 0x8A8A8A, 0x1A1A1A, 0x2A2A2A,
                0x0A0A0A, 0x202020, 0x282828, 0xF2F2F2, 0x777777),
            Create(
                "light",
                "Light Classic",
                false,
                0xF4F4F6, 0xFFFFFF, 0xDCDCE0, 0xFF5500,
                0x1A1A1D, 0x66666E, 0xE4E4E8, 0xD4D4DA,
                0xFFFFFF, 0xEFEFF3, 0xD2D2D8, 0x1A1A1D, 0x888890),
            Create(
                "cyber",
                "Synthwave Purple",
                true,
                0x151022, 0x0E0A18, 0x2D2246, 0xC05CFF,
                0xF0E9FF, 0x9D8EC0, 0x271D40, 0x372959,
                0x191329, 0x2C2147, 0x3A2B5C, 0xF2ECFF, 0x8A78B0),
            Create(
                "ocean",
                "Deep Ocean",
                true,
                0x0E1722, 0x091018, 0x1E3044, 0x22B8CF,
                0xE6F1FA, 0x85A1BA, 0x19293B, 0x233A52,
                0x111C29, 0x1D3045, 0x29415C, 0xE8F3FC, 0x7A96B0)
        };

        public static ThemeDefinition GetById(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                string trimmed = id.Trim();
                for (int i = 0; i < All.Length; i++)
                {
                    if (string.Equals(All[i].Id, trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        return All[i];
                    }
                }
            }
            return All[0];
        }

        private static ThemeDefinition Create(
            string id,
            string displayName,
            bool darkTitleBar,
            int toolbarBg,
            int bodyBg,
            int borderColor,
            int accentColor,
            int textPrimary,
            int textMuted,
            int buttonHover,
            int buttonDown,
            int menuBg,
            int menuHover,
            int menuBorder,
            int menuText,
            int menuMuted)
        {
            ThemeDefinition theme = new ThemeDefinition();
            theme.Id = id;
            theme.DisplayName = displayName;
            theme.DarkTitleBar = darkTitleBar;
            theme.ToolbarBg = Hex(toolbarBg);
            theme.BodyBg = Hex(bodyBg);
            theme.BorderColor = Hex(borderColor);
            theme.AccentColor = Hex(accentColor);
            theme.TextPrimary = Hex(textPrimary);
            theme.TextMuted = Hex(textMuted);
            theme.ButtonHover = Hex(buttonHover);
            theme.ButtonDown = Hex(buttonDown);
            theme.MenuBg = Hex(menuBg);
            theme.MenuHover = Hex(menuHover);
            theme.MenuBorder = Hex(menuBorder);
            theme.MenuText = Hex(menuText);
            theme.MenuMuted = Hex(menuMuted);
            return theme;
        }

        private static Color Hex(int rgb)
        {
            return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }
    }
}
