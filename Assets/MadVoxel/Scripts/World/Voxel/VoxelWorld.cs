using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.World.Voxel
{
    /// <summary>
    /// The voxel volume: a sparse dictionary of chunks plus block get/set. It owns no
    /// streaming policy (see <see cref="ChunkStreamer"/>) and no rendering
    /// (see <see cref="ChunkView"/>) - just the data and the edit notifications.
    /// </summary>
    public class VoxelWorld : MonoBehaviour
    {
        public const int WorldHeightChunks = 12;
        public const int WorldHeight = WorldHeightChunks * Chunk.Size; // 0 .. 191

        readonly Dictionary<ChunkCoord, Chunk> _chunks = new Dictionary<ChunkCoord, Chunk>();

        public BlockRegistry Registry { get; private set; }
        public TerrainGenerator Terrain { get; private set; }
        public int Seed { get; private set; }

        /// <summary>world position, old block, new block.</summary>
        public event Action<Vector3Int, ushort, ushort> BlockChanged;
        public event Action<ChunkCoord> MeshInvalidated;

        public ushort AirId { get; private set; }

        public void Init(BlockRegistry registry, int seed)
        {
            Registry = registry;
            Registry.Build();
            Seed = seed;
            AirId = 0;
            Terrain = new TerrainGenerator(seed, registry);
        }

        public IEnumerable<Chunk> LoadedChunks { get { return _chunks.Values; } }
        public int LoadedChunkCount { get { return _chunks.Count; } }

        public static bool InVerticalRange(int wy)
        {
            return wy >= 0 && wy < WorldHeight;
        }

        public bool TryGetChunk(ChunkCoord coord, out Chunk chunk)
        {
            return _chunks.TryGetValue(coord, out chunk);
        }

        public Chunk GetOrCreateChunk(ChunkCoord coord)
        {
            Chunk chunk;
            if (!_chunks.TryGetValue(coord, out chunk))
            {
                chunk = new Chunk(coord);
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

            // A face on the chunk border also changes the neighbour's silhouette.
            if (lx == 0) InvalidateNeighbour(coord.Offset(-1, 0, 0));
            if (lx == Chunk.Mask) InvalidateNeighbour(coord.Offset(1, 0, 0));
            if (ly == 0) InvalidateNeighbour(coord.Offset(0, -1, 0));
            if (ly == Chunk.Mask) InvalidateNeighbour(coord.Offset(0, 1, 0));
            if (lz == 0) InvalidateNeighbour(coord.Offset(0, 0, -1));
            if (lz == Chunk.Mask) InvalidateNeighbour(coord.Offset(0, 0, 1));

            if (BlockChanged != null) BlockChanged(new Vector3Int(wx, wy, wz), old, id);
            return true;
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
                if (n.Y < 0 || n.Y >= WorldHeightChunks) continue; // outside the world is fine
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
