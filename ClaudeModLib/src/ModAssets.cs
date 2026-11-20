using System.Collections.Generic;
using ProjectAutomata;
using UnityEngine;

namespace ClaudeModLib
{
    /// <summary>
    /// The mod asset table (AssetLoader). Files in a mod's assets/ folder are registered here by file name
    /// (.png -> Texture2D, .svg -> SVGAsset, .sprite -> Sprite, .bundle -> every asset inside by its name).
    /// JSON fields such as visualization, spawnDust, svgIcon, constructionBarIcon resolve ONLY from this table,
    /// never from vanilla GameData. Register vanilla objects here to make them usable from JSON.
    /// </summary>
    public static class ModAssets
    {
        static AssetLoader loader { get { return ModLib.modLoader != null ? ModLib.modLoader.assets : null; } }

        public static bool Has(string name)
        {
            var map = NameMap();
            return map != null && map.ContainsKey(name);
        }

        /// <summary>Mod asset by name, or null (unlike AssetLoader.GetObject this does not log an error).</summary>
        public static T Get<T>(string name) where T : UnityEngine.Object
        {
            var map = NameMap();
            UnityEngine.Object o;
            return map != null && map.TryGetValue(name, out o) ? o as T : null;
        }

        /// <summary>
        /// Registers <paramref name="obj"/> under <paramref name="name"/>. To be visible to your own content JSON,
        /// call it from Mod.OnModWasLoaded (runs before your assets/ and content/ are loaded).
        /// </summary>
        public static void Register(string name, UnityEngine.Object obj)
        {
            var map = NameMap();
            if (map == null) { Log.Error("ModAssets.Register: asset loader not ready"); return; }
            map[name] = obj;
        }

        /// <summary>
        /// Creates an inactive container (DontDestroyOnLoad) to hold prefab-like clones. Objects under it keep
        /// activeSelf=true, so instances the game creates from them are active, while the prototypes never run.
        /// </summary>
        public static Transform PrefabRoot
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("ClaudeModLib_Prefabs");
                    go.SetActive(false);
                    Object.DontDestroyOnLoad(go);
                    _root = go.transform;
                }
                return _root;
            }
        }
        static Transform _root;

        public static ICollection<string> Names()
        {
            var map = NameMap();
            return map != null ? (ICollection<string>)map.Keys : new string[0];
        }

        static Dictionary<string, UnityEngine.Object> NameMap()
        {
            return loader == null ? null : Reflect.GetOr<Dictionary<string, UnityEngine.Object>>(loader, "_nameMap", null);
        }
    }
}
