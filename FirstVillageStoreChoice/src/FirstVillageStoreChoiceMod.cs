// FirstVillageStoreChoice: choose the second-tier (Village) shop of the pre-upgraded starting city yourself.
//
// Vanilla: at map generation one city starts as a Village and UrbanAreaInitialBuilder.BuildTierShops picks its
// Village shop at random. With this mod:
//   1. Map generation: that city stays a Village but its Village shop is NOT built yet (only before any HQ exists).
//   2. You place your HQ:
//      - in that city's region  -> a banner (one month) tells you, and the game's own upgrade flow starts:
//        accept icon over the town -> pick the shop in the native upgrade panel -> it is built. No contracts,
//        no timeout. (Internally the city is a Hamlet only during this accept/choose step, because the native
//        flow always upgrades "current tier -> next tier".)
//      - anywhere else          -> the city gets a random Village shop right away (= vanilla result).
// Nothing extra is stored in the save: the native upgrade state is saved by the game itself.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ClaudeModLib;
using Harmony;
using ProjectAutomata;
using UnityEngine;

namespace FirstVillageStoreChoice
{
    public class FirstVillageStoreChoiceMod : Mod
    {
        const int VillageTierId = 200;
        const int BannerDays = 30;
        const string EventEn = "FVSC_ChooseVillageShop_en", EventZh = "FVSC_ChooseVillageShop_zh";

        static Feature _enabled;
        // Cities currently in our special upgrade -> population before it (restored after the upgrade).
        static readonly Dictionary<SettlementBase, int> _special = new Dictionary<SettlementBase, int>();
        static readonly HashSet<SettlementBase> _handled = new HashSet<SettlementBase>();
        float _nextCheck;

        public override void OnModWasLoaded()
        {
            base.OnModWasLoaded();
            var h = ModLib.Register(this, LocText.Of("First village store choice", "开局村镇商店自选"));
            _enabled = h.AddFeature("enabled",
                LocText.Of("Let me choose the starting village's second-tier shop", "开局村镇的二级商店由我选择"), true,
                LocText.Of("Place your HQ in the pre-upgraded village's region, then accept the upgrade icon and pick the shop. Applies to new games.",
                           "把总部放在开局已升级村镇所在的区，接受城镇上方的升级图标并选择商店。对新开的游戏生效。"));
        }

        public override void OnAllModsLoaded()
        {
            base.OnAllModsLoaded();
            // Our banner events must never be picked by the random event scheduler.
            foreach (var n in new[] { EventEn, EventZh })
            {
                var e = Data.Get<StaticWorldEventData>(n);
                if (e != null) e.triggerMode = WorldEventTriggerMode.MANUAL;
                else Log.Warn("[FVSC] event missing: " + n);
            }
            try { Patch(); } catch (Exception e) { Log.Error("[FVSC] patching failed: " + e); }
        }

        // ------------------------------------------------------------------ patches
        static void Patch()
        {
            var harmony = HarmonyInstance.Create("claudemodding.firstvillagestorechoice");
            var self = typeof(FirstVillageStoreChoiceMod);
            const BindingFlags any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            harmony.Patch(typeof(UrbanAreaInitialBuilder).GetMethod("BuildTierShops", any),
                new HarmonyMethod(self.GetMethod("BuildTierShopsPrefix", any)), null);

            var acceptState = typeof(SettlementAdvancement).GetNestedType("AcceptState", BindingFlags.NonPublic | BindingFlags.Public);
            harmony.Patch(acceptState.GetMethod("Initialize", any), null,
                new HarmonyMethod(self.GetMethod("AcceptInitializePostfix", any)));

            var stateBase = typeof(SettlementAdvancement).GetNestedType("AdvanacementStateBase", BindingFlags.NonPublic | BindingFlags.Public);
            harmony.Patch(stateBase.GetMethod("Advance", any), null,
                new HarmonyMethod(self.GetMethod("AdvancePostfix", any)));
            Log.Info("[FVSC] patches applied");
        }

        // Map generation: do not build the Village shop of a city that starts as a Village (only before any HQ exists).
        static bool BuildTierShopsPrefix(UrbanAreaInitialBuilder __instance, SettlementTier tier)
        {
            try
            {
                if (_enabled == null || !_enabled.enabled || tier == null || tier.tierId != VillageTierId) return true;
                if (HqBuilt()) return true;
                var s = Reflect.Get<SettlementBase>(__instance, "settlement");
                if (s == null || s.tier != tier) return true;
                Log.Info("[FVSC] holding Village shop of " + s.settlementName + " until an HQ is placed");
                return false;
            }
            catch (Exception e) { Log.Warn("[FVSC] prefix: " + e.Message); return true; }
        }

        // Our special upgrade: no contracts (materials count as supplied) and no timeout (waits for the player).
        static void AcceptInitializePostfix(SettlementAdvancement context)
        {
            try
            {
                if (context == null || !_special.ContainsKey(context.settlement)) return;
                var contracts = Reflect.Get<IList>(context, "_contracts");
                if (contracts != null) contracts.Clear();
                var today = TimeManager.instance.today;
                Reflect.Set(context, "_timeoutDate", today.AddDays(36500));
                Log.Info("[FVSC] upgrade offer for " + context.settlement.settlementName + ": no contracts, no timeout");
            }
            catch (Exception e) { Log.Warn("[FVSC] accept postfix: " + e.Message); }
        }

        static void AdvancePostfix(SettlementAdvancement context)
        {
            try
            {
                int pop;
                if (context == null || !_special.TryGetValue(context.settlement, out pop)) return;
                _special.Remove(context.settlement);
                if (context.settlement.population < pop) context.settlement.population = pop;
                Log.Info("[FVSC] " + context.settlement.settlementName + " upgraded with the chosen shop");
            }
            catch (Exception e) { Log.Warn("[FVSC] advance postfix: " + e.Message); }
        }

        // ------------------------------------------------------------------ after HQ placement
        void Update()
        {
            if (!ModLib.inGame || Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 1f;
            if (!HqBuilt()) return;
            try
            {
                foreach (var s in Cities.All())
                {
                    if (s == null || _handled.Contains(s) || !IsHeld(s)) continue;
                    _handled.Add(s);
                    var hq = Hq();
                    if (_enabled.enabled && hq != null && hq.region == s.region && StartChoice(s)) continue;
                    BuildRandomVillageShop(s);
                }
            }
            catch (Exception e) { Log.Warn("[FVSC] update: " + e.Message); }
        }

        // A Village whose Village shop was held back (none built or requested) and that is not already upgrading.
        static bool IsHeld(SettlementBase s)
        {
            if (s.isDead || s.tier == null || s.tier.tierId != VillageTierId) return false;
            if (s.advancement != null && s.advancement.isAdvancing) return false;
            foreach (var shop in VillageShops(s))
                if (s.buildings.Count(shop.building) > 0 || s.buildingQueue.HasRequestForBuilding(shop.building)) return false;
            return true;
        }

        static List<Shop> VillageShops(SettlementBase s) { return s.type.GetShops(s.tier, new List<Shop>()); }

        bool StartChoice(SettlementBase s)
        {
            var village = s.tier;
            var hamlet = village.previousTier;
            if (hamlet == null) return false;
            _special[s] = s.population;
            s.tier = hamlet;                                   // native flow upgrades current -> next
            try { s.advancement.StartAdvancement(Cities.Player); } catch (Exception e) { Log.Warn("[FVSC] StartAdvancement: " + e.Message); }
            if (!s.advancement.isAdvancing)
            {
                Log.Warn("[FVSC] could not start the upgrade offer for " + s.settlementName + "; using a random shop");
                s.tier = village;
                _special.Remove(s);
                return false;
            }
            ShowBanner(s);
            Log.Info("[FVSC] HQ is in " + s.settlementName + "'s region: waiting for the player to accept and choose a shop");
            return true;
        }

        static void BuildRandomVillageShop(SettlementBase s)
        {
            var shops = VillageShops(s);
            if (shops.Count == 0) return;
            var pick = shops[UnityEngine.Random.Range(0, shops.Count)];
            s.buildingQueue.RequestBuilding(pick.building, s.placementContext, null);
            Log.Info("[FVSC] " + s.settlementName + " gets a random Village shop: " + pick.building.name);
        }

        static void ShowBanner(SettlementBase s)
        {
            try
            {
                var data = Data.Get<StaticWorldEventData>(Lang.isChinese ? EventZh : EventEn);
                if (data == null) return;
                var p = WorldEventCreationParams.GetFromPool();
                p.region = s.region;
                p.duration = BannerDays;
                p.forceEvent = true;
                // Same call the game uses for "Settlement Leveled Up" (SettlementAdvancement.OnAdvancementCompleted).
                WorldEventManager.instance.TriggerStaticEvent(data, Cities.Player.worldEvents, p);
            }
            catch (Exception e) { Log.Warn("[FVSC] banner: " + e.Message); }
        }

        static Player LocalPlayer() { return ProjectAutomata.Player.activeActor as Player; }
        static Building Hq() { var p = LocalPlayer(); return p != null ? p.hq : null; }
        static bool HqBuilt() { var p = LocalPlayer(); return p != null && p.hqBuilt; }
    }
}
