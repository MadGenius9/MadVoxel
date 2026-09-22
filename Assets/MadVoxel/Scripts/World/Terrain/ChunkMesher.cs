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

            public bool Same(MaskEntry other)
            {
                return Block == other.Block && Back == other.Back;
            }
        }

        [System.ThreadStatic] static MaskEntry[] _mask;

        static ushort Sample(ushort[] padded, int x, int y, int z)
        {
            // padded is indexed from -1, so shift by one on every axis.
            return padded[((y + 1) * P + (z + 1)) * P + (x + 1)];
        }

        public static ChunkMeshData Build(ushort[] padded, BlockMeta[] meta)
        {
            var data = new ChunkMeshData();
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

                            bool oa = IsOpaque(meta, a);
                            bool ob = IsOpaque(meta, b);

                            if (oa == ob)
                            {
                                mask[n].Block = 0;
                            }
                            else if (oa)
                            {
                                mask[n].Block = a;
                                mask[n].Back = false;
                            }
                            else
                            {
                                mask[n].Block = b;
                                mask[n].Back = true;
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

                            AddQuad(data, mask[n].Block, mask[n].Back, d, x, du, dv, i, j, w, h);

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

        static bool IsOpaque(BlockMeta[] meta, ushort id)
        {
            return id < meta.Length && meta[id].Opaque;
        }

        static void AddQuad(ChunkMeshData data, ushort block, bool back, int axis,
                            int[] x, int[] du, int[] dv, int uOff, int vOff, int w, int h)
        {
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
    }
}
