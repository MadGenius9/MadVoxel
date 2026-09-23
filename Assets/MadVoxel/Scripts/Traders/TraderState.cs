using System.Collections.Generic;
using MadVoxel.Farming.Crops;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Traders
{
    /// <summary>Why a trade did not happen. Every one of these is shown to the player.</summary>
    public enum TradeResult
    {
        Ok,
        NoSuchLine,
        /// <summary>Reputation too low: the line is not on the board yet.</summary>
        NotOffered,
        OutOfStock,
        CannotAfford,
        NoRoom,
        /// <summary>The trader does not want it, or you have none.</summary>
        NotWanted,
        NothingToSell
    }

    /// <summary>
    /// One trader's live books: what is on the shelf, how well they know you, and every
    /// transaction they will make.
    ///
    /// Plain C# with no engine references so the whole economy can be played out in the
    /// headless suite. An exchange that loses an item, pays for goods it did not take,
    /// or lets a full bag swallow a purchase is not something an editor would show you
    /// until it had already eaten someone's afternoon.
    ///
    /// Every transaction is written the same way: work out the whole trade, refuse it
    /// outright if any part fails, and only then move anything. There is no partial
    /// trade and no path that takes payment without handing over goods.
    /// </summary>
    public class TraderState
    {
        public TraderDefinition Definition { get; private set; }
        public int Reputation { get; private set; }

        /// <summary>The hour the shelves were last topped up. Restocking is derived from it.</summary>
        public double LastRestockHours { get; private set; }

        int[] _stock = new int[0];

        public int Tier { get { return TraderPricing.TierFor(Definition, Reputation); } }
        public string TierName { get { return TraderPricing.TierName(Tier); } }
        public int ToNextTier { get { return TraderPricing.ToNextTier(Definition, Reputation); } }

        public int LineCount { get { return Definition != null ? Definition.stock.Count : 0; } }

        public void Init(TraderDefinition definition, double nowHours)
        {
            Definition = definition;
            LastRestockHours = nowHours;

            _stock = new int[definition != null ? definition.stock.Count : 0];
            for (int i = 0; i < _stock.Length; i++) _stock[i] = definition.stock[i].stockCount;
        }

        public TraderStockEntry Line(int index)
        {
            return index >= 0 && index < LineCount ? Definition.stock[index] : default(TraderStockEntry);
        }

        public int StockOf(int index)
        {
            return index >= 0 && index < _stock.Length ? _stock[index] : 0;
        }

        public bool IsOffered(int index)
        {
            return index >= 0 && index < LineCount && TraderPricing.IsOffered(Definition.stock[index], Tier);
        }

        public int PriceOf(int index)
        {
            return TraderPricing.SellPrice(Definition, Line(index), Tier);
        }

        // ---------------------------------------------------------------- restocking

        /// <summary>
        /// Tops the shelves up for however many whole restock cycles have passed. The
        /// remainder is kept, so visiting every hour does not reset the clock and stall
        /// a restock forever.
        /// </summary>
        public void Refresh(double nowHours)
        {
            if (Definition == null) return;

            double elapsed = nowHours - LastRestockHours;
            if (elapsed <= 0.0) return;

            int cycles = TraderPricing.CyclesElapsed(elapsed, Definition.restockHours);
            if (cycles <= 0) return;

            for (int i = 0; i < _stock.Length; i++)
            {
                _stock[i] = TraderPricing.Restocked(_stock[i], Definition.stock[i].stockCount,
                                                    elapsed, Definition.restockHours);
            }

            LastRestockHours += cycles * (double)Mathf.Max(0.1f, Definition.restockHours);
        }

        // ------------------------------------------------------------------- buying

        /// <summary>
        /// Can this be bought right now, and what would it cost? The cost is set even
        /// when the answer is no, so the board can show the price beside the reason.
        /// </summary>
        public TradeResult CanBuy(MadVoxel.Inventory.Inventory bag, int index, int count, out int cost)
        {
            cost = 0;
            if (index < 0 || index >= LineCount || Line(index).item == null) return TradeResult.NoSuchLine;
            if (count <= 0) return TradeResult.NoSuchLine;
            if (!IsOffered(index)) return TradeResult.NotOffered;

            cost = PriceOf(index) * count;

            if (StockOf(index) < count) return TradeResult.OutOfStock;
            if (bag == null) return TradeResult.NoRoom;
            if (Tokens(bag) < cost) return TradeResult.CannotAfford;

            // Asked before anything moves: paying for goods that will not fit is the
            // one way a trade can silently cost the player something for nothing.
            if (!bag.CanFit(Line(index).item, count)) return TradeResult.NoRoom;

            return TradeResult.Ok;
        }

        /// <summary>Buys, or changes nothing at all. Reputation only moves on a real sale.</summary>
        public TradeResult Buy(MadVoxel.Inventory.Inventory bag, int index, int count, out int spent)
        {
            spent = 0;

            int cost;
            var check = CanBuy(bag, index, count, out cost);
            if (check != TradeResult.Ok) return check;

            bag.Remove(Definition.currencyItem, cost);

            int leftover = bag.Add(Line(index).item, count);
            if (leftover > 0)
            {
                // CanFit said it would fit. If it did not, put the money back rather
                // than leave the player short - and say so, because that is a bug.
                bag.Add(Definition.currencyItem, cost);
                bag.Remove(Line(index).item, count - leftover);
                return TradeResult.NoRoom;
            }

            _stock[index] -= count;
            spent = cost;
            Reputation += TraderPricing.ReputationForSpend(cost);
            return TradeResult.Ok;
        }

        // ------------------------------------------------------------------ selling

        public TradeResult CanSell(MadVoxel.Inventory.Inventory bag, ItemDefinition item, int count, out int payout)
        {
            payout = 0;
            if (bag == null || item == null || count <= 0) return TradeResult.NotWanted;

            int each = TraderPricing.BuyPrice(Definition, item, Tier);
            if (each <= 0) return TradeResult.NotWanted;

            int held = bag.CountOf(item);
            if (held < count) return TradeResult.NothingToSell;

            payout = each * count;

            // Tokens take a slot too, and a bag full of stone cannot receive payment.
            if (!bag.CanFit(Definition.currencyItem, payout)) return TradeResult.NoRoom;

            return TradeResult.Ok;
        }

        public TradeResult Sell(MadVoxel.Inventory.Inventory bag, ItemDefinition item, int count, out int earned)
        {
            earned = 0;

            int payout;
            var check = CanSell(bag, item, count, out payout);
            if (check != TradeResult.Ok) return check;

            int removed = bag.Remove(item, count);
            if (removed < count)
            {
                bag.Add(item, removed);
                return TradeResult.NothingToSell;
            }

            bag.Add(Definition.currencyItem, payout);
            earned = payout;
            Reputation += TraderPricing.ReputationForSale(payout);
            return TradeResult.Ok;
        }

        // ------------------------------------------------------------------- litres

        /// <summary>
        /// Buys bulk produce straight out of a machine's hopper. This is what the field
        /// layer is for: you drive the harvest to the outpost rather than carrying it a
        /// sack at a time, and the rate is worse per item than selling by hand because
        /// you can move a thousand times more of it in one trip.
        ///
        /// Returns the litres actually taken, which is everything the trader could pay
        /// for. A payout of zero takes nothing - a trader who cannot afford a hopper
        /// does not get it for free.
        /// </summary>
        public TradeResult BuyLitres(MadVoxel.Inventory.Inventory bag, CropDefinition crop, float litres,
                                     out float taken, out int paid)
        {
            taken = 0f;
            paid = 0;

            if (bag == null || crop == null || litres <= 0f) return TradeResult.NothingToSell;

            int payout = TraderPricing.LitrePayout(Definition, crop, Tier, litres);
            if (payout <= 0) return TradeResult.NotWanted;

            // Reported either way, so the caller can say "no room for 1,200 tokens"
            // rather than "they do not want that", which would be a lie.
            paid = payout;

            if (!bag.CanFit(Definition.currencyItem, payout))
            {
                paid = 0;
                return TradeResult.NoRoom;
            }

            bag.Add(Definition.currencyItem, payout);
            Reputation += TraderPricing.ReputationForSale(payout);

            taken = litres;
            return TradeResult.Ok;
        }

        public int Tokens(MadVoxel.Inventory.Inventory bag)
        {
            return bag != null && Definition != null && Definition.currencyItem != null
                ? bag.CountOf(Definition.currencyItem) : 0;
        }

        // -------------------------------------------------------------------- saving

        public void LoadState(int reputation, double lastRestockHours, IList<int> stock)
        {
            Reputation = Mathf.Max(0, reputation);
            LastRestockHours = lastRestockHours;

            if (stock == null) return;
            for (int i = 0; i < _stock.Length && i < stock.Count; i++)
            {
                _stock[i] = Mathf.Clamp(stock[i], 0, Definition.stock[i].stockCount);
            }
        }

        public int[] SaveStock()
        {
            var copy = new int[_stock.Length];
            System.Array.Copy(_stock, copy, _stock.Length);
            return copy;
        }

        /// <summary>Used by quests and the developer tools; never by a trade.</summary>
        public void AddReputation(int amount)
        {
            Reputation = Mathf.Max(0, Reputation + amount);
        }

        public static string Describe(TradeResult result)
        {
            switch (result)
            {
                case TradeResult.Ok: return "Done";
                case TradeResult.NotOffered: return "Not for you yet";
                case TradeResult.OutOfStock: return "Out of stock";
                case TradeResult.CannotAfford: return "Not enough tokens";
                case TradeResult.NoRoom: return "No room in your bag";
                case TradeResult.NotWanted: return "They do not want that";
                case TradeResult.NothingToSell: return "You have none";
                default: return "No such line";
            }
        }
    }
}
