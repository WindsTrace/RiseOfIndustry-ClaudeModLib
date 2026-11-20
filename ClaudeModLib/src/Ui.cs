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

    /// <summary>
    /// Shared IMGUI styles and widgets, all built from <see cref="Theme"/> (config/theme.json):
    /// themed windows with a title bar, buttons, boxes, switches, progress bars, CJK-capable font.
    /// </summary>
    public static class Ui
    {
        public static GUIStyle label, small, bold, header, button, box;
        /// <summary>Theme colours: Green = good, Red = bad, Yellow/Orange = warning, Blue = info, Grey = subtext.</summary>
        public static Color Green { get { return C(T.good, new Color(0.5f, 1f, 0.5f)); } }
        public static Color Red { get { return C(T.bad, new Color(1f, 0.45f, 0.45f)); } }
        public static Color Yellow { get { return C(T.warn, new Color(1f, 0.85f, 0.2f)); } }
        public static Color Orange { get { return Color.Lerp(Yellow, Red, 0.5f); } }
        public static Color Blue { get { return C(T.info, new Color(0.6f, 0.8f, 1f)); } }
        public static Color Grey { get { return C(T.subtext, new Color(0.8f, 0.8f, 0.8f)); } }

        /// <summary>Large accent number style (like the game's "91,426"), and the section-header text style.</summary>
        public static GUIStyle value, sectionText;
        static GUIStyle _window, _titleBar, _titleText, _barBg, _barFill, _switchText, _clickLabel, _section;
        static GUISkin _skin;
        static Texture2D _white;
        static Font _font, _boldFont;
        static string _fontKey;

        static ThemeData T { get { return Theme.data ?? new ThemeData(); } }
        static Color C(string hex, Color fallback) { return Theme.Col(hex, fallback); }

        /// <summary>UI scale relative to 1080p (windows are drawn in 1080p coordinates).</summary>
        public static float scale { get { return Mathf.Max(1f, Screen.height / 1080f); } }

        internal static void InvalidateStyles() { label = null; _colored.Clear(); }

        static GUISkin _baseSkin;

        internal static void EnsureStyles()
        {
            if (label != null) return;
            // Always derive from Unity's default skin, even if our themed skin is active right now.
            if (_baseSkin == null) _baseSkin = GUI.skin;
            var activeSkin = GUI.skin;
            GUI.skin = _baseSkin;
            try { BuildStyles(); } finally { GUI.skin = activeSkin; }
        }

        static void BuildStyles()
        {
            var t = T;
            string fontKey = string.Join(",", t.fonts ?? new string[0]) + "|" + string.Join(",", t.boldFonts ?? new string[0]) + "@" + t.fontSize;
            if (_font == null || _fontKey != fontKey)
            {
                try { _font = Font.CreateDynamicFontFromOSFont(t.fonts, Mathf.Max(8, t.fontSize)); } catch { _font = null; }
                try { _boldFont = t.boldFonts != null && t.boldFonts.Length > 0 ? Font.CreateDynamicFontFromOSFont(t.boldFonts, Mathf.Max(8, t.fontSize)) : null; }
                catch { _boldFont = null; }
                _fontKey = fontKey;
                try
                {
                    var installed = new HashSet<string>(Font.GetOSInstalledFontNames());
                    Func<string[], string> first = names =>
                    {
                        if (names != null) foreach (var n in names) if (installed.Contains(n)) return n;
                        return "(none found)";
                    };
                    Log.Info("fonts: body=" + first(t.fonts) + ", bold=" + first(t.boldFonts));
                }
                catch { }
            }
            int fs = Mathf.Max(8, t.fontSize);

            label = Text(GUI.skin.label, fs, t.text);
            small = Text(GUI.skin.label, fs - 2, t.subtext);
            bold = Strong(Text(GUI.skin.label, fs, t.text));
            header = Strong(Text(GUI.skin.label, fs + 1, t.header));

            RectOffset border;
            button = Text(GUI.skin.button, fs - 1, t.buttonText);
            button.normal.background = Theme.Texture(t.button, out border); button.border = border;
            button.hover.background = Theme.Texture(t.buttonHover, out border);
            button.active.background = Theme.Texture(t.buttonActive, out border);
            button.focused.background = button.normal.background;
            button.hover.textColor = button.active.textColor = button.focused.textColor = button.normal.textColor;

            box = new GUIStyle(GUI.skin.box);
            box.normal.background = Theme.Texture(t.box, out border); box.border = border;
            box.padding = new RectOffset(8, 8, 6, 6);

            _window = new GUIStyle(GUI.skin.window);
            _window.normal.background = _window.onNormal.background = Theme.Texture(t.window, out border);
            _window.border = border;
            _window.padding = new RectOffset(10, 10, t.titleBarHeight + 6, 10);
            _window.contentOffset = Vector2.zero;

            _titleBar = new GUIStyle();
            _titleBar.normal.background = Theme.Texture(t.titleBar, out border); _titleBar.border = border;
            _titleText = Strong(Text(GUI.skin.label, fs + 1, t.titleText));
            _titleText.alignment = TextAnchor.MiddleLeft;

            _barBg = new GUIStyle(); _barBg.normal.background = Theme.Texture(t.progressBackground, out border); _barBg.border = border;
            _barFill = new GUIStyle(); _barFill.normal.background = Theme.Solid(Color.white);
            _white = Theme.Solid(Color.white);
            _switchText = Text(GUI.skin.label, fs - 3, "#FFFFFFFF");
            _switchText.alignment = TextAnchor.MiddleCenter; Strong(_switchText);
            _clickLabel = Text(GUI.skin.label, fs, t.text);
            _clickLabel.alignment = TextAnchor.MiddleLeft;

            value = Strong(Text(GUI.skin.label, fs + 8, t.header));
            _section = new GUIStyle();
            _section.normal.background = Theme.Texture(t.sectionHeader ?? t.button, out border); _section.border = border;
            _section.margin = new RectOffset(0, 0, 6, 4);
            sectionText = Strong(Text(GUI.skin.label, fs + 1, t.sectionHeaderText ?? t.buttonText));
            sectionText.alignment = TextAnchor.MiddleLeft;
            sectionText.padding = new RectOffset(10, 10, 0, 0);
            _infoGlyph = Strong(Text(GUI.skin.label, fs, t.sectionHeaderText));
            _infoGlyph.alignment = TextAnchor.MiddleCenter; _infoGlyph.padding = new RectOffset(0, 0, 0, 1);
            _tooltip = Text(GUI.skin.box, fs - 1, t.titleText);
            _tooltip.normal.background = Theme.Texture(t.titleBar, out border); _tooltip.border = border;
            _tooltip.alignment = TextAnchor.UpperLeft; _tooltip.wordWrap = true;
            _tooltip.padding = new RectOffset(10, 10, 8, 8);

            // Full skin used while drawing lib windows, so default GUILayout widgets (scrollbars, fields) match too.
            // (the previous skin may still be active this frame, so it is not destroyed)
            _skin = UnityEngine.Object.Instantiate(GUI.skin);
            _skin.hideFlags = HideFlags.DontSave;
            _skin.label = label; _skin.button = button; _skin.box = box; _skin.window = _window;
            if (_font != null) _skin.font = _font;
            var trackTex = Theme.Texture(t.progressBackground, out border);
            var thumbTex = Theme.Texture(t.button, out border);
            _skin.verticalScrollbar = new GUIStyle(GUI.skin.verticalScrollbar) { fixedWidth = 8 };
            _skin.verticalScrollbar.normal.background = trackTex; _skin.verticalScrollbar.border = new RectOffset(0, 0, 0, 0);
            _skin.verticalScrollbarThumb = new GUIStyle(GUI.skin.verticalScrollbarThumb) { fixedWidth = 8 };
            _skin.verticalScrollbarThumb.normal.background = thumbTex; _skin.verticalScrollbarThumb.border = new RectOffset(0, 0, 0, 0);
            _skin.horizontalScrollbar = new GUIStyle(GUI.skin.horizontalScrollbar) { fixedHeight = 8 };
            _skin.horizontalScrollbar.normal.background = trackTex;
            _skin.horizontalScrollbarThumb = new GUIStyle(GUI.skin.horizontalScrollbarThumb) { fixedHeight = 8 };
            _skin.horizontalScrollbarThumb.normal.background = thumbTex;
            _skin.textField = Text(GUI.skin.textField, fs, t.text);
            _skin.textField.normal.background = _skin.textField.focused.background = Theme.Bordered(Color.white, Theme.Col(t.header, Color.gray), 1);
            _skin.textField.border = new RectOffset(1, 1, 1, 1);
        }

        /// <summary>The themed GUISkin (valid after the first OnGUI). Lib windows are drawn with it.</summary>
        public static GUISkin skin { get { EnsureStyles(); return _skin; } }

        /// <summary>Full-width accent bar with a title (like the game's orange "Farmers Market" headers).</summary>
        public static void SectionHeader(string text) { SectionHeader(text, null); }

        /// <summary>
        /// Section header with an ⓘ icon at its right end. Hovering shows <paramref name="infoTooltip"/>;
        /// returns true on the frame the icon is clicked.
        /// </summary>
        public static bool SectionHeader(string text, string infoTooltip)
        {
            EnsureStyles();
            var r = GUILayoutUtility.GetRect(new GUIContent(text), sectionText, GUILayout.Height(28), GUILayout.ExpandWidth(true));
            r.y += 4; r.height -= 2;
            GUI.Box(r, GUIContent.none, _section);
            GUI.Label(r, text, sectionText);
            if (string.IsNullOrEmpty(infoTooltip)) return false;
            var icon = new Rect(r.xMax - r.height + 2, r.y + 3, r.height - 6, r.height - 6);
            return InfoIcon(icon, infoTooltip, true);
        }

        /// <summary>An ⓘ icon in the layout; hover shows the tooltip, returns true when clicked.</summary>
        public static bool InfoIcon(string tooltip)
        {
            EnsureStyles();
            var r = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20), GUILayout.Height(20));
            return InfoIcon(r, tooltip, false);
        }

        static bool InfoIcon(Rect r, string tooltip, bool onAccent)
        {
            // On the orange header: cream disc with orange "i"; elsewhere: orange disc with cream "i".
            var disc = onAccent ? C(T.sectionHeaderText, Color.white) : C(T.header, new Color(0.78f, 0.48f, 0.02f));
            var glyph = onAccent ? C(T.header, new Color(0.78f, 0.48f, 0.02f)) : C(T.sectionHeaderText, Color.white);
            var old = GUI.color; GUI.color = disc; GUI.DrawTexture(r, Circle()); GUI.color = old;
            GUI.Label(r, "i", Colored(_infoGlyph, glyph));
            return GUI.Button(r, new GUIContent("", tooltip), GUIStyle.none);
        }

        static Texture2D _circle;
        static GUIStyle _infoGlyph, _tooltip;

        static Texture2D Circle()
        {
            if (_circle != null) return _circle;
            const int n = 64; float c = (n - 1) / 2f;
            _circle = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int x = 0; x < n; x++)
                for (int y = 0; y < n; y++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    _circle.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(c - d + 0.5f)));   // anti-aliased edge
                }
            _circle.Apply();
            _circle.hideFlags = HideFlags.DontSave;
            return _circle;
        }

        /// <summary>Draws the pending IMGUI tooltip (GUI.tooltip) near the mouse, kept inside the window.</summary>
        static void DrawTooltip(float windowWidth, float windowHeight)
        {
            var tip = GUI.tooltip;
            if (string.IsNullOrEmpty(tip) || Event.current.type != EventType.Repaint) return;
            var content = new GUIContent(tip);
            float w = Mathf.Min(360f, windowWidth - 16f);
            float h = _tooltip.CalcHeight(content, w);
            var m = Event.current.mousePosition;
            float x = Mathf.Clamp(m.x - w + 12f, 8f, windowWidth - w - 8f);
            float y = m.y + 18f;
            if (y + h > windowHeight - 4f) y = Mathf.Max(4f, m.y - h - 8f);
            GUI.Box(new Rect(x, y, w, h), content, _tooltip);
        }

        /// <summary>Emphasis via the real semibold font (FontStyle stays Normal: Unity's synthetic bold looks crude).</summary>
        static GUIStyle Strong(GUIStyle s)
        {
            s.fontStyle = FontStyle.Normal;
            if (_boldFont != null) s.font = _boldFont;
            return s;
        }

        static GUIStyle Text(GUIStyle baseStyle, int size, string colorHex)
        {
            var s = new GUIStyle(baseStyle) { fontSize = size };
            if (_font != null) s.font = _font;
            var c = C(colorHex, Color.white);
            s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = c;
            return s;
        }

        /// <summary>
        /// Draws a themed, draggable window (title bar from the theme) and returns its new rect.
        /// Use inside OnGUI; <paramref name="content"/> uses GUILayout.
        /// </summary>
        public static Rect Window(int id, Rect rect, string title, Action content)
        {
            EnsureStyles();
            float w = rect.width, h = rect.height;
            int tbh = T.titleBarHeight;
            return GUI.Window(id, rect, _ =>
            {
                GUI.Box(new Rect(0, 0, w, tbh), GUIContent.none, _titleBar);
                GUI.Label(new Rect(10, 0, w - 20, tbh), title ?? "", _titleText);
                try { content(); }
                catch (Exception e) { GUILayout.Label(Lang.T("error: ", "出错：") + e.Message, small); }
                DrawTooltip(w, h);
                GUI.DragWindow(new Rect(0, 0, 10000, tbh));
            }, GUIContent.none, _window);
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
            Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2),
                value ? C(T.switchOn, new Color(0.20f, 0.62f, 0.28f)) : C(T.switchOff, new Color(0.62f, 0.20f, 0.20f)));
            var knob = new Rect(value ? r.xMax - 20 : r.x + 2, r.y + 2, 18, r.height - 4);
            Fill(knob, C(T.switchKnob, new Color(0.95f, 0.95f, 0.95f)));
            var stateRect = value ? new Rect(r.x + 2, r.y, r.width - 22, r.height) : new Rect(r.x + 20, r.y, r.width - 22, r.height);
            GUI.Label(stateRect, value ? Lang.T("ON", "开") : Lang.T("OFF", "关"), _switchText);
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);

            GUILayout.Space(6);
            var opts = labelOptions != null && labelOptions.Length > 0 ? labelOptions : new[] { GUILayout.Height(22) };
            if (GUILayout.Button(text, Colored(_clickLabel, value ? Green : Red), opts)) clicked = true;
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
            EnsureStyles();
            var bar = GUILayoutUtility.GetRect(100, height, GUILayout.ExpandWidth(true));
            GUI.Box(bar, GUIContent.none, _barBg);
            var fill = new Rect(bar.x + 1, bar.y + 1, Mathf.Max(0, (bar.width - 2) * Mathf.Clamp01(value01)), bar.height - 2);
            var old = GUI.backgroundColor; GUI.backgroundColor = color;
            if (fill.width > 0) GUI.Box(fill, GUIContent.none, _barFill);
            GUI.backgroundColor = old;
        }

        /// <summary>Label in a colour (text colour is replaced, not multiplied, so it works on light themes).</summary>
        public static void ColoredLabel(string text, Color color, params GUILayoutOption[] opts)
        {
            GUILayout.Label(text, Colored(label, color), opts);
        }

        static readonly Dictionary<string, GUIStyle> _colored = new Dictionary<string, GUIStyle>();

        /// <summary>A cached copy of <paramref name="baseStyle"/> with its text colour set to <paramref name="color"/>.</summary>
        public static GUIStyle Colored(GUIStyle baseStyle, Color color)
        {
            EnsureStyles();
            string key = baseStyle.GetHashCode() + ColorUtility.ToHtmlStringRGBA(color);
            GUIStyle s;
            if (!_colored.TryGetValue(key, out s))
            {
                s = new GUIStyle(baseStyle);
                s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = color;
                s.wordWrap = true;
                _colored[key] = s;
            }
            return s;
        }

        /// <summary>1234567 -> "1.23M", 238400 -> "238.4k".</summary>
        public static string Number(int n)
        {
            if (n >= 1000000) return (n / 1000000f).ToString("0.00") + "M";
            if (n >= 1000) return (n / 1000f).ToString("0.0") + "k";
            return n.ToString();
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
            var oldSkin = GUI.skin;
            GUI.skin = Ui.skin;
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
                        win.rect = Ui.Window(win.id, win.rect, win.title != null ? win.title.text : "", () => captured.draw(captured));
                    }

            if (LibPanel.visible) LibPanel.Draw(mods);
            GUI.matrix = oldMatrix;
            GUI.skin = oldSkin;
        }
    }

    /// <summary>The ClaudeModLib panel (toolbar button / hotkey): mod list, feature switches, dev tools.</summary>
    public static class LibPanel
    {
        public static bool visible;
        static Rect _rect = new Rect(300, 90, 560, 640);
        static Vector2 _scroll;
        static List<ModEntry> _entries;
        static float _nextRefresh;
        static string _status;

        internal static void Draw(List<ModHandle> mods)
        {
            _rect = Ui.Window(0x434D4BFF, _rect, "ClaudeModLib " + ModLib.Version, () => DrawContent(mods));
        }

        static string Txt(LocText t) { return t != null ? t.text : null; }

        static void LangButton(LangMode m, string text)
        {
            var old = GUI.backgroundColor;
            bool selected = Lang.mode == m;
            if (!selected) GUI.backgroundColor = new Color(0.72f, 0.68f, 0.62f);   // unselected: muted, like inactive native tabs
            if (GUILayout.Button(selected ? "✓ " + text : text, Ui.button, GUILayout.Height(26))) Lang.mode = m;
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

            Ui.SectionHeader(Lang.T("Features", "功能开关"));
            foreach (var m in mods)
            {
                if (m.features.Count == 0) continue;
                GUILayout.Label(Txt(m.displayName), Ui.header);
                foreach (var f in m.features)
                {
                    bool v = Ui.Switch(f.enabled, Txt(f.label));
                    if (v != f.enabled) f.enabled = v;
                    var desc = Txt(f.description);
                    if (!string.IsNullOrEmpty(desc)) GUILayout.Label("              " + desc, Ui.small);
                }
            }

            GUILayout.Space(8);
            Ui.SectionHeader(Lang.T("Mods", "模组列表"));
            GUILayout.Label(Lang.T("Enable/disable applies on the next game start.", "启用/禁用在下次启动游戏时生效。"), Ui.small);
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
            string folder = Application.persistentDataPath.Replace('/', '\\');
            if (Ui.SectionHeader(Lang.T("Developer tools", "开发工具"),
                Lang.T("Export files are written to:\n", "导出的文件保存在：\n") + folder +
                "\n\n• claudemodlib_asset_dump.txt\n• claudemodlib_ui_dump.txt\n• output_log.txt" +
                Lang.T("\n\nClick to open this folder (path copied to clipboard).", "\n\n点击打开该文件夹（路径已复制到剪贴板）。")))
            {
                GUIUtility.systemCopyBuffer = folder;
                Application.OpenURL("file:///" + Application.persistentDataPath);
                _status = Lang.T("Folder opened, path copied: ", "已打开文件夹，路径已复制：") + folder;
            }
            if (GUILayout.Button(Lang.T("Dump all game asset names → claudemodlib_asset_dump.txt", "导出全部游戏资源名 → claudemodlib_asset_dump.txt"), Ui.button))
            {
                try { _status = Lang.T("Written: ", "已写入：") + DevTools.DumpAssets(); }
                catch (Exception ex) { _status = Lang.T("Dump failed: ", "导出失败：") + ex.Message; }
            }
            if (GUILayout.Button(Lang.T("Dump UI resources (sprites, fonts, native UI colours) → claudemodlib_ui_dump.txt",
                                        "导出 UI 资源清单（图片、字体、原生界面配色）→ claudemodlib_ui_dump.txt"), Ui.button))
            {
                try { _status = Lang.T("Written: ", "已写入：") + DevTools.DumpUiResources(); }
                catch (Exception ex) { _status = Lang.T("Dump failed: ", "导出失败：") + ex.Message; }
            }

            GUILayout.Space(8);
            Ui.SectionHeader(Lang.T("Appearance", "外观"));
            GUILayout.Label(Lang.T("Theme: ", "主题：") + (Theme.data != null ? Theme.data.name : "?"), Ui.label);
            GUILayout.Label(Theme.filePath ?? "", Ui.small);
            if (Theme.lastError != null) Ui.ColoredLabel(Lang.T("theme.json error: ", "theme.json 出错：") + Theme.lastError, Ui.Red);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Lang.T("Reload theme", "重新加载主题"), Ui.button))
            {
                Theme.Reload();
                _status = Theme.lastError == null ? Lang.T("Theme reloaded.", "主题已重新加载。") : Lang.T("theme.json has errors, kept previous theme.", "theme.json 有错误，保留了原主题。");
            }
            if (GUILayout.Button(Lang.T("Write current theme to theme.json", "把当前主题写入 theme.json"), Ui.button))
            {
                try { Theme.SaveCurrent(); _status = Lang.T("Written: ", "已写入：") + Theme.filePath; }
                catch (Exception ex) { _status = ex.Message; }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label(Lang.T("Panel hotkey: ", "面板快捷键：") + Hotkeys.Describe(Hotkeys.LibToggle)
                + Lang.T(" (rebind it in the game's key-binding settings)", "（可在游戏设置的按键绑定里修改）"), Ui.small);
            if (_status != null) GUILayout.Label(_status, Ui.small);

            GUILayout.EndScrollView();
            if (GUILayout.Button(Lang.T("Close", "关闭"), Ui.button)) visible = false;
        }
    }
}
