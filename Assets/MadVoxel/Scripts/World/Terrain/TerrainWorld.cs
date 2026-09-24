using System;
using MadVoxel.World.Biomes;
using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.World.Terrain
{
    /// <summary>
    /// The voxel volume: a sparse dictionary of chunks plus block get/set. It owns no
    /// streaming policy (see <see cref="ChunkStreamer"/>) and no rendering
    /// (see <see cref="ChunkView"/>) - just the data and the edit notifications.
    /// </summary>
    public class TerrainWorld : MonoBehaviour
    {
        public const int WorldHeightChunks = 12;
        public const int WorldHeight = WorldHeightChunks * Chunk.Size; // 0 .. 191

        /// <summary>
        /// The map is large but finite. Beyond the edge there is no terrain at all, and
        /// the streamer stops generating, so the world has a real boundary rather than
        /// running forever.
        /// </summary>
        public int RadiusChunks { get; private set; }

        readonly Dictionary<ChunkCoord, Chunk> _chunks = new Dictionary<ChunkCoord, Chunk>();

        public BlockRegistry Registry { get; private set; }
        public TerrainGenerator Terrain { get; private set; }
        public int Seed { get; private set; }

        /// <summary>world position, old block, new block.</summary>
        public event Action<Vector3Int, ushort, ushort> BlockChanged;
        public event Action<ChunkCoord> MeshInvalidated;

        public ushort AirId { get; private set; }

        public void Init(BlockRegistry registry, int seed, int radiusChunks, BiomeTable biomes = null)
        {
            RadiusChunks = Mathf.Max(4, radiusChunks);
            Registry = registry;
            Registry.Build();
            Seed = seed;
            AirId = 0;
            Terrain = new TerrainGenerator(seed, registry, RadiusChunks * Chunk.Size, biomes);
        }

        /// <summary>
        /// The region a world column belongs to. Everything outside terrain generation -
        /// farming, weather, the pump, claim heat - asks here rather than keeping its
        /// own copy of the paint.
        /// </summary>
        public BiomeId BiomeAt(int wx, int wz)
        {
            return Terrain != null ? Terrain.BiomeAt(wx, wz) : BiomeId.Farmland;
        }

        public BiomeId BiomeAt(Vector3 world)
        {
            return BiomeAt(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.z));
        }

        public IEnumerable<Chunk> LoadedChunks { get { return _chunks.Values; } }
        public int LoadedChunkCount { get { return _chunks.Count; } }

        public static bool InVerticalRange(int wy)
        {
            return wy >= 0 && wy < WorldHeight;
        }

        /// <summary>Is this chunk column inside the finite map?</summary>
        public bool InBounds(int chunkX, int chunkZ)
        {
            return chunkX >= -RadiusChunks && chunkX < RadiusChunks
                && chunkZ >= -RadiusChunks && chunkZ < RadiusChunks;
        }

        public bool InBounds(ChunkCoord coord)
        {
            return InBounds(coord.X, coord.Z);
        }

        /// <summary>World-space extent of the playable area, for the map edge and spawn placement.</summary>
        public float WorldExtentMetres { get { return RadiusChunks * Chunk.Size; } }

        public bool IsInsideWorld(Vector3 position)
        {
            float extent = WorldExtentMetres;
            return position.x >= -extent && position.x < extent
                && position.z >= -extent && position.z < extent;
        }

        public bool TryGetChunk(ChunkCoord coord, out Chunk chunk)
        {
            return _chunks.TryGetValue(coord, out chunk);
        }

        public Chunk GetOrCreateChunk(ChunkCoord coord)
        {
            if (!InBounds(coord)) return null;

            Chunk chunk;
            if (!_chunks.TryGetValue(coord, out chunk))
            {
                chunk = new Chunk(coord);
                // Paint is deterministic from the seed, so it is resolved the moment the
                // chunk exists rather than waiting on generation or a load.
                if (Terrain != null) chunk.Biome = Terrain.BiomeOfChunk(coord);
                _chunks.Add(coord, chunk);
            }
            return chunk;
        }

        public bool RemoveChunk(ChunkCoord coord)
        {
            return _chunks.Remove(coord);
        }

        public ushort GetBlock(int wx, int wy, int wz)
        {
            if (!InVerticalRange(wy)) return AirId;
            var coord = ChunkCoord.FromWorld(wx, wy, wz);
            Chunk chunk;
            if (!_chunks.TryGetValue(coord, out chunk) || !chunk.Generated) return AirId;
            return chunk.Get(ChunkCoord.FloorMod(wx, Chunk.Size),
                             ChunkCoord.FloorMod(wy, Chunk.Size),
                             ChunkCoord.FloorMod(wz, Chunk.Size));
        }

        public ushort GetBlock(Vector3Int p)
        {
            return GetBlock(p.x, p.y, p.z);
        }

        public BlockDefinition GetBlockDef(int wx, int wy, int wz)
        {
            return Registry.ById(GetBlock(wx, wy, wz));
        }

        public bool IsSolid(int wx, int wy, int wz)
        {
            return Registry.IsSolid(GetBlock(wx, wy, wz));
        }

        public bool IsSolidAt(Vector3 world)
        {
            return IsSolid(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y), Mathf.FloorToInt(world.z));
        }

        /// <summary>Returns false when the target chunk is not loaded, so callers never edit thin air.</summary>
        public bool SetBlock(int wx, int wy, int wz, ushort id, bool markDirty = true)
        {
            if (!InVerticalRange(wy)) return false;

            var coord = ChunkCoord.FromWorld(wx, wy, wz);
            Chunk chunk;
            if (!_chunks.TryGetValue(coord, out chunk) || !chunk.Generated) return false;

            int lx = ChunkCoord.FloorMod(wx, Chunk.Size);
            int ly = ChunkCoord.FloorMod(wy, Chunk.Size);
            int lz = ChunkCoord.FloorMod(wz, Chunk.Size);

            ushort old = chunk.Get(lx, ly, lz);
            if (old == id) return false;
            if (!chunk.Set(lx, ly, lz, id)) return false;

            if (markDirty) chunk.Dirty = true;
            chunk.MeshDirty = true;
            if (MeshInvalidated != null) MeshInvalidated(coord);

            // A block on the border changes the neighbour's silhouette - and for
            // smooth ground it changes the diagonal neighbours too, because surface
            // nets reads a block's corners rather than only its faces. Six neighbours
            // was right while everything was cubes; with rounded terrain it leaves a
            // crack in the mesh and, worse, in the collider, which is a hole a player
            // falls through at the exact place they were just digging.
            int nx = lx == 0 ? -1 : (lx == Chunk.Mask ? 1 : 0);
            int ny = ly == 0 ? -1 : (ly == Chunk.Mask ? 1 : 0);
            int nz = lz == 0 ? -1 : (lz == Chunk.Mask ? 1 : 0);

            // Every neighbour this block touches, including the diagonals. At most
            // seven, and only for a block actually on a border.
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dy == 0 && dz == 0) continue;

                // Only step towards a face this block is actually against.
                if (dx != 0 && dx != nx) continue;
                if (dy != 0 && dy != ny) continue;
                if (dz != 0 && dz != nz) continue;

                InvalidateNeighbour(coord.Offset(dx, dy, dz));
            }

            RaiseChanged(wx, wy, wz, old, id);
            return true;
        }

        void RaiseChanged(int wx, int wy, int wz, ushort old, ushort id)
        {
            if (BlockChanged != null) BlockChanged(new Vector3Int(wx, wy, wz), old, id);
        }

        void InvalidateNeighbour(ChunkCoord coord)
        {
            Chunk chunk;
            if (!_chunks.TryGetValue(coord, out chunk)) return;
            chunk.MeshDirty = true;
            if (MeshInvalidated != null) MeshInvalidated(coord);
        }

        /// <summary>True when every chunk touching this one has finished generating.</summary>
        public bool NeighboursGenerated(ChunkCoord coord)
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dy == 0 && dz == 0) continue;
                var n = coord.Offset(dx, dy, dz);
                if (n.Y < 0 || n.Y >= WorldHeightChunks) continue; // above or below the world is fine
                if (!InBounds(n)) continue;                        // past the map edge is permanently empty
                Chunk c;
                if (!_chunks.TryGetValue(n, out c) || !c.Generated) return false;
            }
            return true;
        }

        /// <summary>
        /// Copies an 18^3 padded block window for the mesher. Called on the main thread;
        /// the returned array is then safe to hand to a worker.
        /// </summary>
        public ushort[] BuildPaddedSnapshot(ChunkCoord coord)
        {
            const int P = Chunk.Size + 2;
            var padded = new ushort[P * P * P];

            // Resolve the 27 chunks that can contribute once, instead of paying a
            // dictionary lookup for every one of the 5832 voxels.
            var neighbours = new Chunk[27];
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                Chunk c;
                var n = coord.Offset(dx, dy, dz);
                if (n.Y < 0 || n.Y >= WorldHeightChunks) continue;
                if (_chunks.TryGetValue(n, out c) && c.Generated)
                {
                    neighbours[(dy + 1) * 9 + (dz + 1) * 3 + (dx + 1)] = c;
                }
            }

            for (int y = -1; y <= Chunk.Size; y++)
            {
                int oy = y < 0 ? -1 : (y >= Chunk.Size ? 1 : 0);
                int ly = y & Chunk.Mask;

                for (int z = -1; z <= Chunk.Size; z++)
                {
                    int oz = z < 0 ? -1 : (z >= Chunk.Size ? 1 : 0);
                    int lz = z & Chunk.Mask;
                    int rowBase = ((y + 1) * P + (z + 1)) * P;

                    for (int x = -1; x <= Chunk.Size; x++)
                    {
                        int ox = x < 0 ? -1 : (x >= Chunk.Size ? 1 : 0);
                        var chunk = neighbours[(oy + 1) * 9 + (oz + 1) * 3 + (ox + 1)];
                        padded[rowBase + (x + 1)] = chunk != null ? chunk.Get(x & Chunk.Mask, ly, lz) : AirId;
                    }
                }
            }
            return padded;
        }

        /// <summary>
        /// Highest non-air block at a column, searching loaded chunks. Falls back to the
        /// generator's height field when the column is not loaded yet.
        /// </summary>
        public int GetSurfaceY(int wx, int wz)
        {
            for (int y = WorldHeight - 1; y >= 0; y--)
            {
                var coord = ChunkCoord.FromWorld(wx, y, wz);
                Chunk chunk;
                if (!_chunks.TryGetValue(coord, out chunk) || !chunk.Generated)
                {
                    // Skip the whole chunk instead of stepping one voxel at a time.
                    y = coord.Y * Chunk.Size;
                    continue;
                }
                ushort id = chunk.Get(ChunkCoord.FloorMod(wx, Chunk.Size),
                                      ChunkCoord.FloorMod(y, Chunk.Size),
                                      ChunkCoord.FloorMod(wz, Chunk.Size));
                if (!Registry.IsAir(id)) return y;
            }
            return Terrain.SurfaceHeight(wx, wz);
        }

        public void Clear()
        {
            _chunks.Clear();
        }
    }
}
