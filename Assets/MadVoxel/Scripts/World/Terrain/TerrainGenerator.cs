using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.World.Terrain
{
    /// <summary>
    /// Deterministic, thread-safe terrain. Given a seed it produces the same world on
    /// every machine, and it never needs the rest of the game to be loaded - that is
    /// what lets chunk generation run off the main thread.
    /// </summary>
    public class TerrainGenerator
    {
        public const int SeaLevel = 48;
        const int TreeCell = 8;

        readonly int _seed;

        // Block ids are resolved once, on the main thread, so the worker touches no Unity objects.
        readonly ushort _air, _bedrock, _stone, _dirt, _grass, _sand, _gravel;
        readonly ushort _coalOre, _ironOre, _log, _leaves, _scrap, _clay;

        public TerrainGenerator(int seed, BlockRegistry registry)
        {
            _seed = seed;
            registry.Build();
            _air = registry.IdOf(BlockIds.Air);
            _bedrock = registry.IdOf(BlockIds.Bedrock);
            _stone = registry.IdOf(BlockIds.Stone);
            _dirt = registry.IdOf(BlockIds.Dirt);
            _grass = registry.IdOf(BlockIds.Grass);
            _sand = registry.IdOf(BlockIds.Sand);
            _gravel = registry.IdOf(BlockIds.Gravel);
            _coalOre = registry.IdOf(BlockIds.CoalOre);
            _ironOre = registry.IdOf(BlockIds.IronOre);
            _log = registry.IdOf(BlockIds.PineLog);
            _leaves = registry.IdOf(BlockIds.PineNeedles);
            _scrap = registry.IdOf(BlockIds.ScrapHeap);
            _clay = registry.IdOf(BlockIds.Clay);
        }

        /// <summary>0 = ruined farmland (flat, tilled, few trees), 1 = pine scrub (hilly, wooded).</summary>
        public float ScrubFactor(int wx, int wz)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.62f,
                Noise.Fbm2D(wx * 0.0021f, wz * 0.0021f, _seed + 991, 3)));
        }

        public int SurfaceHeight(int wx, int wz)
        {
            float continent = Noise.Fbm2D(wx * 0.0026f, wz * 0.0026f, _seed + 17, 4);
            float hills = Noise.Fbm2D(wx * 0.0130f, wz * 0.0130f, _seed + 233, 4);
            float detail = Noise.Fbm2D(wx * 0.041f, wz * 0.041f, _seed + 457, 2);

            float scrub = ScrubFactor(wx, wz);
            float relief = Mathf.Lerp(9f, 30f, scrub);

            float h = SeaLevel - 6f
                      + continent * 30f
                      + (hills - 0.5f) * relief
                      + (detail - 0.5f) * Mathf.Lerp(1.5f, 4f, scrub);

            return Mathf.Clamp(Mathf.RoundToInt(h), 4, TerrainWorld.WorldHeight - 40);
        }

        /// <summary>Fills a chunk-sized array. Safe to call from a worker thread.</summary>
        public void Generate(ChunkCoord coord, ushort[] blocks)
        {
            var origin = coord.Origin;
            int ox = origin.x, oy = origin.y, oz = origin.z;

            // Cheap reject: chunks far above the tallest possible column are pure air.
            for (int lz = 0; lz < Chunk.Size; lz++)
            {
                int wz = oz + lz;
                for (int lx = 0; lx < Chunk.Size; lx++)
                {
                    int wx = ox + lx;
                    int surface = SurfaceHeight(wx, wz);
                    float scrub = ScrubFactor(wx, wz);
                    int soilDepth = 3 + (int)(Noise.Hash01(wx, 7, wz, _seed + 61) * 3f);

                    for (int ly = 0; ly < Chunk.Size; ly++)
                    {
                        int wy = oy + ly;
                        ushort id = _air;

                        if (wy <= 1)
                        {
                            id = _bedrock;
                        }
                        else if (wy > surface)
                        {
                            id = _air;
                        }
                        else if (wy == surface)
                        {
                            id = surface <= SeaLevel - 10 ? _sand : _grass;
                            // Ruined farmland shows bare tilled dirt in wide patches.
                            if (scrub < 0.35f && Noise.Fbm2D(wx * 0.06f, wz * 0.06f, _seed + 771, 2) > 0.58f)
                                id = _dirt;
                        }
                        else if (wy > surface - soilDepth)
                        {
                            id = surface <= SeaLevel - 10 ? _sand : _dirt;
                            if (Noise.Hash01(wx, wy, wz, _seed + 88) > 0.965f) id = _clay;
                        }
                        else
                        {
                            id = _stone;
                            if (Noise.Hash01(wx, wy, wz, _seed + 121) > 0.985f) id = _gravel;

                            if (wy < 74 && Noise.Fbm3D(wx * 0.055f, wy * 0.075f, wz * 0.055f, _seed + 313, 2) > 0.795f)
                                id = _coalOre;
                            if (wy < 46 && Noise.Fbm3D(wx * 0.062f, wy * 0.080f, wz * 0.062f, _seed + 509, 2) > 0.820f)
                                id = _ironOre;
                        }

                        // Caves. Two ridged sheets intersecting gives tunnels rather than blobs.
                        if (id != _air && id != _bedrock && wy > 3 && wy < surface - 1)
                        {
                            float a = Noise.Ridged3D(wx * 0.017f, wy * 0.030f, wz * 0.017f, _seed + 1201, 2);
                            float b = Noise.Ridged3D(wx * 0.017f, wy * 0.030f, wz * 0.017f, _seed + 1303, 2);
                            float taper = Mathf.InverseLerp(surface - 1f, surface - 9f, wy);
                            if (a > 0.945f && b > 0.925f && taper > 0.05f) id = _air;
                        }

                        blocks[Chunk.Index(lx, ly, lz)] = id;
                    }
                }
            }

            ApplySurfaceScrap(coord, blocks);
            ApplyTrees(coord, blocks);
        }

        /// <summary>Rusted heaps poking out of the farmland: the early metal source.</summary>
        void ApplySurfaceScrap(ChunkCoord coord, ushort[] blocks)
        {
            var origin = coord.Origin;
            for (int lz = 0; lz < Chunk.Size; lz++)
            {
                int wz = origin.z + lz;
                for (int lx = 0; lx < Chunk.Size; lx++)
                {
                    int wx = origin.x + lx;
                    if (Noise.Hash01(wx, 3, wz, _seed + 4409) < 0.9955f) continue;

                    int surface = SurfaceHeight(wx, wz);
                    int height = 1 + (int)(Noise.Hash01(wx, 4, wz, _seed + 4410) * 2.99f);
                    for (int i = 0; i < height; i++)
                    {
                        Put(blocks, coord, wx, surface + i, wz, _scrap, true);
                    }
                }
            }
        }

        void ApplyTrees(ChunkCoord coord, ushort[] blocks)
        {
            var origin = coord.Origin;
            int minCellX = ChunkCoord.FloorDiv(origin.x - 3, TreeCell);
            int maxCellX = ChunkCoord.FloorDiv(origin.x + Chunk.Size + 3, TreeCell);
            int minCellZ = ChunkCoord.FloorDiv(origin.z - 3, TreeCell);
            int maxCellZ = ChunkCoord.FloorDiv(origin.z + Chunk.Size + 3, TreeCell);

            for (int cz = minCellZ; cz <= maxCellZ; cz++)
            {
                for (int cx = minCellX; cx <= maxCellX; cx++)
                {
                    uint h = Noise.Hash(cx, 5, cz, _seed + 6607);
                    int tx = cx * TreeCell + (int)(h % TreeCell);
                    int tz = cz * TreeCell + (int)((h >> 8) % TreeCell);

                    float scrub = ScrubFactor(tx, tz);
                    float density = Mathf.Lerp(0.06f, 0.55f, scrub);
                    if (Noise.Hash01(tx, 6, tz, _seed + 6608) > density) continue;

                    int surface = SurfaceHeight(tx, tz);
                    if (surface <= SeaLevel - 8 || surface > TerrainWorld.WorldHeight - 48) continue;

                    // Skip trees on steep ground so they do not hang off cliffs.
                    if (Mathf.Abs(SurfaceHeight(tx + 1, tz) - surface) > 2) continue;
                    if (Mathf.Abs(SurfaceHeight(tx, tz + 1) - surface) > 2) continue;

                    BuildPine(blocks, coord, tx, surface + 1, tz, (int)((h >> 16) % 4u));
                }
            }
        }

        void BuildPine(ushort[] blocks, ChunkCoord coord, int wx, int baseY, int wz, int variant)
        {
            int trunk = 4 + variant;
            for (int i = 0; i < trunk; i++)
            {
                Put(blocks, coord, wx, baseY + i, wz, _log, false);
            }

            // Tapered needle cone, widest two blocks below the tip.
            int top = baseY + trunk;
            for (int layer = 0; layer < 4; layer++)
            {
                int y = top - layer;
                int radius = layer == 0 ? 0 : (layer == 3 ? 2 : 1);
                for (int dz = -radius; dz <= radius; dz++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Mathf.Abs(dx) == radius && Mathf.Abs(dz) == radius && radius > 1) continue;
                        Put(blocks, coord, wx + dx, y, wz + dz, _leaves, true);
                    }
                }
            }
            Put(blocks, coord, wx, top + 1, wz, _leaves, true);
        }

        void Put(ushort[] blocks, ChunkCoord coord, int wx, int wy, int wz, ushort id, bool onlyReplaceAir)
        {
            var origin = coord.Origin;
            int lx = wx - origin.x;
            int ly = wy - origin.y;
            int lz = wz - origin.z;
            if (lx < 0 || lx >= Chunk.Size || ly < 0 || ly >= Chunk.Size || lz < 0 || lz >= Chunk.Size) return;

            int i = Chunk.Index(lx, ly, lz);
            if (onlyReplaceAir && blocks[i] != _air) return;
            blocks[i] = id;
        }

        /// <summary>Safe standing height for spawning the player or a trader.</summary>
        public int SpawnHeight(int wx, int wz)
        {
            return SurfaceHeight(wx, wz) + 1;
        }
    }
}
