using UnityEngine;

namespace MadVoxel.Core
{
    /// <summary>Small deterministic RNG so gameplay rolls can be reproduced from a seed.</summary>
    public struct XorRng
    {
        uint _state;

        public XorRng(int seed)
        {
            _state = (uint)seed;
            if (_state == 0u) _state = 0x9E3779B9u;
        }

        public uint NextUInt()
        {
            unchecked
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return _state;
            }
        }

        public float NextFloat()
        {
            return (NextUInt() & 0xFFFFFF) / 16777216f;
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>Inclusive min, exclusive max.</summary>
        public int Range(int min, int max)
        {
            if (max <= min) return min;
            return min + (int)(NextUInt() % (uint)(max - min));
        }

        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }

        public Vector3 OnUnitCircleXZ()
        {
            float a = NextFloat() * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }
    }
}
