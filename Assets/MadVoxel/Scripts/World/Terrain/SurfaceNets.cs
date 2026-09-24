using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.World.Terrain
{
    /// <summary>
    /// Rounds natural ground off, so a dug crater looks like a crater instead of a
    /// staircase.
    ///
    /// The brief is 7 Days to Die's terrain, and the thing that makes 7DTD's ground
    /// read as ground is that it is not cubes: natural terrain is rendered as a
    /// smoothed surface while player-built blocks keep hard edges. That split is the
    /// whole effect. A world where everything is smooth looks like clay; a world where
    /// everything is cubes looks like Minecraft; the contrast between the two is what
    /// makes a built wall read as built.
    ///
    /// This runs over the occupancy the chunk already stores rather than over a
    /// density field. Real 7DTD keeps a density per voxel, which buys partially-dug
    /// cells - a crater with a lip rather than a crater with a whole-block edge. That
    /// is a larger change: it touches chunk storage, the save format and every caller
    /// that digs. The shape is most of the win and this gets the shape, so density can
    /// come later without any of this being redone.
    ///
    /// Naive surface nets rather than marching cubes: one vertex per cell placed at
    /// the average of its crossing edges, quads joining the four cells around each
    /// sign change. Fewer triangles, no 256-case table, and the vertex welding falls
    /// out of the algorithm instead of needing a pass.
    /// </summary>
    public static class SurfaceNets
    {
        const int S = Chunk.Size;
        const int P = Chunk.Size + 2;

        /// <summary>Cells run from -1 to S-1, so a chunk joins its neighbours seamlessly.</summary>
        const int Lo = -1;
        const int Span = S + 1;

        /// <summary>The 8 corners of a cell, in the order the edge table expects.</summary>
        static readonly int[,] Corners =
        {
            { 0, 0, 0 }, { 1, 0, 0 }, { 0, 1, 0 }, { 1, 1, 0 },
            { 0, 0, 1 }, { 1, 0, 1 }, { 0, 1, 1 }, { 1, 1, 1 }
        };

        /// <summary>The 12 edges, as pairs of corner indices.</summary>
        static readonly int[,] Edges =
        {
            { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 },
            { 0, 2 }, { 1, 3 }, { 4, 6 }, { 5, 7 },
            { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }
        };

        [System.ThreadStatic] static int[] _vertexAt;

        /// <summary>Reused per cell. Eight bools allocated 4913 times a chunk is not free.</summary>
        [System.ThreadStatic] static bool[] _corner;

        static int CellIndex(int x, int y, int z)
        {
            return ((y - Lo) * Span + (z - Lo)) * Span + (x - Lo);
        }

        static ushort Sample(ushort[] padded, int x, int y, int z)
        {
            return padded[((y + 1) * P + (z + 1)) * P + (x + 1)];
        }

        /// <summary>
        /// Builds the smooth half of a chunk into <paramref name="data"/>.
        ///
        /// Only blocks the content marks as natural take part. Everything else is left
        /// for the cube mesher, which is the point: the seam between them is what makes
        /// a built wall read as built.
        /// </summary>
        public static void Build(ushort[] padded, BlockMeta[] meta, ChunkMeshData data)
        {
            int cells = Span * Span * Span;

            if (_vertexAt == null || _vertexAt.Length != cells) _vertexAt = new int[cells];
            if (_corner == null) _corner = new bool[8];

            for (int i = 0; i < cells; i++) _vertexAt[i] = -1;

            // A vertex wherever the surface passes through a cell.
            for (int y = Lo; y < S; y++)
            for (int z = Lo; z < S; z++)
            for (int x = Lo; x < S; x++)
            {
                PlaceVertex(padded, meta, data, x, y, z);
            }

            // Then quads across every sign change.
            for (int y = Lo; y < S; y++)
            for (int z = Lo; z < S; z++)
            for (int x = Lo; x < S; x++)
            {
                EmitQuads(padded, meta, data, x, y, z);
            }
        }

        static bool IsSmooth(BlockMeta[] meta, ushort id)
        {
            return id < meta.Length && meta[id].Smooth;
        }

        static bool SolidAt(ushort[] padded, BlockMeta[] meta, int x, int y, int z)
        {
            return IsSmooth(meta, Sample(padded, x, y, z));
        }

        /// <summary>
        /// One vertex per cell that the surface crosses, at the average of the
        /// midpoints of its crossing edges.
        ///
        /// With occupancy rather than density every crossing sits at the middle of its
        /// edge, so this is a plain average - and that average is exactly what rounds
        /// a staircase off into a slope.
        /// </summary>
        static void PlaceVertex(ushort[] padded, BlockMeta[] meta, ChunkMeshData data, int cx, int cy, int cz)
        {
            bool anySolid = false;
            bool anyAir = false;

            // The eight voxels of this cell.
            var corner = _corner;
            for (int c = 0; c < 8; c++)
            {
                corner[c] = SolidAt(padded, meta, cx + Corners[c, 0], cy + Corners[c, 1], cz + Corners[c, 2]);
                if (corner[c]) anySolid = true; else anyAir = true;
            }

            // Entirely inside the ground or entirely in the air: no surface here.
            if (!anySolid || !anyAir) return;

            float sx = 0f, sy = 0f, sz = 0f;
            int crossings = 0;

            for (int e = 0; e < 12; e++)
            {
                int a = Edges[e, 0];
                int b = Edges[e, 1];
                if (corner[a] == corner[b]) continue;

                sx += (Corners[a, 0] + Corners[b, 0]) * 0.5f;
                sy += (Corners[a, 1] + Corners[b, 1]) * 0.5f;
                sz += (Corners[a, 2] + Corners[b, 2]) * 0.5f;
                crossings++;
            }

            if (crossings == 0) return;

            // Shifted half a block on every axis.
            //
            // Surface nets works on the dual grid - a vertex sits inside a cell, which
            // straddles eight blocks - so without this the whole surface is inset by
            // half a block against the world it describes. Flat ground would render
            // and collide half a metre below the block top every tree, bush and plot
            // in the game is placed against, and a cliff face would sit half a metre
            // inside the blocks that make it.
            //
            // With it, an axis-aligned surface lands exactly where the cube mesher put
            // it, so every placement rule in the project keeps working untouched.
            var position = new Vector3(
                cx + sx / crossings + 0.5f,
                cy + sy / crossings + 0.5f,
                cz + sz / crossings + 0.5f);

            int index = CellIndex(cx, cy, cz);
            _vertexAt[index] = data.Vertices.Count;

            var normal = GradientNormal(padded, meta, cx, cy, cz);

            data.Vertices.Add(position);
            data.Normals.Add(normal);
            data.Uvs.Add(Project(position, normal));
            data.Colors.Add(Shade(corner));
        }

        /// <summary>
        /// Picks the plane to lay the texture on, from whichever way the surface faces.
        ///
        /// A flat top-down projection is right for ground and wrong for everything
        /// else: on a pit wall or a cliff the texture has no variation to run along
        /// and smears into vertical streaks, which is the most obvious tell that a
        /// surface is procedurally textured.
        /// </summary>
        static Vector2 Project(Vector3 position, Vector3 normal)
        {
            float ax = Mathf.Abs(normal.x);
            float ay = Mathf.Abs(normal.y);
            float az = Mathf.Abs(normal.z);

            if (ay >= ax && ay >= az) return new Vector2(position.x, position.z);
            if (ax >= az) return new Vector2(position.z, position.y);
            return new Vector2(position.x, position.y);
        }

        /// <summary>
        /// Which way the ground faces here, from the gradient of the solidity field.
        ///
        /// Computed rather than recalculated from the triangles, because the cube half
        /// of the mesh shares these buffers and its normals are already exactly right.
        /// Calling RecalculateNormals afterwards would average every hard edge in the
        /// chunk into a soft one - a built wall would come out looking inflated, which
        /// is precisely the distinction this whole pass exists to draw.
        ///
        /// Weighted over the cell's own eight corners rather than sampling six
        /// neighbours: a six-tap gradient on a lattice this coarse snaps to the axes
        /// and puts visible facets back into a slope the vertex placement just
        /// rounded off.
        /// </summary>
        static Vector3 GradientNormal(ushort[] padded, BlockMeta[] meta, int cx, int cy, int cz)
        {
            float gx = 0f, gy = 0f, gz = 0f;

            for (int dy = 0; dy <= 1; dy++)
            for (int dz = 0; dz <= 1; dz++)
            for (int dx = 0; dx <= 1; dx++)
            {
                if (!SolidAt(padded, meta, cx + dx, cy + dy, cz + dz)) continue;

                // Corners pull the normal away from themselves: the surface faces out
                // of the solid, so mass on one side means the face looks to the other.
                gx += dx == 0 ? 1f : -1f;
                gy += dy == 0 ? 1f : -1f;
                gz += dz == 0 ? 1f : -1f;
            }

            var normal = new Vector3(gx, gy, gz);

            // A perfectly balanced cell has no gradient to speak of. Up is the least
            // wrong guess for ground, and it is rare enough not to matter.
            return normal.sqrMagnitude < 1e-6f ? Vector3.up : normal.normalized;
        }

        /// <summary>
        /// Ambient occlusion for a smooth vertex: how buried it is.
        ///
        /// The cube mesher can name the three neighbours that close a corner off. A
        /// surface-nets vertex has no corner - it sits somewhere inside a cell - so the
        /// honest measure is simply how much of the cell around it is solid.
        /// </summary>
        static Color32 Shade(bool[] corner)
        {
            int solid = 0;
            for (int c = 0; c < 8; c++) if (corner[c]) solid++;

            // Four of eight is a flat surface, and flat ground is not occluded by
            // anything - so that is the baseline, not the middle of the range.
            // Measuring from an empty cell instead would have shaded every open plain
            // in the world by a fifth, which is not ambient occlusion, it is a tint.
            //
            // Past half, the cell is closing around the vertex: five is a crease, six
            // an inside corner, seven the bottom of a pocket. Below half it is a spur
            // sticking out into the air, which nothing occludes either.
            float enclosed = Mathf.Clamp01((solid - 4) / 3f);
            float light = Mathf.Lerp(1f, 0.55f, enclosed);

            byte v = (byte)Mathf.RoundToInt(Mathf.Clamp01(light) * 255f);
            return new Color32(v, v, v, 255);
        }

        /// <summary>
        /// A quad wherever the surface crosses an axis edge, joining the four cells
        /// that share it. Winding follows which end of the edge is solid, so the face
        /// always points out of the ground.
        /// </summary>
        static void EmitQuads(ushort[] padded, BlockMeta[] meta, ChunkMeshData data, int cx, int cy, int cz)
        {
            // Only interior edges have all four surrounding cells inside the grid.
            if (cx < Lo + 1 || cy < Lo + 1 || cz < Lo + 1) return;

            bool here = SolidAt(padded, meta, cx, cy, cz);

            for (int axis = 0; axis < 3; axis++)
            {
                int nx = cx + (axis == 0 ? 1 : 0);
                int ny = cy + (axis == 1 ? 1 : 0);
                int nz = cz + (axis == 2 ? 1 : 0);

                bool there = SolidAt(padded, meta, nx, ny, nz);
                if (here == there) continue;

                // The other two axes span the quad.
                int u = (axis + 1) % 3;
                int v = (axis + 2) % 3;

                int a = CellIndex(cx, cy, cz);
                int b = CellIndex(cx - Offset(u, 0), cy - Offset(u, 1), cz - Offset(u, 2));
                int c = CellIndex(cx - Offset(u, 0) - Offset(v, 0),
                                  cy - Offset(u, 1) - Offset(v, 1),
                                  cz - Offset(u, 2) - Offset(v, 2));
                int d = CellIndex(cx - Offset(v, 0), cy - Offset(v, 1), cz - Offset(v, 2));

                int va = _vertexAt[a], vb = _vertexAt[b], vc = _vertexAt[c], vd = _vertexAt[d];
                if (va < 0 || vb < 0 || vc < 0 || vd < 0) continue;

                // Whichever end of the edge is ground decides the block, and therefore
                // which sub-mesh and material the face belongs to.
                ushort block = here ? Sample(padded, cx, cy, cz) : Sample(padded, nx, ny, nz);
                var tris = data.TrianglesFor(block);

                if (here)
                {
                    tris.Add(va); tris.Add(vb); tris.Add(vc);
                    tris.Add(va); tris.Add(vc); tris.Add(vd);
                }
                else
                {
                    tris.Add(va); tris.Add(vc); tris.Add(vb);
                    tris.Add(va); tris.Add(vd); tris.Add(vc);
                }
            }
        }

        static int Offset(int axis, int component)
        {
            return axis == component ? 1 : 0;
        }
    }
}
