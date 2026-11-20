// AssetDump: writes the names and key fields of every vanilla GameData asset to
// %USERPROFILE%\AppData\LocalLow\Dapper Penguin Studios\Rise of Industry\roi_asset_dump.txt
// Use the output to find exact names (categories, products, recipes, buildings, vehicles, formulas)
// to reference from content JSON.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ProjectAutomata;
using UnityEngine;

namespace AssetDump
{
    public class AssetDumpMod : Mod
    {
        const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // Types whose fields are dumped in detail. Everything else is listed by name only.
        static readonly string[] DetailTypes =
        {
            "ProductDefinition", "Recipe", "Formula", "TechTreeBuildingUnlock", "TechTreeRecipeUnlock",
            "ResourceNode", "Vehicle", "SettlementTier", "SettlementConfig", "SettlementType",
            "SettlementGrowthCondition", "TechTree", "TechTreeCategory", "ProductCategory",
            "ProductDefinitionDataCategoryProvider", "ProductCategoryModifierInfo"
        };

        // Building components whose fields are dumped (others are listed by type name).
        static readonly string[] DetailComponents =
        {
            "Building", "Factory", "Farm", "GathererHub", "Harvester", "Field", "BuildingVisuals",
            "JITVehicleFleet", "Upkeep", "ProductSpecificProductStorage", "PolluterBuilding",
            "BuildingTopology", "BuildingConnectionManager", "BuildingPlacementValidator",
            "Shop", "TownCenter", "Settlement", "SettlementBase", "ShopWarehouseClient"
        };

        // Runs while this mod loads. A content error in another mod makes the game disable mods and
        // abort loading the rest, so: (1) re-enable everything for the next launch, and
        // (2) put AssetDump first in the load order so this always runs.
        public override void OnModWasLoaded()
        {
            base.OnModWasLoaded();
            try
            {
                var all = modLoader.GetAllMods(true, new List<ModInstance>());
                // Only re-enable our own dev mods; workshop mods keep whatever state the player chose.
                var devMods = new HashSet<string> { "AssetDump", "ClaudeModLib", "StarterChain", "CityProgress" };
                foreach (var mi in all)
                    if (!mi.isEnabled && devMods.Contains(mi.description.name)) modLoader.EnableMod(mi.description.name);
                var ordered = new List<ModInstance>();
                foreach (var mi in all) if (mi.description.name == "AssetDump") ordered.Add(mi);
                foreach (var mi in all) if (mi.description.name != "AssetDump") ordered.Add(mi);
                modLoader.SetModOrder(ordered);
                PlayerPrefs.Save();
                Debug.Log("[AssetDump] all mods re-enabled for next launch; AssetDump ordered first");
            }
            catch (Exception e) { Debug.LogError("[AssetDump] re-enable failed: " + e); }
        }

        public override void OnAllModsLoaded()
        {
            base.OnAllModsLoaded();
            string outPath = System.IO.Path.Combine(Application.persistentDataPath, "roi_asset_dump.txt");
            var sb = new StringBuilder();
            try
            {
                sb.AppendLine("# Rise of Industry asset dump " + DateTime.Now);
                sb.AppendLine("# userModsPath = " + Describe(modLoader.userModsPath, 0));
                sb.AppendLine("# DISABLED_MODS pref = '" + PlayerPrefs.GetString("ProjectAutomata.ModLoader.DISABLED_MODS_PPKEY") + "'");
                var toEnable = new List<string>();
                foreach (var mi in modLoader.allMods)
                {
                    sb.AppendLine("# mod: " + mi.description.name + " @ " + mi.modPath + " enabled=" + mi.isEnabled);
                    if (!mi.isEnabled) toEnable.Add(mi.description.name);
                }
                // Mods get auto-disabled after a load error. Re-enable everything for the next launch.
                foreach (var n in toEnable) modLoader.EnableMod(n);
                if (toEnable.Count > 0)
                {
                    PlayerPrefs.Save();
                    sb.AppendLine("# re-enabled for next launch: " + string.Join(", ", toEnable.ToArray()));
                }

                var all = new List<UnityEngine.Object>();
                foreach (var o in (IEnumerable)GameData.instance.assets)
                {
                    var uo = o as UnityEngine.Object;
                    if (uo != null) all.Add(uo);
                }

                foreach (var group in all.GroupBy(o => o.GetType().Name).OrderBy(g => g.Key))
                {
                    sb.AppendLine();
                    sb.AppendLine("=== " + group.Key + " (" + group.Count() + ")");
                    bool detail = DetailTypes.Contains(group.Key) || group.Key.StartsWith("TechTree");
                    foreach (var o in group.OrderBy(o => o.name))
                    {
                        var b = o as Building;
                        if (b != null) { DumpBuilding(sb, b); continue; }
                        sb.AppendLine("- " + o.name);
                        if (detail) DumpFields(sb, o, "    ");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("=== Prototypes/BuildingPrototype (template for JSON buildings) (1)");
                var proto = Resources.Load<GameObject>("Prototypes/BuildingPrototype");
                if (proto == null) sb.AppendLine("- (not found)");
                else DumpBuilding(sb, proto.GetComponent<Building>());

                sb.AppendLine();
                sb.AppendLine("=== Mod AssetLoader names");
                foreach (var n in modLoader.assets.assetNames) sb.AppendLine("- " + n);
            }
            catch (Exception e)
            {
                sb.AppendLine("!!! EXCEPTION: " + e);
            }
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log("[AssetDump] wrote " + outPath);
        }

        static void DumpBuilding(StringBuilder sb, Building b)
        {
            sb.AppendLine("- " + b.name);
            foreach (var c in b.GetComponents<Component>())
            {
                if (c == null) continue;
                string tn = c.GetType().Name;
                if (tn == "Transform") continue;
                sb.AppendLine("    [" + tn + "]");
                if (DetailComponents.Contains(tn)) DumpFields(sb, c, "        ");
            }
            // First two levels of child objects: visualisation prefab names live here.
            foreach (Transform child in b.transform)
            {
                sb.AppendLine("    child: " + child.name);
                foreach (Transform gc in child) sb.AppendLine("      child: " + gc.name);
            }
        }

        static void DumpFields(StringBuilder sb, object o, string indent)
        {
            for (var t = o.GetType(); t != null && t != typeof(MonoBehaviour) && t != typeof(ScriptableObject); t = t.BaseType)
            {
                foreach (var f in t.GetFields(F | BindingFlags.DeclaredOnly))
                {
                    if (typeof(Delegate).IsAssignableFrom(f.FieldType)) continue;
                    string fname = f.Name;
                    if (fname.Contains("<"))
                    {
                        if (!fname.EndsWith(">k__BackingField")) continue;   // auto-property: show as property name
                        fname = fname.Substring(1, fname.IndexOf('>') - 1);
                    }
                    object v;
                    try { v = f.GetValue(o); } catch { continue; }
                    sb.AppendLine(indent + fname + " = " + Describe(v, 0));
                }
            }
        }

        static string Describe(object v, int depth)
        {
            if (v == null) return "null";
            var uo = v as UnityEngine.Object;
            if (v is UnityEngine.Object) return uo == null ? "null" : "'" + uo.name + "' (" + uo.GetType().Name + ")";
            if (v is string) return "\"" + v + "\"";
            var t = v.GetType();
            if (t.IsPrimitive || t.IsEnum) return v.ToString();
            if (v is Vector2Int) { var p = (Vector2Int)v; return "(" + p.x + "," + p.y + ")"; }
            if (v is string[]) return "[" + string.Join(",", (string[])v) + "]";
            if (depth >= 2) return "{" + t.Name + "}";
            var en = v as IEnumerable;
            if (en != null)
            {
                var items = new List<string>();
                foreach (var i in en) { items.Add(Describe(i, depth + 1)); if (items.Count >= 40) { items.Add("..."); break; } }
                return "[" + string.Join(", ", items.ToArray()) + "]";
            }
            var parts = new List<string>();
            foreach (var f in t.GetFields(F))
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
