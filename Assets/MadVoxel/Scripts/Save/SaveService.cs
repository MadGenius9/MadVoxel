using System;
using System.Collections.Generic;
using MadVoxel.Building;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Farming.Plots;
using MadVoxel.World.Fields;
using MadVoxel.Horde;
using MadVoxel.Inventory;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.Save
{
    /// <summary>
    /// Turns the live world into save files and back. Chunks go through
    /// <see cref="ChunkFileStore"/>; everything else is JSON.
    /// </summary>
    public class SaveService : MonoBehaviour
    {
        ContentDatabase _content;
        ChunkStreamer _streamer;
        StructureWorld _structures;
        BuildingWorld _buildings;
        FieldWorld _fields;
        WorldClock _clock;
        HordeDirector _horde;
        PlayerRig _player;

        string _worldName;
        int _seed;
        float _autosaveTimer;
        float _autosaveInterval = 120f;

        public string WorldName { get { return _worldName; } }
        public int Seed { get { return _seed; } }

        public void Init(ContentDatabase content, ChunkStreamer streamer, StructureWorld structures,
                         BuildingWorld buildings, FieldWorld fields, WorldClock clock, HordeDirector horde, PlayerRig player,
                         string worldName, int seed, float autosaveInterval)
        {
            _content = content;
            _streamer = streamer;
            _structures = structures;
            _buildings = buildings;
            _fields = fields;
            _clock = clock;
            _horde = horde;
            _player = player;
            _worldName = worldName;
            _seed = seed;
            _autosaveInterval = Mathf.Max(20f, autosaveInterval);

            SavePaths.EnsureWorldDirectories(worldName);
        }

        void Update()
        {
            if (_content == null) return;
            _autosaveTimer += Time.deltaTime;
            if (_autosaveTimer < _autosaveInterval) return;
            _autosaveTimer = 0f;
            SaveAll(false);
        }

        // -------------------------------------------------------------------- save

        public void SaveAll(bool announce = true)
        {
            if (_content == null) return;

            var world = WorldSaveIO.ReadWorld(_worldName) ?? new WorldSaveData();
            world.version = 1;
            world.worldName = _worldName;
            world.seed = _seed;
            world.totalHours = _clock != null ? _clock.TotalHours : 0.0;
            world.hordeNumber = _horde != null ? _horde.HordeNumber : 0;
            if (string.IsNullOrEmpty(world.createdUtc)) world.createdUtc = DateTime.UtcNow.ToString("o");
            world.lastPlayedUtc = DateTime.UtcNow.ToString("o");
            WorldSaveIO.WriteWorld(_worldName, world);

            if (_player != null) WorldSaveIO.WritePlayer(_worldName, CapturePlayer());
            if (_structures != null) WorldSaveIO.WriteStructures(_worldName, CaptureStructures());
            if (_streamer != null) _streamer.SaveDirtyChunks();

            if (announce) Notifications.Post("World saved");
        }

        PlayerSaveData CapturePlayer()
        {
            var data = new PlayerSaveData();
            var t = _player.transform;

            data.posX = t.position.x;
            data.posY = t.position.y;
            data.posZ = t.position.z;
            data.yaw = _player.Look != null ? _player.Look.Yaw : 0f;
            data.pitch = _player.Look != null ? _player.Look.Pitch : 0f;

            var stats = _player.Stats;
            data.health = stats.Health;
            data.stamina = stats.Stamina;
            data.food = stats.Food;
            data.water = stats.Water;

            data.selectedHotbar = _player.Inventory.SelectedIndex;
            for (int i = 0; i < _player.Inventory.Bag.Size; i++)
            {
                data.slots.Add(ToData(_player.Inventory.Bag[i]));
            }

            var progression = _player.Progression;
            data.level = progression.Level;
            data.xp = progression.Xp;
            data.perkPoints = progression.UnspentPerkPoints;
            foreach (var id in progression.UnlockedRecipes) data.unlockedRecipes.Add(id);
            foreach (var kv in progression.PerkRanks)
            {
                data.perkRanks.Add(new PerkRankData { perkId = kv.Key, rank = kv.Value });
            }

            data.hasRespawn = _player.HasRespawnPoint;
            data.respawnX = _player.RespawnPoint.x;
            data.respawnY = _player.RespawnPoint.y;
            data.respawnZ = _player.RespawnPoint.z;

            return data;
        }

        StructuresSaveData CaptureStructures()
        {
            var data = new StructuresSaveData();
            var all = _structures.All;

            for (int i = 0; i < all.Count; i++)
            {
                var structure = all[i];
                if (structure == null) continue;

                var entry = new StructureSaveData
                {
                    definitionId = structure.Definition.stringId,
                    cellX = structure.Cell.x,
                    cellY = structure.Cell.y,
                    cellZ = structure.Cell.z,
                    rotation = structure.RotationSteps,
                    health = structure.Health
                };

                var plot = structure.GetComponent<FarmPlotStructure>();
                if (plot != null && plot.Crop != null)
                {
                    entry.cropId = plot.Crop.stringId;
                    entry.plantedAtHours = plot.PlantedAtHours;
                }

                var silo = structure.GetComponent<SiloStructure>();
                if (silo != null)
                {
                    foreach (var kv in silo.Contents)
                    {
                        entry.silo.Add(new SiloEntryData { cropId = kv.Key, litres = kv.Value });
                    }
                }

                var storage = structure.GetComponent<StorageStructure>();
                if (storage != null)
                {
                    entry.isDeathBackpack = storage.IsDeathBackpack;
                    for (int s = 0; s < storage.Contents.Size; s++)
                    {
                        entry.contents.Add(ToData(storage.Contents[s]));
                    }
                }

                data.structures.Add(entry);
            }

            CaptureBuildPieces(data);
            CaptureFields(data);
            return data;
        }

        void CaptureBuildPieces(StructuresSaveData data)
        {
            if (_buildings == null) return;

            var pieces = _buildings.All;
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                if (piece == null) continue;

                data.pieces.Add(new BuildPieceSaveData
                {
                    definitionId = piece.Definition.stringId,
                    x = piece.Address.X,
                    y = piece.Address.Y,
                    z = piece.Address.Z,
                    slot = (int)piece.Address.Slot,
                    side = piece.Address.Side,
                    health = piece.Health,
                    open = piece.IsOpen
                });
            }
        }

        void CaptureFields(StructuresSaveData data)
        {
            if (_fields == null) return;

            foreach (var kv in _fields.Grid.Cells)
            {
                int x, z;
                FieldGrid.Decode(kv.Key, out x, out z);
                var cell = kv.Value;

                data.fieldCells.Add(new FieldCellSaveData
                {
                    x = x,
                    z = z,
                    state = (byte)cell.State,
                    cropIndex = cell.CropIndex,
                    moisture = cell.Moisture,
                    fertiliser = cell.Fertiliser,
                    yieldFactor = cell.YieldFactor,
                    changedAtHours = cell.ChangedAtHours
                });
            }
        }

        void RestoreFields(StructuresSaveData data)
        {
            if (_fields == null || data.fieldCells == null) return;

            for (int i = 0; i < data.fieldCells.Count; i++)
            {
                var entry = data.fieldCells[i];
                _fields.Grid.Set(entry.x, entry.z, new FieldCell
                {
                    State = (FieldCellState)entry.state,
                    CropIndex = entry.cropIndex,
                    Moisture = entry.moisture,
                    Fertiliser = entry.fertiliser,
                    YieldFactor = entry.yieldFactor,
                    ChangedAtHours = entry.changedAtHours
                });
            }
        }

        void RestoreBuildPieces(StructuresSaveData data)
        {
            if (_buildings == null || data.pieces == null) return;

            for (int i = 0; i < data.pieces.Count; i++)
            {
                var entry = data.pieces[i];
                var def = _content.BuildPiece(entry.definitionId);
                if (def == null)
                {
                    Debug.LogWarningFormat("Save references unknown build piece '{0}'.", entry.definitionId);
                    continue;
                }

                var address = new BuildAddress(entry.x, entry.y, entry.z, (BuildSlot)entry.slot, entry.side);
                var piece = _buildings.Place(def, address, entry.health, false);
                if (piece != null && entry.open) piece.SetOpen(true);
            }

            // Everything is back; now confirm what is actually still standing.
            _buildings.RecomputeStability();
        }

        static ItemStackData ToData(ItemStack stack)
        {
            return new ItemStackData
            {
                itemId = stack.IsEmpty ? "" : stack.Item.stringId,
                count = stack.IsEmpty ? 0 : stack.Count,
                durability = stack.Durability
            };
        }

        // -------------------------------------------------------------------- load

        public void RestorePlayer(PlayerSaveData data)
        {
            if (data == null || _player == null) return;

            _player.Motor.Teleport(new Vector3(data.posX, data.posY, data.posZ));
            if (_player.Look != null) _player.Look.SetRotation(data.yaw, data.pitch);

            _player.Stats.LoadState(data.health, data.stamina, data.food, data.water);
            _player.Stats.GrantInvulnerability(3f);

            var bag = _player.Inventory.Bag;
            bag.Clear();
            int count = Mathf.Min(data.slots.Count, bag.Size);
            for (int i = 0; i < count; i++)
            {
                var slot = data.slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.itemId) || slot.count <= 0) continue;

                var item = _content.Item(slot.itemId);
                if (item == null)
                {
                    Debug.LogWarningFormat("Save references unknown item '{0}'; slot dropped.", slot.itemId);
                    continue;
                }
                bag.SetSlot(i, new ItemStack(item, slot.count, slot.durability));
            }
            _player.Inventory.Select(Mathf.Clamp(data.selectedHotbar, 0, PlayerInventory.HotbarSize - 1));

            var ranks = new Dictionary<string, int>();
            for (int i = 0; i < data.perkRanks.Count; i++)
            {
                ranks[data.perkRanks[i].perkId] = data.perkRanks[i].rank;
            }
            _player.Progression.LoadState(data.level, data.xp, data.perkPoints, ranks, data.unlockedRecipes);

            _player.HasRespawnPoint = data.hasRespawn;
            _player.RespawnPoint = new Vector3(data.respawnX, data.respawnY, data.respawnZ);
        }

        public void RestoreStructures(StructuresSaveData data)
        {
            if (data == null || _structures == null) return;

            for (int i = 0; i < data.structures.Count; i++)
            {
                var entry = data.structures[i];
                var def = _content.Structure(entry.definitionId);
                if (def == null)
                {
                    Debug.LogWarningFormat("Save references unknown structure '{0}'.", entry.definitionId);
                    continue;
                }

                var cell = new Vector3Int(entry.cellX, entry.cellY, entry.cellZ);
                var placed = _structures.Place(def, cell, entry.rotation, entry.health, false);
                if (placed == null) continue;

                var plot = placed.GetComponent<FarmPlotStructure>();
                if (plot != null && !string.IsNullOrEmpty(entry.cropId))
                {
                    var crop = _content.Crop(entry.cropId);
                    if (crop != null) plot.RestoreCrop(crop, entry.plantedAtHours);
                    else Debug.LogWarningFormat("Save references unknown crop '{0}'.", entry.cropId);
                }

                var silo = placed.GetComponent<SiloStructure>();
                if (silo != null && entry.silo != null)
                {
                    for (int s2 = 0; s2 < entry.silo.Count; s2++)
                    {
                        silo.RestoreContents(entry.silo[s2].cropId, entry.silo[s2].litres);
                    }
                }

                var storage = placed.GetComponent<StorageStructure>();
                if (storage != null)
                {
                    storage.IsDeathBackpack = entry.isDeathBackpack;
                    int slots = Mathf.Min(entry.contents.Count, storage.Contents.Size);
                    for (int s = 0; s < slots; s++)
                    {
                        var slot = entry.contents[s];
                        if (slot == null || string.IsNullOrEmpty(slot.itemId) || slot.count <= 0) continue;

                        var item = _content.Item(slot.itemId);
                        if (item == null) continue;
                        storage.Contents.SetSlot(s, new ItemStack(item, slot.count, slot.durability));
                    }
                }
            }

            RestoreBuildPieces(data);
            RestoreFields(data);
        }

        void OnApplicationQuit()
        {
            if (_content != null) SaveAll(false);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && _content != null) SaveAll(false);
        }
    }
}
