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

        /// <summary>
        /// Builds the foliage in a chunk.
        ///
        /// <paramref name="origin"/> is the chunk's world corner, and everything
        /// random is keyed off it: a tree has to look the way it does because of where
        /// it stands, not because of where it sits inside its chunk. Keyed on the
        /// local index instead, every chunk grows the same trees at the same offsets
        /// and a leaning trunk snaps straight at each chunk boundary.
        ///
        /// Trunks go into the main buffers, because a trunk is solid and the main mesh
        /// is also the collider. Fronds go into the decoration buffers, because
        /// needles never collided with anything and must not start now.
        /// </summary>
        public static void Build(ushort[] padded, BlockMeta[] meta, ChunkMeshData data, Vector3Int origin)
        {
            var canopy = data.Decoration ?? data;

            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
            for (int x = 0; x < S; x++)
            {
                var id = Sample(padded, x, y, z);
                if (id >= meta.Length) continue;

                switch (meta[id].Foliage)
                {
                    case FoliageForm.Trunk:
                        Trunk(padded, meta, data, id, x, y, z, origin);
                        break;
                    case FoliageForm.Frond:
                        Fronds(canopy, id, x, y, z, origin);
                        break;
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
        static void Trunk(ushort[] padded, BlockMeta[] meta, ChunkMeshData data, ushort block,
                          int x, int y, int z, Vector3Int origin)
        {
            int wx = origin.x + x;
            int wy = origin.y + y;
            int wz = origin.z + z;

            // A lean, fixed per column from its world position so a whole trunk leans
            // as one tree - and, crucially, bounded. The drawn trunk is what a player
            // aims at, and the cell they mine is derived from where their ray lands,
            // so geometry that wanders out of its own block is geometry that cannot be
            // chopped. Measured from the column's base rather than from the chunk's,
            // which is why the lean no longer grows without limit up the tree.
            float leanX = (Hash(wx, 0, wz, 1) - 0.5f) * 0.10f;
            float leanZ = (Hash(wx, 0, wz, 2) - 0.5f) * 0.10f;

            int run = RunBelow(padded, meta, x, y, z);
            float offsetLow = Mathf.Clamp(run, 0, 6);
            float offsetHigh = Mathf.Clamp(run + 1, 0, 6);

            float radius = TrunkRadius * Mathf.Lerp(1f, 0.82f, Hash(wx, 0, wz, 3));

            var centreLow = new Vector3(x + 0.5f + leanX * offsetLow, y, z + 0.5f + leanZ * offsetLow);
            var centreHigh = new Vector3(x + 0.5f + leanX * offsetHigh, y + 1f, z + 0.5f + leanZ * offsetHigh);

            var tris = data.TrianglesFor(block);
            float twist = Hash(wx, 0, wz, 4) * Mathf.PI * 2f;

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
                data.Uvs.Add(new Vector2(u0, wy));
                data.Uvs.Add(new Vector2(u1, wy));
                data.Uvs.Add(new Vector2(u1, wy + 1f));
                data.Uvs.Add(new Vector2(u0, wy + 1f));

                // Darker in the crevices between facets, which is what stops six sides
                // reading as six flat panels.
                byte edge = (byte)(side % 2 == 0 ? 210 : 255);
                var shade = new Color32(edge, edge, edge, 255);
                for (int i = 0; i < 4; i++) data.Colors.Add(shade);

                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }

            // Capped only where the trunk actually ends. A stump is something a player
            // stands on, and an open tube has no top to stand on - but capping every
            // block would put a disc inside the tree at every metre.
            if (!IsTrunkAt(padded, meta, x, y + 1, z)) Cap(data, tris, centreHigh, radius * TrunkTaper, true, twist);
            if (!IsTrunkAt(padded, meta, x, y - 1, z)) Cap(data, tris, centreLow, radius, false, twist);
        }

        /// <summary>How many trunk blocks are stacked directly below this one.</summary>
        static int RunBelow(ushort[] padded, BlockMeta[] meta, int x, int y, int z)
        {
            int run = 0;
            for (int below = y - 1; below >= -1 && run < 8; below--)
            {
                if (!IsTrunkAt(padded, meta, x, below, z)) break;
                run++;
            }
            return run;
        }

        static bool IsTrunkAt(ushort[] padded, BlockMeta[] meta, int x, int y, int z)
        {
            if (y < -1 || y > S) return false;

            var id = Sample(padded, x, y, z);
            return id < meta.Length && meta[id].Foliage == FoliageForm.Trunk;
        }

        static void Cap(ChunkMeshData data, System.Collections.Generic.List<int> tris,
                        Vector3 centre, float radius, bool up, float twist)
        {
            int middle = data.Vertices.Count;
            var normal = up ? Vector3.up : Vector3.down;

            data.Vertices.Add(centre);
            data.Normals.Add(normal);
            data.Uvs.Add(new Vector2(0.5f, 0.5f));
            data.Colors.Add(new Color32(235, 235, 235, 255));

            for (int side = 0; side <= TrunkSides; side++)
            {
                float a = twist + side * Mathf.PI * 2f / TrunkSides;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));

                data.Vertices.Add(centre + d * radius);
                data.Normals.Add(normal);
                data.Uvs.Add(new Vector2(d.x * 0.5f + 0.5f, d.z * 0.5f + 0.5f));
                data.Colors.Add(new Color32(235, 235, 235, 255));
            }

            for (int side = 0; side < TrunkSides; side++)
            {
                int a = middle + 1 + side;
                int b = middle + 2 + side;

                if (up) { tris.Add(middle); tris.Add(b); tris.Add(a); }
                else { tris.Add(middle); tris.Add(a); tris.Add(b); }
            }
        }

        /// <summary>
        /// One block of canopy, as a few drooping fronds.
        ///
        /// Angled down rather than standing up: needles hang, and a cluster of upright
        /// quads reads as a bush sitting on a branch. The droop is most of what makes
        /// a pine look like a pine from below, which is where a player stands.
        /// </summary>
        static void Fronds(ChunkMeshData data, ushort block, int x, int y, int z, Vector3Int origin)
        {
            int wx = origin.x + x;
            int wy = origin.y + y;
            int wz = origin.z + z;

            var tris = data.TrianglesFor(block);

            for (int f = 0; f < FrondsPerBlock; f++)
            {
                float yaw = Hash(wx, wy, wz, 10 + f) * Mathf.PI * 2f;
                float droop = Mathf.Lerp(0.18f, 0.38f, Hash(wx, wy, wz, 20 + f));
                float length = Mathf.Lerp(0.4f, 0.62f, Hash(wx, wy, wz, 30 + f));

                var centre = new Vector3(
                    x + 0.35f + Hash(wx, wy, wz, 40 + f) * 0.3f,
                    y + 0.35f + Hash(wx, wy, wz, 50 + f) * 0.3f,
                    z + 0.35f + Hash(wx, wy, wz, 60 + f) * 0.3f);

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
