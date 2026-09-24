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

        /// <summary>
        /// The second pass. Visually distinct from tilled on purpose: without it you
        /// cannot tell by looking which strips you have already cultivated, and have to
        /// remember it instead.
        /// </summary>
        public const string CultivatedSoil = "madvoxel:cultivated_soil";
        public const string CoalOre = "madvoxel:coal_ore";
        public const string IronOre = "madvoxel:iron_ore";

        /// <summary>
        /// The deep one. Only a tier-three pickaxe touches it, and it only forms well
        /// below the iron band - which is what gives steel tools somewhere to go and
        /// the armoured tier something to cost.
        /// </summary>
        public const string TungstenOre = "madvoxel:tungsten_ore";
        public const string PineLog = "madvoxel:pine_log";
        public const string PineNeedles = "madvoxel:pine_needles";
        public const string ScrapHeap = "madvoxel:scrap_heap";

        /// <summary>
        /// Saturated ground. Dig down to it and a pump set on top has a well. It is a
        /// solid block, not a fluid: the game has no liquid simulation and does not
        /// need one for a farm that runs on pipes.
        /// </summary>
        public const string WaterTable = "madvoxel:water_table";

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
