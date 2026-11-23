using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SoundCloudDesktop
{
    internal sealed class AppSettings
    {
        public int WindowX = -1;
        public int WindowY = -1;
        public int WindowWidth = 1280;
        public int WindowHeight = 820;
        public bool IsMaximized = false;
        public bool CloseToTray = true;
        public bool AlwaysOnTop = false;
        public bool ShowToolbar = true;
        public bool HideUpsellBanners = true;
        public bool BlockAds = true;
        public double ZoomFactor = 1.0;
        public string HomeUrl = "https://soundcloud.com/discover";
        public string ThemeId = "original";
        public string LyricsProvider = "lrclib";
        public string YandexMusicToken = "";
        public string AudioQuality = "high";

        public static string AppDataRoot
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SoundCloudDesktop");
            }
        }

        public static string SettingsPath
        {
            get { return Path.Combine(AppDataRoot, "settings.cfg"); }
        }

        public static string UserDataFolder
        {
            get { return Path.Combine(AppDataRoot, "WebView2Profile"); }
        }

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            try
            {
                if (!File.Exists(SettingsPath)) return s;
                string[] lines = File.ReadAllLines(SettingsPath, Encoding.UTF8);
                Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string raw in lines)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    int idx = raw.IndexOf('=');
                    if (idx <= 0) continue;
                    string k = raw.Substring(0, idx).Trim();
                    string v = raw.Substring(idx + 1).Trim();
                    map[k] = v;
                }

                s.WindowX = ReadInt(map, "WindowX", s.WindowX);
                s.WindowY = ReadInt(map, "WindowY", s.WindowY);
                s.WindowWidth = Math.Max(420, ReadInt(map, "WindowWidth", s.WindowWidth));
                s.WindowHeight = Math.Max(140, ReadInt(map, "WindowHeight", s.WindowHeight));
                s.IsMaximized = ReadBool(map, "IsMaximized", s.IsMaximized);
                s.CloseToTray = ReadBool(map, "CloseToTray", s.CloseToTray);
                s.AlwaysOnTop = ReadBool(map, "AlwaysOnTop", s.AlwaysOnTop);
                s.ShowToolbar = ReadBool(map, "ShowToolbar", s.ShowToolbar);
                s.HideUpsellBanners = ReadBool(map, "HideUpsellBanners", s.HideUpsellBanners);
                s.BlockAds = ReadBool(map, "BlockAds", s.BlockAds);
                s.ZoomFactor = Math.Max(0.5, Math.Min(2.0, ReadDouble(map, "ZoomFactor", s.ZoomFactor)));
                string home;
                if (map.TryGetValue("HomeUrl", out home) && home.StartsWith("https://soundcloud.com", StringComparison.OrdinalIgnoreCase))
                {
                    s.HomeUrl = home;
                }
                string themeId;
                if (map.TryGetValue("ThemeId", out themeId))
                {
                    s.ThemeId = ThemeCatalog.GetById(themeId).Id;
                }
                string lyricsProvider;
                if (map.TryGetValue("LyricsProvider", out lyricsProvider))
                {
                    s.LyricsProvider = string.Equals(lyricsProvider, "yandex", StringComparison.OrdinalIgnoreCase) ? "yandex" : "lrclib";
                }
                string ymToken;
                if (map.TryGetValue("YandexMusicToken", out ymToken))
                {
                    s.YandexMusicToken = ymToken.Trim();
                }
                string aq;
                if (map.TryGetValue("AudioQuality", out aq))
                {
                    string norm = aq.Trim().ToLowerInvariant();
                    s.AudioQuality = (norm == "auto" || norm == "mp3" || norm == "high") ? norm : "high";
                }
            }
            catch
            {
                // Fallback to defaults if file is corrupted
            }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppDataRoot);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("WindowX=" + WindowX.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("WindowY=" + WindowY.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("WindowWidth=" + WindowWidth.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("WindowHeight=" + WindowHeight.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("IsMaximized=" + IsMaximized.ToString());
                sb.AppendLine("CloseToTray=" + CloseToTray.ToString());
                sb.AppendLine("AlwaysOnTop=" + AlwaysOnTop.ToString());
                sb.AppendLine("ShowToolbar=" + ShowToolbar.ToString());
                sb.AppendLine("HideUpsellBanners=" + HideUpsellBanners.ToString());
                sb.AppendLine("BlockAds=" + BlockAds.ToString());
                sb.AppendLine("ZoomFactor=" + ZoomFactor.ToString("0.00", CultureInfo.InvariantCulture));
                sb.AppendLine("HomeUrl=" + HomeUrl);
                sb.AppendLine("ThemeId=" + ThemeCatalog.GetById(ThemeId).Id);
                sb.AppendLine("LyricsProvider=" + (string.Equals(LyricsProvider, "yandex", StringComparison.OrdinalIgnoreCase) ? "yandex" : "lrclib"));
                if (!string.IsNullOrEmpty(YandexMusicToken))
                {
                    sb.AppendLine("YandexMusicToken=" + YandexMusicToken);
                }
                sb.AppendLine("AudioQuality=" + NormalizeAudioQuality(AudioQuality));
                File.WriteAllText(SettingsPath, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                // Best effort save
            }
        }

        public static string NormalizeAudioQuality(string val)
        {
            if (string.IsNullOrEmpty(val)) return "high";
            string norm = val.Trim().ToLowerInvariant();
            return (norm == "auto" || norm == "mp3" || norm == "high") ? norm : "high";
        }

        private static int ReadInt(Dictionary<string, string> map, string key, int def)
        {
            string val;
            int parsed;
            if (map.TryGetValue(key, out val) && int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return parsed;
            return def;
        }

        private static bool ReadBool(Dictionary<string, string> map, string key, bool def)
        {
            string val;
            bool parsed;
            if (map.TryGetValue(key, out val) && bool.TryParse(val, out parsed))
                return parsed;
            return def;
        }

        private static double ReadDouble(Dictionary<string, string> map, string key, double def)
        {
            string val;
            double parsed;
            if (map.TryGetValue(key, out val) && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                return parsed;
            return def;
        }
    }
}
