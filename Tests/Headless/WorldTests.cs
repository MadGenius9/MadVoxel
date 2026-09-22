using MadVoxel.Claim;
using MadVoxel.Content;
using MadVoxel.Inventory;
using MadVoxel.Inventory.Spoil;
using MadVoxel.World.Biomes;
using MadVoxel.World.Weather;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The three world layers that can be wrong without anyone seeing it: where the
    /// biomes land, what the sky is allowed to roll, how long food lasts, and how loud
    /// a claim gets.
    /// </summary>
    public static class WorldTests
    {
        public static void Run(ContentDatabase db)
        {
            Biomes(db);
            Weather(db);
            Spoilage(db);
            Heat();
        }

        // ------------------------------------------------------------------ biomes

        static void Biomes(ContentDatabase db)
        {
            Harness.Section("world: biome paint");

            const int Extent = 1536;
            var map = new BiomeMap(20261, Extent);

            // You wake up on good dirt. This is design, not a roll.
            Harness.Equal(map.At(0, 0), BiomeId.Farmland, "spawn is always farmland");
            Harness.Equal(map.At(40, -40), BiomeId.Farmland, "and so is the ground you first walk on");

            // The shelf is small and at the edge, never a blob in the middle of the map.
            int frostNearSpawn = 0;
            int frostAtEdge = 0;
            for (int z = -600; z <= 600; z += 60)
            {
                for (int x = -600; x <= 600; x += 60)
                {
                    if (map.At(x, z) == BiomeId.FrostShelf) frostNearSpawn++;
                }
            }
            for (int x = -Extent; x < Extent; x += 60)
            {
                if (map.At(x, (int)(Extent * 0.92f)) == BiomeId.FrostShelf) frostAtEdge++;
            }

            Harness.Equal(frostNearSpawn, 0, "no frost shelf anywhere near spawn");
            Harness.Check(frostAtEdge > 0, "but the far edge band is shelf");

            // Every region has to actually appear, or one of the five is a dead entry.
            var seen = new System.Collections.Generic.HashSet<BiomeId>();
            for (int z = -Extent; z < Extent; z += 90)
            {
                for (int x = -Extent; x < Extent; x += 90) seen.Add(map.At(x, z));
            }
            Harness.Equal(seen.Count, 5, "all five regions are painted somewhere on the map");

            // Same seed, same paint - this is what lets the chunk store skip saving it.
            var again = new BiomeMap(20261, Extent);
            bool identical = true;
            for (int z = -900; z <= 900 && identical; z += 37)
            {
                for (int x = -900; x <= 900; x += 37)
                {
                    if (map.At(x, z) != again.At(x, z)) { identical = false; break; }
                }
            }
            Harness.Check(identical, "the same seed repaints the same map, so it need never be saved");

            var other = new BiomeMap(777, Extent);
            bool differs = false;
            for (int x = 200; x <= 1400 && !differs; x += 50)
            {
                if (map.At(x, 200) != other.At(x, 200)) differs = true;
            }
            Harness.Check(differs, "a different seed paints a different map");

            // Borders have to smear, or the fade is a lie and the blend never runs.
            int fadeColumns = 0;
            for (int x = -1200; x <= 1200; x += 7)
            {
                if (map.IsInFade(x, 300)) fadeColumns++;
            }
            Harness.Check(fadeColumns > 0, "borders smear rather than stepping");

            // And the table has to describe every one of them.
            if (db.biomes != null)
            {
                var missing = new System.Collections.Generic.List<string>();
                foreach (BiomeId id in System.Enum.GetValues(typeof(BiomeId)))
                {
                    var def = db.biomes.Find(id);
                    if (def == null) { missing.Add(id.ToString()); continue; }
                    if (def.pumpMultiplier <= 0f) missing.Add(id + " (no pump rate)");
                    if (def.weatherWeights.Count == 0) missing.Add(id + " (no weather)");
                }
                Harness.Check(missing.Count == 0,
                    "every region has a definition with weather and a pump rate"
                    + (missing.Count > 0 ? ": " + string.Join(", ", missing) : ""));

                // The regions have to actually differ, or they are scenery.
                var farm = db.biomes.Find(BiomeId.Farmland);
                var flats = db.biomes.Find(BiomeId.DryFlats);
                Harness.Check(flats.pumpMultiplier < farm.pumpMultiplier, "a well on the flats is weaker than one on farmland");
                Harness.Check(flats.moistureDrainMultiplier > farm.moistureDrainMultiplier, "and the flats dry out faster");
                Harness.Check(flats.solarMultiplier > farm.solarMultiplier, "while the sun is better there");

                var clay = db.biomes.Find(BiomeId.ClayHills);
                Harness.Check(clay.scrapDensityMultiplier > farm.scrapDensityMultiplier, "the rust belt has the scrap");
                Harness.Check(clay.cropYieldMultiplier < farm.cropYieldMultiplier, "and worse dirt");

                var scrub = db.biomes.Find(BiomeId.PineScrub);
                Harness.Check(scrub.treeDensityMultiplier > farm.treeDensityMultiplier, "the scrub has the wood");
                Harness.Check(scrub.heatFloorBonus < farm.heatFloorBonus, "and hides a claim better than open fields");
            }

            // Crops have to have an opinion, and at least one has to refuse somewhere.
            int blocked = 0;
            for (int i = 0; i < db.crops.Count; i++)
            {
                var crop = db.crops[i];
                Harness.Check(crop.biomeYields.Count > 0, crop.displayName + " has an opinion about where it grows");

                foreach (BiomeId id in System.Enum.GetValues(typeof(BiomeId)))
                {
                    if (crop.IsBlockedIn(id)) blocked++;
                }
            }
            Harness.Check(blocked > 0, "at least one crop refuses at least one region outright");
            Harness.Check(db.crops[0].YieldIn(BiomeId.Farmland) >= 1f, "the staple is happy on farmland");
        }

        // ----------------------------------------------------------------- weather

        static void Weather(ContentDatabase db)
        {
            Harness.Section("world: weather");

            var table = db.weather;
            Harness.Check(table != null && table.states.Count == 6, "all six sky states are authored");

            var flats = db.biomes.Find(BiomeId.DryFlats);
            var scrub = db.biomes.Find(BiomeId.PineScrub);

            // Frost is a night. It does not arrive at two in the afternoon.
            for (int i = 0; i <= 20; i++)
            {
                var kind = WeatherSchedule.Roll(flats, table, false, WeatherKind.Clear, i / 20f);
                Harness.Check(kind != WeatherKind.FrostNight, "frost never rolls onto a day (roll " + i + ")");
            }

            bool frostPossible = false;
            var frostBiome = db.biomes.Find(BiomeId.FrostShelf);
            for (int i = 0; i <= 40; i++)
            {
                if (WeatherSchedule.Roll(frostBiome, table, true, WeatherKind.Clear, i / 40f) == WeatherKind.FrostNight)
                {
                    frostPossible = true;
                    break;
                }
            }
            Harness.Check(frostPossible, "but the shelf gets it on a night");

            // A region with zero weight for a state must never see it.
            bool droughtOnShelf = false;
            for (int i = 0; i <= 40; i++)
            {
                if (WeatherSchedule.Roll(frostBiome, table, false, WeatherKind.Clear, i / 40f) == WeatherKind.Drought)
                {
                    droughtOnShelf = true;
                    break;
                }
            }
            Harness.Check(!droughtOnShelf, "a region that never droughts never droughts");

            // The dry flats must actually drought more than the pine scrub, or the
            // whole water economy is the same everywhere.
            int flatsDroughts = 0, scrubDroughts = 0;
            for (int i = 0; i < 200; i++)
            {
                float roll = i / 200f;
                if (WeatherSchedule.Roll(flats, table, false, WeatherKind.Clear, roll) == WeatherKind.Drought) flatsDroughts++;
                if (WeatherSchedule.Roll(scrub, table, false, WeatherKind.Clear, roll) == WeatherKind.Drought) scrubDroughts++;
            }
            Harness.Check(flatsDroughts > scrubDroughts * 3,
                string.Format("the flats drought far more than the scrub ({0} vs {1})", flatsDroughts, scrubDroughts));

            // Repeating what just ended reads as the weather being stuck.
            int repeats = 0;
            for (int i = 0; i < 100; i++)
            {
                if (WeatherSchedule.Roll(flats, table, false, WeatherKind.Drought, i / 100f) == WeatherKind.Drought) repeats++;
            }
            Harness.Check(repeats < 12, string.Format("drought rarely follows drought ({0} in 100)", repeats));

            // Durations stay inside their own range.
            for (int i = 0; i < table.states.Count; i++)
            {
                var state = table.states[i];
                float lo = WeatherSchedule.Duration(state, 0f);
                float hi = WeatherSchedule.Duration(state, 1f);
                Harness.Check(lo >= state.minHours - 0.01f && hi <= state.maxHours + 0.01f,
                    state.kind + " lasts inside its own range");
            }

            var drought = table.Find(WeatherKind.Drought);
            Harness.Check(drought.minHours >= 24f, "a drought is a multi-day event, not an afternoon");
            Harness.Check(table.Find(WeatherKind.Storm).maxHours <= 6f, "a storm is short");
            Harness.Equal(table.Find(WeatherKind.Storm).solarMultiplier, 0f, "and takes the panels to zero");
            Harness.Check(table.Find(WeatherKind.Storm).breakChancePerHour > 0f, "a storm can break something");
            Harness.Check(table.Find(WeatherKind.FrostNight).freezesTaps, "frost freezes taps");
            Harness.Check(table.Find(WeatherKind.Rain).barrelFillLitresPerHour > 0f, "rain fills a barrel");

            // Clear says nothing. That is the point of the one-word line.
            Harness.Equal(WeatherSchedule.ClockLine(14, "17:41", table.Find(WeatherKind.Clear)),
                "DAY 14   17:41", "clear weather adds no word to the clock");
            Harness.Equal(WeatherSchedule.ClockLine(14, "17:41", drought),
                "DAY 14   17:41   DROUGHT", "and a drought does");
        }

        // ---------------------------------------------------------------- spoilage

        static void Spoilage(ContentDatabase db)
        {
            Harness.Section("world: spoilage");

            var stew = db.Item(ItemIds.VegetableStew);
            var canned = db.Item(ItemIds.CannedFood);
            var rot = db.Item(ItemIds.Rot);

            Harness.Check(SpoilRules.CanSpoil(stew), "cooked food goes off");
            Harness.Check(!SpoilRules.CanSpoil(canned), "canned food never does - that is why it is the starting kit");
            Harness.Check(rot != null && !SpoilRules.CanSpoil(rot), "and rot does not rot again");

            var stack = new ItemStack(stew, 3);
            Harness.Equal(stack.SpoilRemaining, stew.spoilHours, "a fresh stack starts with its full shelf life");

            // A crate is a crate.
            var warm = SpoilRules.Tick(stack, 10f, SpoilRules.RateIn(false, false, 6f), rot);
            Harness.Equal(warm.SpoilRemaining, stew.spoilHours - 10f, "ten hours in a box costs ten hours");

            // A fridge with watts is the only thing that slows it...
            var cold = SpoilRules.Tick(stack, 10f, SpoilRules.RateIn(true, true, 6f), rot);
            Harness.Check(cold.SpoilRemaining > warm.SpoilRemaining, "a powered fridge slows the clock");
            Harness.Check(Mathf.Abs(cold.SpoilRemaining - (stew.spoilHours - 10f / 6f)) < 0.01f,
                "by exactly its slowdown");

            // ...and an unpowered one is a cupboard. This is the whole point of the grid.
            var dark = SpoilRules.Tick(stack, 10f, SpoilRules.RateIn(true, false, 6f), rot);
            Harness.Equal(dark.SpoilRemaining, warm.SpoilRemaining, "an unplugged fridge is just a box");

            var spoiled = SpoilRules.Tick(stack, stew.spoilHours + 1f, 1f, rot);
            Harness.Equal(spoiled.Item, rot, "food that runs out becomes rot");
            Harness.Equal(spoiled.Count, 3, "and keeps its count");
            Harness.Check(spoiled.SpoilRemaining < 0f, "rot has no clock of its own");

            // Splitting must not launder a stack, or the fridge is pointless.
            var old = new ItemStack(stew, 8).WithSpoil(4f);
            var half = old.WithCount(4);
            Harness.Equal(half.SpoilRemaining, 4f, "splitting keeps the shelf life");

            // Nor may topping up.
            Harness.Equal(SpoilRules.MergedLife(4f, 36f), 4f, "merging keeps the older clock");
            Harness.Equal(SpoilRules.MergedLife(36f, 4f), 4f, "whichever way round it is");
            Harness.Equal(SpoilRules.MergedLife(-1f, 12f), 12f, "and an unset clock takes the other");

            var bag = new Inventory.Inventory(4);
            bag.Add(stew, 2, -1, 4f);
            bag.Add(stew, 2, -1, 36f);
            Harness.Equal(bag[0].Count, 4, "the two lots merged");
            Harness.Equal(bag[0].SpoilRemaining, 4f, "and the crate remembers the older one");

            Harness.Equal(SpoilRules.Describe(new ItemStack(stew, 1).WithSpoil(50f)), "2d", "two days reads as 2d");
            Harness.Equal(SpoilRules.Describe(new ItemStack(stew, 1).WithSpoil(6f)), "6h", "six hours reads as 6h");
            Harness.Equal(SpoilRules.Describe(new ItemStack(canned, 1)), "", "canned food shows no clock at all");

            // Raw produce should outlast a cooked meal, or cooking is a trap.
            Harness.Check(db.Item(ItemIds.Potato).spoilHours > stew.spoilHours,
                "a raw potato keeps longer than a bowl of stew");
            Harness.Check(db.Item(ItemIds.Grain).spoilHours > db.Item(ItemIds.Potato).spoilHours,
                "and dry grain keeps longest of all");

            // Everything that can spoil must have somewhere to go.
            var orphans = new System.Collections.Generic.List<string>();
            for (int i = 0; i < db.items.Count; i++)
            {
                var item = db.items[i];
                if (SpoilRules.CanSpoil(item) && item.spoiledInto == null) orphans.Add(item.stringId);
            }
            Harness.Check(orphans.Count == 0,
                "every perishable turns into something" + (orphans.Count > 0 ? ": " + string.Join(", ", orphans) : ""));
        }

        // -------------------------------------------------------------------- heat

        static void Heat()
        {
            Harness.Section("world: claim heat");

            var weights = HeatWeights.Default;

            // An empty claim is silent; a farm never is.
            Harness.Equal(ClaimHeatMath.Floor(weights, 0, 0, 0f, 0f), 0f, "an empty claim has no floor");
            Harness.Check(ClaimHeatMath.Floor(weights, 3, 400, 6f, 0f) > 20f,
                "three people and four hundred worked cells is never quiet");

            // Quiet Claim shaves the floor rather than removing it.
            float loud = ClaimHeatMath.Floor(weights, 3, 400, 6f, 0f);
            float quiet = ClaimHeatMath.Floor(weights, 3, 400, 6f, 0.3f);
            Harness.Check(quiet < loud && quiet > 0f, "the perk shaves the floor but cannot silence a farm");

            // Lights after dark push it up; switching them off lets it fall.
            float heat = 10f;
            float floor = 5f;
            float withLights = ClaimHeatMath.Sources(20f, 14f, 0f, 0f, weights, 0f);
            heat = ClaimHeatMath.Step(heat, floor, withLights, weights, 1f, false);
            Harness.Check(heat > 10f, "a lit, running base gets louder within the hour");

            float blackout = ClaimHeatMath.Step(heat, floor, 0f, weights, 1f, false);
            Harness.Check(blackout < heat, "and the blackout switch is felt in the same hour");

            // It can never fall below the floor, however long you hold your breath.
            float settled = blackout;
            for (int i = 0; i < 40; i++) settled = ClaimHeatMath.Step(settled, floor, 0f, weights, 1f, false);
            Harness.Equal(settled, floor, "heat settles at the floor, not at zero");

            // Nor above 100.
            float pegged = 90f;
            for (int i = 0; i < 20; i++) pegged = ClaimHeatMath.Step(pegged, floor, 80f, weights, 1f, false);
            Harness.Equal(pegged, ClaimHeatMath.Max, "and is capped at a hundred");

            // Dawn drags it down on top of the cooling.
            float night = 60f;
            float afterDawn = ClaimHeatMath.Step(night, floor, 0f, weights, 1f, true);
            float afterHour = ClaimHeatMath.Step(night, floor, 0f, weights, 1f, false);
            Harness.Check(afterDawn < afterHour, "dawn pulls harder than an ordinary hour");

            // A trap only counts while it is actually swinging.
            Harness.Equal(ClaimHeatMath.Sources(0f, 0f, 0f, 0f, weights, 0f), 0f, "an armed, idle trap is silent");
            Harness.Check(ClaimHeatMath.Sources(0f, 0f, 8f, 0f, weights, 0f) > 0f, "a swinging one is not");

            Harness.Equal(ClaimHeatMath.Describe(10f), "QUIET", "a quiet claim says so");
            Harness.Equal(ClaimHeatMath.Describe(60f), "LOUD", "a loud one says so");
            Harness.Equal(ClaimHeatMath.Describe(90f), "BEACON", "and a beacon is a beacon");

            Harness.Equal(ClaimHeatMath.WandererMultiplier(0f), 1f, "a silent claim draws no extra wanderers");
            Harness.Check(ClaimHeatMath.WandererMultiplier(100f) > 2f, "a beacon draws a lot more");
            Harness.Equal(ClaimHeatMath.HordeBonus(0f, 10), 0, "and heat adds nothing to a quiet blood moon");
            Harness.Check(ClaimHeatMath.HordeBonus(100f, 10) > 0, "but a loud one is a bigger night");
        }
    }
}
