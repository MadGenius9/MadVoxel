using System.Collections.Generic;
using MadVoxel.Building;
using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.AI
{
    /// <summary>
    /// Keeps a population of wandering zombies around the player. Nights are busier
    /// than days; the horde director spawns its own units on top of this.
    /// </summary>
    public class SpawnDirector : MonoBehaviour
    {
        const float SpawnInterval = 3.5f;
        const float MinPlayerDistance = 26f;

        GameConfig _config;
        WorldClock _clock;
        TerrainWorld _voxels;
        ChunkStreamer _streamer;
        StructureWorld _structures;
        BlockDamageTracker _blockDamage;
        Transform _player;
        PlayerStats _playerStats;
        ZombieDefinition _wanderer;
        Transform _root;

        readonly List<Zombie> _alive = new List<Zombie>();
        float _timer;

        public IReadOnlyList<Zombie> Alive { get { return _alive; } }
        public int WanderingCount { get { return CountAlive(false); } }

        /// <summary>Set by the session. Null means no claim has been staked yet.</summary>
        public MadVoxel.Claim.ClaimHeatTracker Heat { get; set; }

        public void Init(GameConfig config, WorldClock clock, TerrainWorld voxels, ChunkStreamer streamer,
                         StructureWorld structures, BlockDamageTracker blockDamage,
                         Transform player, PlayerStats playerStats, ZombieDefinition wanderer)
        {
            _config = config;
            _clock = clock;
            _voxels = voxels;
            _streamer = streamer;
            _structures = structures;
            _blockDamage = blockDamage;
            _player = player;
            _playerStats = playerStats;
            _wanderer = wanderer;

            var rootGo = new GameObject("Zombies");
            rootGo.transform.SetParent(transform, false);
            _root = rootGo.transform;
        }

        void Update()
        {
            if (_config == null || _player == null) return;

            PruneDead();

            _timer += Time.deltaTime;
            if (_timer < SpawnInterval) return;
            _timer = 0f;

            int cap = _clock.IsNight ? _config.wanderingZombieCapNight : _config.wanderingZombieCapDay;

            // A loud claim is advertised. Heat only lifts the night cap: a farm is not
            // more dangerous at noon for having its lights on.
            if (Heat != null && _clock.IsNight)
            {
                cap = Mathf.RoundToInt(cap * MadVoxel.Claim.ClaimHeatMath.WandererMultiplier(Heat.Heat));
            }
            if (CountAlive(false) >= cap) return;

            Vector3 position;
            if (!TryFindSpawn(_player.position, _config.zombieSpawnRadiusMin, _config.zombieSpawnRadiusMax, out position)) return;

            Spawn(_wanderer, position, false);
        }

        void PruneDead()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] == null) _alive.RemoveAt(i);
            }
        }

        int CountAlive(bool hordeUnits)
        {
            int count = 0;
            for (int i = 0; i < _alive.Count; i++)
            {
                if (_alive[i] != null && _alive[i].IsHordeUnit == hordeUnits) count++;
            }
            return count;
        }

        /// <summary>Finds solid ground on the surface, away from the player and outside claims.</summary>
        public bool TryFindSpawn(Vector3 around, float minRadius, float maxRadius, out Vector3 position)
        {
            position = Vector3.zero;
            for (int attempt = 0; attempt < 14; attempt++)
            {
                float angle = UnityEngine.Random.value * Mathf.PI * 2f;
                float radius = Mathf.Lerp(minRadius, maxRadius, UnityEngine.Random.value);
                var candidate = around + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                if (_streamer != null && !_streamer.IsColumnReady(candidate)) continue;

                int wx = Mathf.FloorToInt(candidate.x);
                int wz = Mathf.FloorToInt(candidate.z);
                int surface = _voxels.GetSurfaceY(wx, wz);
                if (surface <= 0 || surface >= TerrainWorld.WorldHeight - 3) continue;

                var spot = new Vector3(wx + 0.5f, surface + 1.05f, wz + 0.5f);
                if (Vector3.Distance(spot, _player.position) < MinPlayerDistance) continue;
                if (_structures != null && _structures.Claims.IsProtected(spot)) continue;
                if (_voxels.IsSolidAt(spot) || _voxels.IsSolidAt(spot + Vector3.up)) continue;

                position = spot;
                return true;
            }
            return false;
        }

        public Zombie Spawn(ZombieDefinition definition, Vector3 position, bool hordeUnit)
        {
            if (definition == null) return null;

            var go = new GameObject(definition.displayName);
            go.transform.SetParent(_root, false);
            go.transform.position = position;

            var zombie = go.AddComponent<Zombie>();
            zombie.Init(definition, _voxels, _structures, _blockDamage,
                        _structures != null ? _structures.Claims : null, _player, _playerStats);
            zombie.IsHordeUnit = hordeUnit;

            _alive.Add(zombie);
            return zombie;
        }

        public void DespawnHordeUnits()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var z = _alive[i];
                if (z == null) { _alive.RemoveAt(i); continue; }
                if (!z.IsHordeUnit) continue;
                _alive.RemoveAt(i);
                UnityEngine.Object.Destroy(z.gameObject);
            }
        }

        public void DespawnAll()
        {
            for (int i = 0; i < _alive.Count; i++)
            {
                if (_alive[i] != null) UnityEngine.Object.Destroy(_alive[i].gameObject);
            }
            _alive.Clear();
        }

        public int HordeUnitCount { get { return CountAlive(true); } }
    }
}
