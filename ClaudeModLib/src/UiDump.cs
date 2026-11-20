using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SVGImporter;
using UnityEngine;
using UnityEngine.UI;

namespace ClaudeModLib
{
    public static partial class DevTools
    {
        /// <summary>Default path of the UI resource dump (same folder as output_log.txt).</summary>
        public static string DefaultUiDumpPath { get { return System.IO.Path.Combine(Application.persistentDataPath, "claudemodlib_ui_dump.txt"); } }

        /// <summary>
        /// Writes everything needed to theme mod UI after the game's look:
        ///  1) all loaded Sprites (name, size, 9-slice border, atlas texture) - usable in theme.json by name;
        ///  2) Fonts and TextMeshPro font assets;
        ///  3) every visible native UI element (path, Image sprite + colour, text font/size/colour).
        /// Run it inside a game with the native windows you like open (building panel, research...).
        /// </summary>
        public static string DumpUiResources(string path = null)
        {
            path = path ?? DefaultUiDumpPath;
            var sb = new StringBuilder();
            sb.AppendLine("# ClaudeModLib UI dump " + DateTime.Now + "  screen " + Screen.width + "x" + Screen.height);

            var sprites = Resources.FindObjectsOfTypeAll<Sprite>().Where(s => s != null).OrderBy(s => s.name).ToList();
            sb.AppendLine();
            sb.AppendLine("=== Sprites (" + sprites.Count + ")  name | size | border(L,B,R,T) | texture");
            foreach (var s in sprites)
            {
                var b = s.border;
                sb.AppendLine(string.Format("- {0} | {1}x{2} | {3},{4},{5},{6} | {7}", s.name, (int)s.rect.width, (int)s.rect.height,
                    (int)b.x, (int)b.y, (int)b.z, (int)b.w, s.texture != null ? s.texture.name : "null"));
            }

            sb.AppendLine();
            sb.AppendLine("=== Fonts");
            foreach (var f in Resources.FindObjectsOfTypeAll<Font>().OrderBy(f => f.name)) sb.AppendLine("- " + f.name + (f.dynamic ? " (dynamic)" : ""));
            sb.AppendLine("=== TextMeshPro fonts");
            foreach (var f in Resources.FindObjectsOfTypeAll<TMPro.TMP_FontAsset>().OrderBy(f => f.name)) sb.AppendLine("- " + f.name);

            sb.AppendLine();
            sb.AppendLine("=== Visible native UI (path | component | sprite/font | colour)");
            int count = 0;
            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).OrderBy(c => c.name))
            {
                sb.AppendLine("## Canvas " + canvas.name + " (sortingOrder " + canvas.sortingOrder + ")");
                foreach (var g in canvas.GetComponentsInChildren<Graphic>(false))
                {
                    if (++count > 6000) { sb.AppendLine("... truncated"); break; }
                    string what, col = ColorHex(g.color);
                    var img = g as Image;
                    var svg = g as SVGImage;
                    var txt = g as TMPro.TMP_Text;
                    if (img != null) what = "Image | " + (img.sprite != null ? img.sprite.name : "(none)") + " " + img.type;
                    else if (svg != null) what = "SVGImage | " + (svg.vectorGraphics != null ? svg.vectorGraphics.name : "(none)");
                    else if (txt != null) what = "Text | " + (txt.font != null ? txt.font.name : "?") + " " + txt.fontSize + "pt \"" + Short(txt.text) + "\"";
                    else what = g.GetType().Name;
                    sb.AppendLine(PathOf(g.transform, canvas.transform) + " | " + what + " | " + col);
                }
            }

            File.WriteAllText(path, sb.ToString());
            Log.Info("UI dump written: " + path);
            return path;
        }

        /// <summary>"#RRGGBBAA" for a colour (same format theme.json uses).</summary>
        public static string ColorHex(Color c) { return "#" + ColorUtility.ToHtmlStringRGBA(c); }

        static string Short(string s)
        {
            if (s == null) return "";
            s = s.Replace("\n", " ");
            return s.Length > 40 ? s.Substring(0, 40) + "…" : s;
        }

        static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (; t != null && t != root; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }
    }
}
