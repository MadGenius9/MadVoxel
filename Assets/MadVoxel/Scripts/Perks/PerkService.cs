using System;
using System.Collections.Generic;
using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.Perks
{
    /// <summary>Why a rank can or cannot be bought right now.</summary>
    public enum PerkPurchase
    {
        Ok,
        UnknownPerk,
        MaxRank,
        NotEnoughPoints,
        LevelTooLow,
        MissingPrerequisite
    }

    /// <summary>
    /// The buy rules, kept out of the screen so they can be tested without a canvas and
    /// reused by anything else that grants ranks (quest rewards, a respec, a mod).
    /// </summary>
    public static class PerkService
    {
        /// <summary>Points the next rank of this perk costs.</summary>
        public static int CostOfNextRank(PerkDefinition perk)
        {
            return perk == null ? 0 : Mathf.Max(1, perk.pointCostPerRank);
        }

        /// <summary>
        /// Pure check. <paramref name="rankOf"/> answers for prerequisite perks, so the
        /// caller decides whether that is live progression or a hypothetical build.
        /// </summary>
        public static PerkPurchase Evaluate(PerkDefinition perk, int currentRank, int playerLevel,
                                            int availablePoints, Func<string, int> rankOf)
        {
            if (perk == null) return PerkPurchase.UnknownPerk;
            if (currentRank >= perk.maxRank) return PerkPurchase.MaxRank;
            if (playerLevel < perk.requiredPlayerLevel) return PerkPurchase.LevelTooLow;

            if (!string.IsNullOrEmpty(perk.requiresPerkId))
            {
                int have = rankOf != null ? rankOf(perk.requiresPerkId) : 0;
                if (have < perk.requiresPerkRank) return PerkPurchase.MissingPrerequisite;
            }

            if (availablePoints < CostOfNextRank(perk)) return PerkPurchase.NotEnoughPoints;
            return PerkPurchase.Ok;
        }

        /// <summary>A short line for the button tooltip. Never null.</summary>
        public static string Explain(PerkPurchase result, PerkDefinition perk, PerkTreeDefinition tree)
        {
            switch (result)
            {
                case PerkPurchase.Ok:
                    return "";
                case PerkPurchase.MaxRank:
                    return "Maxed";
                case PerkPurchase.LevelTooLow:
                    return perk == null ? "Level too low"
                        : string.Format("Needs level {0}", perk.requiredPlayerLevel);
                case PerkPurchase.NotEnoughPoints:
                    return perk == null ? "No points"
                        : string.Format("Needs {0} point(s)", CostOfNextRank(perk));
                case PerkPurchase.MissingPrerequisite:
                {
                    if (perk == null) return "Locked";
                    var required = tree != null ? tree.Find(perk.requiresPerkId) : null;
                    string name = required != null ? required.displayName : perk.requiresPerkId;
                    return string.Format("Needs {0} rank {1}", name, perk.requiresPerkRank);
                }
                default:
                    return "Unavailable";
            }
        }

        /// <summary>
        /// Recipe ids this perk has handed over by the given rank. The list is indexed
        /// from zero, so entry 0 arrives with rank 1.
        /// </summary>
        public static void CollectUnlocks(PerkDefinition perk, int rank, List<string> into)
        {
            if (perk == null || into == null) return;
            int count = Mathf.Min(rank, perk.unlocksRecipeIds.Count);
            for (int i = 0; i < count; i++)
            {
                string id = perk.unlocksRecipeIds[i];
                if (!string.IsNullOrEmpty(id)) into.Add(id);
            }
        }

        /// <summary>
        /// Spends the points and applies the rank's recipe unlocks. Returns the reason on
        /// failure and leaves progression untouched.
        /// </summary>
        public static PerkPurchase TryBuyRank(PerkTreeDefinition tree, PlayerProgression progression, string perkId)
        {
            if (progression == null) return PerkPurchase.UnknownPerk;

            var perk = tree != null ? tree.Find(perkId) : null;
            if (perk == null) return PerkPurchase.UnknownPerk;

            int rank = progression.GetRank(perk.stringId);
            var verdict = Evaluate(perk, rank, progression.Level, progression.UnspentPerkPoints, progression.GetRank);
            if (verdict != PerkPurchase.Ok) return verdict;

            int newRank = rank + 1;
            progression.SpendPoints(perk.stringId, newRank, CostOfNextRank(perk));

            // Unlocks are re-applied from rank 1 each time: idempotent, and it repairs a
            // save that was written before a mod added an earlier entry to the list.
            if (newRank <= perk.unlocksRecipeIds.Count)
            {
                string id = perk.unlocksRecipeIds[newRank - 1];
                if (!string.IsNullOrEmpty(id))
                {
                    progression.UnlockRecipe(id);
                    Notifications.PostFormat("{0} rank {1}  -  new recipe unlocked", perk.displayName, newRank);
                    return PerkPurchase.Ok;
                }
            }

            Notifications.PostFormat("{0} rank {1}", perk.displayName, newRank);
            return PerkPurchase.Ok;
        }

        /// <summary>
        /// Re-applies every recipe a loaded save's ranks should already have granted.
        /// Without this, a save made before a perk gained an unlock silently loses it.
        /// </summary>
        public static void ReapplyUnlocks(PerkTreeDefinition tree, PlayerProgression progression)
        {
            if (tree == null || progression == null) return;

            var ids = new List<string>();
            for (int i = 0; i < tree.perks.Count; i++)
            {
                var perk = tree.perks[i];
                if (perk == null) continue;

                ids.Clear();
                CollectUnlocks(perk, progression.GetRank(perk.stringId), ids);
                for (int j = 0; j < ids.Count; j++) progression.UnlockRecipe(ids[j]);
            }
        }

        // ------------------------------------------------------------- description

        /// <summary>Human wording for an effect type, used by the skills screen.</summary>
        public static string EffectLabel(PerkEffectType type)
        {
            switch (type)
            {
                case PerkEffectType.MiningSpeedMultiplier: return "Mining speed";
                case PerkEffectType.HarvestYieldMultiplier: return "Harvest yield";
                case PerkEffectType.BlockTierUnlock: return "Building tier";
                case PerkEffectType.MaxStaminaBonus: return "Max stamina";
                case PerkEffectType.StaminaDrainMultiplier: return "Stamina drain";
                case PerkEffectType.MeleeDamageMultiplier: return "Melee damage";
                case PerkEffectType.RangedDamageMultiplier: return "Ranged damage";
                case PerkEffectType.LootQuantityMultiplier: return "Loot found";
                case PerkEffectType.HealingMultiplier: return "Healing";
                case PerkEffectType.VehicleFuelEfficiency: return "Fuel economy";
                case PerkEffectType.RepairSpeedMultiplier: return "Repair speed";
                case PerkEffectType.FieldYieldMultiplier: return "Field yield";
                default: return type.ToString();
            }
        }

        /// <summary>Flat effects read as numbers; everything else reads as a percentage.</summary>
        public static bool IsFlatEffect(PerkEffectType type)
        {
            return type == PerkEffectType.BlockTierUnlock || type == PerkEffectType.MaxStaminaBonus;
        }

        static string Signed(float value, bool flat)
        {
            if (flat) return (value >= 0f ? "+" : "") + Mathf.RoundToInt(value).ToString();
            return (value >= 0f ? "+" : "") + Mathf.RoundToInt(value * 100f) + "%";
        }

        /// <summary>
        /// "Mining speed +12%/rank (now +24%)" for every effect the perk carries.
        /// Returns an empty string for a perk that only unlocks recipes.
        /// </summary>
        public static string DescribeEffects(PerkDefinition perk, int rank)
        {
            if (perk == null || perk.effects.Count == 0) return "";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < perk.effects.Count; i++)
            {
                var effect = perk.effects[i];
                if (i > 0) sb.Append("   ");

                bool flat = IsFlatEffect(effect.type);
                sb.Append(EffectLabel(effect.type)).Append(' ')
                  .Append(Signed(effect.valuePerRank, flat)).Append("/rank");

                if (rank > 0)
                {
                    sb.Append(" (now ").Append(Signed(effect.valuePerRank * rank, flat)).Append(')');
                }
            }
            return sb.ToString();
        }

    }
}
