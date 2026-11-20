using System.Collections.Generic;
using ProjectAutomata;

namespace ClaudeModLib
{
    /// <summary>
    /// Shop product selection. Verified behaviour (Shop.GetValidProducts / Initialize, game 2.3.3):
    /// candidates = every product having ANY of the shop's soldTags; shuffled with new Random(region center);
    /// the first maxProducts are stocked. The choice is made when a shop initializes, so existing shops in a
    /// save may keep their old list. Example: ToyStore sells tag "Toy", maxProducts 8, Village/Town tier cities.
    /// </summary>
    public static class Shops
    {
        public static ProductTag[] SoldTags(Shop shop) { return Reflect.GetOr<ProductTag[]>(shop, "soldTags", new ProductTag[0]); }
        public static int MaxProducts(Shop shop) { return Reflect.GetOr<int>(shop, "maxProducts", 0); }
        public static void SetMaxProducts(Shop shop, int value) { Reflect.Set(shop, "maxProducts", value); }

        /// <summary>Products currently stocked by a placed shop (empty for prefabs).</summary>
        public static List<ProductDefinition> Stocked(Shop shop)
        {
            return new List<ProductDefinition>(Reflect.GetOr<List<ProductDefinition>>(shop, "sold", new List<ProductDefinition>()));
        }

        /// <summary>All products a shop prefab could pick from (tag match), before shuffling.</summary>
        public static List<ProductDefinition> Candidates(Shop shop)
        {
            var tags = SoldTags(shop);
            var list = new List<ProductDefinition>();
            foreach (var p in Data.All<ProductDefinition>()) if (p.HasAnyTag(tags)) list.Add(p);
            return list;
        }

        /// <summary>Shop prefabs (buildings with a Shop component) that would consider <paramref name="product"/>.</summary>
        public static List<Building> ShopsSelling(ProductDefinition product)
        {
            var list = new List<Building>();
            foreach (var b in Data.BuildingsWith<Shop>())
                if (product.HasAnyTag(SoldTags(b.GetComponent<Shop>()))) list.Add(b);
            return list;
        }

        /// <summary>
        /// Guarantees <paramref name="productName"/> is stocked wherever a matching shop appears, by raising
        /// maxProducts of those shop prefabs to the candidate count. Changes vanilla balance (more products per shop).
        /// Call after all mods are loaded. Returns the shops changed.
        /// </summary>
        public static List<string> GuaranteeListing(string productName)
        {
            var changed = new List<string>();
            var product = Data.Require<ProductDefinition>(productName);
            foreach (var b in ShopsSelling(product))
            {
                var shop = b.GetComponent<Shop>();
                int candidates = Candidates(shop).Count, max = MaxProducts(shop);
                if (max < candidates)
                {
                    SetMaxProducts(shop, candidates);
                    changed.Add(b.name + " " + max + "->" + candidates);
                }
            }
            if (changed.Count > 0) Log.Info("GuaranteeListing(" + productName + "): " + string.Join(", ", changed.ToArray()));
            return changed;
        }

        /// <summary>Current demand of a placed shop for a product, as seen by <paramref name="actor"/>.</summary>
        public static int Demand(Shop shop, ProductDefinition product, IActor actor) { return shop.GetDemand(product, actor); }
    }
}
