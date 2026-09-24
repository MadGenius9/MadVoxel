using System.Collections.Generic;
using MadVoxel.Building;
using MadVoxel.Content;
using MadVoxel.Inventory;
using UnityEngine;
using Inv = MadVoxel.Inventory.Inventory;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The furnace. It runs while nobody is looking, which is exactly the condition
    /// under which a system quietly eats an ore, burns fuel it did not have, or
    /// restocks itself twice.
    /// </summary>
    public static class FurnaceTests
    {
        public static void Run(ContentDatabase db)
        {
            if (db == null) return;

            Timing();
            Fuel(db);
            Batches(db);
            Content(db);
            NoCascades(db);
        }

        /// <summary>
        /// No forge recipe may eat another forge recipe's output.
        ///
        /// A furnace has one shared inventory and runs every recipe it can afford,
        /// round-robin, with nothing to choose between them. So a recipe whose
        /// ingredient is another recipe's output consumes it the instant it appears -
        /// and the furnace silently stops being able to produce that item at all,
        /// including any the player put there for safekeeping.
        ///
        /// Steel was written this way first: iron ingots in, steel out, which would
        /// have meant a furnace could never hand back an iron ingot again. Nothing
        /// else in the suite would have caught it, because every recipe involved is
        /// individually correct - the bug only exists in the combination.
        /// </summary>
        static void NoCascades(ContentDatabase db)
        {
            Harness.Section("furnace: one recipe never eats another's output");

            var outputs = new HashSet<ItemDefinition>();
            for (int i = 0; i < db.recipes.Count; i++)
            {
                var recipe = db.recipes[i];
                if (recipe.station == CraftStation.Forge && recipe.output != null) outputs.Add(recipe.output);
            }

            Harness.Check(outputs.Count > 0, "the furnace makes something");

            var cascades = new List<string>();
            for (int i = 0; i < db.recipes.Count; i++)
            {
                var recipe = db.recipes[i];
                if (recipe.station != CraftStation.Forge) continue;

                for (int j = 0; j < recipe.ingredients.Count; j++)
                {
                    var ing = recipe.ingredients[j].item;
                    if (ing != null && outputs.Contains(ing))
                    {
                        cascades.Add(recipe.stringId + " consumes " + ing.stringId);
                    }
                }
            }

            Harness.Check(cascades.Count == 0,
                "no forge recipe consumes what another forges"
                + (cascades.Count > 0 ? ": " + string.Join(", ", cascades) : ""));
        }

        static RecipeDefinition Forge(ContentDatabase db)
        {
            var list = new List<RecipeDefinition>();
            FurnaceRules.CollectRecipes(db.recipes, list);
            return list.Count > 0 ? list[0] : null;
        }

        // ------------------------------------------------------------------ timing

        static void Timing()
        {
            Harness.Section("furnace: time");

            Harness.Equal(FurnaceRules.WorkingSeconds(0.0), 0f, "no hours is no work");
            Harness.Equal(FurnaceRules.WorkingSeconds(-3.0), 0f, "and a clock that went backwards does nothing");

            Harness.Equal(FurnaceRules.WorkingSeconds(1.0), FurnaceRules.WorkSecondsPerGameHour,
                "an hour is an hour's work");
            Harness.Equal(FurnaceRules.WorkingSeconds(3.0), FurnaceRules.WorkSecondsPerGameHour * 3f,
                "and three hours is three");

            // THE round trip. The furnace advances its own clock by the work it did, so
            // the two conversions have to be exact inverses - otherwise a furnace opened
            // repeatedly either loses time or invents it.
            bool exact = true;
            for (float seconds = 0f; seconds < 500f; seconds += 7.3f)
            {
                float back = FurnaceRules.WorkingSeconds(FurnaceRules.HoursForSeconds(seconds));
                if (Mathf.Abs(back - seconds) > 0.01f) exact = false;
            }
            Harness.Check(exact, "hours to seconds and back is exact, so a furnace cannot drift");

            var recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            recipe.craftSeconds = 10f;
            Harness.Check(FurnaceRules.SecondsPerBatch(recipe) > 10f,
                "a furnace is slower per batch than standing over a fire");

            recipe.craftSeconds = 0f;
            Harness.Check(FurnaceRules.SecondsPerBatch(recipe) > 0f,
                "and an instant recipe still takes some time, so it cannot loop forever");
        }

        // -------------------------------------------------------------------- fuel

        static void Fuel(ContentDatabase db)
        {
            Harness.Section("furnace: fuel");

            var coal = db.Item(ItemIds.Coal);
            var wood = db.Item(ItemIds.WoodLog);
            var ore = db.Item(ItemIds.IronOre);

            Harness.Check(coal.fuelSeconds > wood.fuelSeconds, "coal burns longer than wood");

            var box = new Inv(12);
            Harness.Equal(FurnaceRules.FuelSecondsIn(box), 0f, "an empty furnace has no fuel");

            box.Add(ore, 20);
            Harness.Equal(FurnaceRules.FuelSecondsIn(box), 0f, "and ore is not fuel");

            box.Add(wood, 3);
            Harness.Equal(FurnaceRules.FuelSecondsIn(box), wood.fuelSeconds * 3f, "three logs is three logs of burn");

            box.Add(coal, 2);
            Harness.Equal(FurnaceRules.FuelSecondsIn(box), wood.fuelSeconds * 3f + coal.fuelSeconds * 2f,
                "and coal adds to it");

            // Cheapest first. Burning the coal before the wood would spend a player's
            // best fuel on their worst job.
            var mixed = new Inv(12);
            mixed.Add(coal, 1);
            mixed.Add(wood, 4);

            float bank = 0f;
            float got = FurnaceRules.BurnFuel(mixed, wood.fuelSeconds, ref bank);
            Harness.Check(got >= wood.fuelSeconds - 0.01f, "the burn gets what it asked for");
            Harness.Equal(mixed.CountOf(coal), 1, "and the coal is untouched while there is wood");
            Harness.Equal(mixed.CountOf(wood), 3, "one log gone");

            // Asking for more than there is gets what there is, and no more.
            var thin = new Inv(12);
            thin.Add(wood, 1);

            float thinBank = 0f;
            float partial = FurnaceRules.BurnFuel(thin, wood.fuelSeconds * 10f, ref thinBank);
            Harness.Equal(partial, wood.fuelSeconds, "a short furnace burns only what it had");
            Harness.Equal(thin.CountOf(wood), 0, "and empties");
            Harness.Equal(FurnaceRules.BurnFuel(thin, 50f, ref thinBank), 0f, "an empty one burns nothing");

            float nullBank = 0f;
            Harness.Equal(FurnaceRules.BurnFuel(null, 10f, ref nullBank), 0f, "and nothing burns nothing");
            Harness.Equal(FurnaceRules.BurnFuel(mixed, 0f, ref bank), 0f, "asking for no burn takes no fuel");

            // THE one that decides whether a furnace is worth building. A lump of coal
            // outlasts a batch several times over; lighting a fresh one per batch made
            // it cost more fuel per ingot than the campfire it replaces.
            var lump = new Inv(12);
            lump.Add(coal, 1);

            float carried = 0f;
            int batchesFromOne = 0;

            for (int i = 0; i < 20; i++)
            {
                if (FurnaceRules.BurnFuel(lump, 12f, ref carried) < 11.9f) break;
                batchesFromOne++;
            }

            Harness.Check(batchesFromOne >= 6,
                string.Format("one lump of coal carries {0} twelve-second batches, not one", batchesFromOne));
            Harness.Equal(lump.CountOf(coal), 0, "and it is spent when it runs out");
            Harness.Check(carried < 12f, "with less than a batch of burn left over");

            // The remainder must not be conjured out of nothing either.
            var empty = new Inv(12);
            float ghost = 5f;
            Harness.Equal(FurnaceRules.BurnFuel(empty, 5f, ref ghost), 5f, "banked burn is spendable");
            Harness.Equal(ghost, 0f, "and is gone once spent");
            Harness.Equal(FurnaceRules.BurnFuel(empty, 1f, ref ghost), 0f, "with nothing behind it");
        }

        // ----------------------------------------------------------------- batches

        static void Batches(ContentDatabase db)
        {
            Harness.Section("furnace: what actually completes");

            var recipe = Forge(db);
            Harness.Check(recipe != null, "the forge has recipes");
            if (recipe == null) return;

            float perBatch = FurnaceRules.SecondsPerBatch(recipe);

            Harness.Equal(FurnaceRules.BatchesAffordable(perBatch * 5f, 10000f, perBatch, 99, 99), 5,
                "five batches' worth of time makes five");
            Harness.Equal(FurnaceRules.BatchesAffordable(perBatch * 0.9f, 10000f, perBatch, 99, 99), 0,
                "and nine tenths of a batch makes none");

            // Each limit on its own must be able to stop it, or one of them is dead.
            Harness.Equal(FurnaceRules.BatchesAffordable(perBatch * 99f, perBatch * 2f, perBatch, 99, 99), 2,
                "fuel can be the thing that stops it");
            Harness.Equal(FurnaceRules.BatchesAffordable(perBatch * 99f, 10000f, perBatch, 3, 99), 3,
                "so can the ore");
            Harness.Equal(FurnaceRules.BatchesAffordable(perBatch * 99f, 10000f, perBatch, 99, 1), 1,
                "and so can having nowhere to put it");
            Harness.Equal(FurnaceRules.BatchesAffordable(perBatch * 99f, 10000f, perBatch, 0, 99), 0,
                "no ore is no work");
            Harness.Equal(FurnaceRules.BatchesAffordable(perBatch * 99f, 0f, perBatch, 99, 99), 0,
                "no fuel is no work");

            // Ingredients.
            var box = new Inv(12);
            Harness.Equal(FurnaceRules.BatchesFromIngredients(box, recipe), 0, "an empty furnace smelts nothing");

            var first = recipe.ingredients[0];
            box.Add(first.item, first.count * 4);

            int covered = FurnaceRules.BatchesFromIngredients(box, recipe);
            Harness.Check(covered >= 1 && covered <= 4,
                string.Format("four batches of the first ingredient covers {0} batch(es)", covered));

            // A recipe wanting two things is limited by the scarcer one, not the first.
            var twoPart = ScriptableObject.CreateInstance<RecipeDefinition>();
            twoPart.craftSeconds = 4f;
            twoPart.output = db.Item(ItemIds.IronIngot);
            twoPart.outputCount = 1;
            twoPart.ingredients.Add(new RecipeIngredient { item = db.Item(ItemIds.IronOre), count = 2 });
            twoPart.ingredients.Add(new RecipeIngredient { item = db.Item(ItemIds.Sand), count = 1 });

            var pair = new Inv(12);
            pair.Add(db.Item(ItemIds.IronOre), 20);
            pair.Add(db.Item(ItemIds.Sand), 3);

            Harness.Equal(FurnaceRules.BatchesFromIngredients(pair, twoPart), 3,
                "the scarcer ingredient sets the count, whichever one it is");

            Harness.Equal(FurnaceRules.BatchesFromIngredients(null, recipe), 0, "no furnace smelts nothing");
            Harness.Equal(FurnaceRules.BatchesFromIngredients(box, null), 0, "and no recipe does either");

            // Enough burn to finish one, not merely more than none. A splash of banked
            // seconds is how every burn ends, and calling that "still working" is what
            // let a cold furnace go on banking hours.
            Harness.Check(FurnaceRules.HasBurnFor(perBatch, recipe), "exactly one batch of burn is enough");
            Harness.Check(FurnaceRules.HasBurnFor(perBatch * 9f, recipe), "and plenty certainly is");
            Harness.Check(!FurnaceRules.HasBurnFor(perBatch * 0.9f, recipe),
                "nine tenths of a batch is not enough to start one");
            Harness.Check(!FurnaceRules.HasBurnFor(0.01f, recipe), "and a splash is not fuel");
            Harness.Check(!FurnaceRules.HasBurnFor(1000f, null), "a recipe that is not there cannot be started");

            Harness.Equal(FurnaceRules.Describe(false, true, true), "IDLE  -  NOTHING TO SMELT",
                "an idle furnace says so");
            Harness.Equal(FurnaceRules.Describe(true, false, true), "OUT OF FUEL", "and a cold one says why");
            Harness.Equal(FurnaceRules.Describe(true, true, false), "FULL  -  NOWHERE TO PUT IT",
                "and a full one says that instead");
            Harness.Equal(FurnaceRules.Describe(true, true, true), "SMELTING", "a working one just works");

            // THE combination bug. All three answers have to come from the same
            // recipes - the ones the furnace has ingredients for. Measuring fuel over
            // every forge recipe made a furnace with iron ore and nine seconds of burn
            // read SMELTING, because nine seconds covers a batch of glass it has no
            // sand for.
            var cheap = ScriptableObject.CreateInstance<RecipeDefinition>();
            cheap.stringId = "test:cheap";
            cheap.craftSeconds = 2f;
            cheap.output = db.Item(ItemIds.BlockGlass);
            cheap.outputCount = 1;

            var dear = ScriptableObject.CreateInstance<RecipeDefinition>();
            dear.stringId = "test:dear";
            dear.craftSeconds = 40f;
            dear.output = db.Item(ItemIds.IronIngot);
            dear.outputCount = 1;

            var both = new List<RecipeDefinition> { cheap, dear };

            bool work, fuelled, room;

            // Ingredients only for the expensive one, and only enough burn for the
            // cheap one. That is a furnace that cannot do anything.
            FurnaceRules.Survey(both, FurnaceRules.SecondsPerBatch(cheap),
                r => r == dear, r => true, out work, out fuelled, out room);

            Harness.Check(work, "it has something to smelt");
            Harness.Check(!fuelled,
                "but not the fuel to smelt THAT - burn for a recipe it cannot make does not count");
            Harness.Equal(FurnaceRules.Describe(work, fuelled, room), "OUT OF FUEL",
                "so it says it is out of fuel rather than claiming to be smelting");

            // Enough for the one it can actually make.
            FurnaceRules.Survey(both, FurnaceRules.SecondsPerBatch(dear),
                r => r == dear, r => true, out work, out fuelled, out room);
            Harness.Check(fuelled, "and with burn enough for the job it has, it is fuelled");

            // Nothing to smelt at all reads as idle, whatever is in the fuel tray.
            FurnaceRules.Survey(both, 10000f, r => false, r => true, out work, out fuelled, out room);
            Harness.Check(!work && !fuelled && !room, "an empty furnace is idle, however much coal is in it");

            FurnaceRules.Survey(null, 100f, r => true, r => true, out work, out fuelled, out room);
            Harness.Check(!work, "and a furnace that knows no recipes has nothing to do");

            // THE second version of the same bug. Reporting the three answers
            // independently let one recipe supply the fuel and another the room, so a
            // furnace that could run neither of them still read SMELTING. One recipe
            // has to satisfy all three, or none does.
            FurnaceRules.Survey(both, FurnaceRules.SecondsPerBatch(cheap),
                r => true,                    // ingredients for both
                r => r == dear,               // but room only for the expensive one
                out work, out fuelled, out room);

            Harness.Check(work, "with ingredients for both it has something to smelt");
            Harness.Check(!(fuelled && room),
                "but burn for one and room for the other is not a furnace that can run");
            Harness.Check(FurnaceRules.Describe(work, fuelled, room) != "SMELTING",
                "so it does not claim to be smelting while sitting dark");

            // And when one recipe genuinely satisfies everything, it is found even if
            // an earlier one does not.
            FurnaceRules.Survey(both, FurnaceRules.SecondsPerBatch(dear),
                r => r == dear, r => r == dear, out work, out fuelled, out room);

            Harness.Check(work && fuelled && room, "a recipe that can actually run is found");
            Harness.Equal(FurnaceRules.Describe(work, fuelled, room), "SMELTING",
                "and then it really is smelting");

            // With one job fuelled-but-boxed-in and another roomy-but-cold, the answer
            // must not depend on which the content happened to list first. Same state,
            // both orders, same word.
            var crossedA = new List<RecipeDefinition> { cheap, dear };
            var crossedB = new List<RecipeDefinition> { dear, cheap };

            bool fuelA, roomA, fuelB, roomB, workA, workB;
            float burn = FurnaceRules.SecondsPerBatch(cheap);

            FurnaceRules.Survey(crossedA, burn, r => true, r => r == dear, out workA, out fuelA, out roomA);
            FurnaceRules.Survey(crossedB, burn, r => true, r => r == dear, out workB, out fuelB, out roomB);

            Harness.Equal(FurnaceRules.Describe(workA, fuelA, roomA),
                          FurnaceRules.Describe(workB, fuelB, roomB),
                "the same furnace reads the same whichever order its recipes are listed in");
            Harness.Equal(FurnaceRules.Describe(workA, fuelA, roomA), "FULL  -  NOWHERE TO PUT IT",
                "and it names the fuelled job's problem - out of fuel while the coal is lit is a lie");

            // A furnace that stopped for want of ore must not bank the hours it spent
            // cold. The structure decides this by asking whether it could still be
            // working, so that question has to answer honestly for an empty furnace.
            var idle = new Inv(12);
            idle.Add(db.Item(ItemIds.Coal), 10);
            Harness.Equal(FurnaceRules.BatchesFromIngredients(idle, recipe), 0,
                "a furnace with only fuel in it has nothing to smelt");
            Harness.Check(FurnaceRules.FuelSecondsIn(idle) > 0f, "even though it is holding fuel");
        }

        // ---------------------------------------------------------------- content

        static void Content(ContentDatabase db)
        {
            Harness.Section("furnace: the forge as shipped");

            var forge = new List<RecipeDefinition>();
            FurnaceRules.CollectRecipes(db.recipes, forge);

            Harness.Check(forge.Count >= 2, string.Format("the forge knows {0} recipes", forge.Count));

            // A forge recipe that lists fuel as an ingredient charges for it twice: once
            // in the recipe and again as burn time.
            var doubleCharged = new List<string>();
            var noOutput = new List<string>();

            for (int i = 0; i < forge.Count; i++)
            {
                var recipe = forge[i];
                if (recipe.output == null || recipe.outputCount <= 0) noOutput.Add(recipe.stringId);

                for (int j = 0; j < recipe.ingredients.Count; j++)
                {
                    var item = recipe.ingredients[j].item;
                    if (item != null && item.fuelSeconds > 0f) doubleCharged.Add(recipe.stringId + " wants " + item.displayName);
                }
            }

            Harness.Check(doubleCharged.Count == 0,
                "no forge recipe charges for fuel as an ingredient as well as burning it"
                + (doubleCharged.Count > 0 ? ": " + string.Join(", ", doubleCharged) : ""));
            Harness.Check(noOutput.Count == 0, "and every one produces something");

            // The furnace has to be buildable without needing a furnace.
            RecipeDefinition build = null;
            var furnaceItem = db.Item(ItemIds.PieceFurnace);
            for (int i = 0; i < db.recipes.Count; i++)
            {
                if (db.recipes[i].output == furnaceItem) build = db.recipes[i];
            }

            Harness.Check(build != null, "the furnace has a recipe");
            if (build == null) return;

            Harness.Check(build.station != CraftStation.Forge,
                "which is not made at a forge - you would need one to build one");

            // Its ingredients have to be reachable without one too: the campfire path
            // to ingots is what stops the whole metal tier being circular.
            var circular = new List<string>();
            for (int i = 0; i < build.ingredients.Count; i++)
            {
                var item = build.ingredients[i].item;
                if (item == null) continue;

                bool madeOnlyAtForge = false, madeAnywhereElse = false;
                for (int r = 0; r < db.recipes.Count; r++)
                {
                    if (db.recipes[r].output != item) continue;

                    if (db.recipes[r].station == CraftStation.Forge) madeOnlyAtForge = true;
                    else madeAnywhereElse = true;
                }

                if (madeOnlyAtForge && !madeAnywhereElse) circular.Add(item.displayName);
            }

            Harness.Check(circular.Count == 0,
                "and nothing it is built from can only be made in a furnace"
                + (circular.Count > 0 ? ": " + string.Join(", ", circular) : ""));
        }
    }
}
