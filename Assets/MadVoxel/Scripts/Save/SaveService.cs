using System;
using System.Collections.Generic;
using MadVoxel.Building;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.Farming.Plots;
using MadVoxel.World.Fields;
using MadVoxel.Horde;
using MadVoxel.Modding;
using MadVoxel.Colony;
using MadVoxel.Fluid;
using MadVoxel.Inventory;
using MadVoxel.Power;
using MadVoxel.Vehicles;
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

        /// <summary>Stamped into world.json so a later load can warn about missing mods.</summary>
        public System.Collections.Generic.IReadOnlyList<ModManifest> ActiveMods { get; set; }

        /// <summary>The grid, the plumbing and the colony. Set by the session.</summary>
        public PowerWorld Power { get; set; }
        public FluidWorld Fluid { get; set; }
        public ColonyWorld Colony { get; set; }
        public MadVoxel.World.Weather.WeatherDirector Weather { get; set; }
        public MadVoxel.Claim.ClaimHeatTracker Heat { get; set; }
        /// <summary>The machine yard. Set by the session once the HUD exists.</summary>
        public VehicleWorld Vehicles { get; set; }
        /// <summary>The counters at the outposts.</summary>
        public MadVoxel.Traders.TraderWorld Traders { get; set; }

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

            // The sky and the claim's noise are world state, not structure state: a
            // reload must not hand you a different afternoon or a silent farm.
            world.weatherKind = Weather != null ? (int)Weather.Kind : 0;
            world.weatherHoursRemaining = Weather != null ? Weather.HoursRemaining : 0f;
            world.claimHeat = Heat != null ? Heat.Heat : 0f;
            if (string.IsNullOrEmpty(world.createdUtc)) world.createdUtc = DateTime.UtcNow.ToString("o");
            world.lastPlayedUtc = DateTime.UtcNow.ToString("o");

            world.mods.Clear();
            if (ActiveMods != null)
            {
                for (int i = 0; i < ActiveMods.Count; i++) world.mods.Add(ActiveMods[i].Id);
            }
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

            var quests = _player.Quests;
            for (int i = 0; i < quests.Active.Count; i++)
            {
                var entry = quests.Active[i];
                data.activeQuests.Add(new QuestSaveData
                {
                    questId = entry.QuestId,
                    traderId = quests.IssuerOf(entry.QuestId),
                    acceptedAtHours = entry.AcceptedAtHours,
                    counter = entry.Counter
                });
            }
            foreach (var id in quests.Completed) data.completedQuests.Add(id);

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

                // A furnace is a container too, and its slots go down the same path.
                var furnace = structure.GetComponent<FurnaceStructure>();
                if (furnace != null)
                {
                    furnace.Catch();
                    entry.furnaceWorkedToHours = furnace.WorkedToHours;
                    for (int s = 0; s < furnace.Contents.Size; s++)
                    {
                        entry.contents.Add(ToData(furnace.Contents[s]));
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

                // Utilities: the node's own state, plus the id it had, so the wires can
                // be matched back up after the graphs re-number everything on load.
                var electrical = structure.GetComponent<PowerDeviceStructure>();
                if (electrical != null && electrical.Node != null)
                {
                    entry.powerNodeId = electrical.NodeId;
                    entry.powerSwitchedOn = electrical.Node.SwitchedOn;
                    entry.fuelLitres = electrical.Node.FuelLitres;
                    entry.storedWattHours = electrical.Node.StoredWattHours;
                }

                var fitting = structure.GetComponent<FluidDeviceStructure>();
                if (fitting != null && fitting.Node != null)
                {
                    entry.fluidNodeId = fitting.NodeId;
                    entry.fluidSwitchedOn = fitting.Node.SwitchedOn;
                    entry.litres = fitting.Node.Litres;
                    entry.fluidBroken = fitting.Node.IsBroken;
                }

                data.structures.Add(entry);
            }

            CaptureBuildPieces(data);
            CaptureFields(data);
            CaptureLinks(data);
            CaptureColony(data);
            CaptureVehicles(data);
            CaptureTraders(data);
            return data;
        }

        /// <summary>
        /// Every wire and hose, as the pair of node ids the save just recorded. The
        /// graphs are directed and acyclic, so one entry per edge is the whole story.
        /// </summary>
        void CaptureLinks(StructuresSaveData data)
        {
            if (Power != null && Power.Graph != null)
            {
                var nodes = Power.Graph.Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = 0; j < nodes[i].Outputs.Count; j++)
                    {
                        data.wires.Add(new LinkSaveData { fromId = nodes[i].Id, toId = nodes[i].Outputs[j] });
                    }
                }
            }

            if (Fluid != null && Fluid.Graph != null)
            {
                var nodes = Fluid.Graph.Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = 0; j < nodes[i].Outputs.Count; j++)
                    {
                        data.hoses.Add(new LinkSaveData { fromId = nodes[i].Id, toId = nodes[i].Outputs[j] });
                    }
                }
            }
        }

        void CaptureColony(StructuresSaveData data)
        {
            if (Colony == null) return;

            data.colony.founded = Colony.Founded;
            data.colony.colonyName = Colony.ColonyName;
            data.colony.sheltering = Colony.Sheltering;

            var people = Colony.Colonists;
            for (int i = 0; i < people.Count; i++)
            {
                var person = people[i];
                if (person == null || !person.IsAlive) continue;

                data.colony.colonists.Add(new ColonistSaveData
                {
                    name = person.Name,
                    job = (int)person.Job,
                    food = person.Food,
                    water = person.Water,
                    morale = person.Morale
                });
            }
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
                durability = stack.Durability,
                spoilRemaining = stack.SpoilRemaining
            };
        }

        // -------------------------------------------------------------------- load

        public void RestorePlayer(PlayerSaveData data)
        {
            if (data == null || _player == null) return;

            _player.Motor.Teleport(new Vector3(data.posX, data.posY, data.posZ));
            if (_player.Look != null) _player.Look.SetRotation(data.yaw, data.pitch);

            // Perks first: Iron Lungs raises the stamina ceiling, and restoring vitals
            // before the ranks are back would clamp a full bar down to the base pool.
            var ranks = new Dictionary<string, int>();
            for (int i = 0; i < data.perkRanks.Count; i++)
            {
                ranks[data.perkRanks[i].perkId] = data.perkRanks[i].rank;
            }
            _player.Progression.LoadState(data.level, data.xp, data.perkPoints, ranks, data.unlockedRecipes);

            // A save written before a perk gained an unlock would otherwise come back
            // with the rank but not the recipe.
            MadVoxel.Perks.PerkService.ReapplyUnlocks(_content.perkTree, _player.Progression);

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
                var restored = new ItemStack(item, slot.count, slot.durability);
                restored.SpoilRemaining = MadVoxel.Inventory.Spoil.SpoilRules.Normalise(item, slot.spoilRemaining);
                bag.SetSlot(i, restored);
            }
            _player.Inventory.Select(Mathf.Clamp(data.selectedHotbar, 0, PlayerInventory.HotbarSize - 1));

            RestoreQuests(data);

            _player.HasRespawnPoint = data.hasRespawn;
            _player.RespawnPoint = new Vector3(data.respawnX, data.respawnY, data.respawnZ);
        }

        public void RestoreStructures(StructuresSaveData data)
        {
            if (data == null || _structures == null) return;

            // Old node id -> the id the graph has just handed this piece. Wires are
            // stored against the saved ids, so this is what puts them back.
            var powerRemap = new Dictionary<int, int>();
            var fluidRemap = new Dictionary<int, int>();

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

                var furnace = placed.GetComponent<FurnaceStructure>();
                if (furnace != null)
                {
                    furnace.Content = _content;
                    furnace.RestoreState(entry.furnaceWorkedToHours);
                    RestoreSlots(entry.contents, furnace.Contents);

                    // Catch it up now: a furnace loaded and left through a save should
                    // hand you the metal it made while the world was closed.
                    furnace.Catch();
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

                        var stack = new ItemStack(item, slot.count, slot.durability);
                        stack.SpoilRemaining = MadVoxel.Inventory.Spoil.SpoilRules.Normalise(item, slot.spoilRemaining);
                        storage.Contents.SetSlot(s, stack);
                    }
                }

                RestoreUtilities(placed, entry, powerRemap, fluidRemap);
            }

            RestoreBuildPieces(data);
            RestoreFields(data);
            RestoreLinks(data, powerRemap, fluidRemap);
            RestoreColony(data);
            RestoreVehicles(data);
            RestoreTraders(data);
        }

        /// <summary>
        /// Contracts come back with the hour they were taken and whatever was tallied.
        /// A contract whose definition is gone - a mod removed, content renamed - is
        /// dropped with a warning rather than restored as an entry pointing at nothing.
        /// </summary>
        void RestoreQuests(PlayerSaveData data)
        {
            if (data.activeQuests == null) return;

            var entries = new List<MadVoxel.Quests.QuestEntry>();
            var issuers = new List<KeyValuePair<string, string>>();

            for (int i = 0; i < data.activeQuests.Count; i++)
            {
                var saved = data.activeQuests[i];
                if (saved == null || string.IsNullOrEmpty(saved.questId)) continue;

                if (_content.Quest(saved.questId) == null)
                {
                    Debug.LogWarningFormat("Save references unknown contract '{0}'; dropped.", saved.questId);
                    continue;
                }

                entries.Add(new MadVoxel.Quests.QuestEntry
                {
                    QuestId = saved.questId,
                    AcceptedAtHours = saved.acceptedAtHours,
                    Counter = saved.counter
                });
                issuers.Add(new KeyValuePair<string, string>(saved.questId, saved.traderId));
            }

            _player.Quests.LoadState(entries, data.completedQuests, issuers);
        }

        void CaptureTraders(StructuresSaveData data)
        {
            if (Traders == null) return;

            var posts = Traders.All;
            for (int i = 0; i < posts.Count; i++)
            {
                var post = posts[i];
                if (post == null || post.State == null || post.Definition == null) continue;

                var entry = new TraderSaveData
                {
                    traderId = post.Definition.stringId,
                    reputation = post.State.Reputation,
                    lastRestockHours = post.State.LastRestockHours
                };
                entry.stock.AddRange(post.State.SaveStock());

                data.traders.Add(entry);
            }
        }

        void RestoreTraders(StructuresSaveData data)
        {
            if (Traders == null || data.traders == null) return;

            for (int i = 0; i < data.traders.Count; i++)
            {
                var entry = data.traders[i];
                Traders.LoadState(entry.traderId, entry.reputation, entry.lastRestockHours, entry.stock);
            }
        }

        /// <summary>
        /// Machines are saved where they were parked rather than on a cell, because they
        /// are the one placed thing in the game that moves.
        /// </summary>
        void CaptureVehicles(StructuresSaveData data)
        {
            if (Vehicles == null) return;

            var all = Vehicles.All;
            for (int i = 0; i < all.Count; i++)
            {
                var rig = all[i];
                if (rig == null || rig.Definition == null) continue;

                var entry = new VehicleSaveData
                {
                    definitionId = rig.Definition.stringId,
                    posX = rig.transform.position.x,
                    posY = rig.transform.position.y,
                    posZ = rig.transform.position.z,
                    yaw = rig.transform.eulerAngles.y,
                    fuelLitres = rig.FuelLitres,
                    health = rig.Health
                };

                var implement = rig.Implement;
                if (implement != null && implement.Definition != null)
                {
                    entry.implementId = implement.Definition.stringId;
                    entry.hopperLitres = implement.HopperLitres;
                    entry.hopperCropId = implement.Cargo != null ? implement.Cargo.stringId : "";
                }

                data.vehicles.Add(entry);
            }
        }

        void RestoreVehicles(StructuresSaveData data)
        {
            if (Vehicles == null || data.vehicles == null) return;

            for (int i = 0; i < data.vehicles.Count; i++)
            {
                var entry = data.vehicles[i];
                var def = Vehicles.FindDefinition(entry.definitionId);
                if (def == null)
                {
                    Debug.LogWarningFormat("Save references unknown vehicle '{0}'.", entry.definitionId);
                    continue;
                }

                var rig = Vehicles.Spawn(def, new Vector3(entry.posX, entry.posY, entry.posZ), entry.yaw);
                if (rig == null) continue;

                rig.RestoreState(entry.fuelLitres, entry.health);

                if (string.IsNullOrEmpty(entry.implementId)) continue;

                var implementDef = Vehicles.FindImplement(entry.implementId);
                if (implementDef == null)
                {
                    Debug.LogWarningFormat("Save references unknown implement '{0}'.", entry.implementId);
                    continue;
                }

                var implement = Vehicles.Hitch(rig, implementDef);
                if (implement == null) continue;

                // A hopper of grain is an afternoon's work. It comes back loaded, and
                // raised: a machine that resumes mid-furrow on load would plough the
                // line between where it was saved and wherever it settles.
                implement.RestoreHopper(_content.Crop(entry.hopperCropId), entry.hopperLitres);
            }
        }

        /// <summary>Fills a container's slots from saved data, skipping anything unknown.</summary>
        void RestoreSlots(List<ItemStackData> saved, MadVoxel.Inventory.Inventory into)
        {
            if (saved == null || into == null) return;

            int slots = Mathf.Min(saved.Count, into.Size);
            for (int s = 0; s < slots; s++)
            {
                var slot = saved[s];
                if (slot == null || string.IsNullOrEmpty(slot.itemId) || slot.count <= 0) continue;

                var item = _content.Item(slot.itemId);
                if (item == null) continue;

                var stack = new ItemStack(item, slot.count, slot.durability);
                stack.SpoilRemaining = MadVoxel.Inventory.Spoil.SpoilRules.Normalise(item, slot.spoilRemaining);
                into.SetSlot(s, stack);
            }
        }

        /// <summary>Puts a device's own state back, and records its new node id.</summary>
        void RestoreUtilities(PlacedStructure placed, StructureSaveData entry,
                              Dictionary<int, int> powerRemap, Dictionary<int, int> fluidRemap)
        {
            var electrical = placed.GetComponent<PowerDeviceStructure>();
            if (electrical != null && electrical.Node != null && entry.powerNodeId != 0)
            {
                electrical.Node.SwitchedOn = entry.powerSwitchedOn;
                electrical.Node.FuelLitres = entry.fuelLitres;
                electrical.Node.StoredWattHours = entry.storedWattHours;
                powerRemap[entry.powerNodeId] = electrical.NodeId;
            }

            var fitting = placed.GetComponent<FluidDeviceStructure>();
            if (fitting != null && fitting.Node != null && entry.fluidNodeId != 0)
            {
                fitting.Node.SwitchedOn = entry.fluidSwitchedOn;
                fitting.Node.Litres = entry.litres;
                fitting.Node.IsBroken = entry.fluidBroken;
                fluidRemap[entry.fluidNodeId] = fitting.NodeId;
            }
        }

        /// <summary>
        /// Reconnects the grid and the plumbing. A link whose ends did not both come
        /// back - a device removed by a mod, say - is dropped with a warning rather
        /// than wired to whatever now holds that id.
        /// </summary>
        void RestoreLinks(StructuresSaveData data, Dictionary<int, int> powerRemap, Dictionary<int, int> fluidRemap)
        {
            int droppedWires = 0, droppedHoses = 0;

            if (Power != null && Power.Graph != null)
            {
                for (int i = 0; i < data.wires.Count; i++)
                {
                    int from, to;
                    if (!powerRemap.TryGetValue(data.wires[i].fromId, out from)
                        || !powerRemap.TryGetValue(data.wires[i].toId, out to))
                    {
                        droppedWires++;
                        continue;
                    }

                    // Rules are skipped on restore: a wire that was legal when it was
                    // run stays legal, even if a mod has since shortened the reach.
                    var fromNode = Power.Graph.Get(from);
                    var toNode = Power.Graph.Get(to);
                    if (fromNode == null || toNode == null || fromNode.Outputs.Contains(to)) continue;

                    fromNode.Outputs.Add(to);
                    toNode.Inputs.Add(from);
                }
            }

            if (Fluid != null && Fluid.Graph != null)
            {
                for (int i = 0; i < data.hoses.Count; i++)
                {
                    int from, to;
                    if (!fluidRemap.TryGetValue(data.hoses[i].fromId, out from)
                        || !fluidRemap.TryGetValue(data.hoses[i].toId, out to))
                    {
                        droppedHoses++;
                        continue;
                    }

                    var fromNode = Fluid.Graph.Get(from);
                    var toNode = Fluid.Graph.Get(to);
                    if (fromNode == null || toNode == null || fromNode.Outputs.Contains(to)) continue;

                    fromNode.Outputs.Add(to);
                    toNode.Inputs.Add(from);
                }
            }

            if (droppedWires > 0 || droppedHoses > 0)
            {
                Debug.LogWarningFormat("Save dropped {0} wire(s) and {1} hose(s) whose devices are gone.",
                    droppedWires, droppedHoses);
            }
        }

        void RestoreColony(StructuresSaveData data)
        {
            if (Colony == null || data.colony == null || !data.colony.founded) return;

            var people = new List<ColonistState>();
            for (int i = 0; i < data.colony.colonists.Count; i++)
            {
                var entry = data.colony.colonists[i];
                people.Add(new ColonistState
                {
                    Name = entry.name,
                    Job = (ColonyJob)entry.job,
                    Food = entry.food,
                    Water = entry.water,
                    Morale = entry.morale
                });
            }

            Colony.LoadState(data.colony.colonyName, true, people);
            if (data.colony.sheltering) Colony.OrderShelter(true);
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
