using UnityEngine;

namespace MadVoxel.World.Terrain
{
    /// <summary>How a block draws itself, when a cube is the wrong shape for it.</summary>
    public enum FoliageForm : byte
    {
        /// <summary>Not foliage. Meshed as a cube or as smooth ground.</summary>
        None,
        /// <summary>A trunk or a branch: a tapered column instead of a square post.</summary>
        Trunk,
        /// <summary>Canopy: drooping fronds instead of a solid green box.</summary>
        Frond
    }

    /// <summary>
    /// Draws trees as trees.
    ///
    /// A pine in this world is a column of log blocks with needle blocks round the
    /// top, and until now that is exactly what it looked like: a square post under a
    /// cube of green. In a world where the ground has been rounded off and the grass
    /// moves, a forest of boxes is the loudest remaining thing.
    ///
    /// The blocks do not change. That is the whole point of doing it here rather than
    /// replacing trees with props: a log is still a log, still choppable, still worth
    /// wood, still saved in the chunk, and every rule in the game that asks about a
    /// block gets the same answer it always did. Only the geometry that gets sent to
    /// the GPU is different.
    ///
    /// Everything is derived from world position, so a tree looks the same every time
    /// its chunk is meshed, and a tree half cut down looks like a tree half cut down
    /// rather than rearranging itself.
    /// </summary>
    public static class FoliageMesher
    {
        const int S = Chunk.Size;
        const int P = Chunk.Size + 2;

        /// <summary>Sides on a trunk. Six reads as round at any distance you can see bark from.</summary>
        const int TrunkSides = 6;

        /// <summary>Trunk radius at the base of a block, in metres.</summary>
        const float TrunkRadius = 0.34f;

        /// <summary>How much narrower the top of each block is. Taper is what stops it reading as a pipe.</summary>
        const float TrunkTaper = 0.93f;

        /// <summary>Fronds per canopy block.</summary>
        const int FrondsPerBlock = 3;

        static ushort Sample(ushort[] padded, int x, int y, int z)
        {
            return padded[((y + 1) * P + (z + 1)) * P + (x + 1)];
        }

        /// <summary>Deterministic 0..1 from a block position and a channel.</summary>
        static float Hash(int x, int y, int z, int channel)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791);
                h ^= (uint)(channel * 2654435761);
                h ^= h >> 13;
                h *= 0x85EBCA6B;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        /// <summary>Builds the foliage in a chunk into <paramref name="data"/>.</summary>
        public static void Build(ushort[] padded, BlockMeta[] meta, ChunkMeshData data)
        {
            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
            for (int x = 0; x < S; x++)
            {
                var id = Sample(padded, x, y, z);
                if (id >= meta.Length) continue;

                switch (meta[id].Foliage)
                {
                    case FoliageForm.Trunk: Trunk(data, id, x, y, z); break;
                    case FoliageForm.Frond: Fronds(data, id, x, y, z); break;
                }
            }
        }

        /// <summary>
        /// One block of trunk, as a tapered six-sided column.
        ///
        /// Open at both ends on purpose: a trunk is a stack of these and capping every
        /// block would put a disc inside the tree at every metre. The top of the stack
        /// is under the canopy and the bottom is in the ground, so neither is ever
        /// seen - and the two triangles saved are paid for a hundred thousand times in
        /// a forest.
        /// </summary>
        static void Trunk(ChunkMeshData data, ushort block, int x, int y, int z)
        {
            // A lean, fixed per column so a whole trunk leans together rather than
            // zig-zagging up the tree.
            float leanX = (Hash(x, 0, z, 1) - 0.5f) * 0.16f;
            float leanZ = (Hash(x, 0, z, 2) - 0.5f) * 0.16f;

            float radius = TrunkRadius * Mathf.Lerp(1f, 0.82f, Hash(x, 0, z, 3));

            var centreLow = new Vector3(x + 0.5f + leanX * y, y, z + 0.5f + leanZ * y);
            var centreHigh = new Vector3(x + 0.5f + leanX * (y + 1), y + 1f, z + 0.5f + leanZ * (y + 1));

            var tris = data.TrianglesFor(block);
            float twist = Hash(x, 0, z, 4) * Mathf.PI * 2f;

            for (int side = 0; side < TrunkSides; side++)
            {
                float a0 = twist + side * Mathf.PI * 2f / TrunkSides;
                float a1 = twist + (side + 1) * Mathf.PI * 2f / TrunkSides;

                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

                int b = data.Vertices.Count;

                data.Vertices.Add(centreLow + d0 * radius);
                data.Vertices.Add(centreLow + d1 * radius);
                data.Vertices.Add(centreHigh + d1 * radius * TrunkTaper);
                data.Vertices.Add(centreHigh + d0 * radius * TrunkTaper);

                data.Normals.Add(d0); data.Normals.Add(d1);
                data.Normals.Add(d1); data.Normals.Add(d0);

                // Bark runs up the trunk, and the V carries the world height so the
                // texture does not restart at every block boundary.
                float u0 = side / (float)TrunkSides;
                float u1 = (side + 1) / (float)TrunkSides;
                data.Uvs.Add(new Vector2(u0, y));
                data.Uvs.Add(new Vector2(u1, y));
                data.Uvs.Add(new Vector2(u1, y + 1f));
                data.Uvs.Add(new Vector2(u0, y + 1f));

                // Darker in the crevices between facets, which is what stops six sides
                // reading as six flat panels.
                byte edge = (byte)(side % 2 == 0 ? 210 : 255);
                var shade = new Color32(edge, edge, edge, 255);
                for (int i = 0; i < 4; i++) data.Colors.Add(shade);

                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }
        }

        /// <summary>
        /// One block of canopy, as a few drooping fronds.
        ///
        /// Angled down rather than standing up: needles hang, and a cluster of upright
        /// quads reads as a bush sitting on a branch. The droop is most of what makes
        /// a pine look like a pine from below, which is where a player stands.
        /// </summary>
        static void Fronds(ChunkMeshData data, ushort block, int x, int y, int z)
        {
            var tris = data.TrianglesFor(block);

            for (int f = 0; f < FrondsPerBlock; f++)
            {
                float yaw = Hash(x, y, z, 10 + f) * Mathf.PI * 2f;
                float droop = Mathf.Lerp(0.25f, 0.6f, Hash(x, y, z, 20 + f));
                float length = Mathf.Lerp(0.55f, 0.95f, Hash(x, y, z, 30 + f));

                var centre = new Vector3(
                    x + 0.3f + Hash(x, y, z, 40 + f) * 0.4f,
                    y + 0.3f + Hash(x, y, z, 50 + f) * 0.4f,
                    z + 0.3f + Hash(x, y, z, 60 + f) * 0.4f);

                var outward = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                var tip = centre + outward * length - Vector3.up * droop;

                // A flat blade from the middle of the block out to the drooping tip,
                // and a second one rolled ninety degrees so it reads from every angle.
                Blade(data, tris, centre, tip, Vector3.Cross(outward, Vector3.up).normalized, 0.18f);
                Blade(data, tris, centre, tip, Vector3.up, 0.14f);
            }
        }

        static void Blade(ChunkMeshData data, System.Collections.Generic.List<int> tris,
                          Vector3 root, Vector3 tip, Vector3 across, float halfWidth)
        {
            int b = data.Vertices.Count;

            data.Vertices.Add(root - across * halfWidth);
            data.Vertices.Add(root + across * halfWidth);
            data.Vertices.Add(tip + across * halfWidth * 0.35f);
            data.Vertices.Add(tip - across * halfWidth * 0.35f);

            var normal = Vector3.Cross(tip - root, across).normalized;
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.up;
            for (int i = 0; i < 4; i++) data.Normals.Add(normal);

            data.Uvs.Add(new Vector2(0f, 0f));
            data.Uvs.Add(new Vector2(1f, 0f));
            data.Uvs.Add(new Vector2(1f, 1f));
            data.Uvs.Add(new Vector2(0f, 1f));

            // Dark inside the canopy, bright at the tips - the same trick the grass
            // uses, and the reason a tree reads as having depth rather than as a
            // green cloud.
            var deep = new Color32(120, 120, 120, 255);
            var light = new Color32(255, 255, 255, 255);
            data.Colors.Add(deep); data.Colors.Add(deep);
            data.Colors.Add(light); data.Colors.Add(light);

            tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);

            // Backfaces, so a frond is visible from underneath - which is where a
            // player standing in a forest is looking at it from.
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
        }
    }
}
