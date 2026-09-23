using System.Collections.Generic;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Building
{
    /// <summary>
    /// What a furnace gets through while you are not watching it.
    ///
    /// The campfire is a screen you stand at: you hold the button, it cooks. A furnace
    /// is the other thing - you load it, walk away, and come back to metal. That means
    /// its progress has to be worked out from the clock rather than ticked, for the
    /// same reason crop growth is: the player will be two hundred metres away in an
    /// unloaded chunk for most of it, and asleep for the rest.
    ///
    /// Pure and engine-free. A furnace that quietly eats an ore without producing an
    /// ingot, or burns fuel it did not have, is invisible until someone counts.
    /// </summary>
    public static class FurnaceRules
    {
        /// <summary>
        /// Real seconds of work a furnace does per in-game hour. A recipe's craftSeconds
        /// is written for a player standing at a campfire, and this is the exchange rate
        /// into the clock the furnace actually runs on.
        /// </summary>
        public const float WorkSecondsPerGameHour = 45f;

        /// <summary>A furnace is slower per item than standing over a fire, and never stops.</summary>
        public const float SlownessFactor = 1.35f;

        /// <summary>Seconds one batch of a recipe takes in a furnace.</summary>
        public static float SecondsPerBatch(RecipeDefinition recipe)
        {
            if (recipe == null) return 0f;
            return Mathf.Max(0.1f, recipe.craftSeconds * SlownessFactor);
        }

        /// <summary>Working seconds available for a span of game hours.</summary>
        public static float WorkingSeconds(double elapsedGameHours)
        {
            if (elapsedGameHours <= 0.0) return 0f;
            return (float)elapsedGameHours * WorkSecondsPerGameHour;
        }

        /// <summary>Game hours a number of working seconds represents. The inverse, exactly.</summary>
        public static double HoursForSeconds(float seconds)
        {
            return Mathf.Max(0f, seconds) / (double)WorkSecondsPerGameHour;
        }

        /// <summary>Seconds of burn sitting in an inventory.</summary>
        public static float FuelSecondsIn(Inventory.Inventory inventory)
        {
            if (inventory == null) return 0f;

            float total = 0f;
            for (int i = 0; i < inventory.Size; i++)
            {
                var stack = inventory[i];
                if (stack.IsEmpty || stack.Item == null || stack.Item.fuelSeconds <= 0f) continue;

                total += stack.Item.fuelSeconds * stack.Count;
            }
            return total;
        }

        /// <summary>
        /// How many batches actually complete, given everything that can stop one.
        ///
        /// Each limit is a real refusal the player should be able to see: out of time,
        /// out of fuel, out of ore, or no room for what comes out. Taking the smallest
        /// is what stops a furnace consuming an input it cannot finish.
        /// </summary>
        public static int BatchesAffordable(float workingSeconds, float fuelSeconds, float secondsPerBatch,
                                            int byIngredients, int byRoom)
        {
            if (secondsPerBatch <= 0f) return 0;
            if (byIngredients <= 0 || byRoom <= 0) return 0;

            int byTime = Mathf.FloorToInt(workingSeconds / secondsPerBatch);
            int byFuel = Mathf.FloorToInt(fuelSeconds / secondsPerBatch);

            return Mathf.Max(0, Mathf.Min(Mathf.Min(byTime, byFuel), Mathf.Min(byIngredients, byRoom)));
        }

        /// <summary>
        /// How many batches of a recipe the ingredients in an inventory cover.
        /// </summary>
        public static int BatchesFromIngredients(Inventory.Inventory inventory, RecipeDefinition recipe)
        {
            if (inventory == null || recipe == null || recipe.ingredients.Count == 0) return 0;

            int batches = int.MaxValue;
            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                var ingredient = recipe.ingredients[i];
                if (ingredient.item == null || ingredient.count <= 0) continue;

                batches = Mathf.Min(batches, inventory.CountOf(ingredient.item) / ingredient.count);
                if (batches <= 0) return 0;
            }

            return batches == int.MaxValue ? 0 : batches;
        }

        /// <summary>
        /// Burns fuel out of an inventory, cheapest first, and returns the seconds it
        /// actually got. Burning the coal before the wood would make a furnace eat a
        /// player's best fuel on their worst job.
        /// </summary>
        public static float BurnFuel(Inventory.Inventory inventory, float wantedSeconds)
        {
            if (inventory == null || wantedSeconds <= 0f) return 0f;

            float remaining = wantedSeconds;

            while (remaining > 0.001f)
            {
                int slot = CheapestFuelSlot(inventory);
                if (slot < 0) break;

                var stack = inventory[slot];
                float each = stack.Item.fuelSeconds;

                int wantedCount = Mathf.CeilToInt(remaining / each);
                int burn = Mathf.Min(wantedCount, stack.Count);
                if (burn <= 0) break;

                inventory.ConsumeFromSlot(slot, burn);
                remaining -= burn * each;
            }

            return wantedSeconds - Mathf.Max(0f, remaining);
        }

        static int CheapestFuelSlot(Inventory.Inventory inventory)
        {
            int best = -1;
            float bestSeconds = float.MaxValue;

            for (int i = 0; i < inventory.Size; i++)
            {
                var stack = inventory[i];
                if (stack.IsEmpty || stack.Item == null || stack.Item.fuelSeconds <= 0f) continue;

                if (stack.Item.fuelSeconds >= bestSeconds) continue;

                bestSeconds = stack.Item.fuelSeconds;
                best = i;
            }
            return best;
        }

        /// <summary>
        /// The recipes a furnace knows: everything the content declares for the forge.
        /// Nothing is hard-coded here, so a mod that adds a smelt gets it for free.
        /// </summary>
        public static void CollectRecipes(IList<RecipeDefinition> all, List<RecipeDefinition> into)
        {
            if (into == null) return;
            into.Clear();
            if (all == null) return;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].station == CraftStation.Forge) into.Add(all[i]);
            }
        }

        /// <summary>The one line the furnace shows about what it is doing.</summary>
        public static string Describe(bool hasWork, bool hasFuel, bool hasRoom)
        {
            if (!hasWork) return "IDLE  -  NOTHING TO SMELT";
            if (!hasFuel) return "OUT OF FUEL";
            if (!hasRoom) return "FULL  -  NOWHERE TO PUT IT";
            return "SMELTING";
        }
    }
}
