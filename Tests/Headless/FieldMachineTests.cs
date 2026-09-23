using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.Farming.Crops;
using MadVoxel.Inventory;
using MadVoxel.Perks;
using MadVoxel.Vehicles;
using MadVoxel.World.Fields;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The two things about a field machine that are invisible until it is too late:
    /// a swath that skips rows when you drive fast, and a hopper that lets a seeder
    /// sow cells it has no seed for.
    /// </summary>
    public static class FieldMachineTests
    {
        public static void Run(ContentDatabase db)
        {
            Swath();
            Hopper();
            Pass();
            Machines(db);
        }

        static ImplementDefinition Implement(ImplementKind kind, float width)
        {
            var def = ScriptableObject.CreateInstance<ImplementDefinition>();
            def.stringId = "test:" + kind;
            def.displayName = kind.ToString();
            def.kind = kind;
            def.workingWidth = width;
            return def;
        }

        static Vector3 At(float x, float z) { return new Vector3(x, 0f, z); }

        // ------------------------------------------------------------------- swath

        static void Swath()
        {
            Harness.Section("field machines: the swath");

            var cells = new List<Vector2Int>();

            // Standing still, a 3 m implement still covers its full width.
            FieldSwath.Collect(At(0f, 0f), At(0f, 0f), 0f, 3f, cells);
            Harness.Check(cells.Count >= 3,
                string.Format("a stationary 3 m implement covers {0} cells, not one", cells.Count));

            // Pointing north, the implement hangs east-west: the swath must be wide in
            // x and narrow in z, or it is mounted the wrong way round.
            int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                minX = Mathf.Min(minX, cells[i].x); maxX = Mathf.Max(maxX, cells[i].x);
                minZ = Mathf.Min(minZ, cells[i].y); maxZ = Mathf.Max(maxZ, cells[i].y);
            }
            Harness.Check(maxX - minX >= 2, "facing north, the implement spans east-west");
            Harness.Check(maxZ - minZ <= 1, "and is narrow north-south");

            // Turn ninety degrees and the swath must turn with it.
            FieldSwath.Collect(At(0f, 0f), At(0f, 0f), 90f, 3f, cells);
            minX = int.MaxValue; maxX = int.MinValue; minZ = int.MaxValue; maxZ = int.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                minX = Mathf.Min(minX, cells[i].x); maxX = Mathf.Max(maxX, cells[i].x);
                minZ = Mathf.Min(minZ, cells[i].y); maxZ = Mathf.Max(maxZ, cells[i].y);
            }
            Harness.Check(maxZ - minZ >= 2, "facing east, the implement spans north-south");
            Harness.Check(maxX - minX <= 1, "and is narrow east-west");

            // THE ONE THAT MATTERS. Drive 6 m in a single tick - roughly 20 km/h at
            // 30 fps - and every row along the way must be worked. A point-sampled
            // implement returns a handful of cells here and stripes the field.
            FieldSwath.Collect(At(0f, 0f), At(0f, 6f), 0f, 3f, cells);

            var rows = new HashSet<int>();
            for (int i = 0; i < cells.Count; i++) rows.Add(cells[i].y);

            Harness.Check(rows.Count >= 6,
                string.Format("a 6 m sweep works {0} distinct rows - no stripes", rows.Count));
            Harness.Check(cells.Count >= 18,
                string.Format("and {0} cells in total for a 3 m x 6 m pass", cells.Count));

            // Every row between start and finish, with no gaps.
            var sorted = new List<int>(rows);
            sorted.Sort();
            bool contiguous = true;
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] != sorted[i - 1] + 1) contiguous = false;
            }
            Harness.Check(contiguous, "the worked rows are contiguous - not one row in two");

            // Cells are deduplicated, or a crawling machine pays for the same ground
            // sixty times a second.
            FieldSwath.Collect(At(0f, 0f), At(0f, 0.05f), 0f, 3f, cells);
            var unique = new HashSet<Vector2Int>(cells);
            Harness.Equal(unique.Count, cells.Count, "a crawling machine reports each cell once");

            // A teleport or a lag spike must not plough a stripe across the county.
            FieldSwath.Collect(At(0f, 0f), At(0f, 500f), 0f, 3f, cells);
            Harness.Check(cells.Count < 200,
                string.Format("a 500 m jump is capped at {0} cells rather than thousands", cells.Count));

            // ...and what it does work is the far end, where the machine actually is.
            int nearest = int.MaxValue;
            for (int i = 0; i < cells.Count; i++) nearest = Mathf.Min(nearest, Mathf.Abs(cells[i].y - 500));
            Harness.Check(nearest <= 1, "the capped sweep works the ground it arrived at");

            // A wider implement covers proportionally more.
            FieldSwath.Collect(At(0f, 0f), At(0f, 4f), 0f, 3f, cells);
            int narrow = cells.Count;
            FieldSwath.Collect(At(0f, 0f), At(0f, 4f), 0f, 6f, cells);
            Harness.Check(cells.Count > narrow * 1.5f,
                string.Format("doubling the width roughly doubles the ground ({0} -> {1})", narrow, cells.Count));

            Harness.Equal(FieldSwath.CellsWide(3f), 3, "a 3 m implement is 3 cells wide");
        }

        // ------------------------------------------------------------------ hopper

        static void Hopper()
        {
            Harness.Section("field machines: the hopper");

            var plow = Implement(ImplementKind.Plow, 3f);
            Harness.Equal(ImplementWork.CellsAffordable(plow, 0f, 40), 40,
                "a plough has no hopper and is limited by nothing");
            Harness.Check(!ImplementWork.IsBlocked(plow, 0f), "and is never blocked");

            var seeder = Implement(ImplementKind.Seeder, 4f);
            seeder.hopperCapacityLitres = 100f;
            seeder.seedLitresPerCell = 0.5f;

            Harness.Equal(ImplementWork.CellsAffordable(seeder, 100f, 40), 40,
                "a full seeder sows the whole swath");

            // THE ONE THAT MATTERS. Two litres left, half a litre a cell: four cells,
            // not the ten under the machine. A seeder that sows cells it cannot pay for
            // leaves a field that looks sown and comes up bare.
            Harness.Equal(ImplementWork.CellsAffordable(seeder, 2f, 10), 4,
                "a nearly empty seeder sows only the cells it can pay for");
            Harness.Equal(ImplementWork.CellsAffordable(seeder, 0.4f, 10), 0,
                "and less than one cell's worth sows nothing at all");
            Harness.Equal(ImplementWork.CellsAffordable(seeder, 0f, 10), 0, "an empty one sows nothing");

            Harness.Check(ImplementWork.IsBlocked(seeder, 0.1f), "an empty seeder reports blocked");
            Harness.Check(!ImplementWork.IsBlocked(seeder, 50f), "a loaded one does not");

            Harness.Equal(ImplementWork.SeedCost(seeder, 8), 4f, "eight cells at half a litre costs four");

            // A sack goes in whole or not at all. Half a sack is either free seed or a
            // whole item spent on a splash, and both are worse than a full hopper.
            Harness.Check(ImplementWork.Accepts(seeder, 0f, 8f), "an empty hopper takes a sack");
            Harness.Check(ImplementWork.Accepts(seeder, 92f, 8f), "and so does one with exactly room");
            Harness.Check(!ImplementWork.Accepts(seeder, 95f, 8f),
                "a hopper with five litres of room refuses an eight-litre sack outright");
            Harness.Check(!ImplementWork.Accepts(seeder, 100f, 8f), "a full one certainly does");
            Harness.Check(!ImplementWork.Accepts(seeder, 0f, 0f), "and nothing is not a sack");
            Harness.Check(!ImplementWork.Accepts(null, 0f, 8f), "nor is a sack with no implement to take it");

            // A modded drill whose whole hopper is smaller than one sack must refuse
            // rather than fill for free on every press.
            var tiny = Implement(ImplementKind.Seeder, 2f);
            tiny.hopperCapacityLitres = 3f;
            Harness.Check(!ImplementWork.Accepts(tiny, 0f, 8f),
                "a hopper smaller than a sack never takes one");

            var harvester = Implement(ImplementKind.Harvester, 5f);
            harvester.hopperCapacityLitres = 400f;

            Harness.Check(ImplementWork.CellsAffordable(harvester, 0f, 30) > 0,
                "an empty harvester can work");
            Harness.Equal(ImplementWork.CellsAffordable(harvester, 400f, 30), 0,
                "a full one cannot");
            Harness.Check(ImplementWork.IsBlocked(harvester, 400f), "and says so");

            // Filling: overflow is reported rather than quietly kept.
            float hopper = 380f;
            float spilled = ImplementWork.AddToHopper(harvester, ref hopper, 50f);
            Harness.Equal(hopper, 400f, "the hopper fills to capacity");
            Harness.Equal(spilled, 30f, "and the rest is spilled, not pocketed");

            hopper = 0f;
            spilled = ImplementWork.AddToHopper(harvester, ref hopper, 120f);
            Harness.Equal(hopper, 120f, "an empty hopper takes the lot");
            Harness.Equal(spilled, 0f, "with nothing spilled");

            Harness.Equal(ImplementWork.Describe(null, 0f, true), "NO IMPLEMENT", "no implement says so");
            Harness.Equal(ImplementWork.Describe(seeder, 50f, false), "SEEDER  RAISED", "a raised implement says raised");
            Harness.Equal(ImplementWork.Describe(seeder, 50f, true), "SEEDER  WORKING", "a working one says working");
            Harness.Equal(ImplementWork.Describe(seeder, 0f, true), "SEEDER  OUT OF SEED", "an empty one says why it stopped");
            Harness.Equal(ImplementWork.Describe(harvester, 400f, true), "HARVESTER  HOPPER FULL", "and so does a full one");
        }

        // -------------------------------------------------------------- whole pass

        /// <summary>
        /// Drives the four passes over the same strip of ground, exactly the way
        /// ImplementController does: sweep the swath, ask what the hopper can pay for,
        /// then put each cell through the grid.
        ///
        /// The swath and the hopper are each right on their own above. This is the one
        /// that catches them being wrong together - a field that comes up striped,
        /// half-sown or short.
        /// </summary>
        static void Pass()
        {
            Harness.Section("field machines: a whole pass over one strip");

            const float Width = 3f;
            const float Length = 20f;

            var grid = new FieldGrid();
            var cells = new List<Vector2Int>();
            var worked = new List<Vector2Int>();

            // Pass one: break the ground.
            Drive(grid, cells, worked, Width, Length, (g, c) => g.Plow(c.x, c.y, 0.0));

            int plowed = 0;
            for (int i = 0; i < worked.Count; i++)
            {
                if (grid.Get(worked[i].x, worked[i].y).State == FieldCellState.Plowed) plowed++;
            }
            Harness.Equal(plowed, worked.Count,
                string.Format("every one of the {0} cells the plough passed over is broken", worked.Count));
            Harness.Check(worked.Count >= Length * Width * 0.9f,
                string.Format("a {0} m x {1} m strip works {2} cells, near the {3} it covers",
                    Width, Length, worked.Count, Length * Width));

            // No holes: the worked set must be a solid rectangle, not a comb.
            Harness.Check(IsSolidRectangle(worked), "and the worked ground is solid, not striped");

            // Pass two: work it down.
            Drive(grid, cells, worked, Width, Length, (g, c) => g.Cultivate(c.x, c.y, 1.0));
            int cultivated = 0;
            for (int i = 0; i < worked.Count; i++)
            {
                if (grid.Get(worked[i].x, worked[i].y).State == FieldCellState.Cultivated) cultivated++;
            }
            Harness.Equal(cultivated, worked.Count, "the cultivator leaves every cell ready for seed");

            // Pass three: sow it, with deliberately too little seed. Half the strip
            // should come up and half should stay bare - and the bare half must be
            // bare, not sown-and-empty.
            var seeder = Implement(ImplementKind.Seeder, Width);
            seeder.hopperCapacityLitres = 100f;
            seeder.seedLitresPerCell = 1f;

            float hopper = worked.Count * 0.5f;
            int sown = 0;

            for (float z = 0.5f; z < Length; z += 1f)
            {
                FieldSwath.Collect(new Vector3(1.5f, 0f, z), new Vector3(1.5f, 0f, z + 1f), 0f, Width, cells);
                int affordable = ImplementWork.CellsAffordable(seeder, hopper, cells.Count);

                int done = 0;
                for (int i = 0; i < affordable; i++)
                {
                    if (grid.Sow(cells[i].x, cells[i].y, 1, 2.0)) done++;
                }
                hopper -= ImplementWork.SeedCost(seeder, done);
                sown += done;
            }

            Harness.Check(sown > 0 && sown < worked.Count,
                string.Format("half a hopper sows {0} of {1} cells", sown, worked.Count));
            Harness.Check(hopper >= -0.001f, "and never sows more seed than it had");

            int seeded = 0, stillBare = 0;
            for (int i = 0; i < worked.Count; i++)
            {
                var state = grid.Get(worked[i].x, worked[i].y).State;
                if (state == FieldCellState.Seeded) seeded++;
                else if (state == FieldCellState.Cultivated) stillBare++;
            }
            Harness.Equal(seeded, sown, "every cell that took seed is sown");
            Harness.Equal(seeded + stillBare, worked.Count,
                "and every other cell is plainly bare rather than sown-and-empty");

            // Pass four: cut it. The hopper is what decides where the pass stops.
            var harvester = Implement(ImplementKind.Harvester, Width);
            harvester.hopperCapacityLitres = 120f;

            float load = 0f;
            float spilled = 0f;
            int cut = 0;

            for (int i = 0; i < worked.Count; i++)
            {
                var cell = worked[i];
                if (grid.Get(cell.x, cell.y).State != FieldCellState.Seeded) continue;

                // Ripe: growth is a clock difference, so this is just a later hour.
                grid.Refresh(cell.x, cell.y, 2.0 + 48.0, 24f);

                byte crop;
                float litres = grid.Harvest(cell.x, cell.y, 14f, 50.0, out crop);
                if (litres <= 0f) continue;

                spilled += ImplementWork.AddToHopper(harvester, ref load, litres);
                cut++;
            }

            Harness.Check(cut > 0, string.Format("the harvester cuts {0} ripe cells", cut));
            Harness.Equal(load, 120f, "and fills its hopper");
            Harness.Check(spilled > 0f,
                string.Format("the {0:0} L it could not hold is reported as spilled, not pocketed", spilled));

            int stubble = 0;
            for (int i = 0; i < worked.Count; i++)
            {
                if (grid.Get(worked[i].x, worked[i].y).State == FieldCellState.Stubble) stubble++;
            }
            Harness.Equal(stubble, cut, "every cut cell is left as stubble, ready to plough back in");
        }

        /// <summary>Drives one pass north up the strip and records every cell it touched.</summary>
        static void Drive(FieldGrid grid, List<Vector2Int> cells, List<Vector2Int> worked,
                          float width, float length, System.Func<FieldGrid, Vector2Int, bool> operation)
        {
            worked.Clear();

            for (float z = 0.5f; z < length; z += 1f)
            {
                FieldSwath.Collect(new Vector3(1.5f, 0f, z), new Vector3(1.5f, 0f, z + 1f), 0f, width, cells);
                for (int i = 0; i < cells.Count; i++)
                {
                    operation(grid, cells[i]);
                    if (!worked.Contains(cells[i])) worked.Add(cells[i]);
                }
            }
        }

        /// <summary>True when the cells fill their bounding box with no gaps.</summary>
        static bool IsSolidRectangle(List<Vector2Int> cells)
        {
            if (cells.Count == 0) return false;

            int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                minX = Mathf.Min(minX, cells[i].x); maxX = Mathf.Max(maxX, cells[i].x);
                minZ = Mathf.Min(minZ, cells[i].y); maxZ = Mathf.Max(maxZ, cells[i].y);
            }

            int area = (maxX - minX + 1) * (maxZ - minZ + 1);
            return cells.Count == area;
        }

        // ------------------------------------------------------------- the content

        /// <summary>
        /// The machines as shipped. A tractor you cannot craft, or an implement whose
        /// item does not point back at it, is a system that exists only in the tests.
        /// </summary>
        static void Machines(ContentDatabase db)
        {
            Harness.Section("field machines: the content");

            if (db == null) return;

            VehicleDefinition tractor = null;
            for (int i = 0; i < db.vehicles.Count; i++)
            {
                if (db.vehicles[i].stringId == "madvoxel:vehicle_tractor") tractor = db.vehicles[i];
            }
            Harness.Check(tractor != null, "there is a tractor");
            if (tractor == null) return;

            Harness.Check(tractor.maxSpeed < 12f, "which is slower than the buggy, as a tractor should be");
            Harness.Check(tractor.fuelItem != null, "and takes fuel");

            Harness.Equal(db.implements.Count, 4, "four implements ship: plough, cultivator, drill, harvester");

            var kinds = new HashSet<ImplementKind>();
            var problems = new List<string>();

            for (int i = 0; i < db.implements.Count; i++)
            {
                var def = db.implements[i];
                kinds.Add(def.kind);

                if (def.item == null) { problems.Add(def.stringId + " has no item"); continue; }

                // The item and the implement have to point at each other, or the thing
                // is craftable and unhitchable, or hitchable and uncraftable.
                if (def.item.hitchImplement != def) problems.Add(def.stringId + " and its item disagree");
                if (def.item.maxStack != 1) problems.Add(def.stringId + " stacks");
                if (def.workingWidth < FieldGrid.CellSize) problems.Add(def.stringId + " is narrower than a cell");
                if (def.speedMultiplier > 1f) problems.Add(def.stringId + " makes the tractor faster");
            }

            Harness.Check(problems.Count == 0,
                "every implement is carried, hitched and sized sanely"
                + (problems.Count > 0 ? ": " + string.Join(", ", problems) : ""));
            Harness.Equal(kinds.Count, 4, "and all four operations are covered");

            // A seeder with no seed per cell would sow a field for nothing.
            for (int i = 0; i < db.implements.Count; i++)
            {
                var def = db.implements[i];
                if (def.kind != ImplementKind.Seeder) continue;

                Harness.Check(def.seedLitresPerCell > 0f, "the drill spends seed per cell");
                Harness.Check(def.litresPerSeedItem > def.seedLitresPerCell,
                    "and one seed item is worth more than one cell");
                Harness.Check(def.hopperCapacityLitres > 0f, "and it has a hopper to spend from");
            }

            // The tractor kit and every implement must be reachable.
            CheckObtainable(db, ItemIds.TractorKit);
            CheckObtainable(db, ItemIds.ImplementPlow);
            CheckObtainable(db, ItemIds.ImplementCultivator);
            CheckObtainable(db, ItemIds.ImplementSeeder);
            CheckObtainable(db, ItemIds.ImplementHarvester);

            // There has to be something to sow: at least one crop the drill can take.
            int fieldCrops = 0;
            for (int i = 0; i < db.crops.Count; i++)
            {
                if (db.crops[i].growsOnField && db.crops[i].seedItem != null) fieldCrops++;
            }
            Harness.Check(fieldCrops > 0, string.Format("{0} crop(s) can be sown by a drill", fieldCrops));
        }

        static void CheckObtainable(ContentDatabase db, string itemId)
        {
            ItemDefinition item = null;
            for (int i = 0; i < db.items.Count; i++)
            {
                if (db.items[i].stringId == itemId) item = db.items[i];
            }

            if (item == null)
            {
                Harness.Check(false, itemId + " exists");
                return;
            }

            RecipeDefinition recipe = null;
            for (int i = 0; i < db.recipes.Count; i++)
            {
                if (db.recipes[i].output == item) recipe = db.recipes[i];
            }

            if (recipe == null)
            {
                Harness.Check(false, item.displayName + " has a recipe");
                return;
            }

            if (recipe.unlockedByDefault)
            {
                Harness.Check(true, item.displayName + " is craftable from the start");
                return;
            }

            // Locked: the skill that unlocks it must actually list it at a rank it can reach.
            var skill = db.perkTree.Find(recipe.requiredPerkId);
            bool listed = false;
            if (skill != null)
            {
                for (int i = 0; i < skill.unlocksRecipeIds.Count && i < skill.maxRank; i++)
                {
                    if (skill.unlocksRecipeIds[i] == recipe.stringId) listed = true;
                }
            }

            Harness.Check(listed,
                item.displayName + " is unlocked by a rank of " + recipe.requiredPerkId + " you can actually buy");
        }
    }
}
