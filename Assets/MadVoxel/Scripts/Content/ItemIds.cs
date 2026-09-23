namespace MadVoxel.Content
{
    /// <summary>String ids the code refers to by name.</summary>
    public static class ItemIds
    {
        public const string WoodLog = "madvoxel:wood_log";
        public const string Plank = "madvoxel:plank";
        public const string Stone = "madvoxel:stone";
        public const string Dirt = "madvoxel:dirt";
        public const string Sand = "madvoxel:sand";
        public const string Clay = "madvoxel:clay";
        public const string Coal = "madvoxel:coal";
        public const string IronOre = "madvoxel:iron_ore";
        public const string IronIngot = "madvoxel:iron_ingot";
        public const string ScrapMetal = "madvoxel:scrap_metal";
        public const string PlantFibre = "madvoxel:plant_fibre";
        public const string Cloth = "madvoxel:cloth";
        public const string CannedFood = "madvoxel:canned_food";
        public const string WaterBottle = "madvoxel:water_bottle";
        public const string Bandage = "madvoxel:bandage";

        // Farming: seeds, produce and the meals they cook into.
        public const string SeedPotato = "madvoxel:seed_potato";
        public const string SeedCorn = "madvoxel:seed_corn";
        public const string SeedWheat = "madvoxel:seed_wheat";
        public const string Potato = "madvoxel:potato";
        public const string CornEar = "madvoxel:corn_ear";
        public const string Grain = "madvoxel:grain";
        public const string Flour = "madvoxel:flour";
        public const string BakedPotato = "madvoxel:baked_potato";
        public const string CornBread = "madvoxel:corn_bread";
        public const string VegetableStew = "madvoxel:vegetable_stew";
        /// <summary>What food becomes when nobody eats it. Compost, later.</summary>
        public const string Rot = "madvoxel:rot";

        public const string StonePickaxe = "madvoxel:stone_pickaxe";
        public const string StoneAxe = "madvoxel:stone_axe";
        public const string StoneShovel = "madvoxel:stone_shovel";
        public const string IronPickaxe = "madvoxel:iron_pickaxe";
        public const string IronAxe = "madvoxel:iron_axe";
        public const string Club = "madvoxel:club";
        public const string Wrench = "madvoxel:wrench";
        public const string Hammer = "madvoxel:hammer";
        public const string Hoe = "madvoxel:hoe";

        public const string BlockWoodFrame = "madvoxel:block_wood_frame";
        public const string BlockPlanks = "madvoxel:block_planks";
        public const string BlockCobblestone = "madvoxel:block_cobblestone";
        public const string BlockIron = "madvoxel:block_iron";
        public const string BlockSteel = "madvoxel:block_steel";
        public const string BlockGlass = "madvoxel:block_glass";

        // Deployables: free-placed 1 m objects, not part of the snap grid.
        public const string PieceStorageBox = "madvoxel:piece_storage_box";
        public const string PieceWorkbench = "madvoxel:piece_workbench";
        public const string PieceCampfire = "madvoxel:piece_campfire";
        public const string PieceToolCupboard = "madvoxel:piece_tool_cupboard";
        public const string PieceBedroll = "madvoxel:piece_bedroll";
        public const string PieceFarmPlot = "madvoxel:piece_farm_plot";
        public const string PieceSilo = "madvoxel:piece_silo";

        // Rust-style snap pieces. Each item places the Twig tier; the hammer upgrades it.
        public const string SnapFoundation = "madvoxel:snap_foundation";
        public const string SnapFloor = "madvoxel:snap_floor";
        public const string SnapWall = "madvoxel:snap_wall";
        public const string SnapWindowWall = "madvoxel:snap_window_wall";
        public const string SnapDoorway = "madvoxel:snap_doorway";
        public const string SnapHalfWall = "madvoxel:snap_half_wall";
        public const string SnapStairs = "madvoxel:snap_stairs";
        public const string SnapRoof = "madvoxel:snap_roof";
        public const string SnapLadder = "madvoxel:snap_ladder";
        public const string SnapHatch = "madvoxel:snap_hatch";
        public const string SnapDoor = "madvoxel:snap_door";
        public const string SnapFence = "madvoxel:snap_fence";

        // Phase 1 economy / vehicle content.
        public const string TradeToken = "madvoxel:trade_token";
        public const string EngineBlock = "madvoxel:engine_block";
        public const string Wheel = "madvoxel:wheel";
        public const string GasCan = "madvoxel:gas_can";
        public const string BuggyKit = "madvoxel:buggy_kit";

        // Field machines. The kit deploys the tractor; the rest hitch to the back of it.
        public const string TractorKit = "madvoxel:tractor_kit";
        public const string ImplementPlow = "madvoxel:implement_plow";
        public const string ImplementCultivator = "madvoxel:implement_cultivator";
        public const string ImplementSeeder = "madvoxel:implement_seeder";
        public const string ImplementHarvester = "madvoxel:implement_harvester";

        // Electricity. The wire tool is how every connection gets made.
        public const string WireTool = "madvoxel:wire_tool";
        public const string CopperWire = "madvoxel:copper_wire";
        public const string PieceGeneratorBank = "madvoxel:piece_generator_bank";
        public const string PieceBatteryBank = "madvoxel:piece_battery_bank";
        public const string PieceSolarBank = "madvoxel:piece_solar_bank";
        public const string PieceRelay = "madvoxel:piece_relay";
        public const string PieceSwitch = "madvoxel:piece_switch";
        public const string PieceSplitter = "madvoxel:piece_splitter";
        public const string PieceLight = "madvoxel:piece_light";
        public const string PieceFridge = "madvoxel:piece_fridge";
        public const string PieceBladeTrap = "madvoxel:piece_blade_trap";
        public const string PieceFencePost = "madvoxel:piece_fence_post";

        // Water.
        public const string PieceWaterPump = "madvoxel:piece_water_pump";
        public const string PiecePipe = "madvoxel:piece_pipe";
        public const string PieceWaterTank = "madvoxel:piece_water_tank";
        public const string PieceWaterBarrel = "madvoxel:piece_water_barrel";
        public const string PieceTap = "madvoxel:piece_tap";
        public const string PieceSprinkler = "madvoxel:piece_sprinkler";

        // The colony charter.
        public const string PieceColonyBoard = "madvoxel:piece_colony_board";
    }

    public static class StructureIds
    {
        public const string StorageBox = "madvoxel:storage_box";
        public const string Workbench = "madvoxel:workbench";
        public const string Campfire = "madvoxel:campfire";
        public const string ToolCupboard = "madvoxel:tool_cupboard";
        public const string Bedroll = "madvoxel:bedroll";
        public const string FarmPlot = "madvoxel:farm_plot";
        public const string Silo = "madvoxel:silo";
        public const string DeathBackpack = "madvoxel:death_backpack";

        public const string GeneratorBank = "madvoxel:generator_bank";
        public const string BatteryBank = "madvoxel:battery_bank";
        public const string SolarBank = "madvoxel:solar_bank";
        public const string Relay = "madvoxel:relay";
        public const string Switch = "madvoxel:switch";
        public const string Splitter = "madvoxel:splitter";
        public const string Light = "madvoxel:light";
        public const string Fridge = "madvoxel:fridge";
        public const string BladeTrap = "madvoxel:blade_trap";
        public const string FencePost = "madvoxel:fence_post";

        public const string WaterPump = "madvoxel:water_pump";
        public const string Pipe = "madvoxel:pipe";
        public const string WaterTank = "madvoxel:water_tank";
        public const string WaterBarrel = "madvoxel:water_barrel";
        public const string Tap = "madvoxel:tap";
        public const string Sprinkler = "madvoxel:sprinkler";

        public const string ColonyBoard = "madvoxel:colony_board";
    }

    public static class ZombieIds
    {
        public const string Shambler = "madvoxel:shambler";
        public const string Brute = "madvoxel:brute";
    }
}
