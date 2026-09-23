using MadVoxel.Inventory;
using System.Collections.Generic;
using MadVoxel.World.Biomes;
using UnityEngine;

namespace MadVoxel.Farming.Crops
{
    /// <summary>How far along a planted crop is. Empty means the plot is bare soil.</summary>
    public enum CropStage
    {
        Empty,
        Seedling,
        Growing,
        Mature,
        Ready
    }

    /// <summary>
    /// One crop, shared by both farming layers. The garden grows it on a farm plot for
    /// stacks you cook and eat; the field grows it across cells for litres you sell.
    /// Flags decide which layers a crop belongs to, so a crop can be garden-only,
    /// field-only, or - like corn and wheat - both, which is what lets the player carry
    /// their understanding from one scale to the other.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Crop", fileName = "Crop")]
    public class CropDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string stringId = "madvoxel:crop";
        public string displayName = "Crop";

        [Header("Layers")]
        [Tooltip("Can be planted on a garden farm plot.")]
        public bool growsOnPlot = true;
        [Tooltip("Can be sown across a field cell grid (Phase 1).")]
        public bool growsOnField = true;

        [Header("Items")]
        public ItemDefinition seedItem;
        public ItemDefinition harvestItem;
        public int harvestMin = 1;
        public int harvestMax = 3;

        [Header("Growth")]
        [Tooltip("In-game days from planting to harvestable.")]
        public float daysToMature = 2f;
        [Tooltip("True: harvesting leaves the crop growing again. False: the plot goes back to bare soil and needs a new seed.")]
        public bool replants;
        [Range(0f, 1f)]
        [Tooltip("Chance of getting seeds back when the crop does not replant itself.")]
        public float seedReturnChance = 0.6f;
        public int seedReturnMin = 1;
        public int seedReturnMax = 2;

        [Header("Field layer")]
        [Tooltip("Litres yielded per harvested field cell.")]
        public float litresPerCell = 12f;
        [Tooltip("Litres of bulk produce that make one harvest item. The rate a grain bin draws at, and the rate a trader prices litres by.")]
        public float litresPerHarvestItem = 4f;

        /// <summary>Harvest items a quantity of bulk produce is worth, rounded down.</summary>
        public int ItemsFromLitres(float litres)
        {
            if (harvestItem == null || litres <= 0f) return 0;
            return Mathf.FloorToInt(litres / Mathf.Max(0.01f, litresPerHarvestItem));
        }

        /// <summary>The litres those items cost, so drawing never loses a fraction.</summary>
        public float LitresForItems(int items)
        {
            return Mathf.Max(0, items) * Mathf.Max(0.01f, litresPerHarvestItem);
        }

        [Header("Look")]
        public Color plantTint = new Color(0.36f, 0.52f, 0.22f);
        [Tooltip("Height of the plant at full growth, in metres.")]
        public float matureHeight = 0.85f;

        [Header("Reward")]
        public float xpPerHarvest = 8f;

        [Header("Where it grows")]
        [Tooltip("Per-region opinion. A region left out simply uses the biome's own multiplier.")]
        public List<CropBiomeYield> biomeYields = new List<CropBiomeYield>();

        /// <summary>The seed will not go in the ground here at all.</summary>
        public bool IsBlockedIn(BiomeId biome)
        {
            for (int i = 0; i < biomeYields.Count; i++)
            {
                if (biomeYields[i].biome == biome) return biomeYields[i].blocked;
            }
            return false;
        }

        /// <summary>
        /// This crop's own multiplier for a region, or 1 when it has no opinion. The
        /// biome's blanket multiplier is applied on top of this by the caller, so a
        /// crop that is merely unhappy stacks with ground that is merely poor.
        /// </summary>
        public float YieldIn(BiomeId biome)
        {
            for (int i = 0; i < biomeYields.Count; i++)
            {
                if (biomeYields[i].biome == biome)
                {
                    return biomeYields[i].blocked ? 0f : Mathf.Max(0f, biomeYields[i].multiplier);
                }
            }
            return 1f;
        }

        /// <summary>In-game hours from planting to harvestable.</summary>
        public float HoursToMature { get { return Mathf.Max(0.1f, daysToMature) * 24f; } }

        public CropStage StageAt(double hoursGrown)
        {
            float progress = Mathf.Clamp01((float)(hoursGrown / HoursToMature));
            if (progress >= 1f) return CropStage.Ready;
            if (progress >= 0.66f) return CropStage.Mature;
            if (progress >= 0.3f) return CropStage.Growing;
            return CropStage.Seedling;
        }

        public float Progress01(double hoursGrown)
        {
            return Mathf.Clamp01((float)(hoursGrown / HoursToMature));
        }
    }
}
