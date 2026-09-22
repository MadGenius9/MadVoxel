using System.Collections;
using MadVoxel.AI;
using MadVoxel.Building;
using MadVoxel.Content;
using MadVoxel.Core.Player;
using MadVoxel.Horde;
using MadVoxel.Save;
using MadVoxel.UI;
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
        BlockDamageTracker _blockDamage;
        WorldClock _clock;
        SkyController _sky;
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

            BuildWorld(meta.totalHours, meta.hordeNumber);

            var playerData = WorldSaveIO.ReadPlayer(_worldName);
            var structureData = WorldSaveIO.ReadStructures(_worldName);
            _save.RestoreStructures(structureData);
            if (playerData != null) _save.RestorePlayer(playerData);

            Notifications.PostFormat("Loaded '{0}' - day {1}", _worldName, _clock.Day);
            StartCoroutine(SpawnWhenReady(playerData == null));
        }

        void BuildWorld(double totalHours, int hordeNumber)
        {
            _worldRoot = new GameObject("World");

            _voxels = _worldRoot.AddComponent<TerrainWorld>();
            _voxels.Init(_content.blocks, _seed, _content.config.worldRadiusChunks);

            _store = new ChunkFileStore(_worldName, _content.blocks);

            _structures = _worldRoot.AddComponent<StructureWorld>();
            _structures.Init(_voxels);

            _buildings = _worldRoot.AddComponent<BuildingWorld>();
            _buildings.Init(_voxels);

            _blockDamage = _worldRoot.AddComponent<BlockDamageTracker>();
            _blockDamage.Init(_voxels);

            _clock = _worldRoot.AddComponent<WorldClock>();
            _clock.Init(_content.config, totalHours);

            _sky = _worldRoot.AddComponent<SkyController>();
            _sky.Init(_clock);

            _worldSpawn = FindSurfaceSpawn(0, 0);
            _player = PlayerFactory.Create(_content.config, _voxels, _structures, _buildings, _worldSpawn);
            _player.transform.SetParent(_worldRoot.transform, true);

            _streamer = _worldRoot.AddComponent<ChunkStreamer>();
            _streamer.Init(_voxels, _content.config, _store, _player.transform);

            _spawner = _worldRoot.AddComponent<SpawnDirector>();
            _spawner.Init(_content.config, _clock, _voxels, _streamer, _structures, _blockDamage,
                          _player.transform, _player.Stats, _content.Zombie(ZombieIds.Shambler));

            _horde = _worldRoot.AddComponent<HordeDirector>();
            _horde.Init(_content.hordeSchedule, _clock, _spawner, _structures, _buildings, _sky, _player.Progression, _player.transform);
            _horde.LoadState(hordeNumber);

            _save = _worldRoot.AddComponent<SaveService>();
            _save.Init(_content, _streamer, _structures, _buildings, _clock, _horde, _player,
                       _worldName, _seed, _content.config.autosaveIntervalSeconds);

            if (DeveloperToolsEnabled)
            {
                _devTools = _worldRoot.AddComponent<DeveloperTools>();
                _devTools.Init(_content, _clock, _horde, _content.hordeSchedule, _spawner, _voxels, _player);
            }

            _ui.CreateGameplayUi(_player, _clock, _horde, _content, _voxels, _streamer, _seed, DeveloperToolsEnabled);

            _player.Stats.Died += OnPlayerDied;
            Zombie.Died += OnZombieKilled;
            BedrollStructure.UseRequested += OnBedrollUsed;
        }

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
