using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Inventory
{
    public enum CraftStation
    {
        Hand,
        Campfire,
        Workbench,
        Forge
    }

    [Serializable]
    public struct RecipeIngredient
    {
        public ItemDefinition item;
        public int count;
    }

    [CreateAssetMenu(menuName = "MadVoxel/Recipe", fileName = "Recipe")]
    public class RecipeDefinition : ScriptableObject
    {
        public string stringId = "madvoxel:recipe";
        public ItemDefinition output;
        public int outputCount = 1;
        public List<RecipeIngredient> ingredients = new List<RecipeIngredient>();

        [Tooltip("Where the recipe can be crafted. Hand recipes work anywhere.")]
        public CraftStation station = CraftStation.Hand;
        public float craftSeconds = 1.5f;

        [Header("Progression")]
        [Tooltip("Available without spending any skill points.")]
        public bool unlockedByDefault = true;
        [Tooltip("Phase 1 hook: skill string id that unlocks this recipe.")]
        public string requiredSkillId = "";
        [Tooltip("Phase 1 hook: rank of that skill needed.")]
        public int requiredSkillRank = 1;

        public string DisplayName
        {
            get { return output != null ? output.displayName : stringId; }
        }
    }
}
