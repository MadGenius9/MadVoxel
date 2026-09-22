using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.World.Weather
{
    /// <summary>
    /// One at a time, plus wind. The order is part of the save format: append only.
    /// </summary>
    public enum WeatherKind : byte
    {
        Clear = 0,
        Overcast = 1,
        Rain = 2,
        Drought = 3,
        Storm = 4,
        FrostNight = 5
    }

    /// <summary>
    /// What a weather state does to the systems that already exist. Every number here
    /// is a multiplier or a per-hour delta against something the game already
    /// simulates - there is no separate weather simulation, only a set of dials on the
    /// pump, the panel, the soil and the crop.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Weather", fileName = "Weather")]
    public class WeatherDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string stringId = "madvoxel:weather";
        public WeatherKind kind = WeatherKind.Clear;
        [Tooltip("One word for the clock line. Empty means the clock says nothing.")]
        public string clockWord = "";

        [Header("Duration, in game hours")]
        public float minHours = 4f;
        public float maxHours = 10f;
        [Tooltip("Only ever rolled for a night. Frost is the one that uses this.")]
        public bool nightOnly;

        [Header("Water")]
        [Tooltip("Scales what a well or a surface pump draws.")]
        public float pumpMultiplier = 1f;
        [Tooltip("Litres a barrel gains per game hour standing in the open.")]
        public float barrelFillLitresPerHour;
        [Tooltip("An unheated tap stops flowing until this weather ends.")]
        public bool freezesTaps;

        [Header("Soil")]
        [Tooltip("Field moisture change per game hour, before the biome's drain.")]
        public float moisturePerHour;
        [Tooltip("Garden plots without a sprinkler lose this much of a harvest.")]
        [Range(0f, 1f)] public float unwateredPlotYieldLoss;

        [Header("Power")]
        [Tooltip("Scales solar bank output. Storm is zero.")]
        public float solarMultiplier = 1f;
        [Tooltip("Chance per game hour that one exposed pipe, wire or panel breaks.")]
        [Range(0f, 1f)] public float breakChancePerHour;

        [Header("People")]
        [Tooltip("Morale per game hour for a colonist caught outside.")]
        public float outdoorMoralePerHour;
        [Tooltip("Extra stamina drain multiplier while outdoors.")]
        public float staminaDrainMultiplier = 1f;

        [Header("Crops")]
        [Tooltip("Chance per game hour that an unsheltered plot loses a growth stage.")]
        [Range(0f, 1f)] public float plotStageLossChance;

        public bool IsHarsh
        {
            get { return kind == WeatherKind.Storm || kind == WeatherKind.Drought || kind == WeatherKind.FrostNight; }
        }
    }

    /// <summary>How likely one biome is to roll one weather state.</summary>
    [Serializable]
    public struct WeatherWeight
    {
        public WeatherKind kind;
        [Min(0f)] public float weight;
    }

    /// <summary>The whole weather table, plus the rules the director rolls against.</summary>
    [CreateAssetMenu(menuName = "MadVoxel/Weather Table", fileName = "WeatherTable")]
    public class WeatherTable : ScriptableObject
    {
        public List<WeatherDefinition> states = new List<WeatherDefinition>();

        [Tooltip("Clear weather is forced for this many hours at the start of a new world.")]
        public float graceHours = 30f;

        public WeatherDefinition Find(WeatherKind kind)
        {
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i] != null && states[i].kind == kind) return states[i];
            }
            return null;
        }
    }
}
