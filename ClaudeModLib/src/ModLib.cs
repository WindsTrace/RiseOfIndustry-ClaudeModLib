// ClaudeModLib - shared toolkit for Rise of Industry mods (game 2.3.x, Unity 2018.4).
// Entry point + mod/feature registry. See docs/API.md for the full API.
using System;
using System.Collections.Generic;
using System.Reflection;
using ProjectAutomata;
using UnityEngine;

namespace ClaudeModLib
{
    /// <summary>The game-facing Mod component. Do not use directly; use the static <see cref="ModLib"/> API.</summary>
    public class ClaudeModLibMod : Mod
    {
        public override void OnModWasLoaded()
        {
            base.OnModWasLoaded();
            ModLib.Bootstrap(this);
        }

        public override void OnAllModsLoaded()
        {
            base.OnAllModsLoaded();
            ModLib.OnAllModsLoadedInternal();
        }

        void Update() { ModLib.UpdateInternal(); }
        void OnGUI() { ModLib.OnGuiInternal(); }
    }

    /// <summary>A switchable feature of a registered mod (e.g. "show city progress window").</summary>
    public class Feature
    {
        public readonly ModHandle owner;
        public readonly string id;
        public string label;
        public string description;
        bool _enabled;
        public event Action<bool> changed;

        internal Feature(ModHandle owner, string id, string label, bool defaultOn)
        {
            this.owner = owner; this.id = id; this.label = label;
            _enabled = PlayerPrefs.GetInt(PrefKey, defaultOn ? 1 : 0) == 1;
        }

        string PrefKey { get { return "ClaudeModLib." + owner.name + "." + id; } }

        /// <summary>Current state; persisted in PlayerPrefs across sessions.</summary>
        public bool enabled
        {
            get { return _enabled; }
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
                var h = changed; if (h != null) h(value);
            }
        }
    }

    /// <summary>Handle returned by <see cref="ModLib.Register"/>; owns the mod's features and windows.</summary>
    public class ModHandle
    {
        public readonly string name;
        public readonly Mod mod;
        public string displayName;
        internal readonly List<Feature> features = new List<Feature>();
        internal readonly List<LibWindow> windows = new List<LibWindow>();

        internal ModHandle(Mod mod, string name, string displayName) { this.mod = mod; this.name = name; this.displayName = displayName; }

        /// <summary>Adds an on/off switch shown in the ClaudeModLib panel. State is persisted.</summary>
        public Feature AddFeature(string id, string label, bool defaultOn, string description = null)
        {
            var f = new Feature(this, id, label, defaultOn);
            f.description = description;
            features.Add(f);
            return f;
        }

        /// <summary>
        /// Adds a draggable IMGUI window drawn with <paramref name="draw"/> while <paramref name="visibleWhen"/> is enabled.
        /// The window only shows inside a running game (not in menus) unless <paramref name="alsoInMenus"/> is true.
        /// </summary>
        public LibWindow AddWindow(string title, Rect initialRect, Action<LibWindow> draw, Feature visibleWhen, bool alsoInMenus = false)
        {
            var w = new LibWindow(title, initialRect, draw, visibleWhen, alsoInMenus);
            windows.Add(w);
            return w;
        }
    }

    /// <summary>Static entry point of the library.</summary>
    public static class ModLib
    {
        public const string Version = "0.1.0";
        public const string ModName = "ClaudeModLib";

        static ClaudeModLibMod _instance;
        static readonly List<ModHandle> _mods = new List<ModHandle>();
        static readonly List<Action> _onAllLoaded = new List<Action>();
        static readonly List<Action> _onGameReady = new List<Action>();
        static bool _allLoaded, _gameReadyFired;

        /// <summary>The lib's own Mod component (a MonoBehaviour that lives for the whole session).</summary>
        public static Mod instance { get { return _instance; } }
        public static ModLoader modLoader { get { return _instance != null ? _instance.modLoader : null; } }
        public static IList<ModHandle> registeredMods { get { return _mods.AsReadOnly(); } }

        /// <summary>True while a map is loaded and the world is running (SettlementManager exists).</summary>
        public static bool inGame { get; private set; }

        /// <summary>Registers a mod with the lib panel. Call from your Mod.OnModWasLoaded or OnAllModsLoaded.</summary>
        public static ModHandle Register(Mod mod, string displayName = null)
        {
            string name = ModNameOf(mod);
            foreach (var h in _mods) if (h.name == name) return h;
            var handle = new ModHandle(mod, name, displayName ?? name);
            _mods.Add(handle);
            Log.Info("registered mod " + name);
            return handle;
        }

        /// <summary>Runs <paramref name="action"/> once all mods (and their content) are loaded; immediately if already loaded.</summary>
        public static void OnAllModsLoaded(Action action)
        {
            if (_allLoaded) Safe(action, "OnAllModsLoaded callback"); else _onAllLoaded.Add(action);
        }

        /// <summary>Runs <paramref name="action"/> the first time a game world is running (good place to touch scene UI).</summary>
        public static void OnGameReady(Action action)
        {
            if (_gameReadyFired && inGame) Safe(action, "OnGameReady callback"); else _onGameReady.Add(action);
        }

        /// <summary>Name from desc.json of the mod that owns <paramref name="mod"/>.</summary>
        public static string ModNameOf(Mod mod)
        {
            var loader = mod != null ? mod.modLoader : null;
            if (loader != null)
                foreach (var mi in loader.allMods)
                    if (mi.mods != null && mi.mods.Contains(mod)) return mi.description.name;
            return mod != null ? mod.GetType().Assembly.GetName().Name : "?";
        }

        // ------------------------------------------------------------------ internals
        internal static void Bootstrap(ClaudeModLibMod mod)
        {
            _instance = mod;
            // Dependent mods are loaded with Assembly.LoadFile and cannot bind to this assembly by themselves.
            var self = typeof(ModLib).Assembly;
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
                new AssemblyName(e.Name).Name == self.GetName().Name ? self : null;
            Mods.SnapshotLoaded();   // record which mods were enabled at start (EnableMod later changes isEnabled)
            Register(mod, "ClaudeModLib");
            Log.Info("v" + Version + " bootstrapped");
        }

        internal static void OnAllModsLoadedInternal()
        {
            _allLoaded = true;
            foreach (var a in _onAllLoaded) Safe(a, "OnAllModsLoaded callback");
            _onAllLoaded.Clear();
            Hotkeys.InitLibHotkey();
        }

        static float _nextGameCheck;
        internal static void UpdateInternal()
        {
            if (Time.unscaledTime >= _nextGameCheck)
            {
                _nextGameCheck = Time.unscaledTime + 1f;
                bool now = UnityEngine.Object.FindObjectOfType<SettlementManager>() != null;
                if (now && !inGame)
                {
                    inGame = true;
                    if (!_gameReadyFired) { _gameReadyFired = true; }
                    foreach (var a in _onGameReady.ToArray()) Safe(a, "OnGameReady callback");
                    Toolbar.TryInstall();
                }
                else if (!now && inGame) { inGame = false; Toolbar.ResetForNextGame(); }
                if (inGame && !Toolbar.installed) Toolbar.TryInstall();
            }
            if (Hotkeys.LibToggleDown()) LibPanel.visible = !LibPanel.visible;
        }

        internal static void OnGuiInternal()
        {
            LibUi.Draw(_mods);
        }

        internal static void Safe(Action a, string what)
        {
            try { a(); } catch (Exception e) { Log.Error(what + " failed: " + e); }
        }
    }

    /// <summary>Prefixed logging to output_log.txt.</summary>
    public static class Log
    {
        public static void Info(string msg) { Debug.Log("[ClaudeModLib] " + msg); }
        public static void Warn(string msg) { Debug.LogWarning("[ClaudeModLib] " + msg); }
        public static void Error(string msg) { Debug.LogError("[ClaudeModLib] " + msg); }
    }
}
