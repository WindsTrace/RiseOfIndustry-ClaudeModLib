using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ProjectAutomata;
using UnityEngine;

namespace ClaudeModLib
{
    /// <summary>Developer helpers: dump the game database, inspect objects and UI hierarchies.</summary>
    public static partial class DevTools
    {
        /// <summary>Default dump path: %USERPROFILE%\AppData\LocalLow\Dapper Penguin Studios\Rise of Industry\claudemodlib_asset_dump.txt</summary>
        public static string DefaultDumpPath { get { return System.IO.Path.Combine(Application.persistentDataPath, "claudemodlib_asset_dump.txt"); } }

        /// <summary>Types whose fields are written out in full by DumpAssets (others: name only).</summary>
        public static readonly HashSet<string> DetailTypes = new HashSet<string>
        {
            "ProductDefinition", "Recipe", "Formula", "ResourceNode", "Vehicle", "TechTree", "TechTreeCategory",
            "TechTreeBuildingUnlock", "TechTreeRecipeUnlock", "TechTreeGenericUnlock", "SettlementTier", "SettlementConfig",
            "SettlementType", "ProductCategory", "ProductCategoryModifierInfo", "ProductDefinitionDataCategoryProvider", "InputButton"
        };

        /// <summary>Building components whose fields are written out by DumpAssets.</summary>
        public static readonly HashSet<string> DetailComponents = new HashSet<string>
        {
            "Building", "Factory", "Farm", "GathererHub", "Harvester", "Field", "BuildingVisuals", "JITVehicleFleet",
            "Upkeep", "ProductSpecificProductStorage", "PolluterBuilding", "BuildingConnectionManager",
            "BuildingPlacementValidator", "Shop"
        };

        /// <summary>
        /// Writes every GameData asset grouped by type ("=== Type (count)" / "- name" / indented fields) and
        /// returns the path. Use it to find exact asset names for JSON and Data.Get.
        /// </summary>
        public static string DumpAssets(string path = null)
        {
            path = path ?? DefaultDumpPath;
            var sb = new StringBuilder();
            sb.AppendLine("# Rise of Industry asset dump by ClaudeModLib " + ModLib.Version + " at " + DateTime.Now);
            var all = new List<UnityEngine.Object>();
            foreach (var o in (IEnumerable)GameData.instance.assets) { var u = o as UnityEngine.Object; if (u != null) all.Add(u); }
            foreach (var g in all.GroupBy(o => o.GetType().Name).OrderBy(g => g.Key))
            {
                sb.AppendLine();
                sb.AppendLine("=== " + g.Key + " (" + g.Count() + ")");
                bool detail = DetailTypes.Contains(g.Key);
                foreach (var o in g.OrderBy(o => o.name))
                {
                    var b = o as Building;
                    if (b != null) { DumpBuilding(sb, b); continue; }
                    sb.AppendLine("- " + o.name);
                    if (detail) DumpFields(sb, o, "    ");
                }
            }
            sb.AppendLine();
            sb.AppendLine("=== Mod assets (" + ModAssets.Names().Count + ")");
            foreach (var n in ModAssets.Names().OrderBy(n => n)) sb.AppendLine("- " + n);
            File.WriteAllText(path, sb.ToString());
            Log.Info("asset dump written: " + path);
            return path;
        }

        /// <summary>All fields (incl. private and auto-property backing fields) of an object, one per line.</summary>
        public static string DescribeObject(object o)
        {
            var sb = new StringBuilder();
            DumpFields(sb, o, "");
            return sb.ToString();
        }

        /// <summary>GameObject tree with component types, e.g. to find UI elements. Depth-limited.</summary>
        public static string DescribeHierarchy(GameObject root, int maxDepth = 6)
        {
            var sb = new StringBuilder();
            Hierarchy(sb, root.transform, 0, maxDepth);
            return sb.ToString();
        }

        static void Hierarchy(StringBuilder sb, Transform t, int depth, int maxDepth)
        {
            var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name);
            sb.AppendLine(new string(' ', depth * 2) + t.name + (t.gameObject.activeSelf ? "" : " (inactive)") + "  [" + string.Join(", ", comps.ToArray()) + "]");
            if (depth >= maxDepth) return;
            foreach (Transform c in t) Hierarchy(sb, c, depth + 1, maxDepth);
        }

        static void DumpBuilding(StringBuilder sb, Building b)
        {
            sb.AppendLine("- " + b.name);
            foreach (var c in b.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                string tn = c.GetType().Name;
                sb.AppendLine("    [" + tn + "]");
                if (DetailComponents.Contains(tn)) DumpFields(sb, c, "        ");
            }
            foreach (Transform child in b.transform)
            {
                sb.AppendLine("    child: " + child.name);
                foreach (Transform gc in child) sb.AppendLine("      child: " + gc.name);
            }
        }

        static void DumpFields(StringBuilder sb, object o, string indent)
        {
            for (var t = o.GetType(); t != null && t != typeof(MonoBehaviour) && t != typeof(ScriptableObject) && t != typeof(object); t = t.BaseType)
            {
                foreach (var f in t.GetFields(Reflect.All | BindingFlags.DeclaredOnly))
                {
                    if (typeof(Delegate).IsAssignableFrom(f.FieldType)) continue;
                    string name = f.Name;
                    if (name.Contains("<"))
                    {
                        if (!name.EndsWith(">k__BackingField")) continue;
                        name = name.Substring(1, name.IndexOf('>') - 1);
                    }
                    if (f.FieldType.Name.StartsWith("LazyComponentRef")) continue;
                    object v;
                    try { v = f.GetValue(o); } catch { continue; }
                    sb.AppendLine(indent + name + " = " + Describe(v, 0));
                }
            }
        }

        /// <summary>Compact one-line description of a value (Unity objects by name, collections up to 40 items).</summary>
        public static string Describe(object v, int depth = 0)
        {
            if (v == null) return "null";
            var uo = v as UnityEngine.Object;
            if (v is UnityEngine.Object) return uo == null ? "null" : "'" + uo.name + "' (" + uo.GetType().Name + ")";
            if (v is string) return "\"" + v + "\"";
            var t = v.GetType();
            if (t.IsPrimitive || t.IsEnum) return v.ToString();
            if (v is Vector2Int) { var p = (Vector2Int)v; return "(" + p.x + "," + p.y + ")"; }
            if (depth >= 2) return "{" + t.Name + "}";
            var en = v as IEnumerable;
            if (en != null)
            {
                var items = new List<string>();
                foreach (var i in en) { items.Add(Describe(i, depth + 1)); if (items.Count >= 40) { items.Add("..."); break; } }
                return "[" + string.Join(", ", items.ToArray()) + "]";
            }
            var parts = new List<string>();
            foreach (var f in t.GetFields(Reflect.All))
            {
                if (f.Name.Contains("<")) continue;
                object fv;
                try { fv = f.GetValue(v); } catch { continue; }
                parts.Add(f.Name + ": " + Describe(fv, depth + 1));
            }
            return t.Name + "{" + string.Join(", ", parts.ToArray()) + "}";
        }
    }
}
