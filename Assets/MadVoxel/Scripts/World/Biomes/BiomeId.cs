namespace MadVoxel.World.Biomes
{
    /// <summary>
    /// The five regions painted onto the finite map. Stored as a byte in the chunk
    /// header and in save files, so the order is part of the on-disk format: append
    /// only, never reorder.
    /// </summary>
    public enum BiomeId : byte
    {
        /// <summary>Bottomland. Best dirt, every crop happy, shallow scarce ore.</summary>
        Farmland = 0,
        /// <summary>Pine scrub. Best wood and forage, stone near the ridges.</summary>
        PineScrub = 1,
        /// <summary>The rust belt. Scrap, clay, iron; crops sulk without hauled soil.</summary>
        ClayHills = 2,
        /// <summary>Dry flats. Drought country: surface ore, good sun, thirsty fields.</summary>
        DryFlats = 3,
        /// <summary>Frost shelf. Small, at the edge. Hard farming, better loot.</summary>
        FrostShelf = 4
    }

    public static class BiomeIds
    {
        public const string Farmland = "madvoxel:biome_farmland";
        public const string PineScrub = "madvoxel:biome_pine_scrub";
        public const string ClayHills = "madvoxel:biome_clay_hills";
        public const string DryFlats = "madvoxel:biome_dry_flats";
        public const string FrostShelf = "madvoxel:biome_frost_shelf";

        public const int Count = 5;

        public static string StringIdOf(BiomeId id)
        {
            switch (id)
            {
                case BiomeId.PineScrub: return PineScrub;
                case BiomeId.ClayHills: return ClayHills;
                case BiomeId.DryFlats: return DryFlats;
                case BiomeId.FrostShelf: return FrostShelf;
                default: return Farmland;
            }
        }

        /// <summary>The line the visor shows when you cross a fade.</summary>
        public static string DisplayName(BiomeId id)
        {
            switch (id)
            {
                case BiomeId.PineScrub: return "Pine Scrub";
                case BiomeId.ClayHills: return "Clay Hills";
                case BiomeId.DryFlats: return "Dry Flats";
                case BiomeId.FrostShelf: return "Frost Shelf";
                default: return "Farmland";
            }
        }
    }
}
