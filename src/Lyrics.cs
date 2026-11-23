using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace SoundCloudDesktop
{
    internal sealed class LyricsResult
    {
        public bool Found;
        public string Synced;
        public string Plain;
        public string Source;
    }

    internal interface ILyricsProvider
    {
        string Id { get; }
        string DisplayName { get; }
        LyricsResult Fetch(string artist, string title, double duration);
    }

    internal static class LyricsQueryHelper
    {
        public static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = Regex.Replace(s, @"[\(\[][^\)\]]*(official|lyric|audio|video|visuali[sz]er|remaster|free download|out now|prod\.?)[^\)\]]*[\)\]]", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\s+(ft\.?|feat\.?|featuring)\s+.*$", "", RegexOptions.IgnoreCase);
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        public static List<string[]> BuildCandidates(string artist, string title)
        {
            string a = Clean(artist);
            string t = Clean(title);
            var list = new List<string[]>();
            list.Add(new[] { a, t });
            var m = Regex.Match(t, @"^(.+?)\s+[-\u2013\u2014]\s+(.+)$");
            if (m.Success)
            {
                list.Add(new[] { m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim() });
            }
            return list;
        }

        public static string HttpGet(string url, Dictionary<string, string> headers = null)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            using (var wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers[HttpRequestHeader.UserAgent] = "SoundCloudDesktop/1.0";
                if (headers != null)
                {
                    foreach (var kv in headers)
                    {
                        wc.Headers[kv.Key] = kv.Value;
                    }
                }
                return wc.DownloadString(url);
            }
        }

        public static object[] AsArray(object o)
        {
            var arr = o as object[];
            if (arr != null) return arr;
            var al = o as ArrayList;
            return al != null ? al.ToArray() : null;
        }
    }

    internal sealed class LrclibLyricsProvider : ILyricsProvider
    {
        public string Id { get { return "lrclib"; } }
        public string DisplayName { get { return "LRCLIB"; } }

        private static Dictionary<string, object> TryEndpoint(string url, bool isArray, double duration)
        {
            try
            {
                object o = LyricsQueryHelper.Json.DeserializeObject(LyricsQueryHelper.HttpGet(url));
                if (!isArray) return o as Dictionary<string, object>;
                object[] arr = LyricsQueryHelper.AsArray(o);
                if (arr == null) return null;

                Dictionary<string, object> best = null;
                double bestScore = -1;
                foreach (object it in arr)
                {
                    var d = it as Dictionary<string, object>;
                    if (d == null) continue;
                    double sc = 0;
                    if (d.ContainsKey("syncedLyrics") && d["syncedLyrics"] != null) sc += 100;
                    else if (d.ContainsKey("plainLyrics") && d["plainLyrics"] != null) sc += 50;
                    else continue;

                    if (duration > 0 && d.ContainsKey("duration") && d["duration"] != null)
                    {
                        sc -= Math.Min(60, Math.Abs(Convert.ToDouble(d["duration"]) - duration));
                    }
                    if (sc > bestScore)
                    {
                        bestScore = sc;
                        best = d;
                    }
                }
                return best;
            }
            catch
            {
                return null;
            }
        }

        public LyricsResult Fetch(string artist, string title, double duration)
        {
            var cands = LyricsQueryHelper.BuildCandidates(artist, title);
            Dictionary<string, object> r = null;

            foreach (var c in cands)
            {
                string url = "https://lrclib.net/api/get?artist_name=" + Uri.EscapeDataString(c[0]) + "&track_name=" + Uri.EscapeDataString(c[1]);
                if (duration > 0) url += "&duration=" + (int)Math.Round(duration);
                r = TryEndpoint(url, false, duration);
                if (r == null)
                {
                    r = TryEndpoint("https://lrclib.net/api/get?artist_name=" + Uri.EscapeDataString(c[0]) + "&track_name=" + Uri.EscapeDataString(c[1]), false, duration);
                }
                if (r != null && ((r.ContainsKey("syncedLyrics") && r["syncedLyrics"] != null) || (r.ContainsKey("plainLyrics") && r["plainLyrics"] != null)))
                {
                    break;
                }
                r = null;
            }

            if (r == null)
            {
                foreach (var c in cands)
                {
                    string q = string.IsNullOrEmpty(c[0]) ? c[1] : (c[0] + " " + c[1]);
                    r = TryEndpoint("https://lrclib.net/api/search?q=" + Uri.EscapeDataString(q), true, duration);
                    if (r != null) break;
                    r = TryEndpoint("https://lrclib.net/api/search?track_name=" + Uri.EscapeDataString(c[1]), true, duration);
                    if (r != null) break;
                }
            }

            if (r == null) return null;
            string synced = r.ContainsKey("syncedLyrics") ? r["syncedLyrics"] as string : null;
            string plain = r.ContainsKey("plainLyrics") ? r["plainLyrics"] as string : null;
            if (string.IsNullOrWhiteSpace(synced) && string.IsNullOrWhiteSpace(plain)) return null;

            return new LyricsResult
            {
                Found = true,
                Synced = string.IsNullOrWhiteSpace(synced) ? null : synced,
                Plain = string.IsNullOrWhiteSpace(plain) ? null : plain,
                Source = DisplayName
            };
        }
    }

    internal sealed class YandexMusicLyricsProvider : ILyricsProvider
    {
        private const string SignKey = "p93jhgh689SBReK6ghtw62";
        private readonly string _token;

        public YandexMusicLyricsProvider(string token)
        {
            _token = (token ?? "").Trim();
        }

        public string Id { get { return "yandex"; } }
        public string DisplayName { get { return "Yandex Music"; } }

        private Dictionary<string, string> AuthHeaders()
        {
            return new Dictionary<string, string>
            {
                { "Authorization", "OAuth " + _token },
                { "X-Yandex-Music-Client", "YandexMusicAndroid/24023621" }
            };
        }

        private static string ComputeSign(string trackId, long timestamp)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SignKey)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(trackId + timestamp.ToString()));
                return Convert.ToBase64String(hash);
            }
        }

        private string SearchBestTrackId(string query, double durationSec)
        {
            try
            {
                string url = "https://api.music.yandex.net/search?text=" + Uri.EscapeDataString(query) + "&type=track&page=0";
                var root = LyricsQueryHelper.Json.DeserializeObject(LyricsQueryHelper.HttpGet(url, AuthHeaders())) as Dictionary<string, object>;
                if (root == null || !root.ContainsKey("result")) return null;
                var result = root["result"] as Dictionary<string, object>;
                if (result == null || !result.ContainsKey("tracks")) return null;
                var tracks = result["tracks"] as Dictionary<string, object>;
                if (tracks == null || !tracks.ContainsKey("results")) return null;
                object[] items = LyricsQueryHelper.AsArray(tracks["results"]);
                if (items == null || items.Length == 0) return null;

                string bestId = null;
                double bestScore = -1000;
                foreach (object it in items)
                {
                    var d = it as Dictionary<string, object>;
                    if (d == null || !d.ContainsKey("id") || d["id"] == null) continue;
                    double sc = 0;
                    var li = d.ContainsKey("lyricsInfo") ? d["lyricsInfo"] as Dictionary<string, object> : null;
                    if (li != null)
                    {
                        if (li.ContainsKey("hasAvailableSyncLyrics") && Convert.ToBoolean(li["hasAvailableSyncLyrics"])) sc += 100;
                        else if (li.ContainsKey("hasAvailableTextLyrics") && Convert.ToBoolean(li["hasAvailableTextLyrics"])) sc += 50;
                    }
                    if (durationSec > 0 && d.ContainsKey("durationMs") && d["durationMs"] != null)
                    {
                        double dSec = Convert.ToDouble(d["durationMs"]) / 1000.0;
                        sc -= Math.Min(60, Math.Abs(dSec - durationSec));
                    }
                    if (sc > bestScore)
                    {
                        bestScore = sc;
                        bestId = Convert.ToString(d["id"]);
                    }
                }
                return bestId;
            }
            catch
            {
                return null;
            }
        }

        private string FetchLyricsByFormat(string trackId, string format)
        {
            try
            {
                long ts = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                string sign = ComputeSign(trackId, ts);
                string url = "https://api.music.yandex.net/tracks/" + Uri.EscapeDataString(trackId)
                    + "/lyrics?format=" + Uri.EscapeDataString(format)
                    + "&timeStamp=" + ts.ToString()
                    + "&sign=" + Uri.EscapeDataString(sign);
                var root = LyricsQueryHelper.Json.DeserializeObject(LyricsQueryHelper.HttpGet(url, AuthHeaders())) as Dictionary<string, object>;
                if (root == null || !root.ContainsKey("result")) return null;
                var result = root["result"] as Dictionary<string, object>;
                if (result == null || !result.ContainsKey("downloadUrl")) return null;
                string dl = result["downloadUrl"] as string;
                if (string.IsNullOrWhiteSpace(dl)) return null;
                return LyricsQueryHelper.HttpGet(dl);
            }
            catch
            {
                return null;
            }
        }

        private string FetchSupplementLyrics(string trackId)
        {
            try
            {
                string url = "https://api.music.yandex.net/tracks/" + Uri.EscapeDataString(trackId) + "/supplement";
                var root = LyricsQueryHelper.Json.DeserializeObject(LyricsQueryHelper.HttpGet(url, AuthHeaders())) as Dictionary<string, object>;
                if (root == null || !root.ContainsKey("result")) return null;
                var result = root["result"] as Dictionary<string, object>;
                if (result == null || !result.ContainsKey("lyrics")) return null;
                var lyrics = result["lyrics"] as Dictionary<string, object>;
                if (lyrics == null || !lyrics.ContainsKey("fullLyrics")) return null;
                return lyrics["fullLyrics"] as string;
            }
            catch
            {
                return null;
            }
        }

        public LyricsResult Fetch(string artist, string title, double duration)
        {
            if (string.IsNullOrWhiteSpace(_token)) return null;

            var cands = LyricsQueryHelper.BuildCandidates(artist, title);
            foreach (var c in cands)
            {
                string query = string.IsNullOrEmpty(c[0]) ? c[1] : (c[0] + " - " + c[1]);
                if (string.IsNullOrWhiteSpace(query)) continue;
                string trackId = SearchBestTrackId(query, duration);
                if (string.IsNullOrEmpty(trackId)) continue;

                string lrc = FetchLyricsByFormat(trackId, "LRC");
                if (!string.IsNullOrWhiteSpace(lrc))
                {
                    return new LyricsResult { Found = true, Synced = lrc, Source = DisplayName };
                }

                string plain = FetchLyricsByFormat(trackId, "TEXT");
                if (string.IsNullOrWhiteSpace(plain))
                {
                    plain = FetchSupplementLyrics(trackId);
                }
                if (!string.IsNullOrWhiteSpace(plain))
                {
                    return new LyricsResult { Found = true, Plain = plain, Source = DisplayName };
                }
            }

            return null;
        }
    }

    // Pluggable lyrics service: defaults to LRCLIB, with Yandex Music as fallback/alternative.
    internal static class LyricsService
    {
        public static string ResolveYandexToken(string settingsToken)
        {
            if (!string.IsNullOrWhiteSpace(settingsToken)) return settingsToken.Trim();
            string env = Environment.GetEnvironmentVariable("YANDEX_MUSIC_TOKEN");
            return string.IsNullOrWhiteSpace(env) ? "" : env.Trim();
        }

        public static string Fetch(string artist, string title, double duration, string preferredProviderId, string settingsYandexToken)
        {
            string ymToken = ResolveYandexToken(settingsYandexToken);
            var providers = new List<ILyricsProvider>();

            var lrclib = new LrclibLyricsProvider();
            var yandex = new YandexMusicLyricsProvider(ymToken);

            if (string.Equals(preferredProviderId, "yandex", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(ymToken))
            {
                providers.Add(yandex);
                providers.Add(lrclib);
            }
            else
            {
                providers.Add(lrclib);
                if (!string.IsNullOrEmpty(ymToken))
                {
                    providers.Add(yandex);
                }
            }

            LyricsResult plainFallback = null;
            foreach (ILyricsProvider provider in providers)
            {
                LyricsResult r = provider.Fetch(artist, title, duration);
                if (r == null || !r.Found) continue;
                if (!string.IsNullOrWhiteSpace(r.Synced))
                {
                    return Serialize(r);
                }
                if (plainFallback == null && !string.IsNullOrWhiteSpace(r.Plain))
                {
                    plainFallback = r;
                }
            }

            return Serialize(plainFallback);
        }

        private static string Serialize(LyricsResult r)
        {
            var res = new Dictionary<string, object>();
            if (r == null || !r.Found)
            {
                res["found"] = false;
            }
            else
            {
                res["found"] = true;
                res["synced"] = r.Synced;
                res["plain"] = r.Plain;
                res["source"] = r.Source;
            }
            return LyricsQueryHelper.Json.Serialize(res);
        }
    }

    internal static class LyricsBridge
    {
        public static readonly string InitScript = @"
(function () {
    if (window.__scLyrics) return;
    var S = { on: false, pageOn: false, key: '', autoKey: '', manual: null, source: '', lines: null, plain: null, state: 'idle', idx: -2, base: 0, t0: 0, lastTxt: '', playing: false, lastToggle: 0 };
    window.__scLyrics = S;

    function send(o) { try { window.chrome.webview.postMessage(JSON.stringify(o)); } catch (_) {} }
    function $(s) { return document.querySelector(s); }
    function parseT(t) {
        if (!t) return NaN;
        var p = t.replace(/[^0-9:]/g, '').split(':'), s = 0;
        if (!p.length || p[0] === '') return NaN;
        for (var i = 0; i < p.length; i++) s = s * 60 + (parseInt(p[i], 10) || 0);
        return s;
    }
    function fmtT(s) {
        if (isNaN(s) || s < 0) return '0:00';
        var m = Math.floor(s / 60), r = Math.floor(s % 60);
        return m + ':' + (r < 10 ? '0' : '') + r;
    }
    function getArtwork() {
        if (navigator.mediaSession && navigator.mediaSession.metadata && navigator.mediaSession.metadata.artwork && navigator.mediaSession.metadata.artwork.length) {
            var a = navigator.mediaSession.metadata.artwork;
            return a[a.length - 1].src || '';
        }
        var img = $('.playbackSoundBadge img');
        if (img && img.src) return img.src;
        var sp = $('.playbackSoundBadge .sc-artwork');
        if (sp) {
            var bg = (sp.style && sp.style.backgroundImage) || window.getComputedStyle(sp).backgroundImage || '';
            var m = bg.match(/url\(['\x22]?([^'\x22]+)['\x22]?\)/);
            if (m) return m[1];
        }
        return '';
    }
    function info() {
        var tEl = $('.playbackSoundBadge__titleLink'), aEl = $('.playbackSoundBadge__lightLink');
        var title = '', artist = '';
        if (navigator.mediaSession && navigator.mediaSession.metadata) {
            title = navigator.mediaSession.metadata.title || '';
            artist = navigator.mediaSession.metadata.artist || '';
        }
        if (!title && tEl) title = (tEl.getAttribute('title') || tEl.textContent || '').trim();
        if (!artist && aEl) artist = (aEl.getAttribute('title') || aEl.textContent || '').trim();
        var d = $('.playbackTimeline__duration span[aria-hidden]') || $('.playbackTimeline__duration');
        var durTxt = d ? (d.textContent || '').trim() : '';
        return { title: title, artist: artist, duration: parseT(durTxt), durationText: durTxt, artwork: getArtwork() };
    }
    function pos() {
        var now = performance.now();
        var el = $('.playbackTimeline__timePassed span[aria-hidden]') || $('.playbackTimeline__timePassed');
        var txt = el ? el.textContent : '';
        var sec = parseT(txt);
        var pb = $('.playControl');
        S.playing = !!(pb && pb.classList.contains('playing'));
        if (txt !== S.lastTxt && !isNaN(sec)) { S.lastTxt = txt; S.base = sec; S.t0 = now; }
        var dt = S.playing ? Math.min(1, (now - S.t0) / 1000) : 0;
        return S.base + dt;
    }

    var css = document.createElement('style');
    css.textContent =
        '#sc-lyr{position:fixed;right:16px;bottom:62px;width:min(380px,calc(100vw - 32px));height:min(54vh,calc(100vh - 82px));box-sizing:border-box;z-index:2147483000;display:none;flex-direction:column;background:var(--sc-surface,rgba(18,18,18,.94));backdrop-filter:blur(14px);border:1px solid var(--sc-border,rgba(255,255,255,.14));border-radius:14px;box-shadow:0 12px 40px rgba(0,0,0,.55);font-family:Segoe UI,Arial,sans-serif;color:var(--sc-text,#fff);overflow:hidden}' +
        '#sc-lyr.on{display:flex}' +
        '#sc-lyr .h{padding:10px 14px;font-size:12px;color:var(--sc-muted,rgba(255,255,255,.7));display:flex;justify-content:space-between;align-items:center;gap:8px;border-bottom:1px solid var(--sc-border,rgba(255,255,255,.08))}' +
        '#sc-lyr .h span{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}' +
        '#sc-lyr .h b{cursor:pointer;font-size:16px;line-height:1;padding:2px 4px;flex-shrink:0;color:var(--sc-text,#fff);opacity:.8}' +
        '#sc-lyr .h b:hover{opacity:1;color:var(--sc-accent,#ff5500)}' +
        '#sc-lyr .b{position:relative;flex:1;overflow-y:auto;padding:40% 18px;scroll-behavior:smooth;scrollbar-width:none;-webkit-mask-image:linear-gradient(transparent,#000 18%,#000 82%,transparent)}' +
        '#sc-lyr .b::-webkit-scrollbar{display:none}' +
        '#sc-lyr .l{font-size:20px;font-weight:700;line-height:1.35;margin:10px 0;opacity:.5;transition:all .3s ease;cursor:pointer;transform-origin:left center}' +
        '#sc-lyr .l.past{opacity:.28}' +
        '#sc-lyr .l.cur{opacity:1;color:var(--sc-accent,#ff5500);transform:scale(1.06)}' +
        '#sc-lyr .p{font-size:15px;font-weight:500;opacity:.88;white-space:pre-wrap;line-height:1.6}' +
        '#sc-lyr .m{position:absolute;inset:40px 0 0 0;display:flex;align-items:center;justify-content:center;color:var(--sc-muted,rgba(255,255,255,.65));font-size:15px;pointer-events:none}' +
        '#sc-lyr .h .pg{cursor:pointer;font-size:11px;font-weight:600;line-height:1;padding:4px 8px;border-radius:10px;border:1px solid var(--sc-border,rgba(255,255,255,.18));background:var(--sc-surface-2,rgba(255,255,255,.06));color:var(--sc-text,#fff);flex-shrink:0;white-space:nowrap}' +
        '#sc-lyr .h .pg:hover{border-color:var(--sc-accent,#ff5500);color:var(--sc-accent,#ff5500)}' +
        '#sc-lyr-page{position:fixed;inset:0;z-index:2147483647;display:none;flex-direction:column;box-sizing:border-box;background:radial-gradient(circle at 18% 20%,rgba(var(--sc-accent-rgb,255,85,0),.16),transparent 52%),var(--sc-bg,#111111);color:var(--sc-text,#ffffff);font-family:Segoe UI,Arial,sans-serif;overflow:hidden}' +
        '#sc-lyr-page.on{display:flex}' +
        '#sc-lyr-page .top{display:flex;align-items:center;gap:10px;padding:14px 24px;border-bottom:1px solid var(--sc-border,rgba(255,255,255,.12));background:var(--sc-surface,rgba(18,18,18,.88));backdrop-filter:blur(14px);flex-wrap:wrap}' +
        '#sc-lyr-page .sp{flex:1}' +
        '#sc-lyr-page .pill{font-size:11px;font-weight:700;padding:4px 10px;border-radius:999px;background:var(--sc-accent,#ff5500);color:var(--sc-accent-text,#fff);letter-spacing:.3px}' +
        '#sc-lyr-page .btn{font:600 12px Segoe UI,Arial,sans-serif;color:var(--sc-text,#fff);background:var(--sc-surface-2,rgba(255,255,255,.08));border:1px solid var(--sc-border,rgba(255,255,255,.18));border-radius:18px;padding:7px 14px;cursor:pointer;text-decoration:none;display:inline-flex;align-items:center;gap:6px;box-sizing:border-box;white-space:nowrap}' +
        '#sc-lyr-page .btn:hover{border-color:var(--sc-accent,#ff5500);color:var(--sc-accent,#ff5500)}' +
        '#sc-lyr-page .main{flex:1;display:flex;min-height:0;overflow:hidden}' +
        '#sc-lyr-page .hero{width:min(340px,35vw);padding:28px 24px;border-right:1px solid var(--sc-border,rgba(255,255,255,.08));display:flex;flex-direction:column;gap:12px;box-sizing:border-box;overflow-y:auto;background:var(--sc-surface,rgba(18,18,18,.55))}' +
        '#sc-lyr-page .art{width:100%;max-width:240px;aspect-ratio:1/1;border-radius:14px;background:var(--sc-surface-2,#222) center/cover no-repeat;border:1px solid var(--sc-border,rgba(255,255,255,.12));box-shadow:0 12px 32px rgba(0,0,0,.45);display:flex;align-items:center;justify-content:center;font-size:48px;color:var(--sc-muted,rgba(255,255,255,.35))}' +
        '#sc-lyr-page .tt{font-size:20px;font-weight:700;line-height:1.3;word-break:break-word}' +
        '#sc-lyr-page .ar{font-size:14px;color:var(--sc-muted,rgba(255,255,255,.72));word-break:break-word}' +
        '#sc-lyr-page .tm{font-size:12px;font-variant-numeric:tabular-nums;color:var(--sc-muted,rgba(255,255,255,.68))}' +
        '#sc-lyr-page .ms{margin-top:10px;padding-top:12px;border-top:1px solid var(--sc-border,rgba(255,255,255,.1));display:flex;flex-direction:column;gap:8px}' +
        '#sc-lyr-page .ms label{font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.4px;color:var(--sc-muted,rgba(255,255,255,.65))}' +
        '#sc-lyr-page .ms input{width:100%;box-sizing:border-box;padding:7px 10px;border-radius:8px;border:1px solid var(--sc-border,rgba(255,255,255,.18));background:var(--sc-bg,rgba(0,0,0,.35));color:var(--sc-text,#fff);font:13px Segoe UI,Arial,sans-serif}' +
        '#sc-lyr-page .stage{position:relative;flex:1;display:flex;flex-direction:column;min-width:0;min-height:0}' +
        '#sc-lyr-page .pb{position:relative;flex:1;overflow-y:auto;padding:38vh 10% 38vh 8%;scroll-behavior:smooth;scrollbar-width:none;-webkit-mask-image:linear-gradient(transparent,#000 14%,#000 86%,transparent)}' +
        '#sc-lyr-page .pb::-webkit-scrollbar{display:none}' +
        '#sc-lyr-page .l{font-size:clamp(22px,3vw,36px);font-weight:800;line-height:1.35;margin:18px 0;opacity:.38;transition:all .28s ease;cursor:pointer;transform-origin:left center}' +
        '#sc-lyr-page .l:hover{opacity:.75}' +
        '#sc-lyr-page .l.past{opacity:.22}' +
        '#sc-lyr-page .l.cur{opacity:1;color:var(--sc-accent,#ff5500);transform:scale(1.04)}' +
        '#sc-lyr-page .p{font-size:19px;font-weight:500;line-height:1.75;white-space:pre-wrap;opacity:.92;max-width:760px;margin:0 auto}' +
        '#sc-lyr-page .pm{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;color:var(--sc-muted,rgba(255,255,255,.65));font-size:18px;pointer-events:none}' +
        'html.sc-desktop-mini #sc-lyr,html.sc-desktop-mini #sc-lyr-page{display:none!important}';
    (document.head || document.documentElement).appendChild(css);

    var panel, body, head, msg, page, pbody, pmsg, pArt, pTt, pAr, pTm, pPill, pAi, pTi, pPp;
    function el(tag, cls, parent, txt) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (txt) e.textContent = txt;
        if (parent) parent.appendChild(e);
        return e;
    }
    function seekTo(sec) {
        var i = info();
        if (!i.duration || isNaN(i.duration) || i.duration <= 0) return;
        var bar = $('.playbackTimeline__progressWrapper');
        if (!bar) return;
        var rect = bar.getBoundingClientRect();
        if (!rect || rect.width <= 0) return;
        var ratio = Math.max(0, Math.min(1, sec / i.duration));
        var x = rect.left + rect.width * ratio, y = rect.top + rect.height / 2;
        ['mousedown', 'mouseup', 'click'].forEach(function (ev) {
            bar.dispatchEvent(new MouseEvent(ev, { bubbles: true, cancelable: true, clientX: x, clientY: y }));
        });
    }
    function build() {
        if (panel || !document.body) return;
        panel = document.createElement('div'); panel.id = 'sc-lyr';
        head = document.createElement('div'); head.className = 'h';
        var ht = document.createElement('span'); ht.id = 'sc-lyr-t'; head.appendChild(ht);
        var pg = el('button', 'pg', head, '\u0421\u0442\u0440\u0430\u043d\u0438\u0446\u0430 \u0442\u0435\u043a\u0441\u0442\u0430');
        pg.type = 'button'; pg.title = '\u041e\u0442\u043a\u0440\u044b\u0442\u044c \u0441\u0442\u0440\u0430\u043d\u0438\u0446\u0443 \u0442\u0435\u043a\u0441\u0442\u0430 \u043f\u0435\u0441\u043d\u0438';
        pg.onclick = function () { openPage(true); };
        var x = document.createElement('b'); x.textContent = '\u00d7'; x.onclick = function () { toggle(false); }; head.appendChild(x);
        body = document.createElement('div'); body.className = 'b';
        msg = document.createElement('div'); msg.className = 'm';
        panel.appendChild(head); panel.appendChild(body); panel.appendChild(msg);
        page = document.createElement('div'); page.id = 'sc-lyr-page';
        var top = el('div', 'top', page);
        var back = el('button', 'btn', top, '\u2190 \u041d\u0430\u0437\u0430\u0434 \u043a SoundCloud'); back.type = 'button';
        back.onclick = function () { openPage(false); };
        pPill = el('span', 'pill', top, 'LRCLIB'); pPill.style.display = 'none';
        el('span', 'sp', top);
        var gen = el('button', 'btn', top, '\u041d\u0430\u0439\u0442\u0438 \u043d\u0430 Genius'); gen.type = 'button';
        gen.onclick = function () {
            var i = info();
            var q = ((pAi && pAi.value.trim()) || i.artist || '') + ' ' + ((pTi && pTi.value.trim()) || i.title || '');
            send({ type: 'openExternal', url: 'https://genius.com/search?q=' + encodeURIComponent(q.trim() || 'SoundCloud') });
        };
        var cfg = el('button', 'btn', top, '\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438'); cfg.type = 'button';
        cfg.onclick = function () { send({ type: 'openLyricsSettings' }); };
        var main = el('div', 'main', page);
        var hero = el('div', 'hero', main);
        pArt = el('div', 'art', hero, '\u266B');
        pTt = el('div', 'tt', hero, '\u041d\u0435\u0442 \u0430\u043a\u0442\u0438\u0432\u043d\u043e\u0433\u043e \u0442\u0440\u0435\u043a\u0430');
        pAr = el('div', 'ar', hero, 'SoundCloud');
        pTm = el('div', 'tm', hero, '0:00 / 0:00');
        pPp = el('button', 'btn', hero, 'Play / Pause'); pPp.type = 'button';
        pPp.onclick = function () { var b = $('.playControl'); if (b) b.click(); };
        var ms = el('div', 'ms', hero);
        el('label', '', ms, '\u0412\u0440\u0443\u0447\u043d\u0443\u044e \u043d\u0430\u0439\u0442\u0438 \u0442\u0435\u043a\u0441\u0442');
        pAi = el('input', '', ms); pAi.placeholder = '\u0418\u0441\u043f\u043e\u043b\u043d\u0438\u0442\u0435\u043b\u044c';
        pTi = el('input', '', ms); pTi.placeholder = '\u041d\u0430\u0437\u0432\u0430\u043d\u0438\u0435 \u0442\u0440\u0435\u043a\u0430';
        var mgo = el('button', 'btn', ms, '\u041d\u0430\u0439\u0442\u0438 \u0442\u0435\u043a\u0441\u0442'); mgo.type = 'button';
        mgo.onclick = function () {
            var a = (pAi.value || '').trim(), t = (pTi.value || '').trim();
            if (!t) return;
            var i = info();
            S.manual = { artist: a, title: t, forKey: i.artist + '|' + i.title };
            S.key = ''; tick();
        };
        var stg = el('div', 'stage', main);
        pbody = el('div', 'pb', stg);
        pmsg = el('div', 'pm', stg);
        document.body.appendChild(panel); document.body.appendChild(page);
        notifyState();
    }
    function notifyState() {
        send({ type: 'lyricsState', open: !!(S.on || S.pageOn) });
    }
    function toggle(v) {
        build(); if (!panel) return;
        var now = Date.now();
        if (typeof v !== 'boolean') {
            if (now - S.lastToggle < 160) return;
            S.lastToggle = now;
            if (S.pageOn) {
                S.pageOn = false; if (page) page.classList.remove('on');
                S.on = false; panel.classList.remove('on');
                notifyState();
                return;
            }
        }
        S.on = (typeof v === 'boolean') ? v : !S.on;
        panel.classList.toggle('on', S.on);
        notifyState();
        if (S.on) { S.key = ''; tick(); }
    }
    function openPage(v) {
        build(); if (!page) return;
        S.pageOn = (typeof v === 'boolean') ? v : !S.pageOn;
        page.classList.toggle('on', S.pageOn);
        notifyState();
        if (S.pageOn) { if (!S.lines && !S.plain) S.key = ''; tick(); highlight(pos(), true); }
    }
    window.__scLyricsToggle = function (v) { toggle(v); };
    window.__scLyricsOpenPage = function () { openPage(true); };
    S.reload = function () { if (S.on || S.pageOn) { S.key = ''; tick(); } };
    function setMsg(t) { if (msg) msg.textContent = t || ''; if (pmsg) pmsg.textContent = t || ''; }
    function clear() { if (body) body.innerHTML = ''; if (pbody) pbody.innerHTML = ''; S.lines = null; S.plain = null; S.idx = -2; }

    function parseLrc(txt) {
        var out = [], re = /\[(\d+):(\d+(?:[.:]\d+)?)\]/g;
        txt.split(/\r?\n/).forEach(function (ln) {
            var times = [], m, last = 0;
            re.lastIndex = 0;
            while ((m = re.exec(ln)) !== null) { times.push(parseInt(m[1], 10) * 60 + parseFloat(m[2].replace(':', '.'))); last = re.lastIndex; }
            if (!times.length) return;
            var text = ln.slice(last).trim();
            times.forEach(function (t) { out.push({ t: t, s: text }); });
        });
        out.sort(function (a, b) { return a.t - b.t; });
        return out;
    }
    function render() {
        body.innerHTML = '';
        if (pbody) pbody.innerHTML = '';
        if (S.lines && S.lines.length) {
            S.lines.forEach(function (l) {
                var d = document.createElement('div'); d.className = 'l'; d.textContent = l.s || '\u266A';
                d.onclick = function () { seekTo(l.t); };
                body.appendChild(d); l.el = d;
                if (pbody) {
                    var pd = document.createElement('div'); pd.className = 'l'; pd.textContent = l.s || '\u266A';
                    pd.onclick = function () { seekTo(l.t); };
                    pbody.appendChild(pd); l.pel = pd;
                }
            });
            setMsg('');
        } else if (S.plain) {
            var p = document.createElement('div'); p.className = 'p'; p.textContent = S.plain; body.appendChild(p);
            if (pbody) { var pp = document.createElement('div'); pp.className = 'p'; pp.textContent = S.plain; pbody.appendChild(pp); }
            setMsg('');
        }
    }
    S.onData = function (key, r) {
        if (key !== S.key) return;
        clear();
        if (!r || !r.found) { S.state = 'none'; if (pPill) pPill.style.display = 'none'; setMsg('No lyrics found'); return; }
        if (r.synced) { S.lines = parseLrc(r.synced); if (!S.lines.length) S.lines = null; }
        if (!S.lines && r.plain) S.plain = r.plain;
        if (!S.lines && !S.plain) { S.state = 'none'; if (pPill) pPill.style.display = 'none'; setMsg('No lyrics found'); return; }
        S.state = S.lines ? 'synced' : 'plain';
        if (r.source) {
            S.source = r.source;
            var ht = document.getElementById('sc-lyr-t');
            if (ht && ht.textContent.indexOf(' \u2022 ') === -1) ht.textContent += ' \u2022 ' + r.source;
            if (pPill) { pPill.textContent = r.source; pPill.style.display = ''; }
        } else if (pPill) {
            pPill.style.display = 'none';
        }
        render();
    };
    function highlight(p, force) {
        if (S.state !== 'synced' || !S.lines) return;
        var idx = -1, L = S.lines;
        for (var i = 0; i < L.length; i++) { if (L[i].t <= p + 0.15) idx = i; else break; }
        if (idx === S.idx && !force) return;
        S.idx = idx;
        for (var j = 0; j < L.length; j++) {
            var cls = 'l' + (j < idx ? ' past' : (j === idx ? ' cur' : ''));
            if (L[j].el) L[j].el.className = cls;
            if (L[j].pel) L[j].pel.className = cls;
        }
        if (idx >= 0) {
            if (S.on && L[idx].el) {
                var e = L[idx].el;
                body.scrollTo({ top: e.offsetTop - body.clientHeight / 2 + e.clientHeight / 2, behavior: 'smooth' });
            }
            if (S.pageOn && pbody && L[idx].pel) {
                var pe = L[idx].pel;
                pbody.scrollTo({ top: pe.offsetTop - pbody.clientHeight / 2 + pe.clientHeight / 2, behavior: 'smooth' });
            }
        }
    }
    function tick() {
        if (!S.on && !S.pageOn) return;
        var i = info(), p = pos();
        var autoKey = i.artist + '|' + i.title;
        if (S.manual && S.manual.forKey !== autoKey) S.manual = null;
        if (S.pageOn) {
            if (pTt) pTt.textContent = i.title || '\u041d\u0435\u0442 \u0430\u043a\u0442\u0438\u0432\u043d\u043e\u0433\u043e \u0442\u0440\u0435\u043a\u0430';
            if (pAr) pAr.textContent = i.artist || 'SoundCloud';
            if (pTm) pTm.textContent = fmtT(p) + ' / ' + (i.durationText || fmtT(i.duration));
            if (pPp) pPp.textContent = S.playing ? '\u23F8 Pause' : '\u25B6 Play';
            if (pArt) {
                if (i.artwork) { pArt.style.backgroundImage = 'url(\'' + i.artwork.replace(/'/g, '%27') + '\')'; pArt.textContent = ''; }
                else { pArt.style.backgroundImage = 'none'; pArt.textContent = '\u266B'; }
            }
        }
        var qArtist = S.manual ? S.manual.artist : i.artist;
        var qTitle = S.manual ? S.manual.title : i.title;
        if (!qTitle) { if (S.key !== '') { S.key = ''; clear(); } setMsg('Play a track to see lyrics'); return; }
        var key = (S.manual ? 'm:' : '') + qArtist + '|' + qTitle;
        if (key !== S.key) {
            S.key = key; S.autoKey = autoKey; clear(); S.state = 'loading'; setMsg('Loading lyrics\u2026');
            if (pPill) pPill.style.display = 'none';
            if (pAi && document.activeElement !== pAi) pAi.value = qArtist || '';
            if (pTi && document.activeElement !== pTi) pTi.value = qTitle || '';
            var ht = document.getElementById('sc-lyr-t'); if (ht) ht.textContent = (qArtist ? qArtist + ' \u2014 ' : '') + qTitle;
            send({ type: 'lyricsRequest', key: key, artist: qArtist, title: qTitle, duration: isNaN(i.duration) ? 0 : i.duration });
        }
        highlight(p, false);
    }
    setInterval(tick, 120);

    window.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && S.pageOn) {
            e.preventDefault(); openPage(false); return;
        }
        if ((e.ctrlKey || e.metaKey) && !e.shiftKey && !e.altKey && (e.key || '').toLowerCase() === 'y') {
            e.preventDefault(); toggle();
        }
    }, true);
    function init() { build(); }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();
";
    }
}
