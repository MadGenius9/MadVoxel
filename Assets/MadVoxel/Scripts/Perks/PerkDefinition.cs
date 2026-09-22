using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Perks
{
    public enum PerkCategory
    {
        Mining,
        Construction,
        Combat,
        Scavenging,
        Medicine,
        Vehicles
    }

    public enum PerkEffectType
    {
        MiningSpeedMultiplier,
        HarvestYieldMultiplier,
        BlockTierUnlock,
        MaxStaminaBonus,
        StaminaDrainMultiplier,
        MeleeDamageMultiplier,
        RangedDamageMultiplier,
        LootQuantityMultiplier,
        HealingMultiplier,
        VehicleFuelEfficiency,
        RepairSpeedMultiplier
    }

    [Serializable]
    public struct PerkEffect
    {
        public PerkEffectType type;
        [Tooltip("Value applied at rank 1; multiplied by rank for higher ranks.")]
        public float valuePerRank;
    }

    /// <summary>
    /// One node in the 7DTD-style tree. Ranks cost points, unlock recipes and apply
    /// numeric effects. Phase 0 stores ranks; Phase 1 reads the effects.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Perk", fileName = "Perk")]
    public class PerkDefinition : ScriptableObject
    {
        public string stringId = "madvoxel:skill";
        public string displayName = "Perk";
        [TextArea] public string description;
        public PerkCategory category = PerkCategory.Mining;

        [Range(1, 10)] public int maxRank = 5;
        public int pointCostPerRank = 1;
        [Tooltip("Player level needed before the first rank can be bought.")]
        public int requiredPlayerLevel = 1;
        public string requiresPerkId = "";
        public int requiresPerkRank = 1;

        public List<PerkEffect> effects = new List<PerkEffect>();
        [Tooltip("Recipe string ids unlocked when this skill reaches the matching rank index (0 = rank 1).")]
        public List<string> unlocksRecipeIds = new List<string>();

        public float EffectValue(PerkEffectType type, int rank)
        {
            if (rank <= 0) return 0f;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].type == type) return effects[i].valuePerRank * rank;
            }
            return 0f;
        }
    }

    [CreateAssetMenu(menuName = "MadVoxel/Perk Tree", fileName = "PerkTree")]
    public class PerkTreeDefinition : ScriptableObject
    {
        public List<PerkDefinition> perks = new List<PerkDefinition>();

        public PerkDefinition Find(string stringId)
        {
            for (int i = 0; i < perks.Count; i++)
            {
                if (perks[i] != null && perks[i].stringId == stringId) return perks[i];
            }
            return null;
        }

        public IEnumerable<PerkDefinition> InCategory(PerkCategory category)
        {
            for (int i = 0; i < perks.Count; i++)
            {
                if (perks[i] != null && perks[i].category == category) yield return perks[i];
            }
        }
    }
}
