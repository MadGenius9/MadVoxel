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

        public bool IsEmpty { get { return Vertices.Count == 0; } }

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
                    Solid = def != null && !def.isAir && def.solid
                };
            }
            return meta;
        }
    }
}
