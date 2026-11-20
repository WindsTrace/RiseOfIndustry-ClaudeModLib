using System;
using System.Collections.Generic;
using ProjectAutomata;
using UnityEngine;

namespace ClaudeModLib
{
    public enum LangMode { Auto = 0, English = 1, ChineseSimplified = 2 }

    /// <summary>
    /// UI language for ClaudeModLib and mods built on it. Supported: English, Simplified Chinese.
    /// Auto (default) follows the game's language (GameOptions.language): Chinese for "zh*", English otherwise.
    /// The player can override it in the ClaudeModLib panel; the choice is stored in PlayerPrefs.
    /// </summary>
    public static class Lang
    {
        const string PrefKey = "ClaudeModLib.language";
        static readonly Dictionary<string, LocText> _table = new Dictionary<string, LocText>();

        /// <summary>Raised when the effective language may have changed (mode switched by the player).</summary>
        public static event Action changed;

        public static LangMode mode
        {
            get { return (LangMode)PlayerPrefs.GetInt(PrefKey, 0); }
            set
            {
                PlayerPrefs.SetInt(PrefKey, (int)value);
                PlayerPrefs.Save();
                var h = changed; if (h != null) h();
            }
        }

        /// <summary>The game's current language code, e.g. "en", "zh-Hans", "de" (null if unknown).</summary>
        public static string gameLanguageCode
        {
            get
            {
                try { var l = GameOptions.language; return l != null ? l.keycode : null; }
                catch { return null; }
            }
        }

        /// <summary>True if UI text should be Simplified Chinese.</summary>
        public static bool isChinese
        {
            get
            {
                switch (mode)
                {
                    case LangMode.ChineseSimplified: return true;
                    case LangMode.English: return false;
                    default:
                        var code = gameLanguageCode;
                        return code != null && code.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        /// <summary>Pick the text for the current language. Falls back to English when <paramref name="zh"/> is null.</summary>
        public static string T(string en, string zh) { return isChinese && !string.IsNullOrEmpty(zh) ? zh : en; }

        /// <summary>Registers a keyed text (for mods that prefer a string table).</summary>
        public static void Add(string key, string en, string zh) { _table[key] = new LocText(en, zh); }

        /// <summary>Text for <paramref name="key"/> in the current language, or the key itself if unknown.</summary>
        public static string Get(string key)
        {
            LocText t;
            return _table.TryGetValue(key, out t) ? t.text : key;
        }
    }

    /// <summary>
    /// A bilingual text. Strings convert implicitly (same text in both languages), so APIs taking LocText also
    /// accept plain strings. Create with <c>new LocText("English", "中文")</c> or <c>LocText.Of(...)</c>.
    /// </summary>
    public class LocText
    {
        public readonly string en, zh;
        public LocText(string en, string zh = null) { this.en = en; this.zh = zh; }
        public static LocText Of(string en, string zh) { return new LocText(en, zh); }
        public string text { get { return Lang.T(en, zh); } }
        public override string ToString() { return text; }
        public static implicit operator LocText(string s) { return s == null ? null : new LocText(s, null); }
    }
}
