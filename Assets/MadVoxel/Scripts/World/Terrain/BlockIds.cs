namespace MadVoxel.World.Terrain
{
    /// <summary>
    /// String ids for the blocks the code needs by name. Content can add any number of
    /// further blocks without touching this list.
    /// </summary>
    public static class BlockIds
    {
        public const string Air = "madvoxel:air";
        public const string Bedrock = "madvoxel:bedrock";
        public const string Stone = "madvoxel:stone";
        public const string Dirt = "madvoxel:dirt";
        public const string Grass = "madvoxel:grass";
        public const string Sand = "madvoxel:sand";
        public const string Gravel = "madvoxel:gravel";
        public const string Clay = "madvoxel:clay";
        public const string TilledSoil = "madvoxel:tilled_soil";
        public const string CoalOre = "madvoxel:coal_ore";
        public const string IronOre = "madvoxel:iron_ore";
        public const string PineLog = "madvoxel:pine_log";
        public const string PineNeedles = "madvoxel:pine_needles";
        public const string ScrapHeap = "madvoxel:scrap_heap";

        // Wild forage: how the garden starts, before you have any seeds.
        public const string WildYucca = "madvoxel:wild_yucca";
        public const string WildGrain = "madvoxel:wild_grain";
        public const string WildCorn = "madvoxel:wild_corn";

        // Crafted / build blocks. These form the Phase 2 upgrade chain.
        public const string WoodFrame = "madvoxel:wood_frame";
        public const string Planks = "madvoxel:planks";
        public const string Cobblestone = "madvoxel:cobblestone";
        public const string IronBlock = "madvoxel:iron_block";
        public const string SteelBlock = "madvoxel:steel_block";
        public const string Concrete = "madvoxel:concrete";
        public const string Glass = "madvoxel:glass";
    }
}
