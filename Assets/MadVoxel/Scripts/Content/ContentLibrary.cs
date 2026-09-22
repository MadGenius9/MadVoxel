using System.Collections.Generic;
using MadVoxel.AI;
using MadVoxel.Building;
using MadVoxel.Core;
using MadVoxel.Farming.Crops;
using MadVoxel.Horde;
using MadVoxel.Inventory;
using MadVoxel.Quests;
using MadVoxel.Perks;
using MadVoxel.Traders;
using MadVoxel.Vehicles;
using MadVoxel.World.Biomes;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.Content
{
    /// <summary>
    /// The authored content of MadVoxel, expressed in code so the game runs from a
    /// fresh clone. "MadVoxel &gt; Content &gt; Generate ScriptableObject Assets" writes the
    /// very same data out as .asset files for designers to edit by hand.
    /// </summary>
    public static partial class ContentLibrary
    {
        // Grimy survival palette: mud, wet stone, rust, bleached cloth.
        static readonly Color ColDirt = new Color(0.36f, 0.27f, 0.19f);
        static readonly Color ColGrass = new Color(0.32f, 0.36f, 0.20f);
        static readonly Color ColStone = new Color(0.42f, 0.42f, 0.42f);
        static readonly Color ColSand = new Color(0.62f, 0.56f, 0.42f);
        static readonly Color ColGravel = new Color(0.47f, 0.45f, 0.43f);
        static readonly Color ColClay = new Color(0.52f, 0.40f, 0.33f);
        static readonly Color ColCoal = new Color(0.20f, 0.20f, 0.21f);
        static readonly Color ColIron = new Color(0.56f, 0.44f, 0.35f);
        static readonly Color ColWood = new Color(0.36f, 0.26f, 0.16f);
        static readonly Color ColNeedle = new Color(0.20f, 0.28f, 0.17f);
        static readonly Color ColRust = new Color(0.46f, 0.28f, 0.17f);
        static readonly Color ColPlank = new Color(0.48f, 0.36f, 0.22f);
        static readonly Color ColCobble = new Color(0.46f, 0.45f, 0.44f);
        static readonly Color ColIronPlate = new Color(0.44f, 0.42f, 0.40f);
        static readonly Color ColSteel = new Color(0.55f, 0.57f, 0.60f);
        static readonly Color ColConcrete = new Color(0.55f, 0.54f, 0.51f);
        static readonly Color ColGlass = new Color(0.72f, 0.82f, 0.85f, 0.42f);
        static readonly Color ColBedrock = new Color(0.16f, 0.16f, 0.17f);

        public static ContentDatabase BuildRuntime()
        {
            var db = ScriptableObject.CreateInstance<ContentDatabase>();
            db.name = "ContentDatabase";

            db.config = BuildConfig();

            var blockMap = BuildBlocks();
            var itemMap = BuildItems(blockMap);
            var structureMap = BuildStructures(itemMap);
            AddUtilityContent(itemMap, structureMap);

            LinkPlacement(itemMap, blockMap, structureMap);
            LinkBlockDrops(blockMap, itemMap);

            // Snap pieces need the item table (for upgrade costs) and then add items of
            // their own, so they slot in between the two.
            var twigByKind = new Dictionary<BuildPieceKind, BuildPieceDefinition>();
            db.buildPieces = BuildSnapPieces(itemMap, twigByKind);

            db.blocks = BuildRegistry(blockMap);
            db.structures = new List<StructureDefinition>(structureMap.Values);
            db.recipes = BuildRecipes(itemMap);
            AddUtilityRecipes(itemMap, db.recipes);
            AddSnapItems(itemMap, twigByKind, db.recipes);
            db.items = new List<ItemDefinition>(itemMap.Values);
            db.zombies = BuildZombies(itemMap);
            db.hordeSchedule = BuildHordeSchedule(db.zombies);

            db.biomes = BuildBiomes();
            db.weather = BuildWeather();

            db.crops = BuildCrops(itemMap);
            db.perkTree = BuildPerkTree();
            db.quests = BuildQuests(itemMap);
            db.traders = BuildTraders(itemMap, db.quests);
            db.vehicles = BuildVehicles(itemMap);

            db.startingItems = new List<ContentDatabase.StartingStack>
            {
                Starting(itemMap[ItemIds.StoneAxe], 1),
                Starting(itemMap[ItemIds.CannedFood], 2),
                Starting(itemMap[ItemIds.WaterBottle], 2),
                Starting(itemMap[ItemIds.PlantFibre], 8),
                Starting(itemMap[ItemIds.SeedPotato], 3)
            };

            return db;
        }

        static ContentDatabase.StartingStack Starting(ItemDefinition item, int count)
        {
            return new ContentDatabase.StartingStack { item = item, count = count };
        }

        // ------------------------------------------------------------------ config

        static GameConfig BuildConfig()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.name = "GameConfig";
            return config; // field defaults are the tuning; see GameConfig.cs
        }

        // ------------------------------------------------------------------ blocks

        static BlockDefinition Block(string id, string displayName, SurfaceFamily family, Color tint,
                                     float hardness, ToolType tool, int requiredTier, float xp, float structureHealth)
        {
            var def = ScriptableObject.CreateInstance<BlockDefinition>();
            def.name = ShortName(id);
            def.stringId = id;
            def.displayName = displayName;
            def.surfaceFamily = family;
            def.tint = tint;
            def.hardness = hardness;
            def.preferredTool = tool;
            def.requiredToolTier = requiredTier;
            def.harvestXp = xp;
            def.structureHealth = structureHealth;
            def.solid = true;
            def.opaque = true;
            return def;
        }

        static Dictionary<string, BlockDefinition> BuildBlocks()
        {
            var map = new Dictionary<string, BlockDefinition>();

            var air = ScriptableObject.CreateInstance<BlockDefinition>();
            air.name = "Air";
            air.stringId = BlockIds.Air;
            air.displayName = "Air";
            air.isAir = true;
            air.solid = false;
            air.opaque = false;
            air.hardness = -1f;
            map[BlockIds.Air] = air;

            var bedrock = Block(BlockIds.Bedrock, "Bedrock", SurfaceFamily.Stone, ColBedrock, -1f, ToolType.Pickaxe, 99, 0f, -1f);
            map[BlockIds.Bedrock] = bedrock;

            map[BlockIds.Stone] = Block(BlockIds.Stone, "Stone", SurfaceFamily.Stone, ColStone, 2.2f, ToolType.Pickaxe, 0, 1.0f, 200f);
            map[BlockIds.Dirt] = Block(BlockIds.Dirt, "Dirt", SurfaceFamily.Dirt, ColDirt, 0.5f, ToolType.Shovel, 0, 0.4f, 60f);
            map[BlockIds.Grass] = Block(BlockIds.Grass, "Scrub Grass", SurfaceFamily.Grass, ColGrass, 0.6f, ToolType.Shovel, 0, 0.4f, 60f);
            map[BlockIds.Sand] = Block(BlockIds.Sand, "Sand", SurfaceFamily.Sand, ColSand, 0.5f, ToolType.Shovel, 0, 0.4f, 45f);
            map[BlockIds.Gravel] = Block(BlockIds.Gravel, "Gravel", SurfaceFamily.Stone, ColGravel, 0.7f, ToolType.Shovel, 0, 0.6f, 70f);
            // Broken ground: what a plow leaves behind, and what a field reads as.
            var tilled = Block(BlockIds.TilledSoil, "Tilled Soil", SurfaceFamily.Dirt, new Color(0.26f, 0.19f, 0.13f), 0.45f, ToolType.Shovel, 0, 0.3f, 55f);
            map[BlockIds.TilledSoil] = tilled;

            map[BlockIds.Clay] = Block(BlockIds.Clay, "Clay", SurfaceFamily.Dirt, ColClay, 0.7f, ToolType.Shovel, 0, 0.8f, 70f);
            map[BlockIds.CoalOre] = Block(BlockIds.CoalOre, "Coal Seam", SurfaceFamily.Ore, ColCoal, 2.6f, ToolType.Pickaxe, 1, 4f, 220f);
            map[BlockIds.IronOre] = Block(BlockIds.IronOre, "Iron Ore", SurfaceFamily.Ore, ColIron, 3.2f, ToolType.Pickaxe, 1, 5f, 240f);
            map[BlockIds.PineLog] = Block(BlockIds.PineLog, "Pine Log", SurfaceFamily.Wood, ColWood, 1.8f, ToolType.Axe, 0, 3f, 140f);
            map[BlockIds.ScrapHeap] = Block(BlockIds.ScrapHeap, "Scrap Heap", SurfaceFamily.Metal, ColRust, 2.0f, ToolType.Pickaxe, 0, 5f, 150f);

            // Saturated ground: the thing a well is dug to find. Soft, worthless to mine,
            // and the only reason anyone digs past the ore.
            var waterTable = Block(BlockIds.WaterTable, "Water Table", SurfaceFamily.Dirt,
                new Color(0.29f, 0.36f, 0.40f), 0.8f, ToolType.Shovel, 0, 1f, 40f);
            map[BlockIds.WaterTable] = waterTable;

            // Wild forage. Non-solid so you walk through them, cheap to clear, and they
            // are the only source of seeds until a trader sells you better ones.
            AddForage(map, BlockIds.WildYucca, "Wild Yucca", new Color(0.36f, 0.45f, 0.26f));
            AddForage(map, BlockIds.WildGrain, "Wild Grain", new Color(0.62f, 0.55f, 0.28f));
            AddForage(map, BlockIds.WildCorn, "Wild Corn", new Color(0.44f, 0.52f, 0.24f));

            var needles = Block(BlockIds.PineNeedles, "Pine Needles", SurfaceFamily.Foliage, ColNeedle, 0.3f, ToolType.None, 0, 0.2f, 25f);
            needles.opaque = false; // lets light through the canopy and halves the face count
            map[BlockIds.PineNeedles] = needles;

            // Crafted build blocks. Natural XP is 0 so re-mining your own wall earns nothing.
            var frame = Block(BlockIds.WoodFrame, "Wood Frame", SurfaceFamily.Plank, ColPlank * 0.9f, 1.2f, ToolType.Axe, 0, 0f, 140f);
            frame.buildTier = 1;
            map[BlockIds.WoodFrame] = frame;

            var planks = Block(BlockIds.Planks, "Plank Block", SurfaceFamily.Plank, ColPlank, 1.4f, ToolType.Axe, 0, 0f, 170f);
            planks.buildTier = 1;
            map[BlockIds.Planks] = planks;

            var cobble = Block(BlockIds.Cobblestone, "Cobblestone", SurfaceFamily.Stone, ColCobble, 2.4f, ToolType.Pickaxe, 0, 0f, 320f);
            cobble.buildTier = 2;
            map[BlockIds.Cobblestone] = cobble;

            var ironBlock = Block(BlockIds.IronBlock, "Iron Plate Block", SurfaceFamily.Metal, ColIronPlate, 4.0f, ToolType.Pickaxe, 2, 0f, 560f);
            ironBlock.buildTier = 3;
            ironBlock.metallic = 0.7f;
            ironBlock.smoothness = 0.28f;
            map[BlockIds.IronBlock] = ironBlock;

            // Tier 2 (iron) because that is the best tool the game currently crafts; a
            // steel pickaxe arrives with the Phase 2 tool tiers.
            var steelBlock = Block(BlockIds.SteelBlock, "Steel Block", SurfaceFamily.Metal, ColSteel, 6.0f, ToolType.Pickaxe, 2, 0f, 900f);
            steelBlock.buildTier = 4;
            steelBlock.metallic = 0.85f;
            steelBlock.smoothness = 0.4f;
            map[BlockIds.SteelBlock] = steelBlock;

            map[BlockIds.Concrete] = Block(BlockIds.Concrete, "Concrete", SurfaceFamily.Concrete, ColConcrete, 3.4f, ToolType.Pickaxe, 1, 0.5f, 430f);

            var glass = Block(BlockIds.Glass, "Scrap Glass", SurfaceFamily.Stone, ColGlass, 0.4f, ToolType.Pickaxe, 0, 0f, 35f);
            glass.opaque = false;
            glass.transparent = true;
            glass.smoothness = 0.85f;
            map[BlockIds.Glass] = glass;

            // Phase 2 upgrade chain, already wired so the data is real.
            frame.upgradesTo = cobble;
            cobble.upgradesTo = ironBlock;
            ironBlock.upgradesTo = steelBlock;

            return map;
        }

        static void AddForage(Dictionary<string, BlockDefinition> map, string id, string name, Color tint)
        {
            var def = Block(id, name, SurfaceFamily.Foliage, tint, 0.18f, ToolType.None, 0, 1.5f, 12f);
            def.solid = false;
            def.opaque = false;
            map[id] = def;
        }

        static BlockRegistry BuildRegistry(Dictionary<string, BlockDefinition> map)
        {
            var registry = ScriptableObject.CreateInstance<BlockRegistry>();
            registry.name = "BlockRegistry";

            // Air must be index 0; the rest follow a stable, explicit order.
            string[] order =
            {
                BlockIds.Air, BlockIds.Bedrock, BlockIds.Stone, BlockIds.Dirt, BlockIds.Grass,
                BlockIds.Sand, BlockIds.Gravel, BlockIds.Clay, BlockIds.TilledSoil, BlockIds.CoalOre, BlockIds.IronOre,
                BlockIds.PineLog, BlockIds.PineNeedles, BlockIds.ScrapHeap,
                BlockIds.WildYucca, BlockIds.WildGrain, BlockIds.WildCorn,
                BlockIds.WoodFrame, BlockIds.Planks, BlockIds.Cobblestone, BlockIds.IronBlock,
                BlockIds.SteelBlock, BlockIds.Concrete, BlockIds.Glass
            };

            for (int i = 0; i < order.Length; i++) registry.blocks.Add(map[order[i]]);
            registry.Build();
            return registry;
        }

        // ------------------------------------------------------------------- items

        static ItemDefinition Item(string id, string displayName, ItemCategory category, int maxStack,
                                   SurfaceFamily family, Color tint, int tradeValue = 1)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.name = ShortName(id);
            def.stringId = id;
            def.displayName = displayName;
            def.category = category;
            def.maxStack = maxStack;
            def.surfaceFamily = family;
            def.tint = tint;
            def.tradeValue = tradeValue;
            return def;
        }

        static ItemDefinition Tool(string id, string displayName, ToolType type, int tier, float harvestSpeed,
                                   float melee, int durability, Color tint, int tradeValue)
        {
            var def = Item(id, displayName, ItemCategory.Tool, 1, SurfaceFamily.Metal, tint, tradeValue);
            def.toolType = type;
            def.toolTier = tier;
            def.harvestSpeed = harvestSpeed;
            def.meleeDamage = melee;
            def.maxDurability = durability;
            def.attackCooldown = type == ToolType.Melee ? 0.62f : 0.72f;
            return def;
        }

        static Dictionary<string, ItemDefinition> BuildItems(Dictionary<string, BlockDefinition> blocks)
        {
            var map = new Dictionary<string, ItemDefinition>();
            void Add(ItemDefinition def) { map[def.stringId] = def; }

            Add(Item(ItemIds.WoodLog, "Wood Log", ItemCategory.Resource, 64, SurfaceFamily.Wood, ColWood, 2));
            Add(Item(ItemIds.Plank, "Plank", ItemCategory.Resource, 64, SurfaceFamily.Plank, ColPlank, 1));
            Add(Item(ItemIds.Stone, "Stone", ItemCategory.Resource, 64, SurfaceFamily.Stone, ColStone, 1));
            Add(Item(ItemIds.Dirt, "Dirt", ItemCategory.Resource, 64, SurfaceFamily.Dirt, ColDirt, 1));
            Add(Item(ItemIds.Sand, "Sand", ItemCategory.Resource, 64, SurfaceFamily.Sand, ColSand, 1));
            Add(Item(ItemIds.Clay, "Clay", ItemCategory.Resource, 64, SurfaceFamily.Dirt, ColClay, 1));
            Add(Item(ItemIds.Coal, "Coal", ItemCategory.Resource, 64, SurfaceFamily.Ore, ColCoal, 3));
            Add(Item(ItemIds.IronOre, "Iron Ore", ItemCategory.Resource, 64, SurfaceFamily.Ore, ColIron, 4));
            Add(Item(ItemIds.IronIngot, "Iron Ingot", ItemCategory.Resource, 64, SurfaceFamily.Metal, ColIronPlate, 9));
            Add(Item(ItemIds.ScrapMetal, "Scrap Metal", ItemCategory.Resource, 64, SurfaceFamily.Metal, ColRust, 3));
            Add(Item(ItemIds.PlantFibre, "Plant Fibre", ItemCategory.Resource, 64, SurfaceFamily.Foliage, ColNeedle, 1));
            Add(Item(ItemIds.Cloth, "Cloth", ItemCategory.Resource, 64, SurfaceFamily.Cloth, new Color(0.64f, 0.60f, 0.52f), 4));

            var coal = map[ItemIds.Coal];
            coal.fuelSeconds = 80f;
            map[ItemIds.WoodLog].fuelSeconds = 24f;

            var food = Item(ItemIds.CannedFood, "Canned Food", ItemCategory.Consumable, 16, SurfaceFamily.Metal, new Color(0.55f, 0.48f, 0.30f), 12);
            food.foodRestore = 35f;
            food.waterRestore = 4f;
            Add(food);

            var water = Item(ItemIds.WaterBottle, "Water Bottle", ItemCategory.Consumable, 16, SurfaceFamily.Cloth, new Color(0.45f, 0.62f, 0.68f), 8);
            water.waterRestore = 45f;
            Add(water);

            var bandage = Item(ItemIds.Bandage, "Bandage", ItemCategory.Consumable, 16, SurfaceFamily.Cloth, new Color(0.80f, 0.76f, 0.70f), 14);
            bandage.healthRestore = 25f;
            Add(bandage);

            Add(Tool(ItemIds.StonePickaxe, "Stone Pickaxe", ToolType.Pickaxe, 1, 2.4f, 7f, 120, ColStone, 18));
            Add(Tool(ItemIds.StoneAxe, "Stone Axe", ToolType.Axe, 1, 2.4f, 9f, 120, ColStone, 18));
            Add(Tool(ItemIds.StoneShovel, "Stone Shovel", ToolType.Shovel, 1, 2.6f, 6f, 110, ColStone, 16));
            Add(Tool(ItemIds.IronPickaxe, "Iron Pickaxe", ToolType.Pickaxe, 2, 4.2f, 10f, 400, ColIronPlate, 65));
            Add(Tool(ItemIds.IronAxe, "Iron Axe", ToolType.Axe, 2, 4.2f, 14f, 400, ColIronPlate, 65));
            Add(Tool(ItemIds.Club, "Reinforced Club", ToolType.Melee, 1, 1.0f, 15f, 220, ColWood, 24));
            Add(Tool(ItemIds.Wrench, "Wrench", ToolType.Wrench, 1, 1.2f, 6f, 300, ColIronPlate, 40));
            Add(Tool(ItemIds.Hammer, "Building Hammer", ToolType.Hammer, 1, 1.0f, 8f, 600, ColWood, 30));
            Add(Tool(ItemIds.Hoe, "Hoe", ToolType.Shovel, 1, 2.0f, 5f, 260, ColWood, 22));

            AddFarmingItems(map);

            // Block items. Each carries the block it places.
            AddBlockItem(map, blocks, ItemIds.BlockWoodFrame, BlockIds.WoodFrame, "Wood Frame", SurfaceFamily.Plank, ColPlank * 0.9f, 2);
            AddBlockItem(map, blocks, ItemIds.BlockPlanks, BlockIds.Planks, "Plank Block", SurfaceFamily.Plank, ColPlank, 3);
            AddBlockItem(map, blocks, ItemIds.BlockCobblestone, BlockIds.Cobblestone, "Cobblestone Block", SurfaceFamily.Stone, ColCobble, 4);
            AddBlockItem(map, blocks, ItemIds.BlockIron, BlockIds.IronBlock, "Iron Plate Block", SurfaceFamily.Metal, ColIronPlate, 22);
            AddBlockItem(map, blocks, ItemIds.BlockSteel, BlockIds.SteelBlock, "Steel Block", SurfaceFamily.Metal, ColSteel, 48);
            AddBlockItem(map, blocks, ItemIds.BlockGlass, BlockIds.Glass, "Scrap Glass Block", SurfaceFamily.Stone, ColGlass, 6);

            // Snap piece items; the structure reference is linked after structures exist.
            Add(Item(ItemIds.PieceStorageBox, "Storage Box", ItemCategory.Structure, 8, SurfaceFamily.Plank, ColPlank * 1.05f, 26));
            Add(Item(ItemIds.PieceWorkbench, "Workbench", ItemCategory.Structure, 4, SurfaceFamily.Plank, ColPlank, 40));
            Add(Item(ItemIds.PieceCampfire, "Campfire", ItemCategory.Structure, 4, SurfaceFamily.Stone, ColStone, 14));
            Add(Item(ItemIds.PieceToolCupboard, "Tool Cupboard", ItemCategory.Structure, 2, SurfaceFamily.Plank, ColPlank, 120));
            Add(Item(ItemIds.PieceBedroll, "Bedroll", ItemCategory.Structure, 2, SurfaceFamily.Cloth, new Color(0.45f, 0.42f, 0.36f), 30));
            Add(Item(ItemIds.PieceFarmPlot, "Farm Plot", ItemCategory.Structure, 16, SurfaceFamily.Dirt, new Color(0.33f, 0.24f, 0.16f), 14));
            Add(Item(ItemIds.PieceSilo, "Grain Bin", ItemCategory.Structure, 2, SurfaceFamily.Metal, new Color(0.55f, 0.53f, 0.49f), 240));

            // Phase 1 economy and vehicle parts.
            Add(Item(ItemIds.TradeToken, "Trade Token", ItemCategory.Misc, 999, SurfaceFamily.Metal, new Color(0.72f, 0.62f, 0.30f), 1));
            Add(Item(ItemIds.EngineBlock, "Salvaged Engine", ItemCategory.Misc, 4, SurfaceFamily.Metal, ColIronPlate, 180));
            Add(Item(ItemIds.Wheel, "Wheel", ItemCategory.Misc, 8, SurfaceFamily.Cloth, new Color(0.16f, 0.16f, 0.17f), 45));

            var gas = Item(ItemIds.GasCan, "Gas Can", ItemCategory.Misc, 16, SurfaceFamily.Metal, new Color(0.56f, 0.22f, 0.16f), 30);
            gas.fuelSeconds = 0f;
            Add(gas);

            Add(Item(ItemIds.BuggyKit, "Scrap Buggy Kit", ItemCategory.Misc, 1, SurfaceFamily.Metal, ColRust, 600));

            return map;
        }

        /// <summary>
        /// Seeds, produce and meals. Cooked food is the reason to farm in Phase 0: it is
        /// the only thing that restores stamina as well as hunger, which is what gets you
        /// through a horde night.
        /// </summary>
        static void AddFarmingItems(Dictionary<string, ItemDefinition> map)
        {
            void Add(ItemDefinition def) { map[def.stringId] = def; }

            var potatoSeed = Item(ItemIds.SeedPotato, "Potato Seed", ItemCategory.Resource, 64, SurfaceFamily.Foliage, new Color(0.55f, 0.46f, 0.30f), 6);
            potatoSeed.description = "Plant in a farm plot.";
            Add(potatoSeed);

            var cornSeed = Item(ItemIds.SeedCorn, "Corn Seed", ItemCategory.Resource, 64, SurfaceFamily.Foliage, new Color(0.66f, 0.58f, 0.26f), 6);
            cornSeed.description = "Plant in a farm plot, or sow a field with it later.";
            Add(cornSeed);

            var wheatSeed = Item(ItemIds.SeedWheat, "Wheat Seed", ItemCategory.Resource, 64, SurfaceFamily.Foliage, new Color(0.70f, 0.63f, 0.34f), 6);
            wheatSeed.description = "Plant in a farm plot, or sow a field with it later.";
            Add(wheatSeed);

            // What everything turns into when nobody eats it. It does not spoil again,
            // and it is the compost the garden will want in Phase 1.
            var rot = Item(ItemIds.Rot, "Rot", ItemCategory.Resource, 64, SurfaceFamily.Dirt,
                new Color(0.32f, 0.28f, 0.20f), 1);
            rot.description = "Spoiled food. Compost, eventually.";
            Add(rot);

            // Shelf lives are in game hours, and a day is 20 real minutes. Raw produce
            // keeps a few days; a cooked meal is the thing you must actually eat or
            // refrigerate, which is what makes the fridge worth its watts.
            var potato = Item(ItemIds.Potato, "Potato", ItemCategory.Consumable, 64, SurfaceFamily.Dirt, new Color(0.60f, 0.48f, 0.30f), 4);
            potato.foodRestore = 8f;
            potato.spoilHours = 96f;
            potato.spoiledInto = rot;
            Add(potato);

            var corn = Item(ItemIds.CornEar, "Corn Ear", ItemCategory.Consumable, 64, SurfaceFamily.Foliage, new Color(0.76f, 0.67f, 0.25f), 4);
            corn.foodRestore = 7f;
            corn.spoilHours = 72f;
            corn.spoiledInto = rot;
            Add(corn);

            // Dry goods keep. Grain in a sack is the reason a silo is worth building.
            var grain = Item(ItemIds.Grain, "Grain", ItemCategory.Resource, 64, SurfaceFamily.Foliage, new Color(0.72f, 0.64f, 0.36f), 3);
            grain.spoilHours = 336f;
            grain.spoiledInto = rot;
            Add(grain);

            var flour = Item(ItemIds.Flour, "Flour", ItemCategory.Resource, 64, SurfaceFamily.Cloth, new Color(0.82f, 0.78f, 0.70f), 6);
            flour.spoilHours = 288f;
            flour.spoiledInto = rot;
            Add(flour);

            var baked = Item(ItemIds.BakedPotato, "Baked Potato", ItemCategory.Consumable, 16, SurfaceFamily.Dirt, new Color(0.66f, 0.50f, 0.28f), 12);
            baked.foodRestore = 30f;
            baked.staminaRestore = 15f;
            baked.spoilHours = 48f;
            baked.spoiledInto = rot;
            Add(baked);

            var bread = Item(ItemIds.CornBread, "Corn Bread", ItemCategory.Consumable, 16, SurfaceFamily.Cloth, new Color(0.78f, 0.66f, 0.38f), 18);
            bread.foodRestore = 42f;
            bread.staminaRestore = 25f;
            bread.spoilHours = 72f;
            bread.spoiledInto = rot;
            Add(bread);

            var stew = Item(ItemIds.VegetableStew, "Vegetable Stew", ItemCategory.Consumable, 8, SurfaceFamily.Metal, new Color(0.54f, 0.42f, 0.24f), 30);
            stew.foodRestore = 60f;
            stew.waterRestore = 20f;
            stew.healthRestore = 10f;
            stew.staminaRestore = 40f;
            stew.spoilHours = 36f;
            stew.spoiledInto = rot;
            Add(stew);

            // Canned food is the one thing that never goes off, which is exactly why it
            // is in the starting kit and why a trader will always take it.
        }

        static void AddBlockItem(Dictionary<string, ItemDefinition> map, Dictionary<string, BlockDefinition> blocks,
                                 string itemId, string blockId, string displayName, SurfaceFamily family, Color tint, int value)
        {
            var def = Item(itemId, displayName, ItemCategory.Block, 64, family, tint, value);
            def.placeableBlock = blocks[blockId];
            map[itemId] = def;
        }

        static void LinkPlacement(Dictionary<string, ItemDefinition> items,
                                  Dictionary<string, BlockDefinition> blocks,
                                  Dictionary<string, StructureDefinition> structures)
        {
            items[ItemIds.PieceStorageBox].placeableStructure = structures[StructureIds.StorageBox];
            items[ItemIds.PieceWorkbench].placeableStructure = structures[StructureIds.Workbench];
            items[ItemIds.PieceCampfire].placeableStructure = structures[StructureIds.Campfire];
            items[ItemIds.PieceToolCupboard].placeableStructure = structures[StructureIds.ToolCupboard];
            items[ItemIds.PieceFarmPlot].placeableStructure = structures[StructureIds.FarmPlot];
            items[ItemIds.PieceSilo].placeableStructure = structures[StructureIds.Silo];
            items[ItemIds.PieceBedroll].placeableStructure = structures[StructureIds.Bedroll];
        }

        static void LinkBlockDrops(Dictionary<string, BlockDefinition> blocks, Dictionary<string, ItemDefinition> items)
        {
            Drop(blocks[BlockIds.Stone], items[ItemIds.Stone], 1, 1);
            Drop(blocks[BlockIds.Dirt], items[ItemIds.Dirt], 1, 1);
            Drop(blocks[BlockIds.Grass], items[ItemIds.Dirt], 1, 1);
            Drop(blocks[BlockIds.Sand], items[ItemIds.Sand], 1, 1);
            Drop(blocks[BlockIds.Gravel], items[ItemIds.Stone], 1, 1);
            Drop(blocks[BlockIds.Clay], items[ItemIds.Clay], 1, 2);
            Drop(blocks[BlockIds.TilledSoil], items[ItemIds.Dirt], 1, 1);
            Drop(blocks[BlockIds.CoalOre], items[ItemIds.Coal], 1, 3);
            Drop(blocks[BlockIds.IronOre], items[ItemIds.IronOre], 1, 3);
            Drop(blocks[BlockIds.PineLog], items[ItemIds.WoodLog], 1, 2);
            Drop(blocks[BlockIds.PineNeedles], items[ItemIds.PlantFibre], 0, 2);
            Drop(blocks[BlockIds.ScrapHeap], items[ItemIds.ScrapMetal], 2, 5);
            Drop(blocks[BlockIds.Concrete], items[ItemIds.Stone], 2, 3);

            // Forage: fibre or produce in hand, and sometimes the seed that starts a garden.
            Drop(blocks[BlockIds.WildYucca], items[ItemIds.PlantFibre], 1, 3);
            SecondDrop(blocks[BlockIds.WildYucca], items[ItemIds.SeedPotato], 0.45f, 1, 2);
            Drop(blocks[BlockIds.WildGrain], items[ItemIds.Grain], 1, 2);
            SecondDrop(blocks[BlockIds.WildGrain], items[ItemIds.SeedWheat], 0.5f, 1, 2);
            Drop(blocks[BlockIds.WildCorn], items[ItemIds.CornEar], 1, 2);
            SecondDrop(blocks[BlockIds.WildCorn], items[ItemIds.SeedCorn], 0.5f, 1, 2);

            Drop(blocks[BlockIds.WoodFrame], items[ItemIds.BlockWoodFrame], 1, 1);
            Drop(blocks[BlockIds.Planks], items[ItemIds.BlockPlanks], 1, 1);
            Drop(blocks[BlockIds.Cobblestone], items[ItemIds.BlockCobblestone], 1, 1);
            Drop(blocks[BlockIds.IronBlock], items[ItemIds.BlockIron], 1, 1);
            Drop(blocks[BlockIds.SteelBlock], items[ItemIds.BlockSteel], 1, 1);
            Drop(blocks[BlockIds.Glass], items[ItemIds.BlockGlass], 1, 1);
        }

        static void Drop(BlockDefinition block, ItemDefinition item, int min, int max)
        {
            block.dropItem = item;
            block.dropMin = min;
            block.dropMax = max;
        }

        static void SecondDrop(BlockDefinition block, ItemDefinition item, float chance, int min, int max)
        {
            block.secondaryDropItem = item;
            block.secondaryDropChance = chance;
            block.secondaryDropMin = min;
            block.secondaryDropMax = max;
        }

        // -------------------------------------------------------------- structures

        static StructureDefinition Structure(string id, string displayName, StructureKind kind, Vector3Int footprint,
                                             SurfaceFamily family, Color tint, float health)
        {
            var def = ScriptableObject.CreateInstance<StructureDefinition>();
            def.name = ShortName(id);
            def.stringId = id;
            def.displayName = displayName;
            def.kind = kind;
            def.footprint = footprint;
            def.surfaceFamily = family;
            def.tint = tint;
            def.maxHealth = health;
            return def;
        }

        static Dictionary<string, StructureDefinition> BuildStructures(Dictionary<string, ItemDefinition> items)
        {
            var map = new Dictionary<string, StructureDefinition>();

            var box = Structure(StructureIds.StorageBox, "Storage Box", StructureKind.Storage, Vector3Int.one, SurfaceFamily.Plank, ColPlank * 1.05f, 180f);
            box.storageSlots = 24;
            box.salvageItem = items[ItemIds.PieceStorageBox];
            map[box.stringId] = box;

            var bench = Structure(StructureIds.Workbench, "Workbench", StructureKind.CraftStation, Vector3Int.one, SurfaceFamily.Plank, ColPlank, 220f);
            bench.craftStation = CraftStation.Workbench;
            bench.salvageItem = items[ItemIds.PieceWorkbench];
            map[bench.stringId] = bench;

            var fire = Structure(StructureIds.Campfire, "Campfire", StructureKind.Campfire, Vector3Int.one, SurfaceFamily.Wood, ColWood, 90f);
            fire.craftStation = CraftStation.Campfire;
            fire.lightRange = 11f;
            fire.salvageItem = items[ItemIds.PieceCampfire];
            map[fire.stringId] = fire;

            var cupboard = Structure(StructureIds.ToolCupboard, "Tool Cupboard", StructureKind.ToolCupboard, new Vector3Int(1, 2, 1), SurfaceFamily.Plank, ColPlank, 900f);
            cupboard.claimRadius = 24f;
            cupboard.salvageItem = items[ItemIds.PieceToolCupboard];
            map[cupboard.stringId] = cupboard;

            var bedroll = Structure(StructureIds.Bedroll, "Bedroll", StructureKind.Bedroll, Vector3Int.one, SurfaceFamily.Cloth, new Color(0.45f, 0.42f, 0.36f), 60f);
            bedroll.blocksMovement = false;
            bedroll.salvageItem = items[ItemIds.PieceBedroll];
            map[bedroll.stringId] = bedroll;

            // The garden. Low health on purpose: a horde that reaches your plots should
            // cost you dinner.
            var plot = Structure(StructureIds.FarmPlot, "Farm Plot", StructureKind.FarmPlot, Vector3Int.one, SurfaceFamily.Dirt, new Color(0.30f, 0.22f, 0.15f), 70f);
            plot.requiresSoil = true;
            plot.blocksMovement = false;
            plot.salvageItem = items[ItemIds.PieceFarmPlot];
            map[plot.stringId] = plot;

            var silo = Structure(StructureIds.Silo, "Grain Bin", StructureKind.Silo, new Vector3Int(2, 4, 2), SurfaceFamily.Metal, new Color(0.55f, 0.53f, 0.49f), 600f);
            silo.siloCapacityLitres = 20000f;
            silo.salvageItem = items[ItemIds.PieceSilo];
            map[silo.stringId] = silo;

            // Not craftable: spawned by the death handler to hold a dropped bag.
            var backpack = Structure(StructureIds.DeathBackpack, "Backpack", StructureKind.Storage, Vector3Int.one, SurfaceFamily.Cloth, new Color(0.30f, 0.27f, 0.22f), 1000f);
            backpack.storageSlots = 27;
            backpack.requiresSupport = false;
            map[backpack.stringId] = backpack;

            return map;
        }

        // ----------------------------------------------------------------- recipes

        static RecipeIngredient Ing(ItemDefinition item, int count)
        {
            return new RecipeIngredient { item = item, count = count };
        }

        static RecipeDefinition Recipe(string id, ItemDefinition output, int count, CraftStation station,
                                       float seconds, params RecipeIngredient[] ingredients)
        {
            var def = ScriptableObject.CreateInstance<RecipeDefinition>();
            def.name = "Recipe_" + ShortName(id);
            def.stringId = id;
            def.output = output;
            def.outputCount = count;
            def.station = station;
            def.craftSeconds = seconds;
            def.ingredients.AddRange(ingredients);
            return def;
        }

        static List<RecipeDefinition> BuildRecipes(Dictionary<string, ItemDefinition> it)
        {
            var list = new List<RecipeDefinition>();

            list.Add(Recipe("madvoxel:craft_plank", it[ItemIds.Plank], 4, CraftStation.Hand, 0.8f,
                Ing(it[ItemIds.WoodLog], 1)));
            list.Add(Recipe("madvoxel:craft_plant_cloth", it[ItemIds.Cloth], 1, CraftStation.Hand, 1.2f,
                Ing(it[ItemIds.PlantFibre], 6)));
            list.Add(Recipe("madvoxel:craft_bandage", it[ItemIds.Bandage], 1, CraftStation.Hand, 1.2f,
                Ing(it[ItemIds.Cloth], 2), Ing(it[ItemIds.PlantFibre], 2)));

            list.Add(Recipe("madvoxel:craft_stone_pickaxe", it[ItemIds.StonePickaxe], 1, CraftStation.Hand, 2.5f,
                Ing(it[ItemIds.WoodLog], 1), Ing(it[ItemIds.Stone], 3), Ing(it[ItemIds.PlantFibre], 2)));
            list.Add(Recipe("madvoxel:craft_stone_axe", it[ItemIds.StoneAxe], 1, CraftStation.Hand, 2.5f,
                Ing(it[ItemIds.WoodLog], 1), Ing(it[ItemIds.Stone], 3), Ing(it[ItemIds.PlantFibre], 2)));
            list.Add(Recipe("madvoxel:craft_stone_shovel", it[ItemIds.StoneShovel], 1, CraftStation.Hand, 2.5f,
                Ing(it[ItemIds.WoodLog], 1), Ing(it[ItemIds.Stone], 2), Ing(it[ItemIds.PlantFibre], 2)));
            list.Add(Recipe("madvoxel:craft_club", it[ItemIds.Club], 1, CraftStation.Hand, 2.0f,
                Ing(it[ItemIds.WoodLog], 2), Ing(it[ItemIds.ScrapMetal], 2), Ing(it[ItemIds.PlantFibre], 2)));

            // The garden has to be reachable on day one, so the plot and the hoe are
            // hand recipes from what a forager already has.
            list.Add(Recipe("madvoxel:craft_farm_plot", it[ItemIds.PieceFarmPlot], 1, CraftStation.Hand, 2f,
                Ing(it[ItemIds.Plank], 4), Ing(it[ItemIds.PlantFibre], 4)));
            list.Add(Recipe("madvoxel:craft_hoe", it[ItemIds.Hoe], 1, CraftStation.Hand, 2.5f,
                Ing(it[ItemIds.WoodLog], 1), Ing(it[ItemIds.Stone], 2), Ing(it[ItemIds.PlantFibre], 2)));

            list.Add(Recipe("madvoxel:craft_hammer", it[ItemIds.Hammer], 1, CraftStation.Hand, 2.5f,
                Ing(it[ItemIds.WoodLog], 2), Ing(it[ItemIds.Stone], 2), Ing(it[ItemIds.PlantFibre], 2)));

            list.Add(Recipe("madvoxel:craft_campfire", it[ItemIds.PieceCampfire], 1, CraftStation.Hand, 3f,
                Ing(it[ItemIds.Stone], 8), Ing(it[ItemIds.WoodLog], 3)));
            list.Add(Recipe("madvoxel:craft_workbench", it[ItemIds.PieceWorkbench], 1, CraftStation.Hand, 4f,
                Ing(it[ItemIds.Plank], 10), Ing(it[ItemIds.Stone], 4)));
            list.Add(Recipe("madvoxel:craft_bedroll", it[ItemIds.PieceBedroll], 1, CraftStation.Hand, 3f,
                Ing(it[ItemIds.Cloth], 6), Ing(it[ItemIds.PlantFibre], 6)));

            list.Add(Recipe("madvoxel:craft_block_frame", it[ItemIds.BlockWoodFrame], 4, CraftStation.Hand, 0.8f,
                Ing(it[ItemIds.Plank], 2)));
            list.Add(Recipe("madvoxel:craft_block_planks", it[ItemIds.BlockPlanks], 2, CraftStation.Hand, 0.9f,
                Ing(it[ItemIds.Plank], 4)));
            list.Add(Recipe("madvoxel:craft_block_cobble", it[ItemIds.BlockCobblestone], 4, CraftStation.Hand, 1.0f,
                Ing(it[ItemIds.Stone], 4)));

            // Smelting happens at the campfire in Phase 0; the forge arrives in Phase 2.
            list.Add(Recipe("madvoxel:smelt_iron", it[ItemIds.IronIngot], 1, CraftStation.Campfire, 6f,
                Ing(it[ItemIds.IronOre], 2), Ing(it[ItemIds.Coal], 1)));
            list.Add(Recipe("madvoxel:smelt_scrap", it[ItemIds.IronIngot], 1, CraftStation.Campfire, 5f,
                Ing(it[ItemIds.ScrapMetal], 5), Ing(it[ItemIds.Coal], 1)));
            // Cooking. Meals are the only food that restores stamina, which is what makes
            // the garden matter the night before a blood moon.
            list.Add(Recipe("madvoxel:cook_baked_potato", it[ItemIds.BakedPotato], 2, CraftStation.Campfire, 4f,
                Ing(it[ItemIds.Potato], 3)));
            list.Add(Recipe("madvoxel:mill_flour", it[ItemIds.Flour], 2, CraftStation.Campfire, 3f,
                Ing(it[ItemIds.Grain], 4)));
            list.Add(Recipe("madvoxel:cook_corn_bread", it[ItemIds.CornBread], 2, CraftStation.Campfire, 5f,
                Ing(it[ItemIds.Flour], 2), Ing(it[ItemIds.CornEar], 2)));
            list.Add(Recipe("madvoxel:cook_stew", it[ItemIds.VegetableStew], 1, CraftStation.Campfire, 6f,
                Ing(it[ItemIds.Potato], 3), Ing(it[ItemIds.CornEar], 2), Ing(it[ItemIds.WaterBottle], 1)));

            list.Add(Recipe("madvoxel:smelt_glass", it[ItemIds.BlockGlass], 2, CraftStation.Campfire, 4f,
                Ing(it[ItemIds.Sand], 3), Ing(it[ItemIds.Coal], 1)));

            list.Add(Recipe("madvoxel:craft_storage_box", it[ItemIds.PieceStorageBox], 1, CraftStation.Workbench, 4f,
                Ing(it[ItemIds.Plank], 12), Ing(it[ItemIds.IronIngot], 1)));
            list.Add(Recipe("madvoxel:craft_tool_cupboard", it[ItemIds.PieceToolCupboard], 1, CraftStation.Workbench, 6f,
                Ing(it[ItemIds.Plank], 16), Ing(it[ItemIds.IronIngot], 2), Ing(it[ItemIds.Cloth], 2)));

            list.Add(Recipe("madvoxel:craft_wrench", it[ItemIds.Wrench], 1, CraftStation.Workbench, 4f,
                Ing(it[ItemIds.IronIngot], 3), Ing(it[ItemIds.Plank], 1)));
            list.Add(Recipe("madvoxel:craft_iron_pickaxe", it[ItemIds.IronPickaxe], 1, CraftStation.Workbench, 5f,
                Ing(it[ItemIds.IronIngot], 4), Ing(it[ItemIds.Plank], 2)));
            list.Add(Recipe("madvoxel:craft_iron_axe", it[ItemIds.IronAxe], 1, CraftStation.Workbench, 5f,
                Ing(it[ItemIds.IronIngot], 4), Ing(it[ItemIds.Plank], 2)));
            list.Add(Recipe("madvoxel:craft_block_iron", it[ItemIds.BlockIron], 1, CraftStation.Workbench, 3f,
                Ing(it[ItemIds.IronIngot], 4)));

            // The grain bin is the field layer's destination, so it is gated on Agronomist.
            var silo = Recipe("madvoxel:craft_silo", it[ItemIds.PieceSilo], 1, CraftStation.Workbench, 12f,
                Ing(it[ItemIds.IronIngot], 14), Ing(it[ItemIds.ScrapMetal], 30), Ing(it[ItemIds.Plank], 12));
            silo.unlockedByDefault = false;
            silo.requiredPerkId = PerkIds.Agronomist;
            silo.requiredPerkRank = 1;
            list.Add(silo);

            // Locked behind skills. Phase 1 turns the ranks into unlocks; the data is real now.
            var steel = Recipe("madvoxel:craft_block_steel", it[ItemIds.BlockSteel], 1, CraftStation.Workbench, 6f,
                Ing(it[ItemIds.BlockIron], 1), Ing(it[ItemIds.Coal], 4));
            steel.unlockedByDefault = false;
            steel.requiredPerkId = "madvoxel:perk_carpenter";
            steel.requiredPerkRank = 3;
            list.Add(steel);

            var buggy = Recipe("madvoxel:craft_buggy_kit", it[ItemIds.BuggyKit], 1, CraftStation.Workbench, 12f,
                Ing(it[ItemIds.EngineBlock], 1), Ing(it[ItemIds.Wheel], 4),
                Ing(it[ItemIds.IronIngot], 20), Ing(it[ItemIds.ScrapMetal], 40));
            buggy.unlockedByDefault = false;
            buggy.requiredPerkId = "madvoxel:perk_grease_monkey";
            buggy.requiredPerkRank = 1;
            list.Add(buggy);

            return list;
        }

        // ----------------------------------------------------------------- zombies

        static List<ZombieDefinition> BuildZombies(Dictionary<string, ItemDefinition> items)
        {
            var shambler = ScriptableObject.CreateInstance<ZombieDefinition>();
            shambler.name = "Zombie_Shambler";
            shambler.stringId = ZombieIds.Shambler;
            shambler.displayName = "Shambler";
            shambler.maxHealth = 55f;
            shambler.walkSpeed = 1.5f;
            shambler.chaseSpeed = 3.2f;
            shambler.meleeDamage = 9f;
            shambler.digDamagePerSecond = 22f;
            shambler.xpReward = 18f;
            shambler.tint = new Color(0.36f, 0.42f, 0.30f);
            shambler.dropItem = items[ItemIds.Cloth];
            shambler.dropMin = 0;
            shambler.dropMax = 2;

            var brute = ScriptableObject.CreateInstance<ZombieDefinition>();
            brute.name = "Zombie_Brute";
            brute.stringId = ZombieIds.Brute;
            brute.displayName = "Brute";
            brute.maxHealth = 170f;
            brute.walkSpeed = 1.3f;
            brute.chaseSpeed = 2.8f;
            brute.meleeDamage = 20f;
            brute.attackInterval = 1.6f;
            brute.digDamagePerSecond = 55f;
            brute.sightRange = 30f;
            brute.xpReward = 60f;
            brute.height = 2.15f;
            brute.tint = new Color(0.30f, 0.26f, 0.22f);
            brute.dropItem = items[ItemIds.ScrapMetal];
            brute.dropMin = 1;
            brute.dropMax = 4;

            return new List<ZombieDefinition> { shambler, brute };
        }

        static HordeSchedule BuildHordeSchedule(List<ZombieDefinition> zombies)
        {
            var schedule = ScriptableObject.CreateInstance<HordeSchedule>();
            schedule.name = "HordeSchedule";
            schedule.baseZombie = zombies[0];
            schedule.heavyZombie = zombies.Count > 1 ? zombies[1] : null;
            return schedule;
        }

        // ------------------------------------------------------------------ skills

        static PerkDefinition Perk(string id, string name, PerkCategory category, string description,
                                     int maxRank, int requiredLevel, params PerkEffect[] effects)
        {
            var def = ScriptableObject.CreateInstance<PerkDefinition>();
            def.name = "Perk_" + ShortName(id);
            def.stringId = id;
            def.displayName = name;
            def.category = category;
            def.description = description;
            def.maxRank = maxRank;
            def.requiredPlayerLevel = requiredLevel;
            def.effects.AddRange(effects);
            return def;
        }

        static PerkEffect Effect(PerkEffectType type, float perRank)
        {
            return new PerkEffect { type = type, valuePerRank = perRank };
        }

        static PerkTreeDefinition BuildPerkTree()
        {
            var tree = ScriptableObject.CreateInstance<PerkTreeDefinition>();
            tree.name = "PerkTree";

            tree.perks.Add(Perk("madvoxel:perk_miner", "Miner 69er", PerkCategory.Mining,
                "Swing faster on stone, ore and scrap.", 5, 1,
                Effect(PerkEffectType.MiningSpeedMultiplier, 0.12f)));

            tree.perks.Add(Perk("madvoxel:perk_motherlode", "Motherlode", PerkCategory.Mining,
                "Ore and scrap yield more per block.", 5, 4,
                Effect(PerkEffectType.HarvestYieldMultiplier, 0.15f)));

            var carpenter = Perk("madvoxel:perk_carpenter", "Carpenter", PerkCategory.Construction,
                "Unlocks sturdier building blocks, ending in steel.", 4, 1,
                Effect(PerkEffectType.BlockTierUnlock, 1f));
            carpenter.unlocksRecipeIds.Add("madvoxel:craft_block_cobble");
            carpenter.unlocksRecipeIds.Add("madvoxel:craft_block_iron");
            carpenter.unlocksRecipeIds.Add("madvoxel:craft_block_steel");
            tree.perks.Add(carpenter);

            tree.perks.Add(Perk("madvoxel:perk_handyman", "Handyman", PerkCategory.Construction,
                "Repair and upgrade structures faster.", 3, 3,
                Effect(PerkEffectType.RepairSpeedMultiplier, 0.2f)));

            tree.perks.Add(Perk("madvoxel:perk_heavy_hitter", "Heavy Hitter", PerkCategory.Combat,
                "More damage with clubs, axes and anything heavy.", 5, 1,
                Effect(PerkEffectType.MeleeDamageMultiplier, 0.1f)));

            tree.perks.Add(Perk("madvoxel:perk_iron_lungs", "Iron Lungs", PerkCategory.Combat,
                "Sprint and swing longer before your stamina gives out.", 4, 2,
                Effect(PerkEffectType.MaxStaminaBonus, 12f),
                Effect(PerkEffectType.StaminaDrainMultiplier, -0.08f)));

            tree.perks.Add(Perk("madvoxel:perk_scrapper", "Scrapper", PerkCategory.Scavenging,
                "Pull more out of wrecks, heaps and containers.", 5, 1,
                Effect(PerkEffectType.LootQuantityMultiplier, 0.14f)));

            tree.perks.Add(Perk("madvoxel:perk_pack_mule", "Pack Mule", PerkCategory.Scavenging,
                "Carry heavier loads without slowing down.", 3, 5,
                Effect(PerkEffectType.StaminaDrainMultiplier, -0.06f)));

            tree.perks.Add(Perk("madvoxel:perk_field_medic", "Field Medic", PerkCategory.Medicine,
                "Bandages and food do more for you.", 4, 1,
                Effect(PerkEffectType.HealingMultiplier, 0.2f)));

            var physician = Perk("madvoxel:perk_physician", "Physician", PerkCategory.Medicine,
                "Craft better medical supplies.", 3, 6,
                Effect(PerkEffectType.HealingMultiplier, 0.1f));
            physician.unlocksRecipeIds.Add("madvoxel:craft_bandage");
            tree.perks.Add(physician);

            var grease = Perk("madvoxel:perk_grease_monkey", "Grease Monkey", PerkCategory.Vehicles,
                "Build and maintain vehicles.", 3, 5,
                Effect(PerkEffectType.RepairSpeedMultiplier, 0.15f));
            grease.unlocksRecipeIds.Add("madvoxel:craft_buggy_kit");
            tree.perks.Add(grease);

            var farming = Perk(PerkIds.Farming, "Living Off The Land", PerkCategory.Farming,
                "Garden plots yield more, seeds come back more often, and dry spells bite less.", 5, 1,
                Effect(PerkEffectType.HarvestYieldMultiplier, 0.15f),
                Effect(PerkEffectType.DroughtResistance, 0.12f));
            farming.unlocksRecipeIds.Add("madvoxel:craft_farm_plot");
            farming.unlocksRecipeIds.Add("madvoxel:craft_hoe");
            farming.unlocksRecipeIds.Add("madvoxel:craft_sprinkler");
            tree.perks.Add(farming);

            // Quiet Claim: the answer to a farm that has become a beacon.
            tree.perks.Add(Perk("madvoxel:perk_quiet_claim", "Quiet Claim", PerkCategory.Farming,
                "Hedges, baffles and habit. Your claim draws less attention after dark.", 3, 4,
                Effect(PerkEffectType.ClaimHeatReduction, 0.14f)));

            var agronomist = Perk(PerkIds.Agronomist, "Agronomist", PerkCategory.Farming,
                "Unlocks field implements and lifts the yield an acre returns.", 3, 6,
                Effect(PerkEffectType.FieldYieldMultiplier, 0.12f));
            agronomist.unlocksRecipeIds.Add("madvoxel:craft_silo");
            tree.perks.Add(agronomist);

            // The grid perk. Rank 1 is the battery bank, because the first thing anyone
            // wants after a generator is for the lights to survive until morning.
            var electrician = Perk(PerkIds.Electrician, "Electrician", PerkCategory.Electricity,
                "Longer runs, tougher fittings, and the banks that carry a night.", 3, 3,
                Effect(PerkEffectType.WireLengthBonus, 4f),
                Effect(PerkEffectType.StormResistance, 0.2f));
            electrician.unlocksRecipeIds.Add("madvoxel:craft_battery_bank");
            electrician.unlocksRecipeIds.Add("madvoxel:craft_solar_bank");
            electrician.unlocksRecipeIds.Add("madvoxel:craft_fridge");
            tree.perks.Add(electrician);

            tree.perks.Add(Perk("madvoxel:perk_millwright", "Millwright", PerkCategory.Electricity,
                "Pumps pull harder and traps bite deeper.", 4, 5,
                Effect(PerkEffectType.PumpRateMultiplier, 0.12f),
                Effect(PerkEffectType.TrapDamageMultiplier, 0.15f)));

            tree.perks.Add(Perk("madvoxel:perk_economiser", "Economiser", PerkCategory.Vehicles,
                "Squeeze more distance out of every gas can.", 3, 7,
                Effect(PerkEffectType.VehicleFuelEfficiency, 0.15f)));

            return tree;
        }

        // ------------------------------------------------------------------ quests

        static QuestReward Reward(ItemDefinition item, int count)
        {
            return new QuestReward { item = item, count = count };
        }

        static List<QuestDefinition> BuildQuests(Dictionary<string, ItemDefinition> it)
        {
            var list = new List<QuestDefinition>();

            var fetch = ScriptableObject.CreateInstance<QuestDefinition>();
            fetch.name = "Quest_ScrapRun";
            fetch.stringId = "madvoxel:quest_scrap_run";
            fetch.title = "Scrap Run";
            fetch.description = "The forge eats metal faster than I can find it. Bring me scrap, I will not ask where from.";
            fetch.kind = QuestKind.Fetch;
            fetch.objectiveItem = it[ItemIds.ScrapMetal];
            fetch.objectiveCount = 20;
            fetch.xpReward = 140f;
            fetch.reputationReward = 100;
            fetch.rewards.Add(Reward(it[ItemIds.TradeToken], 120));
            fetch.rewards.Add(Reward(it[ItemIds.IronIngot], 4));
            list.Add(fetch);

            var clear = ScriptableObject.CreateInstance<QuestDefinition>();
            clear.name = "Quest_ThinTheHerd";
            clear.stringId = "madvoxel:quest_thin_the_herd";
            clear.title = "Thin the Herd";
            clear.description = "Twelve of them between here and the old farm. Make it zero and come back.";
            clear.kind = QuestKind.Clear;
            clear.objectiveZombieId = ZombieIds.Shambler;
            clear.objectiveCount = 12;
            clear.requiredPlayerLevel = 2;
            clear.xpReward = 220f;
            clear.reputationReward = 150;
            clear.rewards.Add(Reward(it[ItemIds.TradeToken], 200));
            clear.rewards.Add(Reward(it[ItemIds.Bandage], 3));
            list.Add(clear);

            var survive = ScriptableObject.CreateInstance<QuestDefinition>();
            survive.name = "Quest_TwoNightsStanding";
            survive.stringId = "madvoxel:quest_two_nights_standing";
            survive.title = "Two Nights Standing";
            survive.description = "Keep your claim and your skin for two nights. Prove you are worth stocking for.";
            survive.kind = QuestKind.SurviveNights;
            survive.objectiveCount = 2;
            survive.requiredPlayerLevel = 3;
            survive.requiredReputationTier = 1;
            survive.xpReward = 380f;
            survive.reputationReward = 250;
            survive.rewards.Add(Reward(it[ItemIds.TradeToken], 350));
            survive.rewards.Add(Reward(it[ItemIds.IronPickaxe], 1));
            list.Add(survive);

            var garden = ScriptableObject.CreateInstance<QuestDefinition>();
            garden.name = "Quest_FirstHarvest";
            garden.stringId = "madvoxel:quest_first_harvest";
            garden.title = "First Harvest";
            garden.description = "Anyone can loot a can. Bring me food you grew yourself and I will take you seriously.";
            garden.kind = QuestKind.Fetch;
            garden.objectiveItem = it[ItemIds.Potato];
            garden.objectiveCount = 12;
            garden.xpReward = 160f;
            garden.reputationReward = 120;
            garden.rewards.Add(Reward(it[ItemIds.TradeToken], 140));
            garden.rewards.Add(Reward(it[ItemIds.SeedCorn], 8));
            garden.rewards.Add(Reward(it[ItemIds.PieceFarmPlot], 4));
            list.Add(garden);

            // Phase 1: the field layer measures in litres, so this one turns in bulk.
            var bulk = ScriptableObject.CreateInstance<QuestDefinition>();
            bulk.name = "Quest_GrainDelivery";
            bulk.stringId = "madvoxel:quest_grain_delivery";
            bulk.title = "Grain Delivery";
            bulk.description = "The town mill pays by the litre. Plow an acre, fill a bin, and I will move it for you.";
            bulk.kind = QuestKind.DeliverLitres;
            bulk.objectiveItem = it[ItemIds.Grain];
            bulk.objectiveCount = 2000;
            bulk.objectiveCropId = "madvoxel:crop_wheat";
            bulk.requiredPlayerLevel = 6;
            bulk.requiredReputationTier = 1;
            bulk.xpReward = 520f;
            bulk.reputationReward = 300;
            bulk.rewards.Add(Reward(it[ItemIds.TradeToken], 600));
            bulk.rewards.Add(Reward(it[ItemIds.GasCan], 10));
            list.Add(bulk);

            return list;
        }

        // ----------------------------------------------------------------- traders

        static TraderStockEntry Stock(ItemDefinition item, int count, float multiplier, int minTier)
        {
            return new TraderStockEntry { item = item, stockCount = count, priceMultiplier = multiplier, minReputationTier = minTier };
        }

        static List<TraderDefinition> BuildTraders(Dictionary<string, ItemDefinition> it, List<QuestDefinition> quests)
        {
            var vance = ScriptableObject.CreateInstance<TraderDefinition>();
            vance.name = "Trader_Vance";
            vance.stringId = "madvoxel:trader_vance";
            vance.displayName = "Trader Vance";
            vance.greeting = "Tokens on the counter. No credit, no crying.";
            vance.currencyItem = it[ItemIds.TradeToken];
            vance.outpostGridSpacing = 700;
            vance.outpostVariant = 0;
            vance.stock.Add(Stock(it[ItemIds.CannedFood], 12, 1.4f, 0));
            vance.stock.Add(Stock(it[ItemIds.WaterBottle], 12, 1.3f, 0));
            vance.stock.Add(Stock(it[ItemIds.Bandage], 8, 1.5f, 0));
            vance.stock.Add(Stock(it[ItemIds.IronIngot], 20, 1.6f, 1));
            vance.stock.Add(Stock(it[ItemIds.IronPickaxe], 1, 1.9f, 2));
            vance.stock.Add(Stock(it[ItemIds.Wheel], 4, 1.7f, 2));
            vance.stock.Add(Stock(it[ItemIds.EngineBlock], 1, 2.2f, 3));
            vance.stock.Add(Stock(it[ItemIds.GasCan], 10, 1.5f, 1));
            vance.stock.Add(Stock(it[ItemIds.SeedCorn], 24, 1.4f, 0));
            vance.stock.Add(Stock(it[ItemIds.SeedWheat], 24, 1.4f, 0));
            vance.questBoard.AddRange(quests);

            var mara = ScriptableObject.CreateInstance<TraderDefinition>();
            mara.name = "Trader_Mara";
            mara.stringId = "madvoxel:trader_mara";
            mara.displayName = "Trader Mara";
            mara.greeting = "Tools and timber. Cheaper than losing a hand to a rusty axe.";
            mara.currencyItem = it[ItemIds.TradeToken];
            mara.outpostGridSpacing = 700;
            mara.outpostVariant = 1;
            mara.stock.Add(Stock(it[ItemIds.Plank], 64, 1.3f, 0));
            mara.stock.Add(Stock(it[ItemIds.StonePickaxe], 2, 1.4f, 0));
            mara.stock.Add(Stock(it[ItemIds.StoneAxe], 2, 1.4f, 0));
            mara.stock.Add(Stock(it[ItemIds.Wrench], 1, 1.8f, 1));
            mara.stock.Add(Stock(it[ItemIds.PieceStorageBox], 3, 1.6f, 1));
            mara.stock.Add(Stock(it[ItemIds.PieceToolCupboard], 1, 2.0f, 2));
            mara.stock.Add(Stock(it[ItemIds.PieceFarmPlot], 8, 1.5f, 0));
            mara.stock.Add(Stock(it[ItemIds.SeedPotato], 24, 1.4f, 0));
            mara.stock.Add(Stock(it[ItemIds.Hoe], 2, 1.5f, 0));

            return new List<TraderDefinition> { vance, mara };
        }

        // ---------------------------------------------------------------- vehicles

        static List<VehicleDefinition> BuildVehicles(Dictionary<string, ItemDefinition> it)
        {
            var buggy = ScriptableObject.CreateInstance<VehicleDefinition>();
            buggy.name = "Vehicle_ScrapBuggy";
            buggy.stringId = "madvoxel:vehicle_scrap_buggy";
            buggy.displayName = "Scrap Buggy";
            buggy.maxSpeed = 17f;
            buggy.acceleration = 8f;
            buggy.turnRate = 95f;
            buggy.fuelCapacity = 100f;
            buggy.fuelPerSecond = 0.55f;
            buggy.fuelItem = it[ItemIds.GasCan];
            buggy.fuelPerItem = 25f;
            buggy.seats = 1;
            buggy.storageSlots = 18;
            buggy.maxHealth = 420f;
            buggy.tint = new Color(0.44f, 0.31f, 0.22f);

            return new List<VehicleDefinition> { buggy };
        }


        // ------------------------------------------------------------------ crops

        static CropDefinition Crop(string id, string name, ItemDefinition seed, ItemDefinition harvest,
                                   float days, bool replants, bool onField, Color tint, float height,
                                   int min, int max, float litres)
        {
            var def = ScriptableObject.CreateInstance<CropDefinition>();
            def.name = "Crop_" + ShortName(id);
            def.stringId = id;
            def.displayName = name;
            def.seedItem = seed;
            def.harvestItem = harvest;
            def.daysToMature = days;
            def.replants = replants;
            def.growsOnPlot = true;
            def.growsOnField = onField;
            def.plantTint = tint;
            def.matureHeight = height;
            def.harvestMin = min;
            def.harvestMax = max;
            def.litresPerCell = litres;
            return def;
        }

        static List<CropDefinition> BuildCrops(Dictionary<string, ItemDefinition> it)
        {
            var list = new List<CropDefinition>();

            // Potato: quick, garden-only, and it eats the plant - you replant from the
            // seeds it returns. The staple that gets you through week one.
            var potato = Crop("madvoxel:crop_potato", "Potato", it[ItemIds.SeedPotato], it[ItemIds.Potato],
                1.5f, false, false, new Color(0.30f, 0.46f, 0.22f), 0.55f, 2, 4, 0f);
            potato.seedReturnChance = 0.75f;
            potato.xpPerHarvest = 8f;
            // Hardy. It sulks on the shelf and on hardpan but it will not refuse.
            potato.biomeYields.Add(BiomeYield(BiomeId.Farmland, 1.15f));
            potato.biomeYields.Add(BiomeYield(BiomeId.PineScrub, 1.0f));
            potato.biomeYields.Add(BiomeYield(BiomeId.ClayHills, 0.8f));
            potato.biomeYields.Add(BiomeYield(BiomeId.DryFlats, 0.7f));
            potato.biomeYields.Add(BiomeYield(BiomeId.FrostShelf, 0.8f));
            list.Add(potato);

            // Corn: slower, taller, keeps growing after a pick, and scales to a field.
            var corn = Crop("madvoxel:crop_corn", "Corn", it[ItemIds.SeedCorn], it[ItemIds.CornEar],
                2.5f, true, true, new Color(0.34f, 0.50f, 0.20f), 1.35f, 1, 3, 14f);
            corn.xpPerHarvest = 11f;
            // Thirsty and tall. The flats and the shelf are simply not corn country.
            corn.biomeYields.Add(BiomeYield(BiomeId.Farmland, 1.2f));
            corn.biomeYields.Add(BiomeYield(BiomeId.PineScrub, 0.8f));
            corn.biomeYields.Add(BiomeYield(BiomeId.ClayHills, 0.7f));
            corn.biomeYields.Add(BiomeYield(BiomeId.DryFlats, 0.4f));
            corn.biomeYields.Add(Blocked(BiomeId.FrostShelf));
            list.Add(corn);

            // Wheat: the bulk crop. Modest in a plot, the point of an acre.
            var wheat = Crop("madvoxel:crop_wheat", "Wheat", it[ItemIds.SeedWheat], it[ItemIds.Grain],
                2f, false, true, new Color(0.56f, 0.53f, 0.24f), 0.95f, 2, 4, 18f);
            wheat.seedReturnChance = 0.8f;
            wheat.seedReturnMax = 3;
            wheat.xpPerHarvest = 9f;
            // The bulk crop, and the one that makes the dry flats a water problem: it
            // grows there, badly, until you put a sprinkler on it.
            wheat.biomeYields.Add(BiomeYield(BiomeId.Farmland, 1.25f));
            wheat.biomeYields.Add(BiomeYield(BiomeId.PineScrub, 0.75f));
            wheat.biomeYields.Add(BiomeYield(BiomeId.ClayHills, 0.7f));
            wheat.biomeYields.Add(BiomeYield(BiomeId.DryFlats, 0.5f));
            wheat.biomeYields.Add(BiomeYield(BiomeId.FrostShelf, 0.45f));
            list.Add(wheat);

            return list;
        }

        // ------------------------------------------------------------ snap pieces

        struct SnapSpec
        {
            public BuildPieceKind Kind;
            public string Name;
            public BuildSlot Slot;
            public bool RestsOnTerrain;
            public bool Blocks;
            public bool RequiresHost;
            public BuildPieceKind HostKind;
            public float WoodHealth;
            public string ItemId;
            public int PlankCost;
        }

        static readonly SnapSpec[] SnapSet =
        {
            new SnapSpec { Kind = BuildPieceKind.Foundation, Name = "Foundation", Slot = BuildSlot.Floor,
                           RestsOnTerrain = true, Blocks = true, WoodHealth = 280f, ItemId = ItemIds.SnapFoundation, PlankCost = 10 },
            new SnapSpec { Kind = BuildPieceKind.Floor, Name = "Floor", Slot = BuildSlot.Floor,
                           Blocks = true, WoodHealth = 220f, ItemId = ItemIds.SnapFloor, PlankCost = 8 },
            new SnapSpec { Kind = BuildPieceKind.Wall, Name = "Wall", Slot = BuildSlot.Wall,
                           Blocks = true, WoodHealth = 240f, ItemId = ItemIds.SnapWall, PlankCost = 7 },
            new SnapSpec { Kind = BuildPieceKind.WindowWall, Name = "Window Wall", Slot = BuildSlot.Wall,
                           Blocks = true, WoodHealth = 200f, ItemId = ItemIds.SnapWindowWall, PlankCost = 8 },
            new SnapSpec { Kind = BuildPieceKind.Doorway, Name = "Doorway", Slot = BuildSlot.Wall,
                           Blocks = false, WoodHealth = 200f, ItemId = ItemIds.SnapDoorway, PlankCost = 8 },
            new SnapSpec { Kind = BuildPieceKind.HalfWall, Name = "Half Wall", Slot = BuildSlot.Wall,
                           Blocks = true, WoodHealth = 130f, ItemId = ItemIds.SnapHalfWall, PlankCost = 4 },
            new SnapSpec { Kind = BuildPieceKind.Stairs, Name = "Stairs", Slot = BuildSlot.Interior,
                           Blocks = true, WoodHealth = 200f, ItemId = ItemIds.SnapStairs, PlankCost = 10 },
            new SnapSpec { Kind = BuildPieceKind.Roof, Name = "Roof", Slot = BuildSlot.Ceiling,
                           Blocks = true, WoodHealth = 200f, ItemId = ItemIds.SnapRoof, PlankCost = 8 },
            new SnapSpec { Kind = BuildPieceKind.Hatch, Name = "Hatch", Slot = BuildSlot.Floor,
                           Blocks = true, WoodHealth = 180f, ItemId = ItemIds.SnapHatch, PlankCost = 8 },
            new SnapSpec { Kind = BuildPieceKind.Ladder, Name = "Ladder", Slot = BuildSlot.Attachment,
                           Blocks = false, RequiresHost = true, HostKind = BuildPieceKind.Wall,
                           WoodHealth = 80f, ItemId = ItemIds.SnapLadder, PlankCost = 5 },
            new SnapSpec { Kind = BuildPieceKind.Fence, Name = "Fence", Slot = BuildSlot.Wall,
                           Blocks = true, WoodHealth = 90f, ItemId = ItemIds.SnapFence, PlankCost = 3 },
            new SnapSpec { Kind = BuildPieceKind.Door, Name = "Door", Slot = BuildSlot.Attachment,
                           Blocks = true, RequiresHost = true, HostKind = BuildPieceKind.Doorway,
                           WoodHealth = 260f, ItemId = ItemIds.SnapDoor, PlankCost = 9 },
        };

        static readonly BuildTier[] Tiers =
        {
            BuildTier.Twig, BuildTier.Wood, BuildTier.Stone, BuildTier.Metal, BuildTier.Armored
        };

        // Twig is deliberately flimsy: it exists to lay out the shape, not to survive a horde.
        static readonly float[] TierHealth = { 0.22f, 1f, 2.2f, 4.4f, 8f };
        static readonly float[] TierResist = { 0.45f, 1f, 1.9f, 3.3f, 5.5f };
        static readonly float[] TierMetallic = { 0f, 0f, 0f, 0.75f, 0.9f };
        static readonly float[] TierSmooth = { 0.05f, 0.1f, 0.12f, 0.3f, 0.42f };

        static readonly SurfaceFamily[] TierFamily =
        {
            SurfaceFamily.Wood, SurfaceFamily.Plank, SurfaceFamily.Stone, SurfaceFamily.Metal, SurfaceFamily.Metal
        };

        static readonly Color[] TierTint =
        {
            new Color(0.52f, 0.46f, 0.32f),   // twig: pale lashed sticks
            new Color(0.45f, 0.33f, 0.20f),   // wood: dark planks
            new Color(0.44f, 0.43f, 0.41f),   // stone
            new Color(0.44f, 0.39f, 0.34f),   // sheet metal, rust streaked
            new Color(0.30f, 0.31f, 0.33f)    // armored plate
        };

        /// <summary>
        /// Builds the Twig..Armored chain for every snap piece. Only Twig is craftable;
        /// the rest are reached with the hammer, so the player lays out a shape cheaply
        /// and then pays to armour the parts that matter.
        /// </summary>
        static List<BuildPieceDefinition> BuildSnapPieces(Dictionary<string, ItemDefinition> items,
                                                          Dictionary<BuildPieceKind, BuildPieceDefinition> twigByKind)
        {
            var all = new List<BuildPieceDefinition>();

            for (int i = 0; i < SnapSet.Length; i++)
            {
                var spec = SnapSet[i];
                var chain = new BuildPieceDefinition[Tiers.Length];

                for (int t = 0; t < Tiers.Length; t++)
                {
                    var def = ScriptableObject.CreateInstance<BuildPieceDefinition>();
                    var tier = Tiers[t];

                    def.stringId = string.Format("madvoxel:piece_{0}_{1}",
                        spec.Kind.ToString().ToLowerInvariant(), tier.ToString().ToLowerInvariant());
                    def.name = "Piece_" + spec.Kind + "_" + tier;
                    def.displayName = tier + " " + spec.Name;
                    def.kind = spec.Kind;
                    def.tier = tier;
                    def.slot = spec.Slot;
                    def.restsOnTerrain = spec.RestsOnTerrain;
                    def.blocksMovement = spec.Blocks;
                    def.requiresHost = spec.RequiresHost;
                    def.hostKind = spec.HostKind;

                    def.maxHealth = spec.WoodHealth * TierHealth[t];
                    def.damageResistance = TierResist[t];
                    def.surfaceFamily = TierFamily[t];
                    def.tint = TierTint[t];
                    def.metallic = TierMetallic[t];
                    def.smoothness = TierSmooth[t];

                    def.salvageItem = items[ItemIds.Plank];
                    def.salvageCount = Mathf.Max(1, spec.PlankCost / 3);

                    chain[t] = def;
                    all.Add(def);
                }

                // Link the upgrade chain and price each step.
                for (int t = 0; t < Tiers.Length - 1; t++) chain[t].upgradesTo = chain[t + 1];

                SetUpgradeCost(chain[1], items, Ing(items[ItemIds.Plank], Mathf.Max(4, spec.PlankCost)));
                SetUpgradeCost(chain[2], items, Ing(items[ItemIds.Stone], Mathf.Max(8, spec.PlankCost * 2)));

                // Metal and armoured are the Construction perk's payoff.
                SetUpgradeCost(chain[3], items, Ing(items[ItemIds.IronIngot], Mathf.Max(4, spec.PlankCost)));
                chain[3].requiredPerkId = "madvoxel:perk_carpenter";
                chain[3].requiredPerkRank = 2;

                SetUpgradeCost(chain[4], items,
                    Ing(items[ItemIds.IronIngot], Mathf.Max(8, spec.PlankCost * 2)),
                    Ing(items[ItemIds.Coal], 8));
                chain[4].requiredPerkId = "madvoxel:perk_carpenter";
                chain[4].requiredPerkRank = 3;

                twigByKind[spec.Kind] = chain[0];
            }

            return all;
        }

        static void SetUpgradeCost(BuildPieceDefinition def, Dictionary<string, ItemDefinition> items,
                                   params RecipeIngredient[] cost)
        {
            def.upgradeCost.Clear();
            def.upgradeCost.AddRange(cost);
        }

        /// <summary>One placeable item per piece kind, always placing the Twig tier.</summary>
        static void AddSnapItems(Dictionary<string, ItemDefinition> items,
                                 Dictionary<BuildPieceKind, BuildPieceDefinition> twigByKind,
                                 List<RecipeDefinition> recipes)
        {
            for (int i = 0; i < SnapSet.Length; i++)
            {
                var spec = SnapSet[i];
                var twig = twigByKind[spec.Kind];

                var item = Item(spec.ItemId, spec.Name, ItemCategory.Structure, 32,
                    SurfaceFamily.Wood, TierTint[0], Mathf.Max(2, spec.PlankCost / 2));
                item.description = "Places a twig " + spec.Name.ToLowerInvariant() + ". Upgrade it with the hammer.";
                item.placeableBuildPiece = twig;
                items[spec.ItemId] = item;

                recipes.Add(Recipe("madvoxel:craft_" + spec.Kind.ToString().ToLowerInvariant(),
                    item, 1, CraftStation.Hand, 1.0f, Ing(items[ItemIds.Plank], spec.PlankCost)));
            }
        }

        // ------------------------------------------------------------------ helper

        static string ShortName(string stringId)
        {
            int colon = stringId.IndexOf(':');
            var tail = colon >= 0 ? stringId.Substring(colon + 1) : stringId;
            var parts = tail.Split('_');
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                sb.Append(char.ToUpperInvariant(parts[i][0]));
                if (parts[i].Length > 1) sb.Append(parts[i].Substring(1));
            }
            return sb.ToString();
        }
    }
}
