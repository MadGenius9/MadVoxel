using MadVoxel.Farming.Crops;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Traders
{
    /// <summary>
    /// What things cost, what a trader will pay, and what reputation is worth.
    ///
    /// Pure and engine-free, because the failure mode here is not a crash: it is a
    /// spread that closes. The moment a trader will pay more for something than they
    /// charge for it, the whole economy is a button you hold down, and nothing in a
    /// screenshot would tell you. <see cref="Spread"/> and the checks around it exist
    /// entirely for that.
    /// </summary>
    public static class TraderPricing
    {
        /// <summary>Reputation tiers make stock cheaper. Per tier, compounding is not needed.</summary>
        public const float DiscountPerTier = 0.045f;

        /// <summary>And make the trader pay better. Deliberately smaller than the discount.</summary>
        public const float BonusPerTier = 0.035f;

        /// <summary>
        /// Litres of bulk produce one harvest item represents, when the crop does not
        /// say. The rate lives on the crop so a grain bin drawing produce out and a
        /// trader pricing it use the same number - a litre of corn and an ear of corn
        /// must not drift into two different economies.
        /// </summary>
        public const float DefaultLitresPerProduceItem = 4f;

        public static float LitresPerItem(CropDefinition crop)
        {
            if (crop == null || crop.litresPerHarvestItem <= 0f) return DefaultLitresPerProduceItem;
            return crop.litresPerHarvestItem;
        }

        /// <summary>
        /// The wholesale discount on bulk produce. Tipping a hopper is selling to a
        /// middleman: you take less per unit and move a thousand times more of it in a
        /// trip. The trip is the cost - a tractor, a harvester, fuel and the drive out
        /// to the outpost - which is why the discount is mild rather than punishing.
        /// </summary>
        public const float BulkRate = 0.8f;

        /// <summary>Tokens spent per point of reputation. Buying is the faster half.</summary>
        public const int TokensPerReputationBuying = 4;
        public const int TokensPerReputationSelling = 9;

        // -------------------------------------------------------------- reputation

        /// <summary>
        /// Which tier a reputation score sits in. Tier 0 is a stranger; the top tier is
        /// <c>reputationTiers - 1</c>, so a five-tier trader tops out at 4.
        /// </summary>
        public static int TierFor(TraderDefinition trader, int reputation)
        {
            if (trader == null) return 0;

            int perTier = Mathf.Max(1, trader.reputationPerTier);
            int top = Mathf.Max(1, trader.reputationTiers) - 1;

            return Mathf.Clamp(reputation / perTier, 0, top);
        }

        /// <summary>Reputation still owed before the next tier, or zero at the top.</summary>
        public static int ToNextTier(TraderDefinition trader, int reputation)
        {
            if (trader == null) return 0;

            int tier = TierFor(trader, reputation);
            if (tier >= Mathf.Max(1, trader.reputationTiers) - 1) return 0;

            int perTier = Mathf.Max(1, trader.reputationPerTier);
            return (tier + 1) * perTier - reputation;
        }

        public static int ReputationForSpend(int tokens)
        {
            return Mathf.Max(0, tokens) / TokensPerReputationBuying;
        }

        public static int ReputationForSale(int tokens)
        {
            return Mathf.Max(0, tokens) / TokensPerReputationSelling;
        }

        // ------------------------------------------------------------------ prices

        /// <summary>
        /// What the trader charges the player for one of an item. Never free: a
        /// worthless item still costs a token, or a trader with a stack of them is a
        /// way to convert nothing into inventory.
        /// </summary>
        public static int SellPrice(TraderDefinition trader, TraderStockEntry entry, int tier)
        {
            if (trader == null || entry.item == null) return 0;

            float markup = Mathf.Max(0.05f, trader.sellMarkup) * Mathf.Max(0.05f, entry.priceMultiplier);
            float discount = Mathf.Max(0.5f, 1f - tier * DiscountPerTier);

            return Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0, entry.item.tradeValue) * markup * discount));
        }

        /// <summary>
        /// What the trader pays the player for one of an item. Zero is a real answer -
        /// it means "I do not want that" - and the caller must refuse the trade rather
        /// than take the item for nothing.
        /// </summary>
        public static int BuyPrice(TraderDefinition trader, ItemDefinition item, int tier)
        {
            if (trader == null || item == null) return 0;

            // A trader does not buy their own tokens back. Without this, tokens are
            // worth a fraction of themselves and every trade leaks value.
            if (item == trader.currencyItem) return 0;
            if (item.tradeValue <= 0) return 0;

            float rate = Mathf.Max(0.01f, trader.buyRate) * (1f + tier * BonusPerTier);
            return Mathf.Max(1, Mathf.FloorToInt(item.tradeValue * rate));
        }

        /// <summary>
        /// The unrounded buy price. Bulk uses it because a thousand litres priced off a
        /// floored per-item figure compounds the rounding into a large error, and
        /// because the floor to one token hides the difference between cheap crops.
        /// </summary>
        public static float BuyPriceExact(TraderDefinition trader, ItemDefinition item, int tier)
        {
            if (trader == null || item == null) return 0f;
            if (item == trader.currencyItem || item.tradeValue <= 0) return 0f;

            return item.tradeValue * Mathf.Max(0.01f, trader.buyRate) * (1f + tier * BonusPerTier);
        }

        public static bool WillBuy(TraderDefinition trader, ItemDefinition item)
        {
            return BuyPrice(trader, item, 0) > 0;
        }

        /// <summary>
        /// The margin between what a trader charges for an item and what they will pay
        /// for it, at a given tier. It must stay positive at every tier of every trader
        /// or the economy is a money printer.
        /// </summary>
        public static int Spread(TraderDefinition trader, TraderStockEntry entry, int tier)
        {
            return SellPrice(trader, entry, tier) - BuyPrice(trader, entry.item, tier);
        }

        // ------------------------------------------------------------------ litres

        /// <summary>
        /// What a litre of bulk produce fetches, in tokens, as a float - a litre of
        /// wheat is worth well under a token, and rounding each one up would make a
        /// hopper worth more than the farm. The caller rounds the total.
        /// </summary>
        public static float LitreValue(TraderDefinition trader, CropDefinition crop, int tier)
        {
            if (trader == null || crop == null || crop.harvestItem == null) return 0f;

            // The exact rate, not the rounded one. BuyPrice floors and then floors up
            // to one token, which makes every cheap crop worth the same by hand - corn
            // and grain both land on 1 - and pricing litres through that would flatten
            // the whole field economy into a single number.
            float perItem = BuyPriceExact(trader, crop.harvestItem, tier);
            return perItem * BulkRate / LitresPerItem(crop);
        }

        /// <summary>
        /// What a hopper of produce is worth, rounded once at the end. Bulk is where
        /// the field layer pays: it is a worse rate per item than carrying the crop by
        /// hand would be, and you can move a thousand times more of it.
        /// </summary>
        public static int LitrePayout(TraderDefinition trader, CropDefinition crop, int tier, float litres)
        {
            if (litres <= 0f) return 0;

            float value = LitreValue(trader, crop, tier) * litres;
            return Mathf.Max(0, Mathf.FloorToInt(value));
        }

        // ------------------------------------------------------------------ stock

        /// <summary>Is this line of stock visible to a player at this tier?</summary>
        public static bool IsOffered(TraderStockEntry entry, int tier)
        {
            return entry.item != null && tier >= entry.minReputationTier;
        }

        /// <summary>
        /// How much of a line has come back in. Derived from whole restock cycles
        /// elapsed rather than ticked, so a trader you have not visited for a week is
        /// correctly stocked the moment you walk in - and is not stocked twice because
        /// two systems both ticked them.
        /// </summary>
        public static int Restocked(int current, int max, double hoursSinceRestock, float restockHours)
        {
            if (max <= 0) return 0;
            if (current >= max) return max;

            int cycles = Mathf.FloorToInt((float)(hoursSinceRestock / Mathf.Max(0.1f, restockHours)));
            if (cycles <= 0) return Mathf.Clamp(current, 0, max);

            // A whole cycle restores a quarter of the line, so clearing a trader out
            // costs several days rather than one sleep.
            int perCycle = Mathf.Max(1, Mathf.CeilToInt(max * 0.25f));
            return Mathf.Clamp(current + cycles * perCycle, 0, max);
        }

        /// <summary>
        /// How many whole restock cycles to credit, so the caller can advance its
        /// stored hour by exactly that many and keep the remainder.
        /// </summary>
        public static int CyclesElapsed(double hoursSinceRestock, float restockHours)
        {
            return Mathf.Max(0, Mathf.FloorToInt((float)(hoursSinceRestock / Mathf.Max(0.1f, restockHours))));
        }

        // ------------------------------------------------------------------ wording

        public static string TierName(int tier)
        {
            switch (tier)
            {
                case 0: return "STRANGER";
                case 1: return "KNOWN";
                case 2: return "REGULAR";
                case 3: return "TRUSTED";
                default: return "PARTNER";
            }
        }
    }
}
