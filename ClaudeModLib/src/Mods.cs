using System.Collections.Generic;
using ProjectAutomata;

namespace ClaudeModLib
{
    public class ModEntry
    {
        public string name, author, versionString, path;
        public bool enabled;           // state used for THIS session (as loaded)
        public bool enabledNextLaunch; // what the game will do next start
        public bool loaded;            // its code/content was loaded this session
        public bool usesLib;           // depends on ClaudeModLib
        public string[] dependencies;
    }

    /// <summary>
    /// Mod list and enable state. The game loads mods only at start-up: enabling/disabling takes effect on the
    /// next launch. Load order per mod: code (DLL, OnModWasLoaded) -> assets -> content; then OnAllModsLoaded.
    /// A content error in any mod makes the game disable mods and stop loading the remaining ones.
    /// </summary>
    public static class Mods
    {
        static readonly HashSet<string> _loadedAtStart = new HashSet<string>();
        static bool _snapshotTaken;

        internal static void SnapshotLoaded()
        {
            if (_snapshotTaken || ModLib.modLoader == null) return;
            _snapshotTaken = true;
            foreach (var mi in ModLib.modLoader.allMods) if (mi.isEnabled) _loadedAtStart.Add(mi.description.name);
        }

        public static List<ModEntry> List()
        {
            SnapshotLoaded();
            var list = new List<ModEntry>();
            var loader = ModLib.modLoader;
            if (loader == null) return list;
            foreach (var mi in loader.GetAllMods(true, new List<ModInstance>()))
            {
                var d = mi.description;
                var e = new ModEntry();
                e.name = d.name; e.author = d.author; e.versionString = d.versionString; e.path = mi.modPath;
                e.dependencies = d.dependencies ?? new string[0];
                e.enabledNextLaunch = mi.isEnabled;
                e.loaded = _loadedAtStart.Contains(d.name);
                e.enabled = e.loaded;
                e.usesLib = d.name == ModLib.ModName || System.Array.IndexOf(e.dependencies, ModLib.ModName) >= 0;
                list.Add(e);
            }
            return list;
        }

        /// <summary>Enables/disables a mod for the NEXT launch (game stores this in PlayerPrefs).</summary>
        public static void SetEnabledNextLaunch(string modName, bool enabled)
        {
            var loader = ModLib.modLoader;
            if (loader == null) return;
            if (enabled) loader.EnableMod(modName); else loader.DisableMod(modName);
            UnityEngine.PlayerPrefs.Save();
        }
    }
}
