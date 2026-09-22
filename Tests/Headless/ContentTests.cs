using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.Inventory;
using MadVoxel.World.Voxel;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// Runs the real content library. A missing dictionary key or an unresolved
    /// cross-reference here is a hard crash on the game's first frame, so this is the
    /// single most valuable thing to execute outside Unity.
    /// </summary>
    public static class ContentTests
    {
        public static ContentDatabase Database;

        public static void Run()
        {
            Harness.Section("content: the library builds and wires up");

            Database = ContentDatabase.LoadOrBuild();
            Harness.Check(Database != null, "ContentDatabase.LoadOrBuild() returns a database");
            if (Database == null) return;

            Harness.Check(Database.config != null, "game config exists");
            Harness.Check(Database.blocks != null && Database.blocks.blocks.Count > 0, "block registry is populated");
            Harness.Check(Database.items.Count > 0, "items are populated");
            Harness.Check(Database.recipes.Count > 0, "recipes are populated");
            Harness.Check(Database.structures.Count > 0, "structures are populated");
            Harness.Check(Database.zombies.Count > 0, "zombies are populated");
            Harness.Check(Database.hordeSchedule != null, "horde schedule exists");
            Harness.Check(Database.skillTree != null && Database.skillTree.skills.Count > 0, "skill tree is populated");
            Harness.Check(Database.traders.Count > 0, "traders are populated");
            Harness.Check(Database.quests.Count > 0, "quests are populated");
            Harness.Check(Database.vehicles.Count > 0, "vehicles are populated");

            BlockRegistryChecks();
            BlockDropChecks();
            ItemChecks();
            RecipeChecks();
            StructureChecks();
            ProgressionChecks();
            TraderAndQuestChecks();
            StartingKitChecks();
            UniqueIdChecks();
        }

        static void BlockRegistryChecks()
        {
            Harness.Section("content: block registry");
            var registry = Database.blocks;

            Harness.Check(registry.ById(0) != null && registry.ById(0).isAir, "index 0 is air");

            // Every id the code refers to by name has to resolve, or the generator
            // silently produces air.
            string[] named =
            {
                BlockIds.Air, BlockIds.Bedrock, BlockIds.Stone, BlockIds.Dirt, BlockIds.Grass,
                BlockIds.Sand, BlockIds.Gravel, BlockIds.Clay, BlockIds.CoalOre, BlockIds.IronOre,
                BlockIds.PineLog, BlockIds.PineNeedles, BlockIds.ScrapHeap, BlockIds.WoodFrame,
                BlockIds.Planks, BlockIds.Cobblestone, BlockIds.IronBlock, BlockIds.SteelBlock,
                BlockIds.Concrete, BlockIds.Glass
            };
            bool allResolve = true;
            for (int i = 0; i < named.Length; i++)
            {
                if (registry.ByStringId(named[i]) == null) { allResolve = false; Harness.Check(false, "block id resolves: " + named[i]); }
            }
            if (allResolve) Harness.Check(true, string.Format("all {0} named block ids resolve", named.Length));

            bool roundTrips = true;
            for (int i = 0; i < registry.blocks.Count; i++)
            {
                var def = registry.blocks[i];
                if (def == null || def.RuntimeId != i || registry.ById((ushort)i) != def) roundTrips = false;
            }
            Harness.Check(roundTrips, "runtime ids round-trip through the registry");

            // Air must be the only block the mesher treats as empty.
            int airCount = 0;
            for (int i = 0; i < registry.blocks.Count; i++) if (registry.blocks[i].isAir) airCount++;
            Harness.Equal(airCount, 1, "exactly one block is flagged as air");
        }

        static void BlockDropChecks()
        {
            Harness.Section("content: every mineable block yields something");
            var registry = Database.blocks;

            var noDrop = new List<string>();
            for (int i = 0; i < registry.blocks.Count; i++)
            {
                var def = registry.blocks[i];
                if (def.isAir) continue;
                if (def.hardness < 0f) continue;          // indestructible, e.g. bedrock
                if (def.dropItem == null) noDrop.Add(def.stringId);
            }
            Harness.Check(noDrop.Count == 0, "no mineable block drops nothing" + (noDrop.Count > 0 ? ": " + string.Join(", ", noDrop) : ""));

            var badRange = new List<string>();
            for (int i = 0; i < registry.blocks.Count; i++)
            {
                var def = registry.blocks[i];
                if (def.dropItem == null) continue;
                if (def.dropMin < 0 || def.dropMax < def.dropMin) badRange.Add(def.stringId);
            }
            Harness.Check(badRange.Count == 0, "drop ranges are sane" + (badRange.Count > 0 ? ": " + string.Join(", ", badRange) : ""));

            // Crafted building blocks must not pay harvest XP, or walls become XP farms.
            var farmable = new List<string>();
            for (int i = 0; i < registry.blocks.Count; i++)
            {
                var def = registry.blocks[i];
                if (def.buildTier > 0 && def.harvestXp > 0f) farmable.Add(def.stringId);
            }
            Harness.Check(farmable.Count == 0, "crafted build blocks award no harvest XP" + (farmable.Count > 0 ? ": " + string.Join(", ", farmable) : ""));
        }

        static void ItemChecks()
        {
            Harness.Section("content: items");

            var unresolved = new List<string>();
            for (int i = 0; i < Database.items.Count; i++)
            {
                var item = Database.items[i];
                if (Database.Item(item.stringId) != item) unresolved.Add(item.stringId);
            }
            Harness.Check(unresolved.Count == 0, "every item resolves by its string id");

            var badStack = new List<string>();
            for (int i = 0; i < Database.items.Count; i++)
            {
                var item = Database.items[i];
                if (item.maxStack < 1) badStack.Add(item.stringId);
                // A tool that stacks would share one durability value across the stack.
                if (item.HasDurability && item.maxStack != 1) badStack.Add(item.stringId + " (durable but stacks)");
            }
            Harness.Check(badStack.Count == 0, "stack sizes are valid and tools do not stack" + (badStack.Count > 0 ? ": " + string.Join(", ", badStack) : ""));

            // Placement payloads have to point at real content.
            int placeable = 0;
            var brokenPlacement = new List<string>();
            for (int i = 0; i < Database.items.Count; i++)
            {
                var item = Database.items[i];
                if (!item.IsPlaceable) continue;
                placeable++;
                if (item.placeableBlock != null && Database.blocks.ByStringId(item.placeableBlock.stringId) == null)
                    brokenPlacement.Add(item.stringId);
                if (item.placeableStructure != null && Database.Structure(item.placeableStructure.stringId) == null)
                    brokenPlacement.Add(item.stringId);
            }
            Harness.Check(placeable > 0, string.Format("{0} placeable items", placeable));
            Harness.Check(brokenPlacement.Count == 0, "every placeable item points at real content");

            // Every tool tier a block demands must be reachable with some tool.
            int maxToolTier = 0;
            for (int i = 0; i < Database.items.Count; i++) maxToolTier = Mathf.Max(maxToolTier, Database.items[i].toolTier);

            var unreachable = new List<string>();
            for (int i = 0; i < Database.blocks.blocks.Count; i++)
            {
                var def = Database.blocks.blocks[i];
                if (def.isAir || def.hardness < 0f) continue;
                if (def.requiredToolTier > maxToolTier) unreachable.Add(def.stringId);
            }
            Harness.Check(unreachable.Count == 0,
                string.Format("no block needs a tool tier above the best craftable ({0})", maxToolTier)
                + (unreachable.Count > 0 ? ": " + string.Join(", ", unreachable) : ""));
        }

        static void RecipeChecks()
        {
            Harness.Section("content: recipes");

            var broken = new List<string>();
            for (int i = 0; i < Database.recipes.Count; i++)
            {
                var recipe = Database.recipes[i];
                if (recipe.output == null) { broken.Add(recipe.stringId + " (no output)"); continue; }
                if (recipe.outputCount < 1) broken.Add(recipe.stringId + " (output count < 1)");
                if (recipe.ingredients.Count == 0) broken.Add(recipe.stringId + " (no ingredients)");

                for (int j = 0; j < recipe.ingredients.Count; j++)
                {
                    var ing = recipe.ingredients[j];
                    if (ing.item == null) broken.Add(recipe.stringId + " (null ingredient)");
                    else if (ing.count < 1) broken.Add(recipe.stringId + " (ingredient count < 1)");
                    else if (Database.Item(ing.item.stringId) == null) broken.Add(recipe.stringId + " (unknown ingredient)");
                }

                // A recipe that outputs more of its own input is an infinite loop.
                for (int j = 0; j < recipe.ingredients.Count; j++)
                {
                    if (recipe.ingredients[j].item == recipe.output && recipe.outputCount >= recipe.ingredients[j].count)
                        broken.Add(recipe.stringId + " (duplicates its own input)");
                }
            }
            Harness.Check(broken.Count == 0, "every recipe is well formed" + (broken.Count > 0 ? ": " + string.Join("; ", broken) : ""));

            // The starting kit plus hand recipes must reach a workbench without one.
            var handRecipes = new List<string>();
            for (int i = 0; i < Database.recipes.Count; i++)
            {
                var r = Database.recipes[i];
                if (r.station == CraftStation.Hand && r.unlockedByDefault) handRecipes.Add(r.stringId);
            }
            Harness.Check(handRecipes.Count > 0, string.Format("{0} recipes craftable by hand from the start", handRecipes.Count));

            var bench = Database.Item(ItemIds.PieceWorkbench);
            bool benchByHand = false;
            for (int i = 0; i < Database.recipes.Count; i++)
            {
                var r = Database.recipes[i];
                if (r.output == bench && r.station == CraftStation.Hand && r.unlockedByDefault) benchByHand = true;
            }
            Harness.Check(benchByHand, "the workbench itself is craftable by hand (no bootstrap deadlock)");

            var campfire = Database.Item(ItemIds.PieceCampfire);
            bool campfireByHand = false;
            for (int i = 0; i < Database.recipes.Count; i++)
            {
                var r = Database.recipes[i];
                if (r.output == campfire && r.station == CraftStation.Hand && r.unlockedByDefault) campfireByHand = true;
            }
            Harness.Check(campfireByHand, "the campfire is craftable by hand (smelting is reachable)");
        }

        static void StructureChecks()
        {
            Harness.Section("content: structures");

            var broken = new List<string>();
            for (int i = 0; i < Database.structures.Count; i++)
            {
                var def = Database.structures[i];
                if (Database.Structure(def.stringId) != def) broken.Add(def.stringId + " (does not resolve)");
                if (def.footprint.x < 1 || def.footprint.y < 1 || def.footprint.z < 1) broken.Add(def.stringId + " (bad footprint)");
                if (def.maxHealth <= 0f) broken.Add(def.stringId + " (no health)");
                if (def.kind == Building.StructureKind.Storage && def.storageSlots < 1) broken.Add(def.stringId + " (storage with no slots)");
            }
            Harness.Check(broken.Count == 0, "every structure is well formed" + (broken.Count > 0 ? ": " + string.Join("; ", broken) : ""));

            // Rotation currently assumes a square footprint; flag anything that breaks it.
            var nonSquare = new List<string>();
            for (int i = 0; i < Database.structures.Count; i++)
            {
                var def = Database.structures[i];
                if (def.footprint.x != def.footprint.z) nonSquare.Add(def.stringId);
            }
            Harness.Check(nonSquare.Count == 0,
                "every structure footprint is square, as rotation assumes" + (nonSquare.Count > 0 ? ": " + string.Join(", ", nonSquare) : ""));

            // The death backpack is spawned by name from code.
            Harness.Check(Database.Structure(StructureIds.DeathBackpack) != null, "the death backpack structure exists");
            var claim = Database.Structure(StructureIds.ClaimStake);
            Harness.Check(claim != null && claim.claimRadius > 0f, "the claim stake defines a radius");
        }

        static void ProgressionChecks()
        {
            Harness.Section("content: skills and progression");

            var tree = Database.skillTree;
            var categories = new HashSet<Skills.SkillCategory>();
            for (int i = 0; i < tree.skills.Count; i++) categories.Add(tree.skills[i].category);
            Harness.Equal(categories.Count, 6, "all six skill categories are represented");

            // A skill that unlocks a recipe id that does not exist is a dead node.
            var danglingUnlocks = new List<string>();
            for (int i = 0; i < tree.skills.Count; i++)
            {
                var skill = tree.skills[i];
                for (int j = 0; j < skill.unlocksRecipeIds.Count; j++)
                {
                    if (Database.Recipe(skill.unlocksRecipeIds[j]) == null)
                        danglingUnlocks.Add(skill.stringId + " -> " + skill.unlocksRecipeIds[j]);
                }
            }
            Harness.Check(danglingUnlocks.Count == 0,
                "every skill unlock points at a real recipe" + (danglingUnlocks.Count > 0 ? ": " + string.Join(", ", danglingUnlocks) : ""));

            // And the reverse: a locked recipe must name a skill that exists, or it can
            // never be unlocked.
            var unreachable = new List<string>();
            for (int i = 0; i < Database.recipes.Count; i++)
            {
                var recipe = Database.recipes[i];
                if (recipe.unlockedByDefault) continue;
                if (string.IsNullOrEmpty(recipe.requiredSkillId)) { unreachable.Add(recipe.stringId + " (locked, no skill)"); continue; }

                var skill = tree.Find(recipe.requiredSkillId);
                if (skill == null) unreachable.Add(recipe.stringId + " -> unknown skill " + recipe.requiredSkillId);
                else if (recipe.requiredSkillRank > skill.maxRank)
                    unreachable.Add(recipe.stringId + " needs rank " + recipe.requiredSkillRank + " of max " + skill.maxRank);
            }
            Harness.Check(unreachable.Count == 0,
                "every locked recipe is reachable through the skill tree" + (unreachable.Count > 0 ? ": " + string.Join("; ", unreachable) : ""));

            var badSkills = new List<string>();
            for (int i = 0; i < tree.skills.Count; i++)
            {
                var skill = tree.skills[i];
                if (skill.maxRank < 1) badSkills.Add(skill.stringId + " (max rank < 1)");
                if (skill.pointCostPerRank < 1) badSkills.Add(skill.stringId + " (free ranks)");
                if (!string.IsNullOrEmpty(skill.requiresSkillId) && tree.Find(skill.requiresSkillId) == null)
                    badSkills.Add(skill.stringId + " -> unknown prerequisite");
            }
            Harness.Check(badSkills.Count == 0, "skill definitions are well formed" + (badSkills.Count > 0 ? ": " + string.Join("; ", badSkills) : ""));
        }

        static void TraderAndQuestChecks()
        {
            Harness.Section("content: traders, quests, vehicles, horde");

            var broken = new List<string>();
            for (int i = 0; i < Database.traders.Count; i++)
            {
                var trader = Database.traders[i];
                if (trader.currencyItem == null) broken.Add(trader.stringId + " (no currency)");
                if (trader.stock.Count == 0) broken.Add(trader.stringId + " (empty stock)");
                for (int j = 0; j < trader.stock.Count; j++)
                {
                    var entry = trader.stock[j];
                    if (entry.item == null) broken.Add(trader.stringId + " (null stock entry)");
                    else if (Database.Item(entry.item.stringId) == null) broken.Add(trader.stringId + " (unknown stock item)");
                    if (entry.stockCount < 1) broken.Add(trader.stringId + " (zero stock count)");
                    if (entry.priceMultiplier <= 0f) broken.Add(trader.stringId + " (free goods)");
                }
                for (int j = 0; j < trader.questBoard.Count; j++)
                {
                    if (trader.questBoard[j] == null) broken.Add(trader.stringId + " (null quest)");
                }
            }
            Harness.Check(broken.Count == 0, "trader stock is well formed" + (broken.Count > 0 ? ": " + string.Join("; ", broken) : ""));

            // Two traders must not land on the same outpost slot.
            var slots = new HashSet<string>();
            bool distinct = true;
            for (int i = 0; i < Database.traders.Count; i++)
            {
                var t = Database.traders[i];
                if (!slots.Add(t.outpostGridSpacing + ":" + t.outpostVariant)) distinct = false;
            }
            Harness.Check(distinct, "traders occupy distinct outpost slots");

            var questProblems = new List<string>();
            for (int i = 0; i < Database.quests.Count; i++)
            {
                var quest = Database.quests[i];
                if (quest.objectiveCount < 1) questProblems.Add(quest.stringId + " (no objective count)");
                if (quest.kind == Quests.QuestKind.Fetch && quest.objectiveItem == null)
                    questProblems.Add(quest.stringId + " (fetch with no item)");
                if (quest.kind == Quests.QuestKind.Clear && !string.IsNullOrEmpty(quest.objectiveZombieId)
                    && Database.Zombie(quest.objectiveZombieId) == null)
                    questProblems.Add(quest.stringId + " (unknown target zombie)");
                if (quest.rewards.Count == 0 && quest.xpReward <= 0f) questProblems.Add(quest.stringId + " (no reward)");
                for (int j = 0; j < quest.rewards.Count; j++)
                {
                    if (quest.rewards[j].item == null || quest.rewards[j].count < 1)
                        questProblems.Add(quest.stringId + " (bad reward)");
                }
            }
            Harness.Check(questProblems.Count == 0, "quests are well formed" + (questProblems.Count > 0 ? ": " + string.Join("; ", questProblems) : ""));

            for (int i = 0; i < Database.vehicles.Count; i++)
            {
                var v = Database.vehicles[i];
                Harness.Check(v.fuelItem != null && Database.Item(v.fuelItem.stringId) != null, v.displayName + " has a real fuel item");
                Harness.Check(v.fuelCapacity > 0f && v.fuelPerSecond > 0f, v.displayName + " has a sane fuel economy");
            }

            var horde = Database.hordeSchedule;
            Harness.Check(horde.baseZombie != null, "horde schedule has a base zombie");
            Harness.Check(horde.everyNDays >= 1, "blood moon interval is at least one day");
            Harness.Check(horde.heavyZombie == null || horde.heavyFromHordeNumber >= 1, "heavy variant is introduced on a real horde number");

            // Wave size must grow with level and base size, and stay under the cap.
            int early = horde.WaveSize(1, 0);
            int mid = horde.WaveSize(10, 20);
            int late = horde.WaveSize(60, 400);
            Harness.Check(early >= 1, string.Format("level 1 wave is playable: {0}", early));
            Harness.Check(mid > early, string.Format("waves grow with level and base size: {0} -> {1}", early, mid));
            Harness.Check(late <= horde.maxAlive, string.Format("wave size is capped at maxAlive: {0} <= {1}", late, horde.maxAlive));
        }

        static void StartingKitChecks()
        {
            Harness.Section("content: new-world loadout");

            Harness.Check(Database.startingItems.Count > 0, "a starting kit is defined");
            bool allResolve = true;
            for (int i = 0; i < Database.startingItems.Count; i++)
            {
                var entry = Database.startingItems[i];
                if (entry.item == null || Database.Item(entry.item.stringId) == null || entry.count < 1) allResolve = false;
            }
            Harness.Check(allResolve, "every starting item resolves with a positive count");

            // You must be able to gather wood on day one, which means an axe or bare hands
            // on a log that does not demand a tool tier.
            var log = Database.blocks.ByStringId(BlockIds.PineLog);
            Harness.Check(log != null && log.requiredToolTier == 0, "pine logs are harvestable with no tool");
            var stone = Database.blocks.ByStringId(BlockIds.Stone);
            Harness.Check(stone != null && stone.requiredToolTier == 0, "stone is harvestable with no tool");
        }

        static void UniqueIdChecks()
        {
            Harness.Section("content: ids are unique");

            Harness.Check(NoDuplicates(Ids(Database.items), "item"), "item string ids are unique");
            Harness.Check(NoDuplicates(BlockIdList(), "block"), "block string ids are unique");
            Harness.Check(NoDuplicates(RecipeIdList(), "recipe"), "recipe string ids are unique");
            Harness.Check(NoDuplicates(StructureIdList(), "structure"), "structure string ids are unique");
            Harness.Check(Debug.Errors.Count == 0, "the content build logged no errors" +
                (Debug.Errors.Count > 0 ? ": " + string.Join("; ", Debug.Errors) : ""));
        }

        static List<string> Ids(List<ItemDefinition> items)
        {
            var list = new List<string>();
            for (int i = 0; i < items.Count; i++) list.Add(items[i].stringId);
            return list;
        }

        static List<string> BlockIdList()
        {
            var list = new List<string>();
            for (int i = 0; i < Database.blocks.blocks.Count; i++) list.Add(Database.blocks.blocks[i].stringId);
            return list;
        }

        static List<string> RecipeIdList()
        {
            var list = new List<string>();
            for (int i = 0; i < Database.recipes.Count; i++) list.Add(Database.recipes[i].stringId);
            return list;
        }

        static List<string> StructureIdList()
        {
            var list = new List<string>();
            for (int i = 0; i < Database.structures.Count; i++) list.Add(Database.structures[i].stringId);
            return list;
        }

        static bool NoDuplicates(List<string> ids, string label)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                if (!seen.Add(ids[i])) { Debug.LogError("duplicate " + label + " id: " + ids[i]); return false; }
            }
            return true;
        }
    }
}
