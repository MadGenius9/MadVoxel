using System.Collections.Generic;

namespace MadVoxel.Inventory
{
    /// <summary>
    /// Pure recipe logic: what can be made from a bag at a given station. No Unity
    /// objects, so it is equally usable by UI, by quests and by tests.
    /// </summary>
    public static class CraftingService
    {
        public static bool StationSatisfies(CraftStation available, CraftStation required)
        {
            if (required == CraftStation.Hand) return true;
            return available == required;
        }

        public static bool HasIngredients(Inventory inventory, RecipeDefinition recipe)
        {
            if (recipe == null || recipe.output == null) return false;
            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                var ing = recipe.ingredients[i];
                if (ing.item == null || ing.count <= 0) continue;
                if (inventory.CountOf(ing.item) < ing.count) return false;
            }
            return true;
        }

        public static bool CanCraft(Inventory inventory, RecipeDefinition recipe, CraftStation station, ISet<string> unlockedRecipes)
        {
            if (recipe == null || recipe.output == null) return false;
            if (!StationSatisfies(station, recipe.station)) return false;
            if (!IsUnlocked(recipe, unlockedRecipes)) return false;
            if (!HasIngredients(inventory, recipe)) return false;
            return inventory.CanFit(recipe.output, recipe.outputCount);
        }

        public static bool IsUnlocked(RecipeDefinition recipe, ISet<string> unlockedRecipes)
        {
            if (recipe.unlockedByDefault) return true;
            return unlockedRecipes != null && unlockedRecipes.Contains(recipe.stringId);
        }

        /// <summary>Consumes ingredients and deposits the output. Returns false if nothing changed.</summary>
        public static bool Craft(Inventory inventory, RecipeDefinition recipe, CraftStation station, ISet<string> unlockedRecipes)
        {
            if (!CanCraft(inventory, recipe, station, unlockedRecipes)) return false;

            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                var ing = recipe.ingredients[i];
                if (ing.item == null || ing.count <= 0) continue;
                inventory.Remove(ing.item, ing.count);
            }

            int leftover = inventory.Add(recipe.output, recipe.outputCount);
            if (leftover > 0)
            {
                // Should not happen because CanCraft checked space, but never eat the output.
                UnityEngine.Debug.LogWarningFormat("Craft of {0} overflowed by {1}.", recipe.DisplayName, leftover);
            }
            return true;
        }
    }
}
