using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.Inventory;
using MadVoxel.Perks;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// Can you actually get there from a fresh world?
    ///
    /// The obtainability check next door asks whether an item is reachable *at all*,
    /// and a trader selling it counts. That is too weak, and it hid a real wall: every
    /// vehicle in the game needs a salvaged engine, and the only source was one trader's
    /// shelf at the third reputation tier - nine hundred reputation, for a shop you
    /// largely earn reputation at by selling what the vehicle would let you farm.
    ///
    /// So this walks the recipe tree from what the world hands you - drops, crops,
    /// forage, the starting kit - with traders deliberately excluded, and asks what
    /// cannot be built. A trader should be a shortcut, never the gate.
    /// </summary>
    public static class ProgressionTests
    {
        public static void Run(ContentDatabase db)
        {
            if (db == null) return;

            Reachability(db);
            PerkGates(db);
        }

        /// <summary>
        /// Items that are meant to come from people rather than the ground. Currency and
        /// contract rewards are not things you dig up, and that is the point of them.
        /// </summary>
        static readonly HashSet<string> FromPeopleOnly = new HashSet<string>
        {
            ItemIds.TradeToken
        };

        static void Reachability(ContentDatabase db)
        {
            Harness.Section("progression: what you can build without a trader");

            var reachable = new HashSet<ItemDefinition>();

            // 1. What the world gives you for hitting it or picking it.
            var blocks = db.blocks.blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (block.dropItem != null) reachable.Add(block.dropItem);
                if (block.secondaryDropItem != null && block.secondaryDropChance > 0f) reachable.Add(block.secondaryDropItem);
            }

            for (int i = 0; i < db.zombies.Count; i++)
            {
                if (db.zombies[i].dropItem != null && db.zombies[i].dropMax > 0) reachable.Add(db.zombies[i].dropItem);
            }

            for (int i = 0; i < db.crops.Count; i++)
            {
                if (db.crops[i].harvestItem != null) reachable.Add(db.crops[i].harvestItem);
                if (db.crops[i].seedItem != null) reachable.Add(db.crops[i].seedItem);
            }

            for (int i = 0; i < db.startingItems.Count; i++)
            {
                if (db.startingItems[i].item != null) reachable.Add(db.startingItems[i].item);
            }

            int fromWorld = reachable.Count;
            Harness.Check(fromWorld > 8,
                string.Format("{0} items come straight out of the world", fromWorld));

            // 2. Everything a recipe can make from those, and from what those make.
            bool grew = true;
            int rounds = 0;
            while (grew && rounds < 64)
            {
                grew = false;
                rounds++;

                for (int i = 0; i < db.recipes.Count; i++)
                {
                    var recipe = db.recipes[i];
                    if (recipe.output == null || reachable.Contains(recipe.output)) continue;

                    bool affordable = true;
                    for (int j = 0; j < recipe.ingredients.Count; j++)
                    {
                        var item = recipe.ingredients[j].item;
                        if (item != null && !reachable.Contains(item)) { affordable = false; break; }
                    }

                    if (!affordable) continue;

                    reachable.Add(recipe.output);
                    grew = true;
                }
            }

            Harness.Check(rounds < 64, "the recipe tree settles rather than looping");
            Harness.Check(reachable.Count > fromWorld,
                string.Format("crafting turns {0} gathered items into {1}", fromWorld, reachable.Count));

            // 3. Anything a recipe wants that nothing can supply.
            var walls = new List<string>();
            for (int i = 0; i < db.recipes.Count; i++)
            {
                var recipe = db.recipes[i];
                if (recipe.output == null) continue;

                for (int j = 0; j < recipe.ingredients.Count; j++)
                {
                    var item = recipe.ingredients[j].item;
                    if (item == null || reachable.Contains(item)) continue;
                    if (FromPeopleOnly.Contains(item.stringId)) continue;

                    walls.Add(recipe.stringId + " needs " + item.displayName);
                }
            }

            Harness.Check(walls.Count == 0,
                "nothing craftable is walled behind a trader"
                + (walls.Count > 0 ? ": " + string.Join("; ", walls) : ""));

            // 4. And the things the last few passes added, named outright. A general
            // check passing is good; knowing the tractor specifically is buildable is
            // what stops a whole layer shipping unreachable.
            CheckBuildable(db, reachable, ItemIds.TractorKit);
            CheckBuildable(db, reachable, ItemIds.ImplementPlow);
            CheckBuildable(db, reachable, ItemIds.ImplementCultivator);
            CheckBuildable(db, reachable, ItemIds.ImplementSeeder);
            CheckBuildable(db, reachable, ItemIds.ImplementHarvester);
            CheckBuildable(db, reachable, ItemIds.PieceFurnace);
            CheckBuildable(db, reachable, ItemIds.PieceSilo);
            CheckBuildable(db, reachable, ItemIds.EngineBlock);
            CheckBuildable(db, reachable, ItemIds.Wheel);
        }

        static void CheckBuildable(ContentDatabase db, HashSet<ItemDefinition> reachable, string itemId)
        {
            var item = db.Item(itemId);
            if (item == null)
            {
                Harness.Check(false, itemId + " exists");
                return;
            }

            Harness.Check(reachable.Contains(item),
                item.displayName + " can be reached without buying one");
        }

        // ------------------------------------------------------------- perk gates

        /// <summary>
        /// A recipe gated behind a rank you cannot reach, or a rank that costs more
        /// points than the levels required could ever pay for, is a wall with a door
        /// painted on it.
        /// </summary>
        static void PerkGates(ContentDatabase db)
        {
            Harness.Section("progression: the skill gates are payable");

            var tree = db.perkTree;
            var unpayable = new List<string>();

            for (int i = 0; i < db.recipes.Count; i++)
            {
                var recipe = db.recipes[i];
                if (recipe.unlockedByDefault || string.IsNullOrEmpty(recipe.requiredPerkId)) continue;

                var perk = tree.Find(recipe.requiredPerkId);
                if (perk == null) continue;

                // Points to own the rank, and the level the perk itself demands first.
                int cost = recipe.requiredPerkRank * Mathf.Max(1, perk.pointCostPerRank);
                int levelNeeded = Mathf.Max(perk.requiredPlayerLevel, 1);

                // A level-N player has earned N-1 points at one a level.
                int pointsAt = (levelNeeded - 1) * Mathf.Max(1, GetPointsPerLevel());
                if (pointsAt >= cost) continue;

                // Not unpayable, just later. Work out the level it becomes payable at and
                // make sure that is a level the game can actually reach.
                int levelToAfford = cost / Mathf.Max(1, GetPointsPerLevel()) + 1;
                if (levelToAfford > 60) unpayable.Add(recipe.stringId + " needs level " + levelToAfford);
            }

            Harness.Check(unpayable.Count == 0,
                "every perk-gated recipe is payable at a sane level"
                + (unpayable.Count > 0 ? ": " + string.Join(", ", unpayable) : ""));

            // Everything a perk claims to unlock must be a rank that perk actually has.
            var overshoot = new List<string>();
            for (int i = 0; i < tree.perks.Count; i++)
            {
                var perk = tree.perks[i];
                if (perk.unlocksRecipeIds.Count > perk.maxRank)
                {
                    overshoot.Add(string.Format("{0} lists {1} unlocks for {2} ranks",
                        perk.stringId, perk.unlocksRecipeIds.Count, perk.maxRank));
                }
            }

            Harness.Check(overshoot.Count == 0,
                "no skill promises an unlock at a rank it does not have"
                + (overshoot.Count > 0 ? ": " + string.Join(", ", overshoot) : ""));

            // And a recipe's stated rank must match where its skill actually lists it,
            // or the screen says one thing and the unlock does another.
            var mismatched = new List<string>();
            for (int i = 0; i < db.recipes.Count; i++)
            {
                var recipe = db.recipes[i];
                if (recipe.unlockedByDefault || string.IsNullOrEmpty(recipe.requiredPerkId)) continue;

                var perk = tree.Find(recipe.requiredPerkId);
                if (perk == null) continue;

                int listedAt = perk.unlocksRecipeIds.IndexOf(recipe.stringId) + 1;
                if (listedAt == 0) { mismatched.Add(recipe.stringId + " is gated by a skill that never unlocks it"); continue; }

                if (listedAt != recipe.requiredPerkRank)
                {
                    mismatched.Add(string.Format("{0} says rank {1} but unlocks at {2}",
                        recipe.stringId, recipe.requiredPerkRank, listedAt));
                }
            }

            Harness.Check(mismatched.Count == 0,
                "a locked recipe's stated rank is the rank that unlocks it"
                + (mismatched.Count > 0 ? ": " + string.Join("; ", mismatched) : ""));
        }

        static int GetPointsPerLevel()
        {
            // The default on PlayerProgression; content does not override it.
            return 1;
        }
    }
}
