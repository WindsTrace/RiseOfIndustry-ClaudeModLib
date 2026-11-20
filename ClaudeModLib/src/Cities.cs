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

            var adv = s.advancement;
            if (adv != null && adv.isAdvancing) info.status = CityStatus.Advancing;
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

        /// <summary>Static tier table (all SettlementTier assets ordered by tierId).</summary>
        public static List<SettlementTier> Tiers()
        {
            var list = Data.All<SettlementTier>();
            list.Sort((a, b) => a.tierId.CompareTo(b.tierId));
            return list;
        }
    }
}
