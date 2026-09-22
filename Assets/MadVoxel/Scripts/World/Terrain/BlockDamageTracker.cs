using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.World.Terrain
{
    /// <summary>
    /// Accumulated damage on individual voxels. Several zombies chewing the same wall
    /// share progress, and untouched damage decays so a wall repairs itself if the
    /// horde moves on.
    /// </summary>
    public class BlockDamageTracker : MonoBehaviour
    {
        struct Entry
        {
            public float Damage;
            public float LastTouched;
        }

        const float DecayDelay = 25f;
        const float DecayPerSecond = 12f;

        readonly Dictionary<Vector3Int, Entry> _damage = new Dictionary<Vector3Int, Entry>();
        readonly List<Vector3Int> _scratch = new List<Vector3Int>();

        TerrainWorld _world;
        float _sweepTimer;

        public void Init(TerrainWorld world)
        {
            _world = world;
            _world.BlockChanged += (cell, oldId, newId) => _damage.Remove(cell);
        }

        /// <summary>Returns true when the block broke.</summary>
        public bool Damage(Vector3Int cell, float amount)
        {
            var def = _world.GetBlockDef(cell.x, cell.y, cell.z);
            if (def == null || def.isAir) return false;
            if (def.hardness < 0f) return false; // bedrock shrugs it off

            Entry entry;
            if (!_damage.TryGetValue(cell, out entry)) entry = new Entry();

            entry.Damage += amount;
            entry.LastTouched = Time.time;

            if (entry.Damage >= def.structureHealth)
            {
                _damage.Remove(cell);
                _world.SetBlock(cell.x, cell.y, cell.z, _world.AirId);
                return true;
            }

            _damage[cell] = entry;
            return false;
        }

        public float GetDamage(Vector3Int cell)
        {
            Entry entry;
            return _damage.TryGetValue(cell, out entry) ? entry.Damage : 0f;
        }

        void Update()
        {
            _sweepTimer += Time.deltaTime;
            if (_sweepTimer < 1f) return;
            _sweepTimer = 0f;

            _scratch.Clear();
            float now = Time.time;
            foreach (var kv in _damage)
            {
                if (now - kv.Value.LastTouched > DecayDelay) _scratch.Add(kv.Key);
            }

            for (int i = 0; i < _scratch.Count; i++)
            {
                var key = _scratch[i];
                var entry = _damage[key];
                entry.Damage -= DecayPerSecond;
                if (entry.Damage <= 0f) _damage.Remove(key);
                else _damage[key] = entry;
            }
        }

        public void Clear()
        {
            _damage.Clear();
        }
    }
}
