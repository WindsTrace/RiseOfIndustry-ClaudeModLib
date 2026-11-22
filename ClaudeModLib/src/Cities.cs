using System;
using System.Collections;
using System.Collections.Generic;
using ProjectAutomata;
using UnityEngine;

namespace ClaudeModLib
{
    public enum CityStatus
    {
        Growing,            // consuming goods and below the population limit
        Stagnant,           // not growing (not enough goods consumed)
        LimitReached,       // at next tier threshold, advancement not started yet
        WaitingForSponsor,  // at threshold but nobody owns the region permit
        Advancing,          // upgrade contracts in progress
        MaxTier,
        Dead
    }

    /// <summary>Snapshot of one city's growth state. Values are read-only copies.</summary>
    public class CityInfo
    {
        public SettlementBase settlement;
        public string name;
        public int population;
        public SettlementTier tier;
        public SettlementTier nextTier;          // null at max tier
        public int tierThreshold;                // this city's rolled threshold for its current tier
        public int nextTierThreshold;            // this city's rolled threshold for the next tier
        public float progress;                   // 0..1 between the two thresholds
        public CityStatus status;
        public int shopCount;

        public int remaining { get { return Math.Max(0, nextTierThreshold - population); } }
        public string tierName { get { return tier != null ? tier.tierName : "?"; } }
        public string nextTierName { get { return nextTier != null ? nextTier.tierName : null; } }
    }

    /// <summary>
    /// City (settlement) growth API. Mechanics, verified from game code (2.3.3):
    /// population grows every 15 days from goods consumed in its shops (per consumed product:
    /// random 130-330 x category growth multiplier [tier1 x2, tier2 x3, tier3 x4, raw/farm x1], max 5 products);
    /// each city rolls a threshold per tier at start (Village 125k-175k, Town 275k-325k, City 475k-525k,
    /// Metropolis 725k-775k); at the threshold growth stops and, if someone owns the region permit,
    /// upgrade contracts (Wall Panels/Bricks/Reinforced Wall) start; otherwise the city waits for a sponsor.
    /// </summary>
    public static class Cities
    {
        static System.Reflection.FieldInfo _salesField = Reflect.FindField(typeof(Shop), "_sales");
        static System.Reflection.MethodInfo _deliveredMethod =
            typeof(Shop).GetMethod("GetDeliveredByActor", Reflect.All, null, new[] { typeof(IActor) }, null);

        /// <summary>All settlements in the running game (empty outside a game).</summary>
        public static List<SettlementBase> All()
        {
            var manager = UnityEngine.Object.FindObjectOfType<SettlementManager>();
            return manager != null && manager.settlements != null ? new List<SettlementBase>(manager.settlements) : new List<SettlementBase>();
        }

        /// <summary>The human player's actor (use as IActor for sales/delivery queries).</summary>
        public static IActor Player { get { return ProjectAutomata.Player.activeActor; } }

        public static CityInfo GetInfo(SettlementBase s)
        {
            var info = new CityInfo();
            info.settlement = s;
            info.name = s.settlementName;
            info.population = s.population;
            info.tier = s.tier;
            info.nextTier = s.tier != null ? s.tier.nextTier : null;
            var growth = s.growth;
            var shops = s.buildings != null ? s.buildings.shops : null;
            info.shopCount = shops != null ? shops.Count : 0;

            if (s.isDead) { info.status = CityStatus.Dead; return info; }
            if (info.nextTier == null) { info.status = CityStatus.MaxTier; info.progress = 1f; return info; }

            info.tierThreshold = growth != null ? growth.GetTierThreshold(info.tier) : 0;
            info.nextTierThreshold = growth != null ? growth.GetTierThreshold(info.nextTier) : 0;
            int span = Math.Max(1, info.nextTierThreshold - info.tierThreshold);
            info.progress = Mathf.Clamp01((info.population - info.tierThreshold) / (float)span);

            // isAdvancing throws while the world is still being generated (advancement state not initialised yet).
            bool advancing = false;
            try { var adv = s.advancement; advancing = adv != null && adv.isAdvancing; } catch (NullReferenceException) { }
            if (advancing) info.status = CityStatus.Advancing;
            else if (growth != null && growth.IsWaitingForSponsor()) info.status = CityStatus.WaitingForSponsor;
            else if (growth != null && growth.populationLimitReached) info.status = CityStatus.LimitReached;
            else if (growth != null && growth.IsGrowing()) info.status = CityStatus.Growing;
            else info.status = CityStatus.Stagnant;
            return info;
        }

        /// <summary>True if any shop of the city has sales or deliveries attributed to <paramref name="actor"/>.</summary>
        public static bool HasSupplied(SettlementBase s, IActor actor)
        {
            if (actor == null || s.buildings == null || s.buildings.shops == null) return false;
            foreach (var shop in s.buildings.shops)
            {
                if (shop == null) continue;
                if (_salesField != null)
                {
                    var sales = _salesField.GetValue(shop) as IDictionary;
                    if (sales != null && sales.Contains(actor)) return true;
                }
                if (_deliveredMethod != null)
                {
                    var delivered = _deliveredMethod.Invoke(shop, new object[] { actor }) as ICollection;
                    if (delivered != null && delivered.Count > 0) return true;
                }
            }
            return false;
        }

        /// <summary>Tier ids: 100 Hamlet, 200 Village, 300 Town, 400 City, 500 Metropolis, 1000 Bloated.</summary>
        public const int Hamlet = 100, Village = 200, Town = 300, City = 400, Metropolis = 500, Bloated = 1000;

        /// <summary>
        /// Tier name in the current <see cref="Lang"/>, by tierId. Do not use SettlementTier.tierName to tell tiers apart:
        /// the game's Chinese translation uses "村庄" for both Hamlet and Village.
        /// </summary>
        public static string TierName(SettlementTier tier)
        {
            if (tier == null) return "?";
            switch (tier.tierId)
            {
                case Hamlet: return Lang.T("Hamlet", "村庄");
                case Village: return Lang.T("Village", "村镇");
                case Town: return Lang.T("Town", "城镇");
                case City: return Lang.T("City", "城市");
                case Metropolis: return Lang.T("Metropolis", "大都市");
                case Bloated: return Lang.T("Bloated", "超大都市");
                default: return tier.tierName;
            }
        }

        public static SettlementTier TierById(int tierId)
        {
            foreach (var t in Tiers()) if (t.tierId == tierId) return t;
            return null;
        }

        /// <summary>Shop prefabs this city's type can have at <paramref name="tier"/> (what the game picks from).</summary>
        public static List<Shop> ShopsForTier(SettlementBase s, SettlementTier tier)
        {
            return s.type.GetShops(tier, new List<Shop>());
        }

        /// <summary>True if one of the tier's shops is built or queued in the city.</summary>
        public static bool HasTierShop(SettlementBase s, SettlementTier tier)
        {
            foreach (var shop in ShopsForTier(s, tier))
                if (s.buildings.Count(shop.building) > 0 || s.buildingQueue.HasRequestForBuilding(shop.building)) return true;
            return false;
        }

        /// <summary>Asks the city to build a shop (same call the game uses after an upgrade).</summary>
        public static void BuildShop(SettlementBase s, Shop shopPrefab)
        {
            s.buildingQueue.RequestBuilding(shopPrefab.building, s.placementContext, null);
        }

        /// <summary>Static tier table (all SettlementTier assets ordered by tierId).</summary>
        public static List<SettlementTier> Tiers()
        {
            var list = Data.All<SettlementTier>();
            list.Sort((a, b) => a.tierId.CompareTo(b.tierId));
            return list;
        }
    }
}
