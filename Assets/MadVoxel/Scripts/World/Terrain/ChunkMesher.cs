using UnityEngine;

namespace MadVoxel.World.Terrain
{
    /// <summary>
    /// Greedy mesher. Runs on a worker thread over an 18^3 padded copy of the chunk, so
    /// it never touches Unity objects and never needs a neighbour lookup mid-build.
    /// Coplanar faces of the same block type merge into one quad, which is what keeps
    /// a flat farmland chunk at a handful of triangles instead of thousands.
    /// </summary>
    public static class ChunkMesher
    {
        const int S = Chunk.Size;
        const int P = Chunk.Size + 2;

        struct MaskEntry
        {
            public ushort Block;
            public bool Back;

            /// <summary>
            /// Corner occlusion, 0 (buried) to 3 (open), in the quad's vertex order.
            ///
            /// Part of the merge key, which is the whole trick: two faces only join if
            /// their corners are shaded the same, so a merged run has AO that is
            /// constant along the direction it was merged in - and stretching a
            /// constant across a run is exact rather than approximate.
            /// </summary>
            public byte A0, A1, A2, A3;

            public bool Same(MaskEntry other)
            {
                return Block == other.Block && Back == other.Back
                    && A0 == other.A0 && A1 == other.A1 && A2 == other.A2 && A3 == other.A3;
            }
        }

        /// <summary>
        /// How much light reaches a vertex, given the three neighbours that share it.
        ///
        /// Two neighbours meeting at a corner close it off completely, which is why
        /// they short-circuit: that is the inside of a right angle, and it is the shape
        /// the eye reads as depth. Everything else is a straight count.
        /// </summary>
        static byte CornerAo(bool side1, bool side2, bool corner)
        {
            if (side1 && side2) return 0;
            return (byte)(3 - ((side1 ? 1 : 0) + (side2 ? 1 : 0) + (corner ? 1 : 0)));
        }

        [System.ThreadStatic] static MaskEntry[] _mask;

        static ushort Sample(ushort[] padded, int x, int y, int z)
        {
            // padded is indexed from -1, so shift by one on every axis.
            return padded[((y + 1) * P + (z + 1)) * P + (x + 1)];
        }

        /// <summary>
        /// Meshes a chunk: smooth ground first, then everything built, into one set of
        /// buffers with a sub-mesh per block type.
        ///
        /// The two halves never argue over a face, because each treats the other's
        /// blocks as empty space. That is also what makes the seam look right - a
        /// foundation set into a hillside keeps its edges while the hill does not.
        /// </summary>
        public static ChunkMeshData Build(ushort[] padded, BlockMeta[] meta)
        {
            return Build(padded, meta, Vector3Int.zero);
        }

        /// <summary>
        /// Meshes a chunk that knows where it is.
        ///
        /// <paramref name="origin"/> is the chunk's world corner. Foliage needs it:
        /// a tree's lean, its bark and its randomness all have to come from where the
        /// tree stands, not from where it happens to sit inside its chunk - otherwise
        /// identical trees appear at the same offset in every chunk and a trunk
        /// snaps back into line at every chunk boundary.
        /// </summary>
        public static ChunkMeshData Build(ushort[] padded, BlockMeta[] meta, Vector3Int origin)
        {
            var data = new ChunkMeshData(true);
            SurfaceNets.Build(padded, meta, data);
            FoliageMesher.Build(padded, meta, data, origin);
            if (_mask == null || _mask.Length != S * S) _mask = new MaskEntry[S * S];
            var mask = _mask;

            var x = new int[3];
            var q = new int[3];
            var du = new int[3];
            var dv = new int[3];

            for (int d = 0; d < 3; d++)
            {
                int u = (d + 1) % 3;
                int v = (d + 2) % 3;

                q[0] = q[1] = q[2] = 0;
                q[d] = 1;

                for (x[d] = -1; x[d] < S; )
                {
                    int n = 0;
                    for (x[v] = 0; x[v] < S; x[v]++)
                    {
                        for (x[u] = 0; x[u] < S; x[u]++)
                        {
                            ushort a = Sample(padded, x[0], x[1], x[2]);
                            ushort b = Sample(padded, x[0] + q[0], x[1] + q[1], x[2] + q[2]);

                            // Smooth blocks belong to the other mesher, and here they
                            // count as nothing at all - so a built wall standing in dirt
                            // grows the faces that meet it rather than having them
                            // culled against ground that is no longer a cube.
                            bool oa = IsHardOpaque(meta, a);
                            bool ob = IsHardOpaque(meta, b);

                            if (oa == ob)
                            {
                                mask[n].Block = 0;
                            }
                            else if (oa)
                            {
                                mask[n].Block = a;
                                mask[n].Back = false;
                                ShadeCorners(padded, meta, ref mask[n], x, q, u, v, 1);
                            }
                            else
                            {
                                mask[n].Block = b;
                                mask[n].Back = true;
                                ShadeCorners(padded, meta, ref mask[n], x, q, u, v, 0);
                            }
                            n++;
                        }
                    }

                    x[d]++;

                    n = 0;
                    for (int j = 0; j < S; j++)
                    {
                        for (int i = 0; i < S; )
                        {
                            if (mask[n].Block == 0)
                            {
                                i++;
                                n++;
                                continue;
                            }

                            // Widen along u, then grow along v while every row matches.
                            int w = 1;
                            while (i + w < S && mask[n + w].Same(mask[n])) w++;

                            int h = 1;
                            bool blocked = false;
                            while (j + h < S)
                            {
                                for (int k = 0; k < w; k++)
                                {
                                    if (!mask[n + k + h * S].Same(mask[n])) { blocked = true; break; }
                                }
                                if (blocked) break;
                                h++;
                            }

                            x[u] = i;
                            x[v] = j;

                            du[0] = du[1] = du[2] = 0;
                            dv[0] = dv[1] = dv[2] = 0;
                            du[u] = w;
                            dv[v] = h;

                            AddQuad(data, mask[n], d, x, du, dv, i, j, w, h);

                            for (int l = 0; l < h; l++)
                            {
                                for (int k = 0; k < w; k++) mask[n + k + l * S].Block = 0;
                            }

                            i += w;
                            n += w;
                        }
                    }
                }
            }

            return data;
        }

        /// <summary>
        /// Fills a mask entry's four corner shades.
        ///
        /// Everything is sampled on the open side of the face, because that is where
        /// the light is coming from and where an occluder has to sit to block it. The
        /// padded copy runs from -1 to Size, and every sample here stays inside that,
        /// so no bounds check is needed - which is the reason the padding exists.
        /// </summary>
        static void ShadeCorners(ushort[] padded, BlockMeta[] meta, ref MaskEntry entry,
                                 int[] x, int[] q, int u, int v, int airStep)
        {
            // The cell on the open side of this face.
            var air = new int[3];
            air[0] = x[0] + q[0] * airStep;
            air[1] = x[1] + q[1] * airStep;
            air[2] = x[2] + q[2] * airStep;

            entry.A0 = Corner(padded, meta, air, u, v, -1, -1);
            entry.A1 = Corner(padded, meta, air, u, v, +1, -1);
            entry.A2 = Corner(padded, meta, air, u, v, +1, +1);
            entry.A3 = Corner(padded, meta, air, u, v, -1, +1);
        }

        static byte Corner(ushort[] padded, BlockMeta[] meta, int[] air, int u, int v, int su, int sv)
        {
            bool side1 = OpaqueAt(padded, meta, air, u, su, v, 0);
            bool side2 = OpaqueAt(padded, meta, air, u, 0, v, sv);
            bool corner = OpaqueAt(padded, meta, air, u, su, v, sv);
            return CornerAo(side1, side2, corner);
        }

        static bool OpaqueAt(ushort[] padded, BlockMeta[] meta, int[] air, int u, int du, int v, int dv)
        {
            int px = air[0], py = air[1], pz = air[2];

            if (u == 0) px += du; else if (u == 1) py += du; else pz += du;
            if (v == 0) px += dv; else if (v == 1) py += dv; else pz += dv;

            // Ground occludes whether or not it is drawn as a cube; a tree does not
            // occlude at all. Face *culling* asks a different question again - whether
            // the neighbour is a cube - and conflating the two cost a wall its contact
            // shading against the ground it stands in.
            return Occludes(meta, Sample(padded, px, py, pz));
        }

        static bool IsOpaque(BlockMeta[] meta, ushort id)
        {
            return id < meta.Length && meta[id].Opaque;
        }

        /// <summary>Opaque, and meshed as a cube rather than as ground or as a tree.</summary>
        static bool IsHardOpaque(BlockMeta[] meta, ushort id)
        {
            return id < meta.Length && meta[id].Opaque
                && !meta[id].Smooth && meta[id].Foliage == FoliageForm.None;
        }

        /// <summary>
        /// Does this block shade what is next to it?
        ///
        /// Ground does, because it is a solid mass however it is drawn. A tree does
        /// not: a trunk is a column with air all round it and a canopy is mostly gaps,
        /// so treating either as a wall would paint a hard square shadow onto the
        /// ground under every pine in the world.
        /// </summary>
        static bool Occludes(BlockMeta[] meta, ushort id)
        {
            return id < meta.Length && meta[id].Opaque && meta[id].Foliage == FoliageForm.None;
        }

        static void AddQuad(ChunkMeshData data, MaskEntry entry, int axis,
                            int[] x, int[] du, int[] dv, int uOff, int vOff, int w, int h)
        {
            ushort block = entry.Block;
            bool back = entry.Back;

            var v0 = new Vector3(x[0], x[1], x[2]);
            var v1 = new Vector3(x[0] + du[0], x[1] + du[1], x[2] + du[2]);
            var v2 = new Vector3(x[0] + du[0] + dv[0], x[1] + du[1] + dv[1], x[2] + du[2] + dv[2]);
            var v3 = new Vector3(x[0] + dv[0], x[1] + dv[1], x[2] + dv[2]);

            var normal = Vector3.zero;
            normal[axis] = back ? -1f : 1f;

            int baseIndex = data.Vertices.Count;
            data.Vertices.Add(v0);
            data.Vertices.Add(v1);
            data.Vertices.Add(v2);
            data.Vertices.Add(v3);

            for (int i = 0; i < 4; i++) data.Normals.Add(normal);

            // UVs run in block units and are offset by the quad's position so the
            // procedural grime tiles continuously across merged faces and chunk seams.
            data.Uvs.Add(new Vector2(uOff, vOff));
            data.Uvs.Add(new Vector2(uOff + w, vOff));
            data.Uvs.Add(new Vector2(uOff + w, vOff + h));
            data.Uvs.Add(new Vector2(uOff, vOff + h));

            // The corners, in the same order the vertices went in.
            data.Colors.Add(Shade(entry.A0));
            data.Colors.Add(Shade(entry.A1));
            data.Colors.Add(Shade(entry.A2));
            data.Colors.Add(Shade(entry.A3));

            var tris = data.TrianglesFor(block);

            // Winding depends on which axis pair we walked; derive it instead of
            // hand-tabling six cases.
            bool flip = Vector3.Dot(Vector3.Cross(v1 - v0, v2 - v0), normal) < 0f;

            if (flip)
            {
                tris.Add(baseIndex); tris.Add(baseIndex + 2); tris.Add(baseIndex + 1);
                tris.Add(baseIndex); tris.Add(baseIndex + 3); tris.Add(baseIndex + 2);
            }
            else
            {
                tris.Add(baseIndex); tris.Add(baseIndex + 1); tris.Add(baseIndex + 2);
                tris.Add(baseIndex); tris.Add(baseIndex + 2); tris.Add(baseIndex + 3);
            }
        }

        /// <summary>
        /// Turns a 0-3 corner count into a vertex tint.
        ///
        /// The darkest step is deliberately not black. A buried corner in a survival
        /// game is still lit by something, and crushing it to zero makes dug tunnels
        /// read as holes in the render rather than as dark corners.
        /// </summary>
        static Color32 Shade(byte ao)
        {
            const float Darkest = 0.45f;
            float light = Mathf.Lerp(Darkest, 1f, ao / 3f);
            byte c = (byte)Mathf.RoundToInt(Mathf.Clamp01(light) * 255f);
            return new Color32(c, c, c, 255);
        }
    }
}
