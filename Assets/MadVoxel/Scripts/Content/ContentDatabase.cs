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
using MadVoxel.World.Weather;
using UnityEngine;

namespace MadVoxel.Content
{
    /// <summary>
    /// The single content root. Everything gameplay needs is reachable from here, so
    /// no system has to hunt for its data or own a static registry of its own.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Content Database", fileName = "ContentDatabase")]
    public class ContentDatabase : ScriptableObject
    {
        public const string ResourcePath = "MadVoxel/ContentDatabase";

        public GameConfig config;
        public BlockRegistry blocks;
        public List<ItemDefinition> items = new List<ItemDefinition>();
        public List<RecipeDefinition> recipes = new List<RecipeDefinition>();
        public List<StructureDefinition> structures = new List<StructureDefinition>();
        public List<BuildPieceDefinition> buildPieces = new List<BuildPieceDefinition>();
        public List<CropDefinition> crops = new List<CropDefinition>();
        public List<ZombieDefinition> zombies = new List<ZombieDefinition>();
        public HordeSchedule hordeSchedule;

        [Header("The colony")]
        public Colony.ColonyRules colonyRules;

        [Header("World")]
        public BiomeTable biomes;
        public WeatherTable weather;

        [Header("Phase 1 content")]
        public PerkTreeDefinition perkTree;
        public List<TraderDefinition> traders = new List<TraderDefinition>();
        public List<QuestDefinition> quests = new List<QuestDefinition>();
        public List<VehicleDefinition> vehicles = new List<VehicleDefinition>();

        [Header("New world loadout")]
        public List<StartingStack> startingItems = new List<StartingStack>();

        [System.Serializable]
        public struct StartingStack
        {
            public ItemDefinition item;
            public int count;
        }

        readonly Dictionary<string, ItemDefinition> _itemsById = new Dictionary<string, ItemDefinition>();
        readonly Dictionary<string, StructureDefinition> _structuresById = new Dictionary<string, StructureDefinition>();
        readonly Dictionary<string, BuildPieceDefinition> _piecesById = new Dictionary<string, BuildPieceDefinition>();
        readonly Dictionary<string, CropDefinition> _cropsById = new Dictionary<string, CropDefinition>();
        readonly Dictionary<ItemDefinition, CropDefinition> _cropsBySeed = new Dictionary<ItemDefinition, CropDefinition>();
        readonly Dictionary<string, ZombieDefinition> _zombiesById = new Dictionary<string, ZombieDefinition>();
        readonly Dictionary<string, RecipeDefinition> _recipesById = new Dictionary<string, RecipeDefinition>();
        bool _built;

        public void Build()
        {
            _itemsById.Clear();
            _structuresById.Clear();
            _piecesById.Clear();
            _cropsById.Clear();
            _cropsBySeed.Clear();
            _zombiesById.Clear();
            _recipesById.Clear();

            for (int i = 0; i < items.Count; i++) if (items[i] != null) _itemsById[items[i].stringId] = items[i];
            for (int i = 0; i < structures.Count; i++) if (structures[i] != null) _structuresById[structures[i].stringId] = structures[i];
            for (int i = 0; i < buildPieces.Count; i++) if (buildPieces[i] != null) _piecesById[buildPieces[i].stringId] = buildPieces[i];
            for (int i = 0; i < crops.Count; i++)
            {
                var crop = crops[i];
                if (crop == null) continue;
                _cropsById[crop.stringId] = crop;
                if (crop.seedItem != null) _cropsBySeed[crop.seedItem] = crop;
            }
            for (int i = 0; i < zombies.Count; i++) if (zombies[i] != null) _zombiesById[zombies[i].stringId] = zombies[i];
            for (int i = 0; i < recipes.Count; i++) if (recipes[i] != null) _recipesById[recipes[i].stringId] = recipes[i];

            if (blocks != null) blocks.Build();
            _built = true;
        }

        void EnsureBuilt()
        {
            if (!_built) Build();
        }

        public ItemDefinition Item(string stringId)
        {
            EnsureBuilt();
            ItemDefinition def;
            return _itemsById.TryGetValue(stringId, out def) ? def : null;
        }

        public StructureDefinition Structure(string stringId)
        {
            EnsureBuilt();
            StructureDefinition def;
            return _structuresById.TryGetValue(stringId, out def) ? def : null;
        }

        public BuildPieceDefinition BuildPiece(string stringId)
        {
            EnsureBuilt();
            BuildPieceDefinition def;
            return _piecesById.TryGetValue(stringId, out def) ? def : null;
        }

        public CropDefinition Crop(string stringId)
        {
            EnsureBuilt();
            CropDefinition def;
            return _cropsById.TryGetValue(stringId, out def) ? def : null;
        }

        /// <summary>The crop a seed item plants, or null if the item is not a seed.</summary>
        public CropDefinition CropForSeed(ItemDefinition seed)
        {
            EnsureBuilt();
            if (seed == null) return null;
            CropDefinition def;
            return _cropsBySeed.TryGetValue(seed, out def) ? def : null;
        }

        public ZombieDefinition Zombie(string stringId)
        {
            EnsureBuilt();
            ZombieDefinition def;
            return _zombiesById.TryGetValue(stringId, out def) ? def : null;
        }

        public RecipeDefinition Recipe(string stringId)
        {
            EnsureBuilt();
            RecipeDefinition def;
            return _recipesById.TryGetValue(stringId, out def) ? def : null;
        }

        /// <summary>
        /// Loads the authored asset when one exists, otherwise builds the same content
        /// in memory. That keeps the game playable straight from a fresh clone, before
        /// anyone has generated the ScriptableObject assets.
        /// </summary>
        public static ContentDatabase LoadOrBuild()
        {
            var asset = Resources.Load<ContentDatabase>(ResourcePath);
            if (asset != null)
            {
                asset.Build();
                return asset;
            }

            var runtime = ContentLibrary.BuildRuntime();
            runtime.Build();
            return runtime;
        }
    }
}
