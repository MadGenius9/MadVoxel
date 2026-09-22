using System.Collections.Generic;
using MadVoxel.AI;
using MadVoxel.Building;
using MadVoxel.Core;
using MadVoxel.Horde;
using MadVoxel.Inventory;
using MadVoxel.Quests;
using MadVoxel.Skills;
using MadVoxel.Traders;
using MadVoxel.Vehicles;
using MadVoxel.World.Voxel;
using UnityEngine;

namespace MadVoxel.Content
{
    /// <summary>
    /// The authored content of MadVoxel, expressed in code so the game runs from a
    /// fresh clone. "MadVoxel &gt; Content &gt; Generate ScriptableObject Assets" writes the
    /// very same data out as .asset files for designers to edit by hand.
    /// </summary>
    public static class ContentLibrary
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

            LinkPlacement(itemMap, blockMap, structureMap);
            LinkBlockDrops(blockMap, itemMap);

            db.blocks = BuildRegistry(blockMap);
            db.items = new List<ItemDefinition>(itemMap.Values);
            db.structures = new List<StructureDefinition>(structureMap.Values);
            db.recipes = BuildRecipes(itemMap);
            db.zombies = BuildZombies(itemMap);
            db.hordeSchedule = BuildHordeSchedule(db.zombies);

            db.skillTree = BuildSkillTree();
            db.quests = BuildQuests(itemMap);
            db.traders = BuildTraders(itemMap, db.quests);
            db.vehicles = BuildVehicles(itemMap);

            db.startingItems = new List<ContentDatabase.StartingStack>
            {
                Starting(itemMap[ItemIds.StoneAxe], 1),
                Starting(itemMap[ItemIds.CannedFood], 2),
                Starting(itemMap[ItemIds.WaterBottle], 2),
                Starting(itemMap[ItemIds.PlantFibre], 8)
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
            map[BlockIds.Clay] = Block(BlockIds.Clay, "Clay", SurfaceFamily.Dirt, ColClay, 0.7f, ToolType.Shovel, 0, 0.8f, 70f);
            map[BlockIds.CoalOre] = Block(BlockIds.CoalOre, "Coal Seam", SurfaceFamily.Ore, ColCoal, 2.6f, ToolType.Pickaxe, 1, 4f, 220f);
            map[BlockIds.IronOre] = Block(BlockIds.IronOre, "Iron Ore", SurfaceFamily.Ore, ColIron, 3.2f, ToolType.Pickaxe, 1, 5f, 240f);
            map[BlockIds.PineLog] = Block(BlockIds.PineLog, "Pine Log", SurfaceFamily.Wood, ColWood, 1.8f, ToolType.Axe, 0, 3f, 140f);
            map[BlockIds.ScrapHeap] = Block(BlockIds.ScrapHeap, "Scrap Heap", SurfaceFamily.Metal, ColRust, 2.0f, ToolType.Pickaxe, 0, 5f, 150f);

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

            var steelBlock = Block(BlockIds.SteelBlock, "Steel Block", SurfaceFamily.Metal, ColSteel, 6.0f, ToolType.Pickaxe, 3, 0f, 900f);
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

        static BlockRegistry BuildRegistry(Dictionary<string, BlockDefinition> map)
        {
            var registry = ScriptableObject.CreateInstance<BlockRegistry>();
            registry.name = "BlockRegistry";

            // Air must be index 0; the rest follow a stable, explicit order.
            string[] order =
            {
                BlockIds.Air, BlockIds.Bedrock, BlockIds.Stone, BlockIds.Dirt, BlockIds.Grass,
                BlockIds.Sand, BlockIds.Gravel, BlockIds.Clay, BlockIds.CoalOre, BlockIds.IronOre,
                BlockIds.PineLog, BlockIds.PineNeedles, BlockIds.ScrapHeap,
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

            // Block items. Each carries the block it places.
            AddBlockItem(map, blocks, ItemIds.BlockWoodFrame, BlockIds.WoodFrame, "Wood Frame", SurfaceFamily.Plank, ColPlank * 0.9f, 2);
            AddBlockItem(map, blocks, ItemIds.BlockPlanks, BlockIds.Planks, "Plank Block", SurfaceFamily.Plank, ColPlank, 3);
            AddBlockItem(map, blocks, ItemIds.BlockCobblestone, BlockIds.Cobblestone, "Cobblestone Block", SurfaceFamily.Stone, ColCobble, 4);
            AddBlockItem(map, blocks, ItemIds.BlockIron, BlockIds.IronBlock, "Iron Plate Block", SurfaceFamily.Metal, ColIronPlate, 22);
            AddBlockItem(map, blocks, ItemIds.BlockSteel, BlockIds.SteelBlock, "Steel Block", SurfaceFamily.Metal, ColSteel, 48);
            AddBlockItem(map, blocks, ItemIds.BlockGlass, BlockIds.Glass, "Scrap Glass Block", SurfaceFamily.Stone, ColGlass, 6);

            // Snap piece items; the structure reference is linked after structures exist.
            Add(Item(ItemIds.PieceDoor, "Wooden Door", ItemCategory.Structure, 8, SurfaceFamily.Plank, ColPlank, 20));
            Add(Item(ItemIds.PieceLadder, "Ladder", ItemCategory.Structure, 16, SurfaceFamily.Wood, ColWood, 8));
            Add(Item(ItemIds.PieceStorageBox, "Storage Box", ItemCategory.Structure, 8, SurfaceFamily.Plank, ColPlank * 1.05f, 26));
            Add(Item(ItemIds.PieceWorkbench, "Workbench", ItemCategory.Structure, 4, SurfaceFamily.Plank, ColPlank, 40));
            Add(Item(ItemIds.PieceCampfire, "Campfire", ItemCategory.Structure, 4, SurfaceFamily.Stone, ColStone, 14));
            Add(Item(ItemIds.PieceClaimStake, "Land Claim Stake", ItemCategory.Structure, 2, SurfaceFamily.Wood, ColWood, 120));
            Add(Item(ItemIds.PieceBedroll, "Bedroll", ItemCategory.Structure, 2, SurfaceFamily.Cloth, new Color(0.45f, 0.42f, 0.36f), 30));

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
            items[ItemIds.PieceDoor].placeableStructure = structures[StructureIds.Door];
            items[ItemIds.PieceLadder].placeableStructure = structures[StructureIds.Ladder];
            items[ItemIds.PieceStorageBox].placeableStructure = structures[StructureIds.StorageBox];
            items[ItemIds.PieceWorkbench].placeableStructure = structures[StructureIds.Workbench];
            items[ItemIds.PieceCampfire].placeableStructure = structures[StructureIds.Campfire];
            items[ItemIds.PieceClaimStake].placeableStructure = structures[StructureIds.ClaimStake];
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
            Drop(blocks[BlockIds.CoalOre], items[ItemIds.Coal], 1, 3);
            Drop(blocks[BlockIds.IronOre], items[ItemIds.IronOre], 1, 3);
            Drop(blocks[BlockIds.PineLog], items[ItemIds.WoodLog], 1, 2);
            Drop(blocks[BlockIds.PineNeedles], items[ItemIds.PlantFibre], 0, 2);
            Drop(blocks[BlockIds.ScrapHeap], items[ItemIds.ScrapMetal], 2, 5);
            Drop(blocks[BlockIds.Concrete], items[ItemIds.Stone], 2, 3);

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

            var door = Structure(StructureIds.Door, "Wooden Door", StructureKind.Door, new Vector3Int(1, 2, 1), SurfaceFamily.Plank, ColPlank, 260f);
            door.salvageItem = items[ItemIds.PieceDoor];
            map[door.stringId] = door;

            var ladder = Structure(StructureIds.Ladder, "Ladder", StructureKind.Ladder, Vector3Int.one, SurfaceFamily.Wood, ColWood, 70f);
            ladder.requiresSupport = false;
            ladder.blocksMovement = false;
            ladder.salvageItem = items[ItemIds.PieceLadder];
            map[ladder.stringId] = ladder;

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

            var stake = Structure(StructureIds.ClaimStake, "Land Claim Stake", StructureKind.ClaimStake, new Vector3Int(1, 2, 1), SurfaceFamily.Wood, ColWood, 900f);
            stake.claimRadius = 24f;
            stake.salvageItem = items[ItemIds.PieceClaimStake];
            map[stake.stringId] = stake;

            var bedroll = Structure(StructureIds.Bedroll, "Bedroll", StructureKind.Bedroll, Vector3Int.one, SurfaceFamily.Cloth, new Color(0.45f, 0.42f, 0.36f), 60f);
            bedroll.blocksMovement = false;
            bedroll.salvageItem = items[ItemIds.PieceBedroll];
            map[bedroll.stringId] = bedroll;

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

            list.Add(Recipe("madvoxel:craft_campfire", it[ItemIds.PieceCampfire], 1, CraftStation.Hand, 3f,
                Ing(it[ItemIds.Stone], 8), Ing(it[ItemIds.WoodLog], 3)));
            list.Add(Recipe("madvoxel:craft_workbench", it[ItemIds.PieceWorkbench], 1, CraftStation.Hand, 4f,
                Ing(it[ItemIds.Plank], 10), Ing(it[ItemIds.Stone], 4)));
            list.Add(Recipe("madvoxel:craft_ladder", it[ItemIds.PieceLadder], 2, CraftStation.Hand, 1.2f,
                Ing(it[ItemIds.Plank], 4)));
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
            list.Add(Recipe("madvoxel:smelt_glass", it[ItemIds.BlockGlass], 2, CraftStation.Campfire, 4f,
                Ing(it[ItemIds.Sand], 3), Ing(it[ItemIds.Coal], 1)));

            list.Add(Recipe("madvoxel:craft_door", it[ItemIds.PieceDoor], 1, CraftStation.Workbench, 4f,
                Ing(it[ItemIds.Plank], 8), Ing(it[ItemIds.IronIngot], 1)));
            list.Add(Recipe("madvoxel:craft_storage_box", it[ItemIds.PieceStorageBox], 1, CraftStation.Workbench, 4f,
                Ing(it[ItemIds.Plank], 12), Ing(it[ItemIds.IronIngot], 1)));
            list.Add(Recipe("madvoxel:craft_claim_stake", it[ItemIds.PieceClaimStake], 1, CraftStation.Workbench, 6f,
                Ing(it[ItemIds.WoodLog], 6), Ing(it[ItemIds.IronIngot], 2), Ing(it[ItemIds.Cloth], 2)));
            list.Add(Recipe("madvoxel:craft_wrench", it[ItemIds.Wrench], 1, CraftStation.Workbench, 4f,
                Ing(it[ItemIds.IronIngot], 3), Ing(it[ItemIds.Plank], 1)));
            list.Add(Recipe("madvoxel:craft_iron_pickaxe", it[ItemIds.IronPickaxe], 1, CraftStation.Workbench, 5f,
                Ing(it[ItemIds.IronIngot], 4), Ing(it[ItemIds.Plank], 2)));
            list.Add(Recipe("madvoxel:craft_iron_axe", it[ItemIds.IronAxe], 1, CraftStation.Workbench, 5f,
                Ing(it[ItemIds.IronIngot], 4), Ing(it[ItemIds.Plank], 2)));
            list.Add(Recipe("madvoxel:craft_block_iron", it[ItemIds.BlockIron], 1, CraftStation.Workbench, 3f,
                Ing(it[ItemIds.IronIngot], 4)));

            // Locked behind skills. Phase 1 turns the ranks into unlocks; the data is real now.
            var steel = Recipe("madvoxel:craft_block_steel", it[ItemIds.BlockSteel], 1, CraftStation.Workbench, 6f,
                Ing(it[ItemIds.BlockIron], 1), Ing(it[ItemIds.Coal], 4));
            steel.unlockedByDefault = false;
            steel.requiredSkillId = "madvoxel:skill_carpenter";
            steel.requiredSkillRank = 3;
            list.Add(steel);

            var buggy = Recipe("madvoxel:craft_buggy_kit", it[ItemIds.BuggyKit], 1, CraftStation.Workbench, 12f,
                Ing(it[ItemIds.EngineBlock], 1), Ing(it[ItemIds.Wheel], 4),
                Ing(it[ItemIds.IronIngot], 20), Ing(it[ItemIds.ScrapMetal], 40));
            buggy.unlockedByDefault = false;
            buggy.requiredSkillId = "madvoxel:skill_grease_monkey";
            buggy.requiredSkillRank = 1;
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

        static SkillDefinition Skill(string id, string name, SkillCategory category, string description,
                                     int maxRank, int requiredLevel, params SkillEffect[] effects)
        {
            var def = ScriptableObject.CreateInstance<SkillDefinition>();
            def.name = "Skill_" + ShortName(id);
            def.stringId = id;
            def.displayName = name;
            def.category = category;
            def.description = description;
            def.maxRank = maxRank;
            def.requiredPlayerLevel = requiredLevel;
            def.effects.AddRange(effects);
            return def;
        }

        static SkillEffect Effect(SkillEffectType type, float perRank)
        {
            return new SkillEffect { type = type, valuePerRank = perRank };
        }

        static SkillTreeDefinition BuildSkillTree()
        {
            var tree = ScriptableObject.CreateInstance<SkillTreeDefinition>();
            tree.name = "SkillTree";

            tree.skills.Add(Skill("madvoxel:skill_miner", "Miner 69er", SkillCategory.Mining,
                "Swing faster on stone, ore and scrap.", 5, 1,
                Effect(SkillEffectType.MiningSpeedMultiplier, 0.12f)));

            tree.skills.Add(Skill("madvoxel:skill_motherlode", "Motherlode", SkillCategory.Mining,
                "Ore and scrap yield more per block.", 5, 4,
                Effect(SkillEffectType.HarvestYieldMultiplier, 0.15f)));

            var carpenter = Skill("madvoxel:skill_carpenter", "Carpenter", SkillCategory.Construction,
                "Unlocks sturdier building blocks, ending in steel.", 4, 1,
                Effect(SkillEffectType.BlockTierUnlock, 1f));
            carpenter.unlocksRecipeIds.Add("madvoxel:craft_block_cobble");
            carpenter.unlocksRecipeIds.Add("madvoxel:craft_block_iron");
            carpenter.unlocksRecipeIds.Add("madvoxel:craft_block_steel");
            tree.skills.Add(carpenter);

            tree.skills.Add(Skill("madvoxel:skill_handyman", "Handyman", SkillCategory.Construction,
                "Repair and upgrade structures faster.", 3, 3,
                Effect(SkillEffectType.RepairSpeedMultiplier, 0.2f)));

            tree.skills.Add(Skill("madvoxel:skill_heavy_hitter", "Heavy Hitter", SkillCategory.Combat,
                "More damage with clubs, axes and anything heavy.", 5, 1,
                Effect(SkillEffectType.MeleeDamageMultiplier, 0.1f)));

            tree.skills.Add(Skill("madvoxel:skill_iron_lungs", "Iron Lungs", SkillCategory.Combat,
                "Sprint and swing longer before your stamina gives out.", 4, 2,
                Effect(SkillEffectType.MaxStaminaBonus, 12f),
                Effect(SkillEffectType.StaminaDrainMultiplier, -0.08f)));

            tree.skills.Add(Skill("madvoxel:skill_scrapper", "Scrapper", SkillCategory.Scavenging,
                "Pull more out of wrecks, heaps and containers.", 5, 1,
                Effect(SkillEffectType.LootQuantityMultiplier, 0.14f)));

            tree.skills.Add(Skill("madvoxel:skill_pack_mule", "Pack Mule", SkillCategory.Scavenging,
                "Carry heavier loads without slowing down.", 3, 5,
                Effect(SkillEffectType.StaminaDrainMultiplier, -0.06f)));

            tree.skills.Add(Skill("madvoxel:skill_field_medic", "Field Medic", SkillCategory.Medicine,
                "Bandages and food do more for you.", 4, 1,
                Effect(SkillEffectType.HealingMultiplier, 0.2f)));

            var physician = Skill("madvoxel:skill_physician", "Physician", SkillCategory.Medicine,
                "Craft better medical supplies.", 3, 6,
                Effect(SkillEffectType.HealingMultiplier, 0.1f));
            physician.unlocksRecipeIds.Add("madvoxel:craft_bandage");
            tree.skills.Add(physician);

            var grease = Skill("madvoxel:skill_grease_monkey", "Grease Monkey", SkillCategory.Vehicles,
                "Build and maintain vehicles.", 3, 5,
                Effect(SkillEffectType.RepairSpeedMultiplier, 0.15f));
            grease.unlocksRecipeIds.Add("madvoxel:craft_buggy_kit");
            tree.skills.Add(grease);

            tree.skills.Add(Skill("madvoxel:skill_economiser", "Economiser", SkillCategory.Vehicles,
                "Squeeze more distance out of every gas can.", 3, 7,
                Effect(SkillEffectType.VehicleFuelEfficiency, 0.15f)));

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
            mara.stock.Add(Stock(it[ItemIds.PieceClaimStake], 1, 2.0f, 2));

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
