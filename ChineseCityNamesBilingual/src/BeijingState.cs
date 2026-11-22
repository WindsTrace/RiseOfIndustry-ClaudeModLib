// ChineseCityNamesBilingual: when the "China 汉字+Pinyin" name list is chosen for a new map, the State
// (the government building every region connects to) gets the name "北京 Běijīng".
//
// Names are drawn in random order (RegionsWGOperation -> SettlementNameList.Next), so a postfix on
// ConnectionsWGOperation.Execute - the map-generation step that creates the State building - renames the
// State's region (and its city, if it already exists) to Beijing. If another region already drew Beijing,
// the two swap names so no name appears twice. Names are plain strings saved by the game; nothing else changes.
// No dependency on ClaudeModLib.
using System;
using System.Reflection;
using Harmony;
using ProjectAutomata;
using UnityEngine;

namespace ChineseCityNamesBilingual
{
    public class BeijingStateMod : Mod
    {
        const string OurList = "ChineseCitiesNamesBilingual";
        const string Beijing = "北京 Běijīng";
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public override void OnAllModsLoaded()
        {
            base.OnAllModsLoaded();
            try
            {
                var harmony = HarmonyInstance.Create("claudemodding.chinesecitynamesbilingual");
                harmony.Patch(typeof(ConnectionsWGOperation).GetMethod("Execute", Any), null,
                    new HarmonyMethod(typeof(BeijingStateMod).GetMethod("AfterConnections", Any)));
                Debug.Log("[ChineseCityNamesBilingual] State-in-Beijing patch applied");
            }
            catch (Exception e) { Debug.LogError("[ChineseCityNamesBilingual] patch failed: " + e); }
        }

        static void AfterConnections()
        {
            try
            {
                var names = GameParametersManager.instance.world.settlementNames;
                if (names == null || names.name != OurList) return;
                var state = State.instance;
                if (state == null || state.stateBuilding == null) return;

                var stateRegion = RegionManager.instance.GetRegionByTile(state.stateBuilding.tile);
                if (stateRegion == null) stateRegion = state.stateBuilding.region;
                if (stateRegion == null) { Debug.LogWarning("[ChineseCityNamesBilingual] State region not found"); return; }
                if (stateRegion.regionName == Beijing) return;

                string oldName = stateRegion.regionName;
                Region beijingRegion = null;
                foreach (var r in RegionManager.instance.regions) if (r != null && r.regionName == Beijing) { beijingRegion = r; break; }

                Rename(stateRegion, Beijing);
                if (beijingRegion != null) Rename(beijingRegion, oldName);   // swap: keep every name unique
                Debug.Log("[ChineseCityNamesBilingual] State region renamed " + oldName + " -> " + Beijing +
                          (beijingRegion != null ? " (swapped with the region that had drawn Beijing)" : ""));
            }
            catch (Exception e) { Debug.LogWarning("[ChineseCityNamesBilingual] rename failed: " + e.Message); }
        }

        // Region name, plus the city's name if the city already exists (cities take the region's name when created).
        static void Rename(Region region, string name)
        {
            typeof(Region).GetField("_regionName", Any).SetValue(region, name);
            var s = region.settlement;
            if (s != null) typeof(SettlementBase).GetField("_settlementName", Any).SetValue(s, name);
        }
    }
}
