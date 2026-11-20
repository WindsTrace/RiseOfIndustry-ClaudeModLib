using System;
using System.Collections;
using System.Collections.Generic;
using ProjectAutomata;
using UnityEngine;

namespace ClaudeModLib
{
    /// <summary>
    /// Helpers for adding content that the JSON system cannot fully express in game version 2.3.x.
    /// Typical flow: products / recipes / unlocks / tech trees in content/*.json, then these helpers for the rest.
    /// </summary>
    public static class Content
    {
        // ------------------------------------------------------------------ buildings
        /// <summary>
        /// Clones a vanilla building (all ~40 components, footprint, road connection, parking spots) and registers
        /// it in GameData under <paramref name="newName"/>. Call from Mod.OnModWasLoaded so your JSON
        /// (e.g. a TechTreeBuildingUnlock) can reference it. Assign recipes later with SetFactoryRecipes,
        /// because your recipes only exist after content loading.
        /// Why: a JSON "ProjectAutomata.Building" starts from a 10-component prototype and several components a
        /// working factory needs (BuildingTransportManager, RecipeWarehouseClient, VehicleQueue ...) have no JSON model.
        /// </summary>
        public static Building CloneBuilding(string templateName, string newName, Action<Building> configure = null)
        {
            var template = Data.Require<Building>(templateName);
            var go = UnityEngine.Object.Instantiate(template.gameObject, ModAssets.PrefabRoot, false);
            go.name = newName;
            var b = go.GetComponent<Building>();
            if (configure != null) configure(b);
            Data.Register(b);
            Log.Info("cloned building " + templateName + " -> " + newName);
            return b;
        }

        /// <summary>Sets the display texts and base cost of a building.</summary>
        public static void SetBuildingInfo(Building b, string title, string panelTitle, string description, float baseCost)
        {
            if (title != null) b.buildingName = title;
            if (panelTitle != null) Reflect.Set(b, "buildingPanelName", panelTitle);
            if (description != null) b.description = description;
            if (baseCost > 0) b.baseCost = baseCost;
        }

        /// <summary>Replaces the recipes a factory/farm/gatherer building can run. First recipe becomes the initial one.</summary>
        public static void SetFactoryRecipes(Building b, params string[] recipeNames)
        {
            var user = b.GetComponent<RecipeUser>();
            if (user == null) throw new InvalidOperationException(b.name + " has no RecipeUser (Factory/GathererHub) component");
            var list = new List<Recipe>();
            foreach (var n in recipeNames) list.Add(Data.Require<Recipe>(n));
            Reflect.Set(user, "availableRecipes", list.ToArray());
            Reflect.Set(user, "initialRecipe", list.Count > 0 ? list[0] : null);
        }

        /// <summary>
        /// Replaces the 3D model under the building's "Visuals" child with an instance of <paramref name="modelPrefab"/>
        /// (e.g. a prefab from your .bundle via ModAssets.Get&lt;GameObject&gt;). Returns false if there is no "Visuals" child.
        /// </summary>
        public static bool ReplaceModel(Building b, GameObject modelPrefab)
        {
            var visuals = b.transform.Find("Visuals");
            if (visuals == null || modelPrefab == null) return false;
            for (int i = visuals.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(visuals.GetChild(i).gameObject);
            var m = UnityEngine.Object.Instantiate(modelPrefab, visuals, false);
            m.name = modelPrefab.name;
            return true;
        }

        // ------------------------------------------------------------------ products
        /// <summary>
        /// Copies what product JSON cannot set from a vanilla product: the cargo model shown on trucks
        /// (visualRepresentation) and the factory category provider (decides the UI row by recipe tier).
        /// Call after all mods are loaded.
        /// </summary>
        public static void CopyProductPresentation(string productName, string templateProductName)
        {
            var p = Data.Require<ProductDefinition>(productName);
            var t = Data.Require<ProductDefinition>(templateProductName);
            if (p.visualRepresentation == null) p.SetVisualRepresentation(t.visualRepresentation);
            Reflect.Set(p, "categoryProvider", Reflect.Get(t, "categoryProvider"));
        }

        // ------------------------------------------------------------------ tech tree
        /// <summary>
        /// Places an existing unlock (vanilla or modded) on a tech tree page at <paramref name="column"/>.
        /// Lines between nodes come from requiredUnlocks; to draw a line from a vanilla prerequisite on your own
        /// page, add that vanilla unlock to your page with this method. Call after all mods are loaded.
        /// </summary>
        public static void AddUnlockToTree(string unlockName, string treeName, int column)
        {
            var unlock = Data.Require<TechTreeUnlock>(unlockName);
            var tree = Data.Require<TechTree>(treeName);
            if (!unlock.ContainedByTree(tree))
            {
                var placementType = typeof(TechTreeUnlock).GetNestedType("TechTreePlacement", Reflect.All | System.Reflection.BindingFlags.Public);
                var list = (IList)Reflect.Get(unlock, "trees");
                // Copy an existing placement so the override icon/name objects stay valid, then retarget it.
                object placement = list.Count > 0 ? list[0] : Activator.CreateInstance(placementType);
                placementType.GetField("tree", Reflect.All).SetValue(placement, tree);
                placementType.GetField("column", Reflect.All).SetValue(placement, column);
                list.Add(placement);
            }
            RefreshTree(treeName);
        }

        /// <summary>Rebuilds a tree's node list after changes (TechTree/TechTreeUnlock.OnGameDataLoaded are idempotent).</summary>
        public static void RefreshTree(string treeName)
        {
            var tree = Data.Require<TechTree>(treeName);
            foreach (var u in Data.All<TechTreeUnlock>()) u.OnGameDataLoaded();
            tree.OnGameDataLoaded();
        }

        /// <summary>Names of unlocks on a tree page with their columns, e.g. "Steel@0".</summary>
        public static List<string> DescribeTree(string treeName)
        {
            var tree = Data.Require<TechTree>(treeName);
            var list = new List<string>();
            foreach (var o in (IEnumerable)tree.unlocks)
            {
                var u = (TechTreeUnlock)o;
                list.Add(u.name + "@" + u.GetColumnForTree(tree));
            }
            return list;
        }
    }
}
