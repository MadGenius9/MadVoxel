using System.Collections;
using MadVoxel.AI;
using MadVoxel.Building;
using MadVoxel.Content;
using MadVoxel.Core.Player;
using MadVoxel.Horde;
using MadVoxel.Modding;
using MadVoxel.Claim;
using MadVoxel.Colony;
using MadVoxel.Fluid;
using MadVoxel.Inventory.Spoil;
using MadVoxel.Power;
using MadVoxel.Save;
using MadVoxel.World.Biomes;
using MadVoxel.World.Fields;
using MadVoxel.World.Weather;
using MadVoxel.UI;
using MadVoxel.Vehicles;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.Core
{
    /// <summary>
    /// Builds and tears down a play session: world, player, streaming, threats and
    /// save. It wires systems together and owns the lifecycle - it deliberately holds
    /// no gameplay rules of its own.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        ContentDatabase _content;
        UiRoot _ui;

        GameObject _worldRoot;
        TerrainWorld _voxels;
        ChunkStreamer _streamer;
        StructureWorld _structures;
        BuildingWorld _buildings;
        FieldWorld _fields;
        Vehicles.VehicleWorld _vehicles;
        BlockDamageTracker _blockDamage;
        WorldClock _clock;
        SkyController _sky;
        WeatherDirector _weather;
        PowerWorld _power;
        FluidWorld _fluid;
        ClaimHeatTracker _heat;
        SpoilService _spoil;
        ColonyWorld _colony;
        WeatherEffects _weatherEffects;
        SpawnDirector _spawner;
        HordeDirector _horde;
        SaveService _save;
        PlayerRig _player;
        ChunkFileStore _store;
        DeveloperTools _devTools;

        string _worldName;
        int _seed;
        Vector3 _worldSpawn;
        float _lastEdgeWarning = -99f;

        /// <summary>Set by GameBootstrap; off means no test hotkeys are registered at all.</summary>
        public bool DeveloperToolsEnabled { get; set; }

        /// <summary>Mods loaded this session; stamped into each world save.</summary>
        public System.Collections.Generic.IReadOnlyList<ModManifest> ActiveMods { get; set; }

        public bool IsRunning { get { return _worldRoot != null; } }
        public PlayerRig Player { get { return _player; } }

        public void Init(ContentDatabase content, UiRoot ui)
        {
            _content = content;
            _ui = ui;

            _ui.SaveRequested = () => { if (_save != null) _save.SaveAll(); };
            _ui.QuitToMenuRequested = QuitToMenu;
            _ui.RespawnRequested = Respawn;
        }

        // ------------------------------------------------------------------- start

        public void StartNewWorld(string worldName, int seed)
        {
            Teardown();
            _worldName = SavePaths.Sanitise(worldName);
            _seed = seed;

            BuildWorld(_content.config.startHour, 0);
            GrantStartingKit();

            _save.SaveAll(false);
            Notifications.PostFormat("New world '{0}' - seed {1}", _worldName, _seed);
            StartCoroutine(SpawnWhenReady(true));
        }

        public void LoadWorld(string worldName)
        {
            var meta = WorldSaveIO.ReadWorld(worldName);
            if (meta == null)
            {
                Debug.LogErrorFormat("No world save found for '{0}'.", worldName);
                return;
            }

            Teardown();
            _worldName = SavePaths.Sanitise(worldName);
            _seed = meta.seed;

            WarnAboutMissingMods(meta);
            BuildWorld(meta.totalHours, meta.hordeNumber);

            // The sky and the claim's noise go back before the structures do, so a
            // solar bank restored during a storm is dark on its very first solve
            // rather than briefly lit and then wrong.
            _weather.Force((World.Weather.WeatherKind)meta.weatherKind,
                Mathf.Max(0.5f, meta.weatherHoursRemaining));
            _heat.LoadState(meta.claimHeat);

            var playerData = WorldSaveIO.ReadPlayer(_worldName);
            var structureData = WorldSaveIO.ReadStructures(_worldName);
            _save.RestoreStructures(structureData);
            if (playerData != null) _save.RestorePlayer(playerData);

            Notifications.PostFormat("Loaded '{0}' - day {1}", _worldName, _clock.Day);
            StartCoroutine(SpawnWhenReady(playerData == null));
        }

        /// <summary>
        /// A world built with a mod and reopened without it will have lost content. The
        /// save layer already degrades unknown blocks to air rather than corrupting the
        /// chunk, but the player deserves to be told before they walk into the hole.
        /// </summary>
        void WarnAboutMissingMods(Save.WorldSaveData meta)
        {
            if (meta.mods == null || meta.mods.Count == 0) return;

            var present = new System.Collections.Generic.HashSet<string>();
            if (ActiveMods != null)
            {
                for (int i = 0; i < ActiveMods.Count; i++) present.Add(ActiveMods[i].Id);
            }

            var missing = new System.Collections.Generic.List<string>();
            for (int i = 0; i < meta.mods.Count; i++)
            {
                if (!present.Contains(meta.mods[i])) missing.Add(meta.mods[i]);
            }

            if (missing.Count == 0) return;

            Notifications.PostFormat("This world was built with {0} mod(s) that are not loaded: {1}",
                missing.Count, string.Join(", ", missing.ToArray()));
            Debug.LogWarningFormat("MadVoxel: world '{0}' is missing mod(s): {1}", _worldName, string.Join(", ", missing.ToArray()));
        }

        void BuildWorld(double totalHours, int hordeNumber)
        {
            _worldRoot = new GameObject("World");

            _voxels = _worldRoot.AddComponent<TerrainWorld>();
            _voxels.Init(_content.blocks, _seed, _content.config.worldRadiusChunks, _content.biomes);

            _store = new ChunkFileStore(_worldName, _content.blocks);

            _clock = _worldRoot.AddComponent<WorldClock>();
            _clock.Init(_content.config, totalHours);

            _structures = _worldRoot.AddComponent<StructureWorld>();
            _structures.Init(_voxels, _clock, _content);

            _buildings = _worldRoot.AddComponent<BuildingWorld>();
            _buildings.Init(_voxels);

            _fields = _worldRoot.AddComponent<FieldWorld>();
            _fields.Init(_voxels, _clock, _content);

            _blockDamage = _worldRoot.AddComponent<BlockDamageTracker>();
            _blockDamage.Init(_voxels);

            _sky = _worldRoot.AddComponent<SkyController>();
            _sky.Init(_clock);

            // The sky rolls from wherever the player is standing, so it is created here
            // and pointed at the rig once that exists.
            _weather = _worldRoot.AddComponent<WeatherDirector>();

            _worldSpawn = FindSurfaceSpawn(0, 0);
            _player = PlayerFactory.Create(_content.config, _voxels, _structures, _buildings, _fields, _worldSpawn);
            _player.transform.SetParent(_worldRoot.transform, true);
            _player.Progression.BindPerkTree(_content.perkTree);
            _weather.Init(_clock, _content, () => _voxels.BiomeAt(_player.transform.position));

            // The grid and the plumbing. Power first: the pump asks it for watts.
            _power = _worldRoot.AddComponent<PowerWorld>();
            _power.Init(_structures, _voxels, _clock, _weather, _player.Progression);

            _fluid = _worldRoot.AddComponent<FluidWorld>();
            _fluid.Init(_structures, _voxels, _clock, _weather, _power, _fields, _player.Progression);

            _player.Interaction.BindUtilities(_power, _fluid);

            _heat = _worldRoot.AddComponent<ClaimHeatTracker>();
            _heat.Init(_structures, _power, _fields, _clock, _voxels, _content, _player.Progression);

            _spoil = _worldRoot.AddComponent<SpoilService>();
            _spoil.Init(_structures, _player, _clock, _content);

            _weatherEffects = _worldRoot.AddComponent<WeatherEffects>();
            _weatherEffects.Init(_weather, _structures, _power, _fluid, _clock, _player.Progression, _seed);

            _streamer = _worldRoot.AddComponent<ChunkStreamer>();
            _streamer.Init(_voxels, _content.config, _store, _player.transform);

            _spawner = _worldRoot.AddComponent<SpawnDirector>();
            _spawner.Init(_content.config, _clock, _voxels, _streamer, _structures, _blockDamage,
                          _player.transform, _player.Stats, _content.Zombie(ZombieIds.Shambler));

            _horde = _worldRoot.AddComponent<HordeDirector>();
            _horde.Init(_content.hordeSchedule, _clock, _spawner, _structures, _buildings, _sky, _player.Progression, _player.transform);
            _horde.LoadState(hordeNumber);

            // Heat is read by the horde and the wanderer cap, so it is handed over once
            // both of them exist rather than before they do.
            _horde.Heat = _heat;
            _spawner.Heat = _heat;

            // The colony last: it asks every other system questions and answers none,
            // so everything it talks to has to be standing before it is.
            _colony = _worldRoot.AddComponent<ColonyWorld>();
            _colony.Init(_structures, _buildings, _spawner, _fluid, _power, _clock, _weather, _content, _heat);

            // A board planted before the colony existed still needs pointing at it.
            _structures.Placed += OnStructurePlacedForColony;
            _structures.Removed += OnStructureLostForColony;
            _buildings.Removed += OnPieceLostForColony;

            _save = _worldRoot.AddComponent<SaveService>();
            _save.Init(_content, _streamer, _structures, _buildings, _fields, _clock, _horde, _player,
                       _worldName, _seed, _content.config.autosaveIntervalSeconds);
            _save.ActiveMods = ActiveMods;
            _save.Power = _power;
            _save.Fluid = _fluid;
            _save.Colony = _colony;
            _save.Weather = _weather;
            _save.Heat = _heat;

            if (DeveloperToolsEnabled)
            {
                _devTools = _worldRoot.AddComponent<DeveloperTools>();
                _devTools.Init(_content, _clock, _horde, _content.hordeSchedule, _spawner, _structures, _voxels, _player);
                _devTools.Weather = _weather;
                _devTools.Colony = _colony;
                _devTools.Heat = _heat;
            }

            _ui.CreateGameplayUi(_player, _clock, _horde, _content, _voxels, _streamer,
                                 _structures, _spawner, _weather, _colony, _heat,
                                 _seed, DeveloperToolsEnabled);

            // Machines last of all: the yard needs the field grid to work and the HUD to
            // report into, and the HUD only exists once the gameplay UI is up.
            _vehicles = _worldRoot.AddComponent<VehicleWorld>();
            _vehicles.Init(_content, _structures, _voxels, _fields, _player, _ui.Hud);
            _player.Interaction.Vehicles = _vehicles;
            _save.Vehicles = _vehicles;

            // The claim ring lives in the world, not on the visor, so it hangs off the
            // world root and dies with it.
            var ringGo = new GameObject("ClaimRing");
            ringGo.transform.SetParent(_worldRoot.transform, false);
            ringGo.AddComponent<ClaimRangeRing>().Init(_structures, _player);

            _player.Stats.Died += OnPlayerDied;
            Zombie.Died += OnZombieKilled;
            BedrollStructure.UseRequested += OnBedrollUsed;
        }

        void OnStructurePlacedForColony(Building.PlacedStructure structure)
        {
            var board = structure.GetComponent<ColonyBoardStructure>();
            if (board != null) board.Colony = _colony;
        }

        /// <summary>
        /// Losing something inside the claim is what the colony means by a breach. It
        /// is deliberately generous - a chewed crate counts - because the morale hit is
        /// about the horde getting in at all, not about what it ate.
        /// </summary>
        void OnStructureLostForColony(Building.PlacedStructure structure)
        {
            if (_colony == null || !_colony.Founded) return;

            var claim = _colony.Claim();
            if (claim != null && claim.Contains(structure.transform.position)) _colony.ReportBreach();
        }

        void OnPieceLostForColony(Building.BuildPiece piece)
        {
            if (_colony == null || !_colony.Founded) return;

            var claim = _colony.Claim();
            if (claim != null && claim.Contains(piece.transform.position)) _colony.ReportBreach();
        }

        /// <summary>The colony, for the board screen and the save writer.</summary>
        public ColonyWorld Colony { get { return _colony; } }

        Vector3 FindSurfaceSpawn(int wx, int wz)
        {
            // The generator's height field is deterministic, so a spawn point can be
            // picked before a single chunk exists.
            for (int attempt = 0; attempt < 64; attempt++)
            {
                int x = wx + (attempt % 8) * 7;
                int z = wz + (attempt / 8) * 7;
                int height = _voxels.Terrain.SurfaceHeight(x, z);
                if (height > TerrainGenerator.SeaLevel - 6 && height < TerrainWorld.WorldHeight - 40)
                {
                    return new Vector3(x + 0.5f, height + 2.2f, z + 0.5f);
                }
            }
            return new Vector3(0.5f, TerrainGenerator.SeaLevel + 6f, 0.5f);
        }

        /// <summary>Holds the player still until the ground under them actually exists.</summary>
        IEnumerator SpawnWhenReady(bool placeAtWorldSpawn)
        {
            _player.Motor.enabled = false;
            _ui.SetState(UiState.Playing);
            Notifications.Post("Generating terrain...");

            Vector3 target = placeAtWorldSpawn ? _worldSpawn : _player.transform.position;

            float deadline = Time.realtimeSinceStartup + 30f;
            while (!_streamer.IsAreaReady(target) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (placeAtWorldSpawn)
            {
                int surface = _voxels.GetSurfaceY(Mathf.FloorToInt(target.x), Mathf.FloorToInt(target.z));
                target = new Vector3(target.x, surface + 2f, target.z);
            }

            // Generated is not the same as collidable: the chunk mesh has to be built and
            // baked before the character controller has anything to stand on.
            while (Time.realtimeSinceStartup < deadline && !HasGroundBelow(target))
            {
                yield return null;
            }

            _player.Motor.enabled = true;
            _player.Motor.Teleport(target);
            _player.Stats.GrantInvulnerability(5f);
            Notifications.Post("Wake up. Find wood, find stone, get inside before dark.");
            AnnounceNearestOutpost();
        }

        /// <summary>
        /// The map is finite, so the edge has to push back. A soft clamp reads better
        /// than an invisible wall you can get stuck against.
        /// </summary>
        void KeepInsideWorld()
        {
            var position = _player.transform.position;
            if (_voxels.IsInsideWorld(position)) return;

            float extent = _voxels.WorldExtentMetres - 1.5f;
            var clamped = new Vector3(
                Mathf.Clamp(position.x, -extent, extent),
                position.y,
                Mathf.Clamp(position.z, -extent, extent));

            _player.Motor.Teleport(clamped);

            if (Time.time - _lastEdgeWarning > 8f)
            {
                _lastEdgeWarning = Time.time;
                Notifications.Post("The land runs out here.");
            }
        }

        static bool HasGroundBelow(Vector3 position)
        {
            RaycastHit hit;
            if (!Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                return false;
            return hit.collider.GetComponentInParent<ChunkView>() != null;
        }

        /// <summary>
        /// There is no map or compass yet, so the one navigational hint the player gets
        /// is a bearing to the nearest trader.
        /// </summary>
        void AnnounceNearestOutpost()
        {
            if (_voxels == null || _voxels.Terrain == null || _voxels.Terrain.Pois == null) return;

            World.Terrain.Poi outpost;
            if (!_voxels.Terrain.Pois.TryFindNearestTrader(_player.transform.position, out outpost)) return;

            Vector3 delta = new Vector3(outpost.CentreX, 0f, outpost.CentreZ) - _player.transform.position;
            float distance = delta.magnitude;
            Notifications.PostFormat("Trader outpost about {0}m {1}", Mathf.RoundToInt(distance), Compass(delta));
        }

        static string Compass(Vector3 delta)
        {
            float angle = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            string[] points = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            int index = Mathf.RoundToInt(angle / 45f) % 8;
            return points[index];
        }

        void GrantStartingKit()
        {
            for (int i = 0; i < _content.startingItems.Count; i++)
            {
                var entry = _content.startingItems[i];
                if (entry.item == null || entry.count <= 0) continue;
                _player.Inventory.Bag.Add(entry.item, entry.count);
            }
            _player.Inventory.Select(0);
        }

        void Update()
        {
            if (_player == null || _voxels == null) return;

            KeepInsideWorld();

            // Streaming can lag behind a sprinting player; catch anyone who drops out
            // of the bottom of the world rather than letting them fall forever.
            if (_player.transform.position.y >= -6f) return;

            int x = Mathf.FloorToInt(_player.transform.position.x);
            int z = Mathf.FloorToInt(_player.transform.position.z);
            int surface = Mathf.Max(_voxels.GetSurfaceY(x, z), _voxels.Terrain.SurfaceHeight(x, z));
            _player.Motor.Teleport(new Vector3(x + 0.5f, surface + 2.5f, z + 0.5f));
            _player.Stats.GrantInvulnerability(3f);
        }

        // -------------------------------------------------------------- gameplay

        void OnZombieKilled(Zombie zombie, GameObject killer)
        {
            if (zombie == null || zombie.Definition == null) return;
            if (killer == null || _player == null || killer != _player.gameObject) return;

            _player.Progression.AddXp(zombie.Definition.xpReward, Perks.XpSource.Kill);

            var def = zombie.Definition;
            if (def.dropItem != null && def.dropMax > 0)
            {
                int count = Random.Range(def.dropMin, def.dropMax + 1);
                if (count > 0) _player.Inventory.Collect(def.dropItem, count);
            }
        }

        void OnBedrollUsed(BedrollStructure bedroll)
        {
            if (bedroll == null || _player == null) return;

            _player.HasRespawnPoint = true;
            _player.RespawnPoint = bedroll.transform.position + Vector3.up * 0.4f;

            if (_clock.IsNight && !_horde.IsBloodMoonActive)
            {
                _clock.SkipToHour(_content.config.dawnHour + 0.25f);
                Notifications.Post("Spawn point set. You sleep until dawn.");
            }
            else if (_horde.IsBloodMoonActive)
            {
                Notifications.Post("Spawn point set. Nobody sleeps through a blood moon.");
            }
            else
            {
                Notifications.Post("Spawn point set.");
            }
        }

        void OnPlayerDied()
        {
            string detail;

            if (_content.config.dropBagOnDeath && !_player.Inventory.Bag.IsEmpty)
            {
                var backpack = DropBackpack();
                detail = backpack != null
                    ? string.Format("Day {0}. Your hotbar stayed with you; the rest of your bag is in a backpack where you fell.", _clock.Day)
                    : string.Format("Day {0}. Your bag was lost.", _clock.Day);
            }
            else
            {
                detail = string.Format("Day {0}.", _clock.Day);
            }

            _save.SaveAll(false);
            _ui.ShowDeath(detail);
        }

        StorageStructure DropBackpack()
        {
            var def = _content.Structure(StructureIds.DeathBackpack);
            if (def == null) return null;

            var cell = Vector3Int.FloorToInt(_player.transform.position);
            cell.y = Mathf.Clamp(cell.y, 1, TerrainWorld.WorldHeight - 2);

            // Find the first free cell at or just above where the player fell.
            for (int i = 0; i < 4; i++)
            {
                var candidate = new Vector3Int(cell.x, cell.y + i, cell.z);
                if (_structures.IsOccupied(candidate)) continue;
                if (_voxels.IsSolid(candidate.x, candidate.y, candidate.z)) continue;

                var placed = _structures.Place(def, candidate, 0, -1f, false);
                if (placed == null) continue;

                var storage = placed.GetComponent<StorageStructure>();
                if (storage == null) return null;

                storage.IsDeathBackpack = true;
                _player.Inventory.DumpBagInto(storage.Contents);
                return storage;
            }
            return null;
        }

        void Respawn()
        {
            if (_player == null) return;

            Vector3 target = _player.HasRespawnPoint ? _player.RespawnPoint : _worldSpawn;
            int surface = _voxels.GetSurfaceY(Mathf.FloorToInt(target.x), Mathf.FloorToInt(target.z));
            if (!_player.HasRespawnPoint) target = new Vector3(target.x, surface + 2f, target.z);

            _player.Stats.ResetToFull();
            _player.Motor.Teleport(target);
            _ui.SetState(UiState.Playing);
            Notifications.Post("You come round. Everything still hurts.");
        }

        // ---------------------------------------------------------------- teardown

        public void QuitToMenu()
        {
            if (_save != null) _save.SaveAll(false);
            Teardown();
            _ui.SetState(UiState.MainMenu);
        }

        public void Teardown()
        {
            if (_player != null && _player.Stats != null) _player.Stats.Died -= OnPlayerDied;
            Zombie.Died -= OnZombieKilled;
            BedrollStructure.UseRequested -= OnBedrollUsed;

            StopAllCoroutines();
            _ui.DestroyGameplayUi();

            if (_worldRoot != null) Destroy(_worldRoot);

            _worldRoot = null;
            _voxels = null;
            _streamer = null;
            _structures = null;
            _buildings = null;
            _fields = null;
            _vehicles = null;
            _blockDamage = null;
            _clock = null;
            _sky = null;
            _spawner = null;
            _horde = null;
            _save = null;
            _player = null;
            _devTools = null;
            _store = null;

            Time.timeScale = 1f;
        }

        void OnApplicationQuit()
        {
            if (_save != null) _save.SaveAll(false);
        }
    }
}
