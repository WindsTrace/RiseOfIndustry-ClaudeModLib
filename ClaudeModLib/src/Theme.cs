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
        public ThemeFill() { }
        public ThemeFill(string color) { this.color = color; }
    }

    /// <summary>Contents of ClaudeModLib/config/theme.json. Colours are "#RRGGBB" or "#RRGGBBAA".</summary>
    public class ThemeData
    {
        public string name = "Default (Rise of Industry green)";
        public string[] fonts = { "Microsoft YaHei", "SimHei", "Arial" };
        public int fontSize = 14;
        public int titleBarHeight = 26;

        public ThemeFill window = new ThemeFill("#1C2B21F0");
        public ThemeFill titleBar = new ThemeFill("#3E5E3AFF");
        public ThemeFill box = new ThemeFill("#FFFFFF10");
        public ThemeFill button = new ThemeFill("#3E5E3AFF");
        public ThemeFill buttonHover = new ThemeFill("#527A4CFF");
        public ThemeFill buttonActive = new ThemeFill("#2E472BFF");
        public ThemeFill progressBackground = new ThemeFill("#00000080");

        public string titleText = "#F4ECD6FF";
        public string text = "#EDE6D3FF";
        public string subtext = "#B8B29FFF";
        public string header = "#E9C46AFF";
        public string buttonText = "#F4ECD6FF";
        public string switchOn = "#3F9C4BFF";
        public string switchOff = "#A63D35FF";
        public string switchKnob = "#F4ECD6FF";
        public string good = "#8BD17CFF";
        public string bad = "#F07B6EFF";
        public string warn = "#F2C14EFF";
        public string info = "#9CC8F0FF";
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
            return Solid(Col(f.color, Color.clear));
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
