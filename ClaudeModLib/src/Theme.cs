using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ProjectAutomata;
using UnityEngine;

namespace ClaudeModLib
{
    /// <summary>One themed surface: a flat colour, or a game sprite (by name) multiplied by a tint.</summary>
    public class ThemeFill
    {
        public string color = "#00000000";
        /// <summary>Optional game Sprite name (see claudemodlib_ui_dump.txt). Empty = use <see cref="color"/>.</summary>
        public string sprite = "";
        /// <summary>Multiplied onto the sprite (many native UI sprites are white and tinted).</summary>
        public string tint = "#FFFFFFFF";
        /// <summary>Optional outline for flat colours (like the game's orange icon frames). Empty / 0 = none.</summary>
        public string borderColor = "";
        public int borderWidth = 0;
        public ThemeFill() { }
        public ThemeFill(string color) { this.color = color; }
        public ThemeFill(string color, string borderColor, int borderWidth) { this.color = color; this.borderColor = borderColor; this.borderWidth = borderWidth; }
    }

    /// <summary>Contents of ClaudeModLib/config/theme.json. Colours are "#RRGGBB" or "#RRGGBBAA".</summary>
    /// <summary>Defaults = the game's native look (cream panels, orange accents, dark-brown title bar).</summary>
    public class ThemeData
    {
        public string name = "Native (cream / orange)";
        /// <summary>Body text. Later names fill in missing glyphs (CJK).</summary>
        public string[] fonts = { "Bahnschrift SemiCondensed", "Bahnschrift", "Microsoft YaHei", "SimHei", "Arial" };
        /// <summary>Emphasis (titles, headers, numbers): a REAL semibold face, never Unity's synthetic bold.</summary>
        public string[] boldFonts = { "Bahnschrift SemiBold SemiConden", "Bahnschrift SemiBold", "Microsoft YaHei", "SimHei", "Arial" };
        public int fontSize = 14;
        public int titleBarHeight = 30;

        public ThemeFill window = new ThemeFill("#F1EBDCFF");
        public ThemeFill titleBar = new ThemeFill("#3A2B20FF");
        public ThemeFill sectionHeader = new ThemeFill("#C87A06FF");
        public ThemeFill box = new ThemeFill("#F7F2E7FF", "#C87A06FF", 2);
        public ThemeFill button = new ThemeFill("#C87A06FF");
        public ThemeFill buttonHover = new ThemeFill("#DC8E1CFF");
        public ThemeFill buttonActive = new ThemeFill("#A86504FF");
        public ThemeFill progressBackground = new ThemeFill("#9F9C91FF");

        public string titleText = "#F1EBDCFF";
        public string sectionHeaderText = "#F1EBDCFF";
        public string text = "#4A4036FF";
        public string subtext = "#86837BFF";
        public string header = "#C87A06FF";
        public string buttonText = "#F7F2E7FF";
        public string switchOn = "#5E8C31FF";
        public string switchOff = "#B5452FFF";
        public string switchKnob = "#F7F2E7FF";
        public string good = "#4E8A2EFF";
        public string bad = "#B3412EFF";
        public string warn = "#C87A06FF";
        public string info = "#3D7AA8FF";
    }

    /// <summary>
    /// Theme loader. Edit ClaudeModLib/config/theme.json (or call <see cref="Reload"/> / the panel button) to restyle
    /// every ClaudeModLib window: colours, game sprites with 9-slice borders, fonts. Missing file = built-in defaults.
    /// </summary>
    public static class Theme
    {
        public static ThemeData data { get; private set; }
        public static string filePath { get; private set; }
        public static string lastError { get; private set; }
        /// <summary>Raised after the theme was (re)loaded; styles are rebuilt on the next OnGUI.</summary>
        public static event Action reloaded;

        static readonly Dictionary<string, Texture2D> _texCache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Vector4> _borderCache = new Dictionary<string, Vector4>();

        internal static void Init(string modPath)
        {
            filePath = System.IO.Path.Combine(System.IO.Path.Combine(modPath, "config"), "theme.json");
            Reload();
        }

        /// <summary>Re-reads theme.json. On a parse error the previous theme stays and <see cref="lastError"/> is set.</summary>
        public static void Reload()
        {
            lastError = null;
            var d = new ThemeData();
            try
            {
                if (filePath != null && File.Exists(filePath))
                    JsonConvert.PopulateObject(File.ReadAllText(filePath), d);
            }
            catch (Exception e) { lastError = e.Message; Log.Warn("theme.json: " + e.Message); if (data != null) return; }
            data = d;
            foreach (var t in _texCache.Values) if (t != null) UnityEngine.Object.Destroy(t);
            _texCache.Clear(); _borderCache.Clear();
            Ui.InvalidateStyles();
            var h = reloaded; if (h != null) h();
            Log.Info("theme loaded: " + d.name + (filePath != null && File.Exists(filePath) ? "" : " (defaults)"));
        }

        /// <summary>Writes the current theme (or defaults) to theme.json so it can be edited.</summary>
        public static void SaveCurrent()
        {
            if (filePath == null) return;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, JsonConvert.SerializeObject(data ?? new ThemeData(), Formatting.Indented));
        }

        public static Color Col(string hex, Color fallback)
        {
            Color c;
            return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c) ? c : fallback;
        }

        /// <summary>Texture for a fill (cached) and its 9-slice border (left, bottom, right, top) in pixels.</summary>
        internal static Texture2D Texture(ThemeFill f, out RectOffset border)
        {
            border = new RectOffset(0, 0, 0, 0);
            if (f == null) return Solid(Color.clear);
            if (!string.IsNullOrEmpty(f.sprite))
            {
                string key = f.sprite + "|" + f.tint;
                Texture2D tex;
                Vector4 b;
                if (!_texCache.TryGetValue(key, out tex))
                {
                    var sprite = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s != null && s.name == f.sprite);
                    tex = sprite != null ? CopySprite(sprite, Col(f.tint, Color.white)) : null;
                    _texCache[key] = tex;
                    _borderCache[key] = sprite != null ? sprite.border : Vector4.zero;
                    if (sprite == null) Log.Warn("theme sprite not loaded (yet?): " + f.sprite + " - using colour");
                }
                if (tex != null)
                {
                    b = _borderCache[key];
                    border = new RectOffset((int)b.x, (int)b.z, (int)b.w, (int)b.y);
                    return tex;
                }
            }
            if (f.borderWidth > 0 && !string.IsNullOrEmpty(f.borderColor))
            {
                int bw = Mathf.Clamp(f.borderWidth, 1, 8);
                border = new RectOffset(bw, bw, bw, bw);
                return Bordered(Col(f.color, Color.clear), Col(f.borderColor, Color.clear), bw);
            }
            return Solid(Col(f.color, Color.clear));
        }

        /// <summary>A 9-sliceable texture: fill colour with a <paramref name="bw"/>-pixel outline.</summary>
        internal static Texture2D Bordered(Color fill, Color line, int bw)
        {
            string key = "B" + ColorUtility.ToHtmlStringRGBA(fill) + ColorUtility.ToHtmlStringRGBA(line) + bw;
            Texture2D t;
            if (_texCache.TryGetValue(key, out t) && t != null) return t;
            int n = bw * 2 + 2;
            t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            for (int x = 0; x < n; x++)
                for (int y = 0; y < n; y++)
                    t.SetPixel(x, y, x < bw || y < bw || x >= n - bw || y >= n - bw ? line : fill);
            t.Apply();
            t.hideFlags = HideFlags.DontSave;
            _texCache[key] = t;
            return t;
        }

        internal static Texture2D Solid(Color c)
        {
            string key = "#" + ColorUtility.ToHtmlStringRGBA(c);
            Texture2D t;
            if (_texCache.TryGetValue(key, out t) && t != null) return t;
            t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c); t.Apply();
            t.hideFlags = HideFlags.DontSave;
            _texCache[key] = t;
            return t;
        }

        /// <summary>Copies a (possibly atlased, non-readable) sprite into its own readable texture, multiplied by tint.</summary>
        static Texture2D CopySprite(Sprite s, Color tint)
        {
            var src = s.texture;
            var r = s.textureRect;
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var tex = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
                if (tint != Color.white)
                {
                    var px = tex.GetPixels();
                    for (int i = 0; i < px.Length; i++) px[i] *= tint;
                    tex.SetPixels(px);
                }
                tex.Apply();
                tex.hideFlags = HideFlags.DontSave;
                return tex;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
