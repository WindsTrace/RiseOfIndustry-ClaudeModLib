using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClaudeModLib
{
    /// <summary>A draggable IMGUI window owned by a registered mod (see ModHandle.AddWindow).</summary>
    public class LibWindow
    {
        static int _nextId = 0x434D4C00;
        public readonly int id = _nextId++;
        public string title;
        public Rect rect;
        public readonly Action<LibWindow> draw;
        public readonly Feature visibleWhen;
        public readonly bool alsoInMenus;
        public Vector2 scroll;

        internal LibWindow(string title, Rect rect, Action<LibWindow> draw, Feature visibleWhen, bool alsoInMenus)
        { this.title = title; this.rect = rect; this.draw = draw; this.visibleWhen = visibleWhen; this.alsoInMenus = alsoInMenus; }

        public bool visible { get { return (visibleWhen == null || visibleWhen.enabled) && (alsoInMenus || ModLib.inGame); } }
    }

    /// <summary>Shared IMGUI styles and widgets (CJK-capable font, progress bar, colours).</summary>
    public static class Ui
    {
        public static GUIStyle label, small, bold, header, button;
        public static readonly Color Green = new Color(0.5f, 1f, 0.5f), Yellow = new Color(1f, 0.85f, 0.2f),
            Orange = new Color(1f, 0.5f, 0.3f), Blue = new Color(0.6f, 0.8f, 1f), Grey = new Color(0.8f, 0.8f, 0.8f);
        static GUIStyle _barBg, _barFill;
        static Font _font;

        /// <summary>UI scale relative to 1080p (windows are drawn in 1080p coordinates).</summary>
        public static float scale { get { return Mathf.Max(1f, Screen.height / 1080f); } }

        internal static void EnsureStyles()
        {
            if (label != null) return;
            try { _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 14); } catch { _font = null; }
            label = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            small.normal.textColor = Grey;
            bold = new GUIStyle(label) { fontStyle = FontStyle.Bold };
            header = new GUIStyle(label) { fontSize = 16, fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { fontSize = 13 };
            if (_font != null)
            {
                foreach (var s in new[] { label, small, bold, header, button }) s.font = _font;
                GUI.skin.window.font = _font; GUI.skin.toggle.font = _font; GUI.skin.box.font = _font;
            }
            _barBg = new GUIStyle(GUI.skin.box); _barBg.normal.background = Solid(new Color(0.15f, 0.15f, 0.15f, 0.9f));
            _barFill = new GUIStyle(GUI.skin.box); _barFill.normal.background = Solid(Color.white);
        }

        /// <summary>Horizontal progress bar filling the layout width.</summary>
        public static void ProgressBar(float value01, Color color, float height = 14)
        {
            var bar = GUILayoutUtility.GetRect(100, height, GUILayout.ExpandWidth(true));
            GUI.Box(bar, GUIContent.none, _barBg);
            var fill = new Rect(bar.x + 1, bar.y + 1, Mathf.Max(0, (bar.width - 2) * Mathf.Clamp01(value01)), bar.height - 2);
            var old = GUI.backgroundColor; GUI.backgroundColor = color;
            if (fill.width > 0) GUI.Box(fill, GUIContent.none, _barFill);
            GUI.backgroundColor = old;
        }

        /// <summary>Label in a colour.</summary>
        public static void ColoredLabel(string text, Color color, params GUILayoutOption[] opts)
        {
            var old = GUI.contentColor; GUI.contentColor = color;
            GUILayout.Label(text, label, opts);
            GUI.contentColor = old;
        }

        /// <summary>1234567 -> "1.23M", 238400 -> "238.4k".</summary>
        public static string Number(int n)
        {
            if (n >= 1000000) return (n / 1000000f).ToString("0.00") + "M";
            if (n >= 1000) return (n / 1000f).ToString("0.0") + "k";
            return n.ToString();
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t;
        }
    }

    internal static class LibUi
    {
        internal static void Draw(List<ModHandle> mods)
        {
            bool anyWindow = LibPanel.visible || (ModLib.inGame && !Toolbar.installed);
            foreach (var m in mods) foreach (var w in m.windows) if (w.visible) anyWindow = true;
            if (!anyWindow) return;

            Ui.EnsureStyles();
            float s = Ui.scale;
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

            // Fallback launcher when the toolbar button could not be installed.
            if (ModLib.inGame && !Toolbar.installed)
            {
                float w = Screen.width / s;
                if (GUI.Button(new Rect(w - 110, 60, 100, 26), "Mods ▾", Ui.button)) LibPanel.visible = !LibPanel.visible;
            }

            foreach (var m in mods)
                foreach (var win in m.windows)
                    if (win.visible)
                    {
                        var captured = win;
                        win.rect = GUI.Window(win.id, win.rect, id =>
                        {
                            try { captured.draw(captured); }
                            catch (Exception e) { GUILayout.Label("error: " + e.Message, Ui.small); }
                            GUI.DragWindow(new Rect(0, 0, 10000, 22));
                        }, win.title);
                    }

            if (LibPanel.visible) LibPanel.Draw(mods);
            GUI.matrix = oldMatrix;
        }
    }

    /// <summary>The ClaudeModLib panel (toolbar button / hotkey): mod list, feature switches, dev tools.</summary>
    public static class LibPanel
    {
        public static bool visible;
        static Rect _rect = new Rect(300, 100, 520, 520);
        static Vector2 _scroll;
        static List<ModEntry> _entries;
        static float _nextRefresh;
        static string _status;

        internal static void Draw(List<ModHandle> mods)
        {
            _rect = GUI.Window(0x434D4BFF, _rect, id => DrawContent(mods), "ClaudeModLib " + ModLib.Version);
        }

        static void DrawContent(List<ModHandle> mods)
        {
            if (_entries == null || Time.unscaledTime > _nextRefresh) { _entries = Mods.List(); _nextRefresh = Time.unscaledTime + 2f; }
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("功能开关", Ui.header);
            foreach (var m in mods)
            {
                if (m.features.Count == 0) continue;
                GUILayout.Label(m.displayName, Ui.bold);
                foreach (var f in m.features)
                {
                    bool v = GUILayout.Toggle(f.enabled, " " + f.label, Ui.label);
                    if (v != f.enabled) f.enabled = v;
                    if (!string.IsNullOrEmpty(f.description)) GUILayout.Label("    " + f.description, Ui.small);
                }
            }

            GUILayout.Space(8);
            GUILayout.Label("Mod 列表（启用/禁用在下次启动游戏时生效）", Ui.header);
            foreach (var e in _entries)
            {
                GUILayout.BeginHorizontal();
                Ui.ColoredLabel(e.loaded ? "● 已加载" : "○ 未加载", e.loaded ? Ui.Green : Ui.Grey, GUILayout.Width(80));
                GUILayout.Label(e.name + "  " + (e.versionString ?? "") + (e.usesLib ? "  ★" : ""), Ui.label);
                GUILayout.FlexibleSpace();
                bool next = GUILayout.Toggle(e.enabledNextLaunch, " 下次启用", Ui.label, GUILayout.Width(90));
                if (next != e.enabledNextLaunch)
                {
                    if (e.name == ModLib.ModName && !next) _status = "ClaudeModLib 被禁用后，依赖它的 mod 也无法工作。";
                    Mods.SetEnabledNextLaunch(e.name, next);
                    _entries = Mods.List();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("★ = 使用 ClaudeModLib 的 mod", Ui.small);

            GUILayout.Space(8);
            GUILayout.Label("开发工具", Ui.header);
            if (GUILayout.Button("导出全部游戏资源名 → claudemodlib_asset_dump.txt", Ui.button))
            {
                try { _status = "已写入：" + DevTools.DumpAssets(); } catch (Exception ex) { _status = "导出失败：" + ex.Message; }
            }
            GUILayout.Label("面板快捷键：" + Hotkeys.Describe(Hotkeys.LibToggle) + "（可在游戏设置的按键绑定里修改）", Ui.small);
            if (_status != null) GUILayout.Label(_status, Ui.small);

            GUILayout.EndScrollView();
            if (GUILayout.Button("关闭", Ui.button)) visible = false;
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }
    }
}
