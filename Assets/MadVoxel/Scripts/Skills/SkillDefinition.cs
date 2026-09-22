using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Skills
{
    public enum SkillCategory
    {
        Mining,
        Construction,
        Combat,
        Scavenging,
        Medicine,
        Vehicles
    }

    public enum SkillEffectType
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
    public struct SkillEffect
    {
        public SkillEffectType type;
        [Tooltip("Value applied at rank 1; multiplied by rank for higher ranks.")]
        public float valuePerRank;
    }

    /// <summary>
    /// One node in the 7DTD-style tree. Ranks cost points, unlock recipes and apply
    /// numeric effects. Phase 0 stores ranks; Phase 1 reads the effects.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Skill", fileName = "Skill")]
    public class SkillDefinition : ScriptableObject
    {
        public string stringId = "madvoxel:skill";
        public string displayName = "Skill";
        [TextArea] public string description;
        public SkillCategory category = SkillCategory.Mining;

        [Range(1, 10)] public int maxRank = 5;
        public int pointCostPerRank = 1;
        [Tooltip("Player level needed before the first rank can be bought.")]
        public int requiredPlayerLevel = 1;
        public string requiresSkillId = "";
        public int requiresSkillRank = 1;

        public List<SkillEffect> effects = new List<SkillEffect>();
        [Tooltip("Recipe string ids unlocked when this skill reaches the matching rank index (0 = rank 1).")]
        public List<string> unlocksRecipeIds = new List<string>();

        public float EffectValue(SkillEffectType type, int rank)
        {
            if (rank <= 0) return 0f;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].type == type) return effects[i].valuePerRank * rank;
            }
            return 0f;
        }
    }

    [CreateAssetMenu(menuName = "MadVoxel/Skill Tree", fileName = "SkillTree")]
    public class SkillTreeDefinition : ScriptableObject
    {
        public List<SkillDefinition> skills = new List<SkillDefinition>();

        public SkillDefinition Find(string stringId)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] != null && skills[i].stringId == stringId) return skills[i];
            }
            return null;
        }

        public IEnumerable<SkillDefinition> InCategory(SkillCategory category)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] != null && skills[i].category == category) yield return skills[i];
            }
        }
    }
}
