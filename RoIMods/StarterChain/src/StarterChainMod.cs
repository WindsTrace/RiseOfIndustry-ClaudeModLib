// StarterChain: example production chain built on ClaudeModLib.
//   2 Steel + 1 LightBulb -> 1 Toy Robot, made in a new "Toy Robot Workshop", researched on a new "Robotics" page.
//
//   content/*.json  (official JSON content system) -> product, recipe, tech-tree page, unlocks
//   this file       (C# via ClaudeModLib)          -> building clone, recipes, tech-tree line, shop listing
//
// Load order per mod: code (OnModWasLoaded) -> assets -> content -> (all mods) OnAllModsLoaded.
using ClaudeModLib;
using ProjectAutomata;
using UnityEngine;

namespace StarterChain
{
    public class StarterChainMod : Mod
    {
        const string BuildingName = "ToyRobotWorkshop";       // referenced by content/ToyRobotUnlocks.json
        const string ProductName = "ToyRobot";
        const string CustomModel = "ToyRobotWorkshopModel";   // optional prefab in assets/*.bundle

        Building _building;
        Feature _guaranteeListing;

        public override void OnModWasLoaded()
        {
            base.OnModWasLoaded();
            var handle = ModLib.Register(this, LocText.Of("Example chain (Toy Robot)", "示例生产链 (Toy Robot)"));
            _guaranteeListing = handle.AddFeature("guaranteeListing",
                LocText.Of("Guarantee Toy Stores list Toy Robot (applies on next game start)", "保证玩具店上架 Toy Robot（下次启动游戏生效）"), true,
                LocText.Of("Raises ToyStore's product limit from 8 to all candidates; changes vanilla balance",
                           "把 ToyStore 的上架上限从 8 提到全部候选数；会改变原版平衡"));

            // Before content loads, so the JSON unlock can reference the building by name.
            _building = Content.CloneBuilding("ToyFactory", BuildingName, b =>
                Content.SetBuildingInfo(b, "TOY ROBOT WORKSHOP", "Toy Robot Workshop",
                    "Assembles Toy Robots from Steel and Light Bulbs.", 450000f));
        }

        public override void OnAllModsLoaded()
        {
            base.OnAllModsLoaded();
            Content.SetFactoryRecipes(_building, "ToyRobot");
            var model = ModAssets.Get<GameObject>(CustomModel);
            if (model != null) Content.ReplaceModel(_building, model);

            Content.CopyProductPresentation(ProductName, "Marbles");

            // Draw the research line from vanilla prerequisites on our own page.
            Content.AddUnlockToTree("Steel", "Robotics", 0);
            Content.AddUnlockToTree("Light Bulb", "Robotics", 1);
            Debug.Log("[StarterChain] Robotics tree: " + string.Join(", ", Content.DescribeTree("Robotics").ToArray()));

            if (_guaranteeListing.enabled) Shops.GuaranteeListing(ProductName);
        }
    }
}
