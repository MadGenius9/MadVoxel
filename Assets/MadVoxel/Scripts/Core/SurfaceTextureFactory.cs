using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Core
{
    /// <summary>
    /// Builds tiling grime textures at runtime so the game looks like worn wood, rust
    /// and mud rather than flat gouache, without shipping any art yet.
    /// </summary>
    public static class SurfaceTextureFactory
    {
        const int Size = 128;
        static readonly Dictionary<SurfaceFamily, Texture2D> Cache = new Dictionary<SurfaceFamily, Texture2D>();

        public static Texture2D Get(SurfaceFamily family)
        {
            Texture2D tex;
            if (Cache.TryGetValue(family, out tex) && tex != null) return tex;

            tex = Build(family);
            Cache[family] = tex;
            return tex;
        }

        static Texture2D Build(SurfaceFamily family)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
            tex.name = "MadVoxel_" + family;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 4;

            int seed = (int)family * 7717 + 13;
            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float v = SampleFamily(family, x, y, seed);
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
                    pixels[y * Size + x] = new Color32(b, b, b, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            return tex;
        }

        // Tileable because every sample wraps the lookup coordinates at Size.
        static float TileNoise(float x, float y, float scale, int seed)
        {
            // Sampling a torus keeps the texture seamless at the tile border.
            float u = x / Size * Mathf.PI * 2f;
            float v = y / Size * Mathf.PI * 2f;
            float nx = Mathf.Cos(u) * scale;
            float ny = Mathf.Sin(u) * scale;
            float nz = Mathf.Cos(v) * scale;
            float nw = Mathf.Sin(v) * scale;
            return Noise.Fbm3D(nx + nw * 0.5f, ny, nz, seed, 4);
        }

        static float SampleFamily(SurfaceFamily family, int x, int y, int seed)
        {
            switch (family)
            {
                case SurfaceFamily.Grass:
                case SurfaceFamily.Foliage:
                {
                    float f = TileNoise(x, y, 7f, seed) * 0.55f + TileNoise(x, y, 19f, seed + 3) * 0.45f;
                    return Mathf.Lerp(0.62f, 1.0f, f);
                }
                case SurfaceFamily.Dirt:
                {
                    float f = TileNoise(x, y, 5f, seed) * 0.6f + TileNoise(x, y, 17f, seed + 5) * 0.4f;
                    return Mathf.Lerp(0.58f, 1.0f, f);
                }
                case SurfaceFamily.Sand:
                {
                    float f = TileNoise(x, y, 23f, seed);
                    return Mathf.Lerp(0.80f, 1.0f, f);
                }
                case SurfaceFamily.Stone:
                case SurfaceFamily.Ore:
                {
                    float f = TileNoise(x, y, 4f, seed) * 0.5f + TileNoise(x, y, 11f, seed + 2) * 0.5f;
                    float cracks = Mathf.Abs(TileNoise(x, y, 3f, seed + 9) * 2f - 1f);
                    cracks = Mathf.SmoothStep(0f, 1f, cracks * 1.4f);
                    return Mathf.Lerp(0.45f, 1.0f, f) * Mathf.Lerp(0.72f, 1f, cracks);
                }
                case SurfaceFamily.Wood:
                case SurfaceFamily.Plank:
                {
                    // Stretched grain plus plank seams every 1/4 of the tile.
                    float grain = TileNoise(x * 1f, y * 6f, 9f, seed);
                    float rings = Mathf.Sin((y * 0.55f + grain * 6f)) * 0.5f + 0.5f;
                    float seam = (family == SurfaceFamily.Plank && (y % 32 < 2)) ? 0.55f : 1f;
                    return Mathf.Lerp(0.55f, 1.0f, rings * 0.6f + grain * 0.4f) * seam;
                }
                case SurfaceFamily.Metal:
                {
                    float rust = TileNoise(x, y, 6f, seed);
                    float scratch = TileNoise(x * 4f, y * 0.4f, 21f, seed + 4);
                    return Mathf.Lerp(0.55f, 1.0f, rust * 0.7f + scratch * 0.3f);
                }
                case SurfaceFamily.Concrete:
                {
                    float f = TileNoise(x, y, 8f, seed) * 0.5f + TileNoise(x, y, 31f, seed + 6) * 0.5f;
                    float stain = TileNoise(x, y, 2.5f, seed + 11);
                    return Mathf.Lerp(0.70f, 1.0f, f) * Mathf.Lerp(0.78f, 1f, stain);
                }
                case SurfaceFamily.Cloth:
                {
                    float weave = (Mathf.Sin(x * 1.6f) * Mathf.Sin(y * 1.6f)) * 0.5f + 0.5f;
                    float f = TileNoise(x, y, 13f, seed);
                    return Mathf.Lerp(0.72f, 1.0f, weave * 0.5f + f * 0.5f);
                }
                case SurfaceFamily.Flesh:
                {
                    float f = TileNoise(x, y, 10f, seed);
                    return Mathf.Lerp(0.70f, 1.0f, f);
                }
                default:
                    return 1f;
            }
        }
    }
}
