using System;
using System.Collections.Generic;
using MadVoxel.Core;
using MadVoxel.World.Terrain;
using MadVoxel.World.Weather;
using UnityEngine;

namespace MadVoxel.World.Biomes
{
    /// <summary>
    /// Everything a region changes about the systems that already exist. There is no
    /// biome simulation: this is a sheet of multipliers that terrain, farming, water,
    /// power, weather and claim heat each read when they want to know where they are.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Biome", fileName = "Biome")]
    public class BiomeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string stringId = BiomeIds.Farmland;
        public BiomeId id = BiomeId.Farmland;
        public string displayName = "Farmland";
        [TextArea] public string description;
        [Tooltip("Muted, for a map later. Never candy.")]
        public Color mapTint = new Color(0.42f, 0.48f, 0.28f);

        [Header("Ground")]
        [Tooltip("What the top block is when it is not sand or a POI pad.")]
        public string surfaceBlockId = BlockIds.Grass;
        [Tooltip("What sits just under the surface.")]
        public string subSurfaceBlockId = BlockIds.Dirt;
        [Tooltip("Chance a sub-surface block is clay instead.")]
        [Range(0f, 1f)] public float clayChance = 0.035f;
        [Tooltip("Chance the surface shows bare worked dirt rather than growth.")]
        [Range(0f, 1f)] public float bareDirtChance = 0.15f;
        [Tooltip("Soil depth before stone, in blocks.")]
        public int soilDepth = 4;

        [Header("What is in the ground")]
        public float oreDensityMultiplier = 1f;
        [Tooltip("Pushes ore nearer the surface. 0 leaves the default depth bands alone.")]
        public int oreDepthBonus;
        public float treeDensityMultiplier = 1f;
        public float forageDensityMultiplier = 1f;
        public float scrapDensityMultiplier = 1f;

        [Header("Farming")]
        [Tooltip("Applies to every crop here before the crop's own override.")]
        public float cropYieldMultiplier = 1f;
        [Tooltip("Scales how fast a worked field cell dries out.")]
        public float moistureDrainMultiplier = 1f;
        [Tooltip("Scales garden plot growth speed. Below 1 is slower.")]
        public float plotGrowthMultiplier = 1f;

        [Header("Water and power")]
        [Tooltip("Scales what a pump draws here. Dry flats are punishing.")]
        public float pumpMultiplier = 1f;
        [Tooltip("Scales solar bank output before the weather's own multiplier.")]
        public float solarMultiplier = 1f;

        [Header("Claim")]
        [Tooltip("The quietest a claim in this biome can ever be. A farm is never silent.")]
        public float heatFloorBonus;

        [Header("Weather")]
        [Tooltip("Relative odds of each state. Anything left out never rolls here.")]
        public List<WeatherWeight> weatherWeights = new List<WeatherWeight>();

        public float WeightOf(WeatherKind kind)
        {
            for (int i = 0; i < weatherWeights.Count; i++)
            {
                if (weatherWeights[i].kind == kind) return Mathf.Max(0f, weatherWeights[i].weight);
            }
            return 0f;
        }
    }

    /// <summary>The five biomes, looked up by id without a dictionary miss.</summary>
    [CreateAssetMenu(menuName = "MadVoxel/Biome Table", fileName = "BiomeTable")]
    public class BiomeTable : ScriptableObject
    {
        public List<BiomeDefinition> biomes = new List<BiomeDefinition>();

        public BiomeDefinition Find(BiomeId id)
        {
            for (int i = 0; i < biomes.Count; i++)
            {
                if (biomes[i] != null && biomes[i].id == id) return biomes[i];
            }
            return null;
        }

        public BiomeDefinition Find(string stringId)
        {
            for (int i = 0; i < biomes.Count; i++)
            {
                if (biomes[i] != null && biomes[i].stringId == stringId) return biomes[i];
            }
            return null;
        }
    }

    /// <summary>A crop's opinion of one biome.</summary>
    [Serializable]
    public struct CropBiomeYield
    {
        public BiomeId biome;
        [Tooltip("Multiplier on the harvest. 0 with 'blocked' means it will not grow here.")]
        public float multiplier;
        [Tooltip("The seed refuses to go in the ground at all.")]
        public bool blocked;
    }
}
