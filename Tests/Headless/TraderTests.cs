using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.Inventory;
using MadVoxel.Farming.Crops;
using MadVoxel.Traders;
using Inv = MadVoxel.Inventory.Inventory;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The trader economy. Most of this is about one failure: a spread that closes.
    /// The moment a trader pays more for something than they charge for it, the game
    /// is a button you hold down, and nothing on screen would tell you.
    /// </summary>
    public static class TraderTests
    {
        public static void Run(ContentDatabase db)
        {
            if (db == null) return;

            Reputation(db);
            NoMoneyPrinter(db);
            Buying(db);
            Selling(db);
            Litres(db);
            Restocking(db);
        }

        static TraderDefinition Vance(ContentDatabase db)
        {
            for (int i = 0; i < db.traders.Count; i++)
            {
                if (db.traders[i].stringId == "madvoxel:trader_vance") return db.traders[i];
            }
            return db.traders.Count > 0 ? db.traders[0] : null;
        }

        static Inv Bag(ContentDatabase db, int tokens)
        {
            var bag = new Inv(36);
            if (tokens > 0) bag.Add(db.Item(ItemIds.TradeToken), tokens);
            return bag;
        }

        /// <summary>The index of the first line a stranger can already buy.</summary>
        static int OpenLine(TraderState state)
        {
            for (int i = 0; i < state.LineCount; i++)
            {
                if (state.IsOffered(i)) return i;
            }
            return -1;
        }

        // -------------------------------------------------------------- reputation

        static void Reputation(ContentDatabase db)
        {
            Harness.Section("traders: reputation");

            var trader = Vance(db);
            Harness.Check(trader != null, "there is a trader to test");
            if (trader == null) return;

            int perTier = trader.reputationPerTier;
            int top = trader.reputationTiers - 1;

            Harness.Equal(TraderPricing.TierFor(trader, 0), 0, "a new player is a stranger");
            Harness.Equal(TraderPricing.TierFor(trader, perTier - 1), 0, "one short of a tier is still the old tier");
            Harness.Equal(TraderPricing.TierFor(trader, perTier), 1, "and the next point earns it");
            Harness.Equal(TraderPricing.TierFor(trader, perTier * 99), top,
                "reputation caps at the top tier rather than running off the end");
            Harness.Equal(TraderPricing.TierFor(trader, -50), 0, "and cannot go below stranger");

            Harness.Equal(TraderPricing.ToNextTier(trader, 0), perTier, "a stranger owes a full tier");
            Harness.Equal(TraderPricing.ToNextTier(trader, perTier - 1), 1, "and at the edge, one point");
            Harness.Equal(TraderPricing.ToNextTier(trader, perTier * 99), 0, "the top tier owes nothing");

            Harness.Check(TraderPricing.ReputationForSpend(100) > TraderPricing.ReputationForSale(100),
                "buying earns a trader's regard faster than selling to them");
            Harness.Equal(TraderPricing.ReputationForSpend(0), 0, "spending nothing earns nothing");
            Harness.Equal(TraderPricing.ReputationForSpend(-100), 0, "and neither does a negative");
        }

        // ---------------------------------------------------------- the money printer

        /// <summary>
        /// Every line of every shipped trader, at every reputation tier they have. If
        /// any of them ever pays more than it charges, buy it and sell it back forever.
        /// </summary>
        static void NoMoneyPrinter(ContentDatabase db)
        {
            Harness.Section("traders: the spread never closes");

            var closed = new List<string>();
            int lines = 0;

            for (int t = 0; t < db.traders.Count; t++)
            {
                var trader = db.traders[t];
                for (int tier = 0; tier < trader.reputationTiers; tier++)
                {
                    for (int s = 0; s < trader.stock.Count; s++)
                    {
                        var entry = trader.stock[s];
                        lines++;

                        int spread = TraderPricing.Spread(trader, entry, tier);
                        if (spread <= 0)
                        {
                            closed.Add(string.Format("{0} tier {1}: {2} sells {3}, buys {4}",
                                trader.displayName, tier, entry.item.displayName,
                                TraderPricing.SellPrice(trader, entry, tier),
                                TraderPricing.BuyPrice(trader, entry.item, tier)));
                        }
                    }
                }
            }

            Harness.Check(closed.Count == 0,
                string.Format("all {0} trader line/tier combinations keep a positive spread", lines)
                + (closed.Count > 0 ? ": " + string.Join("; ", closed) : ""));

            // And the same thing played out with real money and a real bag, at the top
            // tier where the margin is thinnest.
            var vance = Vance(db);
            var state = new TraderState();
            state.Init(vance, 0.0);
            state.AddReputation(vance.reputationPerTier * vance.reputationTiers);

            var bag = Bag(db, 5000);
            var token = db.Item(ItemIds.TradeToken);
            int before = bag.CountOf(token);

            int line = OpenLine(state);
            Harness.Check(line >= 0, "a partner can buy something");

            int spent;
            var bought = state.Buy(bag, line, 1, out spent);
            Harness.Equal((int)bought, (int)TradeResult.Ok, "a partner buys one of the first line");

            int earned;
            state.Sell(bag, state.Line(line).item, 1, out earned);

            Harness.Check(earned < spent,
                string.Format("selling it straight back returns {0} of the {1} paid", earned, spent));
            Harness.Check(bag.CountOf(token) < before,
                "so a buy-and-sell-back round trip always leaves the player poorer");
        }

        // ------------------------------------------------------------------ buying

        static void Buying(ContentDatabase db)
        {
            Harness.Section("traders: buying");

            var vance = Vance(db);
            var state = new TraderState();
            state.Init(vance, 0.0);

            var token = db.Item(ItemIds.TradeToken);
            int line = OpenLine(state);
            var item = state.Line(line).item;

            // Broke: the trade is refused and nothing moves.
            var broke = new Inv(36);
            int stockBefore = state.StockOf(line);

            int spent;
            Harness.Equal((int)state.Buy(broke, line, 1, out spent), (int)TradeResult.CannotAfford,
                "no tokens, no goods");
            Harness.Equal(spent, 0, "and nothing was spent");
            Harness.Equal(state.StockOf(line), stockBefore, "the shelf is untouched");
            Harness.Equal(broke.CountOf(item), 0, "and the bag is empty");

            // A real purchase.
            var bag = Bag(db, 500);
            int price = state.PriceOf(line);

            Harness.Equal((int)state.Buy(bag, line, 2, out spent), (int)TradeResult.Ok, "with tokens it goes through");
            Harness.Equal(spent, price * 2, "the cost is the price times the count");
            Harness.Equal(bag.CountOf(token), 500 - price * 2, "exactly that many tokens leave the bag");
            Harness.Equal(bag.CountOf(item), 2, "and two of the goods arrive");
            Harness.Equal(state.StockOf(line), stockBefore - 2, "the shelf is two lighter");
            Harness.Check(state.Reputation > 0, "and the trader thinks a little better of you");

            // More than they have: take the shelf rather than refusing it. Refusing
            // outright reads as a broken shop while the board still shows stock.
            int onShelf = state.StockOf(line);
            Harness.Equal((int)state.Buy(bag, line, 9999, out spent), (int)TradeResult.Ok,
                "asking for more than they have buys what they have");
            Harness.Equal(state.StockOf(line), 0, "which clears the shelf");
            Harness.Equal(spent, price * onShelf, "and costs exactly what was taken");

            Harness.Equal((int)state.Buy(bag, line, 1, out spent), (int)TradeResult.OutOfStock,
                "an empty shelf then refuses");
            Harness.Equal(spent, 0, "and a refused trade spends nothing");

            // The purse clamps the same way the shelf does.
            var thin = new TraderState();
            thin.Init(vance, 0.0);

            int thinLine = OpenLine(thin);
            int unit = thin.PriceOf(thinLine);

            var pocket = Bag(db, unit * 3);
            int bought;
            Harness.Equal((int)thin.Buy(pocket, thinLine, 10, out spent, out bought), (int)TradeResult.Ok,
                "asking for ten with three tokens' worth buys what you can afford");
            Harness.Equal(bought, 3, "which is three");
            Harness.Equal(spent, unit * 3, "for exactly three tokens' worth");
            Harness.Equal(pocket.CountOf(token), 0, "leaving the purse empty");

            Harness.Equal((int)thin.Buy(pocket, thinLine, 1, out spent, out bought), (int)TradeResult.CannotAfford,
                "and an empty purse then refuses");
            Harness.Equal(bought, 0, "having bought nothing");

            // A line above your station.
            int gated = -1;
            for (int i = 0; i < state.LineCount; i++)
            {
                if (state.Line(i).minReputationTier > 0) gated = i;
            }
            if (gated >= 0)
            {
                var rich = Bag(db, 3000);
                var locked = new TraderState();
                locked.Init(vance, 0.0);
                Harness.Equal((int)locked.Buy(rich, gated, 1, out spent), (int)TradeResult.NotOffered,
                    "money does not buy what reputation gates");

                locked.AddReputation(vance.reputationPerTier * locked.Line(gated).minReputationTier);
                Harness.Equal((int)locked.Buy(rich, gated, 1, out spent), (int)TradeResult.Ok,
                    "and reputation does");
            }

            // A bag with no room must not take payment. Fill every slot with something
            // that cannot stack with the purchase.
            var full = new Inv(36);
            var filler = db.Item(ItemIds.EngineBlock);
            for (int i = 0; i < full.Size; i++) full.SetSlot(i, new ItemStack(filler, filler.maxStack));
            full.SetSlot(0, new ItemStack(token, 500));

            var tight = new TraderState();
            tight.Init(vance, 0.0);

            int tokensBefore = full.CountOf(token);
            var result = tight.Buy(full, line, 1, out spent);
            Harness.Check(result == TradeResult.NoRoom || result == TradeResult.Ok,
                "a full bag either refuses the trade or finds room");
            if (result == TradeResult.NoRoom)
            {
                Harness.Equal(full.CountOf(token), tokensBefore, "and a refused trade never takes the money");
                Harness.Equal(tight.StockOf(line), tight.Line(line).stockCount, "nor the stock");
            }

            Harness.Equal((int)state.Buy(bag, -1, 1, out spent), (int)TradeResult.NoSuchLine, "there is no line -1");
            Harness.Equal((int)state.Buy(bag, line, 0, out spent), (int)TradeResult.NoSuchLine, "nor a trade of nothing");
        }

        // ----------------------------------------------------------------- selling

        static void Selling(ContentDatabase db)
        {
            Harness.Section("traders: selling");

            var vance = Vance(db);
            var state = new TraderState();
            state.Init(vance, 0.0);

            var token = db.Item(ItemIds.TradeToken);
            var scrap = db.Item(ItemIds.ScrapMetal);

            var bag = new Inv(36);
            bag.Add(scrap, 40);

            int earned;
            Harness.Equal((int)state.Sell(bag, scrap, 10, out earned), (int)TradeResult.Ok, "scrap sells");
            Harness.Check(earned > 0, "for something");
            Harness.Equal(bag.CountOf(scrap), 30, "and exactly ten leave the bag");
            Harness.Equal(bag.CountOf(token), earned, "with the payment arriving in tokens");

            Harness.Equal((int)state.Sell(bag, scrap, 999, out earned), (int)TradeResult.NothingToSell,
                "you cannot sell what you do not have");
            Harness.Equal(earned, 0, "and are paid nothing for trying");
            Harness.Equal(bag.CountOf(scrap), 30, "the scrap stays put");

            // THE one that would leak value forever: tokens are not a commodity.
            Harness.Check(!TraderPricing.WillBuy(vance, token), "a trader will not buy their own tokens");
            Harness.Equal((int)state.Sell(bag, token, 1, out earned), (int)TradeResult.NotWanted,
                "so selling tokens back is refused");

            int tokensBefore = bag.CountOf(token);
            state.Sell(bag, token, 5, out earned);
            Harness.Equal(bag.CountOf(token), tokensBefore, "and the attempt costs nothing");

            Harness.Equal((int)state.Sell(bag, null, 1, out earned), (int)TradeResult.NotWanted, "nor is nothing sellable");
            Harness.Equal((int)state.Sell(bag, scrap, 0, out earned), (int)TradeResult.NotWanted, "nor a sale of zero");

            // A better-regarded player is paid better for the same thing.
            var partner = new TraderState();
            partner.Init(vance, 0.0);
            partner.AddReputation(vance.reputationPerTier * 4);

            Harness.Check(TraderPricing.BuyPrice(vance, scrap, partner.Tier) >= TraderPricing.BuyPrice(vance, scrap, 0),
                "a partner is paid at least as well as a stranger");
        }

        // ------------------------------------------------------------------ litres

        static void Litres(ContentDatabase db)
        {
            Harness.Section("traders: bulk produce");

            var vance = Vance(db);
            var state = new TraderState();
            state.Init(vance, 0.0);

            CropDefinitionShim.Find(db, out var corn);
            Harness.Check(corn != null, "there is a field crop to sell");
            if (corn == null) return;

            var token = db.Item(ItemIds.TradeToken);
            var bag = new Inv(36);

            int paid;
            float taken;
            state.BuyLitres(bag, corn, 1000f, out taken, out paid);

            Harness.Equal(taken, 1000f, "a thousand litres is taken");
            Harness.Check(paid > 0, string.Format("and paid for: {0} tokens", paid));
            Harness.Equal(bag.CountOf(token), paid, "the tokens arrive in the bag");

            // Linear, so a bigger hopper is worth proportionally more and there is no
            // threshold to game by tipping in dribs.
            var a = new TraderState(); a.Init(vance, 0.0);
            var b = new TraderState(); b.Init(vance, 0.0);
            var bagA = new Inv(36);
            var bagB = new Inv(36);

            int paidOnce, paidTwice, second;
            float tookOnce, tookA, tookB;
            a.BuyLitres(bagA, corn, 2000f, out tookOnce, out paidOnce);
            b.BuyLitres(bagB, corn, 1000f, out tookA, out paidTwice);
            b.BuyLitres(bagB, corn, 1000f, out tookB, out second);

            Harness.Check(Mathf.Abs(paidOnce - (paidTwice + second)) <= 2,
                string.Format("2000 L in one tip ({0}) matches two of 1000 ({1})", paidOnce, paidTwice + second));

            // Wholesaling takes a cut: a sack's worth of litres fetches less than the
            // produce is worth per unit.
            float perLitre = TraderPricing.LitreValue(vance, corn, 0);
            float exact = TraderPricing.BuyPriceExact(vance, corn.harvestItem, 0);
            Harness.Check(perLitre * TraderPricing.LitresPerItem(corn) < exact,
                string.Format("bulk takes a wholesale cut: {0:0.00} against {1:0.00} a sack's worth",
                    perLitre * TraderPricing.LitresPerItem(corn), exact));

            // Crops of different worth must fetch different rates. Routing litres
            // through the rounded per-item price flattened corn and grain onto the same
            // number, which made which crop you sowed an acre of stop mattering.
            CropDefinition cheap = null, dear = null;
            for (int i = 0; i < db.crops.Count; i++)
            {
                var c = db.crops[i];
                if (!c.growsOnField || c.harvestItem == null) continue;
                if (cheap == null || c.harvestItem.tradeValue < cheap.harvestItem.tradeValue) cheap = c;
                if (dear == null || c.harvestItem.tradeValue > dear.harvestItem.tradeValue) dear = c;
            }

            if (cheap != null && dear != null && cheap.harvestItem.tradeValue != dear.harvestItem.tradeValue)
            {
                Harness.Check(TraderPricing.LitreValue(vance, dear, 0) > TraderPricing.LitreValue(vance, cheap, 0),
                    string.Format("{0} fetches more per litre than {1}", dear.displayName, cheap.displayName));
            }

            // A seeder's hopper holds seed measured in litres. If a trader bought that
            // as produce, buying seed and tipping it back would be a laundry.
            Harness.Check(TraderPricing.LitrePayout(vance, corn, 0, 8f) < TraderPricing.SellPrice(
                    vance, new TraderStockEntry { item = corn.seedItem, priceMultiplier = 1.4f }, 0),
                "one seed item's worth of litres is worth less than the seed cost");

            Harness.Equal((int)state.BuyLitres(bag, corn, 0f, out taken, out paid), (int)TradeResult.NothingToSell,
                "nothing tipped is nothing paid");
            Harness.Equal((int)state.BuyLitres(bag, null, 500f, out taken, out paid), (int)TradeResult.NothingToSell,
                "and a crop they cannot price is refused");
            Harness.Equal(paid, 0, "with no payment");

            // A bag with no room for the tokens must say so rather than claiming the
            // trader does not want a full harvester of corn.
            var stuffed = new Inv(36);
            var brick = db.Item(ItemIds.EngineBlock);
            for (int i = 0; i < stuffed.Size; i++) stuffed.SetSlot(i, new ItemStack(brick, brick.maxStack));

            var busy = new TraderState();
            busy.Init(vance, 0.0);

            Harness.Equal((int)busy.BuyLitres(stuffed, corn, 1000f, out taken, out paid), (int)TradeResult.NoRoom,
                "a full bag cannot be paid");
            Harness.Equal(taken, 0f, "and the load stays in the hopper");
            Harness.Equal(paid, 0, "with nothing paid");
        }

        // --------------------------------------------------------------- restocking

        static void Restocking(ContentDatabase db)
        {
            Harness.Section("traders: restocking");

            var vance = Vance(db);
            float hours = vance.restockHours;

            Harness.Equal(TraderPricing.Restocked(5, 10, 0.0, hours), 5, "no time, no restock");
            Harness.Equal(TraderPricing.Restocked(10, 10, 999.0, hours), 10, "a full shelf stays full");
            Harness.Equal(TraderPricing.Restocked(0, 10, 999.0, hours), 10, "and a long absence fills it");
            Harness.Check(TraderPricing.Restocked(0, 20, hours, hours) < 20,
                "one cycle does not refill a cleared line outright");
            Harness.Check(TraderPricing.Restocked(0, 20, hours, hours) > 0, "but it does bring some back");

            // A trader you visit every hour must still restock. If the stored hour were
            // reset on every visit rather than advanced by whole cycles, it never would.
            var state = new TraderState();
            state.Init(vance, 0.0);

            var bag = new Inv(36);
            bag.Add(db.Item(ItemIds.TradeToken), 3000);

            int line = OpenLine(state);
            int spent;
            state.Buy(bag, line, state.StockOf(line), out spent);
            Harness.Equal(state.StockOf(line), 0, "the line is cleared out");

            for (double t = 0.0; t <= hours * 3.0; t += 1.0) state.Refresh(t);

            Harness.Check(state.StockOf(line) > 0,
                string.Format("visiting every hour for three cycles still restocks it to {0}", state.StockOf(line)));

            // And a single visit after the same time restocks the same amount.
            var patient = new TraderState();
            patient.Init(vance, 0.0);
            patient.Buy(bag, line, patient.StockOf(line), out spent);
            patient.Refresh(hours * 3.0);

            Harness.Equal(patient.StockOf(line), state.StockOf(line),
                "a trader left alone and a trader visited hourly restock identically");

            Harness.Check(state.LastRestockHours > 0.0, "and the restock clock advanced rather than drifting");
        }
    }

    /// <summary>Finds a field crop with something to sell, without hard-coding one.</summary>
    static class CropDefinitionShim
    {
        public static void Find(ContentDatabase db, out MadVoxel.Farming.Crops.CropDefinition crop)
        {
            crop = null;
            for (int i = 0; i < db.crops.Count; i++)
            {
                if (db.crops[i].growsOnField && db.crops[i].harvestItem != null) { crop = db.crops[i]; return; }
            }
        }
    }
}
