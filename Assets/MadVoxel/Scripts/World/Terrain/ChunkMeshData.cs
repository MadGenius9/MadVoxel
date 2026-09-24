using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.World.Terrain
{
    /// <summary>
    /// Plain mesh buffers produced off the main thread. One sub-mesh per block type so
    /// each keeps its own material without needing a texture atlas yet.
    /// </summary>
    public class ChunkMeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>(2048);
        public readonly List<Vector3> Normals = new List<Vector3>(2048);
        public readonly List<Vector2> Uvs = new List<Vector2>(2048);

        /// <summary>
        /// Baked ambient occlusion, one greyscale value per vertex.
        ///
        /// Corners, trenches and doorways read perfectly flat without it, which is the
        /// single strongest reason a cube world looks like a toy. Computed on the
        /// worker thread from neighbour occupancy, so it costs nothing at runtime.
        /// </summary>
        public readonly List<Color32> Colors = new List<Color32>(2048);

        /// <summary>Block runtime id -> triangle indices into <see cref="Vertices"/>.</summary>
        public readonly Dictionary<ushort, List<int>> Triangles = new Dictionary<ushort, List<int>>();
        public readonly List<ushort> BlockOrder = new List<ushort>();

        /// <summary>
        /// Nothing to draw. Triangles decide it, not vertices.
        ///
        /// Surface nets places a vertex in any cell the surface crosses, including the
        /// border cells a chunk shares with the one below it - so a chunk of pure air
        /// sitting on top of ground produces a few hundred vertices and not one
        /// triangle. Counting vertices, the streamer would keep a live renderer and a
        /// zero-triangle mesh collider for every empty chunk above the landscape.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                if (Vertices.Count == 0) return true;

                foreach (var list in Triangles)
                {
                    if (list.Value.Count > 0) return false;
                }
                return true;
            }
        }

        public List<int> TrianglesFor(ushort blockId)
        {
            List<int> list;
            if (!Triangles.TryGetValue(blockId, out list))
            {
                list = new List<int>(512);
                Triangles.Add(blockId, list);
                BlockOrder.Add(blockId);
            }
            return list;
        }

        public void Clear()
        {
            Vertices.Clear();
            Normals.Clear();
            Uvs.Clear();
            Colors.Clear();
            Triangles.Clear();
            BlockOrder.Clear();
        }
    }

    /// <summary>Thread-safe snapshot of the block flags the mesher needs.</summary>
    public struct BlockMeta
    {
        public bool Air;
        public bool Opaque;
        public bool Solid;

        /// <summary>Natural ground, meshed as a smooth surface instead of as cubes.</summary>
        public bool Smooth;

        public static BlockMeta[] Snapshot(BlockRegistry registry)
        {
            registry.Build();
            var meta = new BlockMeta[registry.Count];
            for (int i = 0; i < registry.Count; i++)
            {
                var def = registry.ById((ushort)i);
                meta[i] = new BlockMeta
                {
                    Air = def == null || def.isAir,
                    Opaque = def != null && !def.isAir && def.opaque,
                    Solid = def != null && !def.isAir && def.solid,
                    Smooth = def != null && !def.isAir && def.smoothTerrain
                };
            }
            return meta;
        }
    }
}
