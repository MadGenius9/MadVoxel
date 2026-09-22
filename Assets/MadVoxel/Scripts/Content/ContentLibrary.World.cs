using System.Collections.Generic;
using MadVoxel.World.Biomes;
using MadVoxel.World.Terrain;
using MadVoxel.World.Weather;
using UnityEngine;

namespace MadVoxel.Content
{
    /// <summary>
    /// The world's regions and its sky. Split from the main content file because both
    /// are read by terrain generation, farming, water, power and claim heat, and a
    /// change here has consequences a long way from the ore tables.
    /// </summary>
    public static partial class ContentLibrary
    {
        // ------------------------------------------------------------------ biomes

        static BiomeDefinition Biome(BiomeId id, string stringId, string name, string description, Color tint)
        {
            var def = ScriptableObject.CreateInstance<BiomeDefinition>();
            def.name = "Biome_" + id;
            def.id = id;
            def.stringId = stringId;
            def.displayName = name;
            def.description = description;
            def.mapTint = tint;
            return def;
        }

        static WeatherWeight Weight(WeatherKind kind, float weight)
        {
            return new WeatherWeight { kind = kind, weight = weight };
        }

        static BiomeTable BuildBiomes()
        {
            var table = ScriptableObject.CreateInstance<BiomeTable>();
            table.name = "BiomeTable";

            // ---- Farmland. The starter dirt, and the reason the game is about a farm.
            var farmland = Biome(BiomeId.Farmland, BiomeIds.Farmland, "Farmland",
                "Old bottomland. The dirt still remembers what it was for.",
                new Color(0.42f, 0.46f, 0.26f));
            farmland.surfaceBlockId = BlockIds.Grass;
            farmland.subSurfaceBlockId = BlockIds.Dirt;
            farmland.soilDepth = 5;
            farmland.bareDirtChance = 0.30f;
            farmland.clayChance = 0.03f;
            farmland.oreDensityMultiplier = 0.55f;   // shallow and scarce
            farmland.treeDensityMultiplier = 0.7f;
            farmland.forageDensityMultiplier = 1.3f;
            farmland.scrapDensityMultiplier = 0.8f;
            farmland.cropYieldMultiplier = 1.15f;
            farmland.plotGrowthMultiplier = 1.15f;
            farmland.moistureDrainMultiplier = 0.8f;
            farmland.pumpMultiplier = 1.1f;
            farmland.heatFloorBonus = 6f;            // open fields carry
            farmland.weatherWeights.AddRange(new[]
            {
                Weight(WeatherKind.Clear, 42f), Weight(WeatherKind.Overcast, 24f),
                Weight(WeatherKind.Rain, 22f), Weight(WeatherKind.Storm, 6f),
                Weight(WeatherKind.Drought, 4f), Weight(WeatherKind.FrostNight, 2f)
            });
            table.biomes.Add(farmland);

            // ---- Pine scrub. Wood, cover, and a quiet place to hide a shack.
            var scrub = Biome(BiomeId.PineScrub, BiomeIds.PineScrub, "Pine Scrub",
                "Second-growth pine over thin soil. Good timber, poor ploughing.",
                new Color(0.24f, 0.33f, 0.22f));
            scrub.surfaceBlockId = BlockIds.Grass;
            scrub.subSurfaceBlockId = BlockIds.Dirt;
            scrub.soilDepth = 3;
            scrub.bareDirtChance = 0.08f;
            scrub.clayChance = 0.02f;
            scrub.oreDensityMultiplier = 1.0f;
            scrub.treeDensityMultiplier = 2.2f;
            scrub.forageDensityMultiplier = 1.6f;
            scrub.scrapDensityMultiplier = 0.5f;
            scrub.cropYieldMultiplier = 0.85f;
            scrub.plotGrowthMultiplier = 0.95f;
            scrub.moistureDrainMultiplier = 0.7f;
            scrub.pumpMultiplier = 1.0f;
            scrub.heatFloorBonus = -4f;              // trees swallow a claim
            scrub.weatherWeights.AddRange(new[]
            {
                Weight(WeatherKind.Clear, 34f), Weight(WeatherKind.Overcast, 30f),
                Weight(WeatherKind.Rain, 26f), Weight(WeatherKind.Storm, 6f),
                Weight(WeatherKind.Drought, 1f), Weight(WeatherKind.FrostNight, 3f)
            });
            table.biomes.Add(scrub);

            // ---- Clay hills. The rust belt: where the pipe and the wire come from.
            var clay = Biome(BiomeId.ClayHills, BiomeIds.ClayHills, "Clay Hills",
                "Cut banks and collapsed sheds. Everything here used to be machinery.",
                new Color(0.44f, 0.30f, 0.22f));
            clay.surfaceBlockId = BlockIds.Dirt;
            clay.subSurfaceBlockId = BlockIds.Clay;
            clay.soilDepth = 4;
            clay.bareDirtChance = 0.55f;
            clay.clayChance = 0.34f;
            clay.oreDensityMultiplier = 1.5f;
            clay.treeDensityMultiplier = 0.45f;
            clay.forageDensityMultiplier = 0.55f;
            clay.scrapDensityMultiplier = 3.0f;      // the reason you come here
            clay.cropYieldMultiplier = 0.6f;
            clay.plotGrowthMultiplier = 0.8f;
            clay.moistureDrainMultiplier = 1.1f;
            clay.pumpMultiplier = 0.9f;
            clay.heatFloorBonus = 0f;
            clay.weatherWeights.AddRange(new[]
            {
                Weight(WeatherKind.Clear, 38f), Weight(WeatherKind.Overcast, 26f),
                Weight(WeatherKind.Rain, 16f), Weight(WeatherKind.Storm, 10f),
                Weight(WeatherKind.Drought, 8f), Weight(WeatherKind.FrostNight, 2f)
            });
            table.biomes.Add(clay);

            // ---- Dry flats. Sun for the panels, nothing for the wheat.
            var flats = Biome(BiomeId.DryFlats, BiomeIds.DryFlats, "Dry Flats",
                "Hardpan and sun. Solar country. Bring your own water.",
                new Color(0.58f, 0.50f, 0.34f));
            flats.surfaceBlockId = BlockIds.Sand;
            flats.subSurfaceBlockId = BlockIds.Sand;
            flats.soilDepth = 3;
            flats.bareDirtChance = 0.0f;
            flats.clayChance = 0.01f;
            flats.oreDensityMultiplier = 1.35f;
            flats.oreDepthBonus = 18;                // ore at the surface
            flats.treeDensityMultiplier = 0.05f;
            flats.forageDensityMultiplier = 0.4f;
            flats.scrapDensityMultiplier = 1.2f;
            flats.cropYieldMultiplier = 0.45f;
            flats.plotGrowthMultiplier = 0.75f;
            flats.moistureDrainMultiplier = 2.6f;    // a field here drinks
            flats.pumpMultiplier = 0.55f;            // and the well is weak
            flats.solarMultiplier = 1.35f;
            flats.heatFloorBonus = 2f;
            flats.weatherWeights.AddRange(new[]
            {
                Weight(WeatherKind.Clear, 46f), Weight(WeatherKind.Overcast, 12f),
                Weight(WeatherKind.Rain, 4f), Weight(WeatherKind.Storm, 6f),
                Weight(WeatherKind.Drought, 30f), Weight(WeatherKind.FrostNight, 2f)
            });
            table.biomes.Add(flats);

            // ---- Frost shelf. Small, at the edge, and it wants you to leave.
            var frost = Biome(BiomeId.FrostShelf, BiomeIds.FrostShelf, "Frost Shelf",
                "The cold lip of the map. Better wrecks, worse everything else.",
                new Color(0.62f, 0.66f, 0.70f));
            frost.surfaceBlockId = BlockIds.Gravel;
            frost.subSurfaceBlockId = BlockIds.Stone;
            frost.soilDepth = 2;
            frost.bareDirtChance = 0.0f;
            frost.clayChance = 0.0f;
            frost.oreDensityMultiplier = 1.25f;
            frost.treeDensityMultiplier = 0.35f;
            frost.forageDensityMultiplier = 0.5f;
            frost.scrapDensityMultiplier = 1.8f;
            frost.cropYieldMultiplier = 0.35f;
            frost.plotGrowthMultiplier = 0.55f;
            frost.moistureDrainMultiplier = 0.5f;
            frost.pumpMultiplier = 0.8f;
            frost.solarMultiplier = 0.7f;
            frost.heatFloorBonus = -2f;
            frost.weatherWeights.AddRange(new[]
            {
                Weight(WeatherKind.Clear, 22f), Weight(WeatherKind.Overcast, 30f),
                Weight(WeatherKind.Rain, 8f), Weight(WeatherKind.Storm, 8f),
                Weight(WeatherKind.Drought, 0f), Weight(WeatherKind.FrostNight, 32f)
            });
            table.biomes.Add(frost);

            return table;
        }

        // ------------------------------------------------------------ crop opinions

        static CropBiomeYield BiomeYield(BiomeId biome, float multiplier)
        {
            return new CropBiomeYield { biome = biome, multiplier = multiplier, blocked = false };
        }

        /// <summary>The seed will not go in at all. Used sparingly - it removes a choice.</summary>
        static CropBiomeYield Blocked(BiomeId biome)
        {
            return new CropBiomeYield { biome = biome, multiplier = 0f, blocked = true };
        }

        // ----------------------------------------------------------------- weather

        static WeatherDefinition Weather(WeatherKind kind, string stringId, string clockWord,
                                         float minHours, float maxHours)
        {
            var def = ScriptableObject.CreateInstance<WeatherDefinition>();
            def.name = "Weather_" + kind;
            def.kind = kind;
            def.stringId = stringId;
            def.clockWord = clockWord;
            def.minHours = minHours;
            def.maxHours = maxHours;
            return def;
        }

        static WeatherTable BuildWeather()
        {
            var table = ScriptableObject.CreateInstance<WeatherTable>();
            table.name = "WeatherTable";

            // Clear says nothing on the clock. A HUD that announces "CLEAR" all week is
            // wallpaper, and the whole point of the one-word line is that it means
            // something when it appears.
            var clear = Weather(WeatherKind.Clear, "madvoxel:weather_clear", "", 8f, 26f);
            table.states.Add(clear);

            var overcast = Weather(WeatherKind.Overcast, "madvoxel:weather_overcast", "OVERCAST", 5f, 14f);
            overcast.solarMultiplier = 0.45f;
            table.states.Add(overcast);

            var rain = Weather(WeatherKind.Rain, "madvoxel:weather_rain", "RAIN", 3f, 9f);
            rain.solarMultiplier = 0.25f;
            rain.barrelFillLitresPerHour = 14f;      // a barrel is a real water source in rain
            rain.moisturePerHour = 0.09f;
            rain.pumpMultiplier = 1.15f;
            rain.outdoorMoralePerHour = -0.4f;
            table.states.Add(rain);

            // Drought is the long one: multi-day, and it is what makes the tank and the
            // sprinkler worth the copper.
            var drought = Weather(WeatherKind.Drought, "madvoxel:weather_drought", "DROUGHT", 40f, 96f);
            drought.solarMultiplier = 1.1f;
            drought.pumpMultiplier = 0.4f;
            drought.moisturePerHour = -0.035f;
            drought.unwateredPlotYieldLoss = 0.45f;
            drought.outdoorMoralePerHour = -0.25f;
            drought.staminaDrainMultiplier = 1.2f;
            table.states.Add(drought);

            // Short and expensive.
            var storm = Weather(WeatherKind.Storm, "madvoxel:weather_storm", "STORM", 2f, 5f);
            storm.solarMultiplier = 0f;
            storm.barrelFillLitresPerHour = 22f;
            storm.moisturePerHour = 0.14f;
            storm.breakChancePerHour = 0.30f;        // one exposed pipe, wire or panel
            storm.outdoorMoralePerHour = -1.2f;
            storm.staminaDrainMultiplier = 1.3f;
            table.states.Add(storm);

            var frost = Weather(WeatherKind.FrostNight, "madvoxel:weather_frost", "FROST", 6f, 10f);
            frost.nightOnly = true;
            frost.solarMultiplier = 0.6f;
            frost.freezesTaps = true;
            frost.pumpMultiplier = 0.5f;
            frost.plotStageLossChance = 0.06f;
            frost.outdoorMoralePerHour = -0.9f;
            frost.staminaDrainMultiplier = 1.35f;
            table.states.Add(frost);

            return table;
        }
    }
}
