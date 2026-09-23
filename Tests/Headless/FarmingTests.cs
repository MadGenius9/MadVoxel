using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.Farming.Crops;
using MadVoxel.Inventory;
using MadVoxel.World.Fields;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// Both farming layers. The garden's growth timing and the field's state machine are
    /// pure logic, so they can be run for real; the plot GameObject and the hoe cannot.
    /// </summary>
    public static class FarmingTests
    {
        public static void Run(ContentDatabase db)
        {
            CropContent(db);
            GardenGrowth(db);
            FieldStateMachine(db);
            FieldYield(db);
            BulkDrawing(db);
        }

        /// <summary>
        /// Litres out of a grain bin and back into harvest items. The bin used to be a
        /// black hole - a harvester could tip in and nothing could come out - and the
        /// rate that fixed it is the same one a trader prices litres by, so storing a
        /// harvest and carrying it stay two ways of holding the same thing.
        /// </summary>
        static void BulkDrawing(ContentDatabase db)
        {
            Harness.Section("farming: drawing bulk back out");

            CropDefinition crop = null;
            for (int i = 0; i < db.crops.Count; i++)
            {
                if (db.crops[i].growsOnField && db.crops[i].harvestItem != null) crop = db.crops[i];
            }

            Harness.Check(crop != null, "there is a field crop with produce");
            if (crop == null) return;

            Harness.Check(crop.litresPerHarvestItem > 0f, "and it says what a sack of it is worth");

            Harness.Equal(crop.ItemsFromLitres(0f), 0, "an empty bin draws nothing");
            Harness.Equal(crop.ItemsFromLitres(-50f), 0, "and neither does a negative");

            // A part-filled sack is not a sack. Rounding up here would let a bin with a
            // splash in the bottom print produce forever.
            Harness.Equal(crop.ItemsFromLitres(crop.litresPerHarvestItem * 0.99f), 0,
                "just under one sack's worth draws nothing");
            Harness.Equal(crop.ItemsFromLitres(crop.litresPerHarvestItem), 1, "exactly one draws one");
            Harness.Equal(crop.ItemsFromLitres(crop.litresPerHarvestItem * 7.6f), 7, "and seven and a bit draws seven");

            // THE round trip: drawing n items must cost exactly what n items are worth.
            // Any drift either way is a way to make or destroy produce by clicking.
            bool exact = true;
            for (int n = 0; n <= 50; n++)
            {
                if (crop.ItemsFromLitres(crop.LitresForItems(n)) != n) exact = false;
            }
            Harness.Check(exact, "drawing n sacks costs exactly n sacks' worth, for every n up to fifty");

            bool neverGains = true;
            for (float litres = 0f; litres < 200f; litres += 0.7f)
            {
                if (crop.LitresForItems(crop.ItemsFromLitres(litres)) > litres + 0.001f) neverGains = false;
            }
            Harness.Check(neverGains, "and drawing never costs less than what came out");

            Harness.Equal(crop.LitresForItems(-5), 0f, "you cannot draw a negative number of sacks");

            // The trader must price litres at the same rate the bin draws them, or
            // storing a harvest and carrying it become two different economies.
            Harness.Equal(MadVoxel.Traders.TraderPricing.LitresPerItem(crop), crop.litresPerHarvestItem,
                "the bin and the counter agree on what a sack is worth");
        }

        static void CropContent(ContentDatabase db)
        {
            Harness.Section("farming: crop content");

            Harness.Check(db.crops.Count >= 3, string.Format("{0} crops defined", db.crops.Count));

            var problems = new List<string>();
            for (int i = 0; i < db.crops.Count; i++)
            {
                var crop = db.crops[i];
                if (crop.seedItem == null) problems.Add(crop.stringId + " has no seed");
                else if (db.Item(crop.seedItem.stringId) == null) problems.Add(crop.stringId + " seed is not in the item table");

                if (crop.harvestItem == null) problems.Add(crop.stringId + " has no harvest item");
                else if (db.Item(crop.harvestItem.stringId) == null) problems.Add(crop.stringId + " harvest is not in the item table");

                if (crop.daysToMature <= 0f) problems.Add(crop.stringId + " matures instantly");
                if (crop.harvestMax < crop.harvestMin || crop.harvestMin < 1) problems.Add(crop.stringId + " has a bad yield range");
                if (!crop.growsOnPlot && !crop.growsOnField) problems.Add(crop.stringId + " grows nowhere");
                if (crop.growsOnField && crop.litresPerCell <= 0f) problems.Add(crop.stringId + " is a field crop with no litres");
                if (db.Crop(crop.stringId) != crop) problems.Add(crop.stringId + " does not resolve by id");
                if (db.CropForSeed(crop.seedItem) != crop) problems.Add(crop.stringId + " is not reachable from its seed");
            }
            Harness.Check(problems.Count == 0, "every crop is well formed"
                + (problems.Count > 0 ? ": " + string.Join("; ", problems) : ""));

            // A crop that neither replants nor returns seed is a dead end.
            var deadEnds = new List<string>();
            for (int i = 0; i < db.crops.Count; i++)
            {
                var crop = db.crops[i];
                if (crop.replants) continue;
                if (crop.seedReturnChance <= 0f) deadEnds.Add(crop.stringId);
            }
            Harness.Check(deadEnds.Count == 0, "no crop is a dead end - it replants or returns seed"
                + (deadEnds.Count > 0 ? ": " + string.Join(", ", deadEnds) : ""));

            // Phase 0 must be playable without a trader, so forage has to yield seeds.
            var seedSources = new List<string>();
            for (int i = 0; i < db.blocks.blocks.Count; i++)
            {
                var block = db.blocks.blocks[i];
                if (block.secondaryDropItem == null) continue;
                if (db.CropForSeed(block.secondaryDropItem) != null) seedSources.Add(block.stringId);
            }
            Harness.Check(seedSources.Count >= 3,
                string.Format("{0} wild plants drop plantable seeds", seedSources.Count));

            // And cooked food has to be worth the farming.
            var meals = new List<ItemDefinition>();
            for (int i = 0; i < db.items.Count; i++)
            {
                if (db.items[i].staminaRestore > 0f) meals.Add(db.items[i]);
            }
            Harness.Check(meals.Count >= 2, string.Format("{0} cooked meals restore stamina", meals.Count));

            // Each meal must be craftable from things a garden produces.
            var uncookable = new List<string>();
            for (int i = 0; i < meals.Count; i++)
            {
                bool hasRecipe = false;
                for (int j = 0; j < db.recipes.Count; j++)
                {
                    if (db.recipes[j].output == meals[i]) hasRecipe = true;
                }
                if (!hasRecipe) uncookable.Add(meals[i].stringId);
            }
            Harness.Check(uncookable.Count == 0, "every meal has a recipe"
                + (uncookable.Count > 0 ? ": " + string.Join(", ", uncookable) : ""));

            // The plot itself must be craftable by hand on day one.
            var plotItem = db.Item(ItemIds.PieceFarmPlot);
            bool plotByHand = false;
            for (int i = 0; i < db.recipes.Count; i++)
            {
                var r = db.recipes[i];
                if (r.output == plotItem && r.station == CraftStation.Hand && r.unlockedByDefault) plotByHand = true;
            }
            Harness.Check(plotByHand, "farm plots are craftable by hand (the garden is reachable on day one)");

            var plotStructure = db.Structure(StructureIds.FarmPlot);
            Harness.Check(plotStructure != null && plotStructure.requiresSoil, "farm plots must be placed on soil");
        }

        static void GardenGrowth(ContentDatabase db)
        {
            Harness.Section("farming: garden growth");

            var potato = db.Crop("madvoxel:crop_potato");
            Harness.Check(potato != null, "the potato crop exists");
            if (potato == null) return;

            float full = potato.HoursToMature;
            Harness.Equal(potato.StageAt(0.0), CropStage.Seedling, "a freshly planted crop is a seedling");
            Harness.Equal(potato.StageAt(full * 0.4f), CropStage.Growing, "40% through it is growing");
            Harness.Equal(potato.StageAt(full * 0.8f), CropStage.Mature, "80% through it is mature");
            Harness.Equal(potato.StageAt(full), CropStage.Ready, "at full time it is ready");
            Harness.Equal(potato.StageAt(full * 3f), CropStage.Ready, "and it stays ready if left");

            Harness.Check(Mathf.Abs(potato.Progress01(full * 0.5f) - 0.5f) < 0.01f, "progress tracks elapsed hours");
            Harness.Check(potato.Progress01(full * 9f) <= 1f, "progress never exceeds 1");

            // Growth is a clock difference, which is what makes it survive a reload.
            Harness.Equal(potato.StageAt(-5.0), CropStage.Seedling, "a negative elapsed time does not break the stage");

            // Days-to-mature must be a real wait but not a wall for a Phase 0 session.
            var tooSlow = new List<string>();
            for (int i = 0; i < db.crops.Count; i++)
            {
                if (db.crops[i].daysToMature > 4f) tooSlow.Add(db.crops[i].stringId);
            }
            Harness.Check(tooSlow.Count == 0, "no crop takes more than four days"
                + (tooSlow.Count > 0 ? ": " + string.Join(", ", tooSlow) : ""));
        }

        static void FieldStateMachine(ContentDatabase db)
        {
            Harness.Section("farming: field cell state machine");

            var grid = new FieldGrid();
            const int x = 12, z = -40;

            Harness.Equal(grid.Get(x, z).State, FieldCellState.Wild, "unworked ground reads as wild");
            Harness.Equal(grid.WorkedCellCount, 0, "and is not stored");

            Harness.Equal(grid.Plow(x, z, 100.0), true, "wild ground can be plowed");
            Harness.Equal(grid.Get(x, z).State, FieldCellState.Plowed, "the cell is now plowed");
            Harness.Equal(grid.WorkedCellCount, 1, "worked cells are stored");
            Harness.Equal(grid.Plow(x, z, 101.0), false, "plowing twice does nothing");

            Harness.Equal(grid.Cultivate(x, z, 102.0), true, "plowed ground can be cultivated");
            Harness.Equal(grid.Sow(x, z, 1, 103.0), true, "cultivated ground can be sown");
            Harness.Equal(grid.Get(x, z).CropIndex, (byte)1, "the crop is recorded");
            Harness.Equal(grid.Sow(x, z, 2, 104.0), false, "a sown cell cannot be sown again");

            // Growth walks Seeded -> Growing -> Ready off the clock.
            grid.Refresh(x, z, 103.0 + 1.0, 48f);
            Harness.Equal(grid.Get(x, z).State, FieldCellState.Seeded, "just after sowing it is still seeded");

            grid.Refresh(x, z, 103.0 + 24.0, 48f);
            Harness.Equal(grid.Get(x, z).State, FieldCellState.Growing, "halfway it is growing");

            grid.Refresh(x, z, 103.0 + 48.0, 48f);
            Harness.Equal(grid.Get(x, z).State, FieldCellState.Ready, "at full time it is ready");

            byte harvested;
            float litres = grid.Harvest(x, z, 20f, 160.0, out harvested);
            Harness.Check(litres > 0f, string.Format("harvesting a ready cell yields litres ({0:0.0})", litres));
            Harness.Equal(harvested, (byte)1, "and reports which crop came off");
            Harness.Equal(grid.Get(x, z).State, FieldCellState.Stubble, "the cell is left as stubble");

            float none = grid.Harvest(x, z, 20f, 161.0, out harvested);
            Harness.Equal(none, 0f, "harvesting stubble yields nothing");

            Harness.Equal(grid.Plow(x, z, 170.0), true, "stubble can be plowed back round");

            // Trampling costs the crop, not the land.
            grid.Cultivate(x, z, 171.0);
            grid.Sow(x, z, 1, 172.0);
            Harness.Equal(grid.Trample(x, z, 173.0), true, "a growing cell can be trampled");
            Harness.Equal(grid.Get(x, z).State, FieldCellState.Stubble, "trampling knocks it back to stubble");
            Harness.Equal(grid.Trample(x, z, 174.0), false, "trampling bare stubble does nothing");
            Harness.Check(grid.Get(x, z).IsWorkable, "and the ground stays workable");

            // Negative coordinates must not collide with positive ones.
            var a = FieldGrid.Key(-1, 5);
            var b = FieldGrid.Key(5, -1);
            Harness.Check(a != b, "cell keys do not collide across sign");

            int dx, dz;
            FieldGrid.Decode(FieldGrid.Key(-7, 19), out dx, out dz);
            Harness.Check(dx == -7 && dz == 19, "cell keys decode back to their coordinates");
        }

        static void FieldYield(ContentDatabase db)
        {
            Harness.Section("farming: field yield");

            var grid = new FieldGrid();
            const int x = 0, z = 0;

            grid.Plow(x, z, 0.0);
            grid.Sow(x, z, 1, 1.0);
            grid.Refresh(x, z, 1.0 + 48.0, 48f);

            byte crop;
            float first = grid.Harvest(x, z, 20f, 50.0, out crop);

            // Fertility falls with each crop taken off, so a second run yields less.
            grid.Plow(x, z, 51.0);
            grid.Sow(x, z, 1, 52.0);
            grid.Refresh(x, z, 52.0 + 48.0, 48f);
            float second = grid.Harvest(x, z, 20f, 101.0, out crop);

            Harness.Check(second < first,
                string.Format("repeated cropping without fertiliser yields less ({0:0.0} then {1:0.0} L)", first, second));
            Harness.Check(second > 0f, "but never falls to nothing");

            // A whole swath, the way a Phase 1 implement will drive it.
            var field = new FieldGrid();
            var swath = new RectInt(0, 0, 8, 4);
            int plowed = field.Apply(swath, (cx, cz) => field.Plow(cx, cz, 0.0));
            Harness.Equal(plowed, 32, "a swath plows every cell in the rectangle");
            Harness.Equal(field.WorkedCellCount, 32, "and all of them are stored");

            int sown = field.Apply(swath, (cx, cz) => field.Sow(cx, cz, 1, 1.0));
            Harness.Equal(sown, 32, "and the same swath can be sown");
        }
    }
}
