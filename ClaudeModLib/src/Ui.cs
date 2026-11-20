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
        public LocText title;
        public Rect rect;
        public readonly Action<LibWindow> draw;
        public readonly Feature visibleWhen;
        public readonly bool alsoInMenus;
        public Vector2 scroll;

        internal LibWindow(LocText title, Rect rect, Action<LibWindow> draw, Feature visibleWhen, bool alsoInMenus)
        { this.title = title; this.rect = rect; this.draw = draw; this.visibleWhen = visibleWhen; this.alsoInMenus = alsoInMenus; }

        public bool visible { get { return (visibleWhen == null || visibleWhen.enabled) && (alsoInMenus || ModLib.inGame); } }
    }

    /// <summary>Shared IMGUI styles and widgets (CJK-capable font, progress bar, colours).</summary>
    public static class Ui
    {
        public static GUIStyle label, small, bold, header, button;
        public static readonly Color Green = new Color(0.5f, 1f, 0.5f), Yellow = new Color(1f, 0.85f, 0.2f),
            Orange = new Color(1f, 0.5f, 0.3f), Blue = new Color(0.6f, 0.8f, 1f), Grey = new Color(0.8f, 0.8f, 0.8f),
            Red = new Color(1f, 0.45f, 0.45f);
        static readonly Color SwitchOn = new Color(0.20f, 0.62f, 0.28f), SwitchOff = new Color(0.62f, 0.20f, 0.20f);
        static GUIStyle _barBg, _barFill, _switchText, _clickLabel;
        static Texture2D _white;
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
            _white = Solid(Color.white);
            _switchText = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 11 };
            _switchText.normal.textColor = Color.white;
            _clickLabel = new GUIStyle(label) { alignment = TextAnchor.MiddleLeft };
            _clickLabel.hover.textColor = Color.white;
        }

        /// <summary>
        /// On/off switch: a green "ON" / red "OFF" slider followed by a label coloured the same way.
        /// Clicking the switch or the label flips it. Returns the new value.
        /// <code>f.enabled = Ui.Switch(f.enabled, "Show window");</code>
        /// </summary>
        public static bool Switch(bool value, string text, params GUILayoutOption[] labelOptions)
        {
            EnsureStyles();
            GUILayout.BeginHorizontal();
            var r = GUILayoutUtility.GetRect(54, 22, GUILayout.Width(54), GUILayout.Height(22));
            Fill(r, new Color(0f, 0f, 0f, 0.55f));
            Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), value ? SwitchOn : SwitchOff);
            var knob = new Rect(value ? r.xMax - 20 : r.x + 2, r.y + 2, 18, r.height - 4);
            Fill(knob, new Color(0.95f, 0.95f, 0.95f));
            var stateRect = value ? new Rect(r.x + 2, r.y, r.width - 22, r.height) : new Rect(r.x + 20, r.y, r.width - 22, r.height);
            GUI.Label(stateRect, value ? Lang.T("ON", "开") : Lang.T("OFF", "关"), _switchText);
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);

            GUILayout.Space(6);
            var old = GUI.contentColor; GUI.contentColor = value ? Green : Red;
            var opts = labelOptions != null && labelOptions.Length > 0 ? labelOptions : new[] { GUILayout.Height(22) };
            if (GUILayout.Button(text, _clickLabel, opts)) clicked = true;
            GUI.contentColor = old;
            GUILayout.EndHorizontal();
            return clicked ? !value : value;
        }

        static void Fill(Rect r, Color c)
        {
            var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, _white); GUI.color = o;
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
                if (GUI.Button(new Rect(w - 110, 60, 100, 26), Lang.T("Mods ▾", "模组 ▾"), Ui.button)) LibPanel.visible = !LibPanel.visible;
            }

            foreach (var m in mods)
                foreach (var win in m.windows)
                    if (win.visible)
                    {
                        var captured = win;
                        win.rect = GUI.Window(win.id, win.rect, id =>
                        {
                            try { captured.draw(captured); }
                            catch (Exception e) { GUILayout.Label(Lang.T("error: ", "出错：") + e.Message, Ui.small); }
                            GUI.DragWindow(new Rect(0, 0, 10000, 22));
                        }, win.title != null ? win.title.text : "");
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

        static string Txt(LocText t) { return t != null ? t.text : null; }

        static void LangButton(LangMode m, string text)
        {
            var old = GUI.backgroundColor;
            if (Lang.mode == m) GUI.backgroundColor = Ui.Green;
            if (GUILayout.Button(text, Ui.button)) Lang.mode = m;
            GUI.backgroundColor = old;
        }

        static void DrawContent(List<ModHandle> mods)
        {
            if (_entries == null || Time.unscaledTime > _nextRefresh) { _entries = Mods.List(); _nextRefresh = Time.unscaledTime + 2f; }
            _scroll = GUILayout.BeginScrollView(_scroll);

            // Language
            GUILayout.BeginHorizontal();
            GUILayout.Label(Lang.T("Language", "语言") + ":", Ui.bold, GUILayout.Width(80));
            LangButton(LangMode.Auto, Lang.T("Auto (game)", "自动（跟随游戏）"));
            LangButton(LangMode.English, "English");
            LangButton(LangMode.ChineseSimplified, "简体中文");
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            GUILayout.Label(Lang.T("Features", "功能开关"), Ui.header);
            foreach (var m in mods)
            {
                if (m.features.Count == 0) continue;
                GUILayout.Label(Txt(m.displayName), Ui.bold);
                foreach (var f in m.features)
                {
                    bool v = Ui.Switch(f.enabled, Txt(f.label));
                    if (v != f.enabled) f.enabled = v;
                    var desc = Txt(f.description);
                    if (!string.IsNullOrEmpty(desc)) GUILayout.Label("              " + desc, Ui.small);
                }
            }

            GUILayout.Space(8);
            GUILayout.Label(Lang.T("Mods (enable/disable applies on next game start)", "Mod 列表（启用/禁用在下次启动游戏时生效）"), Ui.header);
            foreach (var e in _entries)
            {
                GUILayout.BeginHorizontal();
                Ui.ColoredLabel(e.loaded ? Lang.T("● loaded", "● 已加载") : Lang.T("○ not loaded", "○ 未加载"),
                    e.loaded ? Ui.Green : Ui.Grey, GUILayout.Width(95), GUILayout.Height(22));
                GUILayout.Label(e.name + "  " + (e.versionString ?? "") + (e.usesLib ? "  ★" : ""), Ui.label, GUILayout.Height(22));
                GUILayout.FlexibleSpace();
                bool next = Ui.Switch(e.enabledNextLaunch, Lang.T("next start", "下次启用"), GUILayout.Width(80), GUILayout.Height(22));
                if (next != e.enabledNextLaunch)
                {
                    if (e.name == ModLib.ModName && !next)
                        _status = Lang.T("Without ClaudeModLib, mods that depend on it will not work.", "ClaudeModLib 被禁用后，依赖它的 mod 也无法工作。");
                    Mods.SetEnabledNextLaunch(e.name, next);
                    _entries = Mods.List();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(Lang.T("★ = uses ClaudeModLib", "★ = 使用 ClaudeModLib 的 mod"), Ui.small);

            GUILayout.Space(8);
            GUILayout.Label(Lang.T("Developer tools", "开发工具"), Ui.header);
            if (GUILayout.Button(Lang.T("Dump all game asset names → claudemodlib_asset_dump.txt", "导出全部游戏资源名 → claudemodlib_asset_dump.txt"), Ui.button))
            {
                try { _status = Lang.T("Written: ", "已写入：") + DevTools.DumpAssets(); }
                catch (Exception ex) { _status = Lang.T("Dump failed: ", "导出失败：") + ex.Message; }
            }
            GUILayout.Label(Lang.T("Panel hotkey: ", "面板快捷键：") + Hotkeys.Describe(Hotkeys.LibToggle)
                + Lang.T(" (rebind it in the game's key-binding settings)", "（可在游戏设置的按键绑定里修改）"), Ui.small);
            if (_status != null) GUILayout.Label(_status, Ui.small);

            GUILayout.EndScrollView();
            if (GUILayout.Button(Lang.T("Close", "关闭"), Ui.button)) visible = false;
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }
    }
}
