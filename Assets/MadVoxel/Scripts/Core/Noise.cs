using UnityEngine;

namespace MadVoxel.Core
{
    /// <summary>
    /// Deterministic hash based value noise. Pure integer hashing so a given seed
    /// produces identical terrain on every machine and every run.
    /// </summary>
    public static class Noise
    {
        public static uint Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 2166136261u;
                h = (h ^ (uint)x) * 16777619u;
                h = (h ^ (uint)y) * 16777619u;
                h = (h ^ (uint)z) * 16777619u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        public static float Hash01(int x, int y, int z, int seed)
        {
            return (Hash(x, y, z, seed) & 0xFFFFFF) / 16777215f;
        }

        static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }

        public static float Value2D(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = Smooth(x - x0);
            float fy = Smooth(y - y0);

            float v00 = Hash01(x0, 0, y0, seed);
            float v10 = Hash01(x0 + 1, 0, y0, seed);
            float v01 = Hash01(x0, 0, y0 + 1, seed);
            float v11 = Hash01(x0 + 1, 0, y0 + 1, seed);

            float a = Mathf.Lerp(v00, v10, fx);
            float b = Mathf.Lerp(v01, v11, fx);
            return Mathf.Lerp(a, b, fy);
        }

        public static float Value3D(float x, float y, float z, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int z0 = Mathf.FloorToInt(z);
            float fx = Smooth(x - x0);
            float fy = Smooth(y - y0);
            float fz = Smooth(z - z0);

            float c000 = Hash01(x0, y0, z0, seed);
            float c100 = Hash01(x0 + 1, y0, z0, seed);
            float c010 = Hash01(x0, y0 + 1, z0, seed);
            float c110 = Hash01(x0 + 1, y0 + 1, z0, seed);
            float c001 = Hash01(x0, y0, z0 + 1, seed);
            float c101 = Hash01(x0 + 1, y0, z0 + 1, seed);
            float c011 = Hash01(x0, y0 + 1, z0 + 1, seed);
            float c111 = Hash01(x0 + 1, y0 + 1, z0 + 1, seed);

            float x00 = Mathf.Lerp(c000, c100, fx);
            float x10 = Mathf.Lerp(c010, c110, fx);
            float x01 = Mathf.Lerp(c001, c101, fx);
            float x11 = Mathf.Lerp(c011, c111, fx);

            float y0v = Mathf.Lerp(x00, x10, fy);
            float y1v = Mathf.Lerp(x01, x11, fy);
            return Mathf.Lerp(y0v, y1v, fz);
        }

        public static float Fbm2D(float x, float y, int seed, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f;
            float amp = 1f;
            float norm = 0f;
            float freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value2D(x * freq, y * freq, seed + i * 7919) * amp;
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        public static float Fbm3D(float x, float y, float z, int seed, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f;
            float amp = 1f;
            float norm = 0f;
            float freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value3D(x * freq, y * freq, z * freq, seed + i * 6151) * amp;
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Ridged noise, useful for cave tunnels: peaks near 1 along thin sheets.</summary>
        public static float Ridged3D(float x, float y, float z, int seed, int octaves)
        {
            float v = Fbm3D(x, y, z, seed, octaves);
            return 1f - Mathf.Abs(v * 2f - 1f);
        }
    }
}
