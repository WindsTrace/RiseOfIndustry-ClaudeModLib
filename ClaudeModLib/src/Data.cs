using System;
using System.Collections;
using System.Collections.Generic;
using ProjectAutomata;
using UnityEngine;

namespace ClaudeModLib
{
    /// <summary>
    /// Typed access to the game database (GameData). Every product, recipe, building, vehicle, formula,
    /// tech-tree unlock, category, tag ... is a named asset in it. Names are ASSET names
    /// (e.g. "LightBulb"), not display names ("Light Bulbs"); use DevTools.DumpAssets to list them.
    /// </summary>
    public static class Data
    {
        /// <summary>Asset by name, or null.</summary>
        public static T Get<T>(string name) where T : UnityEngine.Object
        {
            T a;
            return GameData.instance.TryGetAsset<T>(name, out a) ? a : null;
        }

        /// <summary>Asset by name; throws KeyNotFoundException with a helpful message if missing.</summary>
        public static T Require<T>(string name) where T : UnityEngine.Object
        {
            var a = Get<T>(name);
            if (a == null) throw new KeyNotFoundException(typeof(T).Name + " '" + name + "' not found in GameData (asset names, not display names).");
            return a;
        }

        /// <summary>All assets of a type (including subclasses registered under that type).</summary>
        public static List<T> All<T>() where T : UnityEngine.Object
        {
            var list = new List<T>();
            foreach (var o in (IEnumerable)GameData.instance.GetAssetsRO(typeof(T)))
            {
                var t = o as T;
                if (t != null) list.Add(t);
            }
            return list;
        }

        public static List<string> Names<T>() where T : UnityEngine.Object
        {
            var names = new List<string>();
            foreach (var a in All<T>()) names.Add(a.name);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        /// <summary>Every building prefab carrying component <typeparamref name="TComp"/> (e.g. Factory, Shop).</summary>
        public static List<Building> BuildingsWith<TComp>() where TComp : Component
        {
            var list = new List<Building>();
            foreach (var b in All<Building>()) if (b.GetComponent<TComp>() != null) list.Add(b);
            return list;
        }

        /// <summary>Adds an object to GameData so it can be found by name (what JSON content does internally).</summary>
        public static void Register(UnityEngine.Object asset) { GameData.instance.RegisterModAsset(asset); }

        /// <summary>Products carrying any of the given ProductTag names.</summary>
        public static List<ProductDefinition> ProductsWithTag(params string[] tagNames)
        {
            var tags = new List<ProductTag>();
            foreach (var n in tagNames) { var t = Get<ProductTag>(n); if (t != null) tags.Add(t); }
            var list = new List<ProductDefinition>();
            foreach (var p in All<ProductDefinition>()) if (p.HasAnyTag(tags)) list.Add(p);
            return list;
        }

        /// <summary>The recipe producing <paramref name="product"/> (first match), or null.</summary>
        public static Recipe RecipeFor(ProductDefinition product)
        {
            foreach (var r in All<Recipe>())
                foreach (var res in (IEnumerable)r.result)
                    if (Reflect.GetOr<ProductDefinition>(res, "_definition", null) == product) return r;
            return null;
        }
    }
}
