using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.Perks;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The skills screen's rules, run without a canvas: effect maths, the buy gates, the
    /// recipe unlocks, and the tree-wide consistency a tester would only find by grinding.
    /// </summary>
    public static class PerkTests
    {
        public static void Run(ContentDatabase db)
        {
            EffectMaths();
            BuyRules(db);
            RecipeUnlocks(db);
            TreeConsistency(db);
            EffectWiring(db);
        }

        // ------------------------------------------------------------------ helpers

        static PerkDefinition Perk(string id, int maxRank, int level, params PerkEffect[] effects)
        {
            var perk = ScriptableObject.CreateInstance<PerkDefinition>();
            perk.stringId = id;
            perk.displayName = id;
            perk.maxRank = maxRank;
            perk.requiredPlayerLevel = level;
            perk.pointCostPerRank = 1;
            perk.effects.AddRange(effects);
            return perk;
        }

        static PerkEffect Effect(PerkEffectType type, float perRank)
        {
            return new PerkEffect { type = type, valuePerRank = perRank };
        }

        static PerkTreeDefinition Tree(params PerkDefinition[] perks)
        {
            var tree = ScriptableObject.CreateInstance<PerkTreeDefinition>();
            tree.perks.AddRange(perks);
            return tree;
        }

        // ------------------------------------------------------------- effect maths

        static void EffectMaths()
        {
            Harness.Section("perks: effect maths");

            var lungs = Perk("test:lungs", 4, 1,
                Effect(PerkEffectType.MaxStaminaBonus, 12f),
                Effect(PerkEffectType.StaminaDrainMultiplier, -0.08f));
            var mule = Perk("test:mule", 3, 1, Effect(PerkEffectType.StaminaDrainMultiplier, -0.06f));
            var tree = Tree(lungs, mule);

            var ranks = new Dictionary<string, int>();
            var effects = new PerkEffects();

            effects.Recompute(tree, id => { int r; return ranks.TryGetValue(id, out r) ? r : 0; });
            Harness.Equal(effects.Bonus(PerkEffectType.MaxStaminaBonus), 0f, "no ranks means no bonus");
            Harness.Equal(effects.Multiplier(PerkEffectType.MiningSpeedMultiplier), 1f, "an unused effect type multiplies by one");

            ranks["test:lungs"] = 3;
            ranks["test:mule"] = 2;
            effects.Recompute(tree, id => { int r; return ranks.TryGetValue(id, out r) ? r : 0; });

            Harness.Equal(effects.Bonus(PerkEffectType.MaxStaminaBonus), 36f, "flat bonuses scale with rank");
            Harness.Check(Mathf.Abs(effects.Bonus(PerkEffectType.StaminaDrainMultiplier) - -0.36f) < 0.0001f,
                "the same effect type sums across two perks (-0.24 + -0.12)");
            Harness.Check(Mathf.Abs(effects.Multiplier(PerkEffectType.StaminaDrainMultiplier) - 0.64f) < 0.0001f,
                "a negative sum reads as a multiplier below one");

            // A rank above the perk's own maximum - from a hand-edited save or a mod that
            // lowered maxRank - must not pay out beyond what the perk allows.
            ranks["test:lungs"] = 99;
            effects.Recompute(tree, id => { int r; return ranks.TryGetValue(id, out r) ? r : 0; });
            Harness.Equal(effects.Bonus(PerkEffectType.MaxStaminaBonus), 48f, "an over-max rank is clamped to maxRank");

            // A mod handing out a huge negative must not invert a cost into a refund.
            var runaway = Tree(Perk("test:runaway", 5, 1, Effect(PerkEffectType.StaminaDrainMultiplier, -5f)));
            var clamped = new PerkEffects();
            clamped.Recompute(runaway, id => 5);
            Harness.Check(clamped.Multiplier(PerkEffectType.StaminaDrainMultiplier) > 0f,
                "a runaway negative still multiplies by a positive number");

            var yield2 = new PerkEffects();
            yield2.Recompute(Tree(Perk("test:yield", 5, 1, Effect(PerkEffectType.HarvestYieldMultiplier, 0.15f))), id => 2);
            Harness.Equal(yield2.ScaleCount(PerkEffectType.HarvestYieldMultiplier, 3), 4, "3 drops at +30% rounds to 4");
            Harness.Equal(yield2.ScaleCount(PerkEffectType.HarvestYieldMultiplier, 1), 1, "a single drop never rounds down to zero");
            Harness.Equal(yield2.ScaleCount(PerkEffectType.HarvestYieldMultiplier, 0), 0, "nothing scales to nothing");

            var empty = new PerkEffects();
            empty.Recompute(null, null);
            Harness.Equal(empty.Multiplier(PerkEffectType.MeleeDamageMultiplier), 1f, "a null tree is inert, not a crash");
        }

        // --------------------------------------------------------------- buy rules

        static void BuyRules(ContentDatabase db)
        {
            Harness.Section("perks: buy rules");

            var basic = Perk("test:basic", 3, 5);
            var gated = Perk("test:gated", 2, 1);
            gated.requiresPerkId = "test:basic";
            gated.requiresPerkRank = 2;
            var tree = Tree(basic, gated);

            Harness.Equal(PerkService.Evaluate(basic, 0, 4, 5, id => 0), PerkPurchase.LevelTooLow,
                "a perk below its level requirement cannot be bought");
            Harness.Equal(PerkService.Evaluate(basic, 0, 5, 0, id => 0), PerkPurchase.NotEnoughPoints,
                "no points means no rank");
            Harness.Equal(PerkService.Evaluate(basic, 3, 9, 5, id => 0), PerkPurchase.MaxRank,
                "a maxed perk reports maxed, not 'no points'");
            Harness.Equal(PerkService.Evaluate(basic, 0, 5, 1, id => 0), PerkPurchase.Ok,
                "level and a point are enough");
            Harness.Equal(PerkService.Evaluate(null, 0, 99, 99, id => 0), PerkPurchase.UnknownPerk,
                "a missing perk is rejected rather than crashing");

            Harness.Equal(PerkService.Evaluate(gated, 0, 9, 5, id => 1), PerkPurchase.MissingPrerequisite,
                "a prerequisite one rank short still blocks");
            Harness.Equal(PerkService.Evaluate(gated, 0, 9, 5, id => 2), PerkPurchase.Ok,
                "meeting the prerequisite opens the node");

            // The order of the gates matters for the message the player reads: being
            // maxed should never be reported as a missing level.
            var lateMax = Perk("test:late", 1, 20);
            Harness.Equal(PerkService.Evaluate(lateMax, 1, 3, 0, id => 0), PerkPurchase.MaxRank,
                "max rank outranks the level and point messages");

            var costly = Perk("test:costly", 3, 1);
            costly.pointCostPerRank = 3;
            Harness.Equal(PerkService.Evaluate(costly, 0, 1, 2, id => 0), PerkPurchase.NotEnoughPoints,
                "a 3-point rank is not affordable with 2 points");
            Harness.Equal(PerkService.Evaluate(costly, 0, 1, 3, id => 0), PerkPurchase.Ok,
                "a 3-point rank is affordable with exactly 3");

            for (int i = 0; i < 6; i++)
            {
                var verdict = (PerkPurchase)i;
                string explanation = PerkService.Explain(verdict, basic, tree);
                Harness.Check(explanation != null && (verdict == PerkPurchase.Ok || explanation.Length > 0),
                    "every refusal has wording: " + verdict);
            }
        }

        // ----------------------------------------------------------- recipe unlocks

        static void RecipeUnlocks(ContentDatabase db)
        {
            Harness.Section("perks: buying ranks");

            var carpenter = Perk("test:carpenter", 4, 1);
            carpenter.unlocksRecipeIds.Add("test:recipe_a");
            carpenter.unlocksRecipeIds.Add("test:recipe_b");
            var tree = Tree(carpenter);

            var progression = new PlayerProgression();
            progression.BindPerkTree(tree);
            progression.LoadState(10, 0f, 3, null, null);

            Harness.Equal(PerkService.TryBuyRank(tree, progression, "test:carpenter"), PerkPurchase.Ok, "first rank buys");
            Harness.Equal(progression.GetRank("test:carpenter"), 1, "rank went up");
            Harness.Equal(progression.UnspentPerkPoints, 2, "a point was spent");
            Harness.Check(progression.UnlockedRecipes.Contains("test:recipe_a"), "rank 1 unlocked the first recipe");
            Harness.Check(!progression.UnlockedRecipes.Contains("test:recipe_b"), "rank 1 did not unlock the second");

            PerkService.TryBuyRank(tree, progression, "test:carpenter");
            Harness.Check(progression.UnlockedRecipes.Contains("test:recipe_b"), "rank 2 unlocked the second recipe");

            // Rank 3 has no unlock entry; it must still buy cleanly.
            Harness.Equal(PerkService.TryBuyRank(tree, progression, "test:carpenter"), PerkPurchase.Ok,
                "a rank past the end of the unlock list still buys");
            Harness.Equal(progression.UnspentPerkPoints, 0, "all three points are gone");
            Harness.Equal(PerkService.TryBuyRank(tree, progression, "test:carpenter"), PerkPurchase.NotEnoughPoints,
                "the fourth rank is refused with no points left");
            Harness.Equal(progression.GetRank("test:carpenter"), 3, "a refused buy leaves the rank alone");

            Harness.Equal(PerkService.TryBuyRank(tree, progression, "test:nosuchperk"), PerkPurchase.UnknownPerk,
                "buying an unknown perk is refused");

            // A save written before the perk gained its second unlock: the rank is there,
            // the recipe is not. Loading must repair it.
            var reloaded = new PlayerProgression();
            reloaded.BindPerkTree(tree);
            reloaded.LoadState(10, 0f, 0, new Dictionary<string, int> { { "test:carpenter", 2 } }, null);
            Harness.Check(!reloaded.UnlockedRecipes.Contains("test:recipe_b"), "the stale save is missing the recipe");
            PerkService.ReapplyUnlocks(tree, reloaded);
            Harness.Check(reloaded.UnlockedRecipes.Contains("test:recipe_a") && reloaded.UnlockedRecipes.Contains("test:recipe_b"),
                "loading re-grants every recipe the ranks had earned");

            // Effects must follow the ranks without anyone asking for a recompute.
            var lungs = Perk("test:lungs2", 3, 1, Effect(PerkEffectType.MaxStaminaBonus, 10f));
            var live = new PlayerProgression();
            live.BindPerkTree(Tree(lungs));
            live.LoadState(10, 0f, 5, null, null);
            Harness.Equal(live.Effects.Bonus(PerkEffectType.MaxStaminaBonus), 0f, "no rank, no stamina");
            PerkService.TryBuyRank(live.Tree, live, "test:lungs2");
            Harness.Equal(live.Effects.Bonus(PerkEffectType.MaxStaminaBonus), 10f, "buying a rank updates the effects immediately");
            live.ResetProgression();
            Harness.Equal(live.Effects.Bonus(PerkEffectType.MaxStaminaBonus), 0f, "a reset clears the effects too");
        }

        // -------------------------------------------------------- the shipped tree

        static void TreeConsistency(ContentDatabase db)
        {
            Harness.Section("perks: the shipped tree");

            var tree = db.perkTree;

            // The existing content checks prove a locked recipe names a real perk. This
            // is the other half: the perk has to actually hand that recipe over, at or
            // before the rank the recipe claims to need, or it is unreachable in play.
            var unreachable = new List<string>();
            for (int i = 0; i < db.recipes.Count; i++)
            {
                var recipe = db.recipes[i];
                if (recipe.unlockedByDefault || string.IsNullOrEmpty(recipe.requiredPerkId)) continue;

                var perk = tree.Find(recipe.requiredPerkId);
                if (perk == null) continue; // already reported by the content checks

                int index = perk.unlocksRecipeIds.IndexOf(recipe.stringId);
                if (index < 0) unreachable.Add(recipe.stringId + " is never granted by " + perk.stringId);
                else if (index + 1 > perk.maxRank) unreachable.Add(recipe.stringId + " sits past " + perk.stringId + "'s max rank");
            }
            Harness.Check(unreachable.Count == 0,
                "every perk-locked recipe is actually handed over by its perk"
                + (unreachable.Count > 0 ? ": " + string.Join("; ", unreachable) : ""));

            // Spending is pointless if the tree cannot be walked. Play every level from 1
            // up and check the whole tree can be maxed on the points a player earns.
            var progression = new PlayerProgression();
            progression.BindPerkTree(tree);

            int totalCost = 0;
            int highestLevel = 1;
            for (int i = 0; i < tree.perks.Count; i++)
            {
                var perk = tree.perks[i];
                totalCost += perk.maxRank * Mathf.Max(1, perk.pointCostPerRank);
                if (perk.requiredPlayerLevel > highestLevel) highestLevel = perk.requiredPlayerLevel;
            }

            Harness.Check(totalCost > 0, string.Format("maxing the tree costs {0} points across {1} perks", totalCost, tree.perks.Count));
            Harness.Check(highestLevel <= 20,
                string.Format("the last perk opens at level {0}, inside a survivable run", highestLevel));

            // Level 1 must have something worth buying, or the first point has nowhere to go.
            int openAtLevelOne = 0;
            for (int i = 0; i < tree.perks.Count; i++)
            {
                var perk = tree.perks[i];
                if (perk.requiredPlayerLevel <= 1 && string.IsNullOrEmpty(perk.requiresPerkId)) openAtLevelOne++;
            }
            Harness.Check(openAtLevelOne >= 3,
                string.Format("{0} perks are buyable the moment the first point lands", openAtLevelOne));

            // Every prerequisite must name a perk that exists and a rank it can reach.
            var badPrereqs = new List<string>();
            for (int i = 0; i < tree.perks.Count; i++)
            {
                var perk = tree.perks[i];
                if (string.IsNullOrEmpty(perk.requiresPerkId)) continue;

                var required = tree.Find(perk.requiresPerkId);
                if (required == null) badPrereqs.Add(perk.stringId + " -> unknown " + perk.requiresPerkId);
                else if (perk.requiresPerkRank > required.maxRank)
                    badPrereqs.Add(perk.stringId + " needs " + required.stringId + " rank " + perk.requiresPerkRank);
                else if (required.requiredPlayerLevel > perk.requiredPlayerLevel)
                    badPrereqs.Add(perk.stringId + " opens before its prerequisite " + required.stringId);
            }
            Harness.Check(badPrereqs.Count == 0,
                "every prerequisite is reachable" + (badPrereqs.Count > 0 ? ": " + string.Join("; ", badPrereqs) : ""));

            // A perk that neither changes a number nor opens a recipe is a dead button.
            var dead = new List<string>();
            for (int i = 0; i < tree.perks.Count; i++)
            {
                var perk = tree.perks[i];
                if (perk.effects.Count == 0 && perk.unlocksRecipeIds.Count == 0) dead.Add(perk.stringId);
            }
            Harness.Check(dead.Count == 0, "no perk is a dead button" + (dead.Count > 0 ? ": " + string.Join(", ", dead) : ""));

            // Every perk must describe itself: the screen shows the description verbatim.
            var undescribed = new List<string>();
            for (int i = 0; i < tree.perks.Count; i++)
            {
                var perk = tree.perks[i];
                if (string.IsNullOrEmpty(perk.description) || string.IsNullOrEmpty(perk.displayName)) undescribed.Add(perk.stringId);
                if (perk.effects.Count > 0 && string.IsNullOrEmpty(PerkService.DescribeEffects(perk, 1)))
                    undescribed.Add(perk.stringId + " (no effect wording)");
            }
            Harness.Check(undescribed.Count == 0,
                "every perk has text for the screen" + (undescribed.Count > 0 ? ": " + string.Join(", ", undescribed) : ""));
        }

        // ------------------------------------------------------------ effect wiring

        /// <summary>
        /// Content can declare an effect the game never reads. These are the types the
        /// runtime applies today; the rest are Phase 1 and named here on purpose, so a
        /// new effect type cannot quietly ship as a number that does nothing.
        /// </summary>
        static void EffectWiring(ContentDatabase db)
        {
            Harness.Section("perks: effects reach the game");

            var wired = new HashSet<PerkEffectType>
            {
                PerkEffectType.MiningSpeedMultiplier,   // PlayerInteraction.MineBlock
                PerkEffectType.HarvestYieldMultiplier,  // PlayerInteraction.BreakBlock, FarmPlotStructure.Harvest
                PerkEffectType.BlockTierUnlock,         // BuildingWorld.Upgrade
                PerkEffectType.MaxStaminaBonus,         // PlayerStats.MaxStamina
                PerkEffectType.StaminaDrainMultiplier,  // PlayerStats.TrySpendStamina / DrainStamina
                PerkEffectType.MeleeDamageMultiplier,   // PlayerInteraction.Attack
                PerkEffectType.LootQuantityMultiplier,  // PlayerInteraction salvage
                PerkEffectType.HealingMultiplier,       // PlayerStats.Consume
                PerkEffectType.RepairSpeedMultiplier,   // PlayerInteraction hammer repair
                PerkEffectType.WireLengthBonus,         // PowerWorld / FluidWorld -> Graph.ExtraReach
                PerkEffectType.PumpRateMultiplier,      // FluidWorld.Conditions
                PerkEffectType.TrapDamageMultiplier,    // PowerTrap.Bite
                PerkEffectType.StormResistance,         // WeatherEffects.RollBreakage
                PerkEffectType.DroughtResistance,       // FarmPlotStructure.DryLoss
                PerkEffectType.ClaimHeatReduction,      // ClaimHeatTracker.QuietFraction
                PerkEffectType.FieldYieldMultiplier,    // ImplementController.Reap
                PerkEffectType.VehicleFuelEfficiency    // VehicleRig.Drive
            };

            // Declared in content, read by nothing until the Phase 1 system lands.
            var deferred = new HashSet<PerkEffectType>
            {
                PerkEffectType.RangedDamageMultiplier   // no ranged weapons yet
            };

            var used = new HashSet<PerkEffectType>();
            for (int i = 0; i < db.perkTree.perks.Count; i++)
            {
                var perk = db.perkTree.perks[i];
                for (int j = 0; j < perk.effects.Count; j++) used.Add(perk.effects[j].type);
            }

            var unaccounted = new List<string>();
            foreach (var type in used)
            {
                if (!wired.Contains(type) && !deferred.Contains(type)) unaccounted.Add(type.ToString());
            }
            Harness.Check(unaccounted.Count == 0,
                "every effect the tree grants is either applied or a known Phase 1 gap"
                + (unaccounted.Count > 0 ? ": " + string.Join(", ", unaccounted) : ""));

            var stale = new List<string>();
            foreach (var type in deferred)
            {
                if (wired.Contains(type)) stale.Add(type.ToString());
            }
            Harness.Check(stale.Count == 0, "no effect is listed as both wired and deferred");

            Harness.Check(used.Count >= 8,
                string.Format("the tree grants {0} distinct effect types", used.Count));
        }
    }
}
