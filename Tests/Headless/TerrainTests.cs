using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>Chunk storage, coordinate maths, greedy meshing and terrain generation.</summary>
    public static class TerrainTests
    {
        const int S = Chunk.Size;
        const int P = Chunk.Size + 2;

        public static void Run(ContentDatabase db)
        {
            Coordinates();
            ChunkStorage();
            Mesher();
            Terrain(db);
            Pois(db);
            OreBands(db);
        }

        static void Pois(ContentDatabase db)
        {
            Harness.Section("terrain: points of interest");

            const int extent = 1536;
            var gen = new TerrainGenerator(133742, db.blocks, extent);
            var planner = gen.Pois;

            Harness.Check(planner != null && planner.All.Count > 0,
                planner != null ? string.Format("{0} POIs across the map", planner.All.Count) : "planner exists");
            if (planner == null) return;

            Harness.Equal(planner.TraderOutposts.Count, 2, "exactly two trader outposts");

            // Outposts must be reachable but not on top of spawn.
            var tooClose = new List<string>();
            for (int i = 0; i < planner.TraderOutposts.Count; i++)
            {
                var t = planner.TraderOutposts[i];
                float distance = Mathf.Sqrt(t.CentreX * (float)t.CentreX + t.CentreZ * (float)t.CentreZ);
                if (distance < 120f) tooClose.Add(string.Format("outpost at {0}m", Mathf.RoundToInt(distance)));
                if (Mathf.Abs(t.CentreX) > extent || Mathf.Abs(t.CentreZ) > extent) tooClose.Add("outpost outside the map");
            }
            Harness.Check(tooClose.Count == 0, "outposts are off spawn and inside the map"
                + (tooClose.Count > 0 ? ": " + string.Join(", ", tooClose) : ""));

            // No two POIs may overlap, or their stamps would fight.
            var overlaps = new List<string>();
            for (int i = 0; i < planner.All.Count; i++)
            {
                for (int j = i + 1; j < planner.All.Count; j++)
                {
                    var a = planner.All[i];
                    var b = planner.All[j];
                    bool apart = a.MaxX + PoiPlanner.PadMargin < b.MinX - PoiPlanner.PadMargin
                              || b.MaxX + PoiPlanner.PadMargin < a.MinX - PoiPlanner.PadMargin
                              || a.MaxZ + PoiPlanner.PadMargin < b.MinZ - PoiPlanner.PadMargin
                              || b.MaxZ + PoiPlanner.PadMargin < a.MinZ - PoiPlanner.PadMargin;
                    if (!apart) overlaps.Add(a.Kind + " overlaps " + b.Kind);
                }
            }
            Harness.Check(overlaps.Count == 0, "no two POIs overlap"
                + (overlaps.Count > 0 ? ": " + string.Join(", ", overlaps) : ""));

            // The pad must actually be flat, or foundations cannot be placed on it.
            var outpost = planner.TraderOutposts[0];
            bool flat = true;
            for (int wz = outpost.MinZ; wz <= outpost.MaxZ; wz += 2)
            {
                for (int wx = outpost.MinX; wx <= outpost.MaxX; wx += 2)
                {
                    if (gen.SurfaceHeight(wx, wz) != outpost.PadY) flat = false;
                }
            }
            Harness.Check(flat, "the outpost pad is level, so snap foundations fit it");

            // Lookup must agree with the footprint.
            Poi found;
            Harness.Check(planner.TryGetAt(outpost.CentreX, outpost.CentreZ, out found)
                          && found.Kind == PoiKind.TraderOutpost, "the planner finds the outpost at its centre");
            Harness.Check(!planner.TryGetAt(outpost.CentreX + 4000, outpost.CentreZ + 4000, out found),
                "and finds nothing far away");

            // Determinism: the same seed lays out the same POIs.
            var again = new TerrainGenerator(133742, db.blocks, extent).Pois;
            bool same = again.All.Count == planner.All.Count;
            for (int i = 0; same && i < again.All.Count; i++)
            {
                if (again.All[i].CentreX != planner.All[i].CentreX ||
                    again.All[i].CentreZ != planner.All[i].CentreZ ||
                    again.All[i].Kind != planner.All[i].Kind) same = false;
            }
            Harness.Check(same, "the same seed lays out the same POIs");

            // And the stamp has to put real blocks in the chunk.
            int concreteOrIron = 0;
            var concrete = db.blocks.IdOf(BlockIds.Concrete);
            var iron = db.blocks.IdOf(BlockIds.IronBlock);

            int chunkX = Mathf.FloorToInt(outpost.CentreX / (float)Chunk.Size);
            int chunkZ = Mathf.FloorToInt(outpost.CentreZ / (float)Chunk.Size);
            for (int cy = 0; cy < TerrainWorld.WorldHeightChunks; cy++)
            {
                var blocks = new ushort[Chunk.Volume];
                gen.Generate(new ChunkCoord(chunkX, cy, chunkZ), blocks);
                for (int i = 0; i < blocks.Length; i++)
                {
                    if (blocks[i] == concrete || blocks[i] == iron) concreteOrIron++;
                }
            }
            Harness.Check(concreteOrIron > 0, string.Format("the outpost stamps built blocks into the world ({0} voxels)", concreteOrIron));
        }

        /// <summary>
        /// That the ore bands actually produce ore, and in the right order of depth.
        ///
        /// A noise cut is one number, and a cut set slightly too high does not fail -
        /// it just quietly generates a world with no tungsten in it anywhere. Nothing
        /// else in this suite would notice: the block exists, the recipe resolves, the
        /// reachability closure is satisfied because the block nominally drops it, and
        /// the first sign of trouble is a player digging to bedrock for an evening and
        /// finding nothing. So the generator gets asked directly.
        /// </summary>
        static void OreBands(ContentDatabase db)
        {
            Harness.Section("voxel: the ore bands");

            var gen = new TerrainGenerator(133742, db.blocks);

            var coal = db.blocks.IdOf(BlockIds.CoalOre);
            var iron = db.blocks.IdOf(BlockIds.IronOre);
            var tungsten = db.blocks.IdOf(BlockIds.TungstenOre);

            int coalCount = 0, ironCount = 0, tungstenCount = 0;
            int deepestIron = int.MaxValue, shallowestTungsten = int.MinValue;

            // A column of chunks through the whole world, in a few places, so a single
            // unlucky spot cannot decide the answer.
            // Enough of them that rarity cannot be mistaken for absence. A single
            // column of an 0.2%-dense band holds a handful of voxels on a good day and
            // none on a bad one, and a test that flakes on the seed is worse than no
            // test - it teaches you to ignore it.
            var spots = new[]
            {
                new[] { 0, 0 }, new[] { 12, -7 }, new[] { -20, 31 }, new[] { 44, 44 },
                new[] { -55, -13 }, new[] { 27, 61 }, new[] { -38, 48 }, new[] { 70, -29 },
                new[] { 5, -62 }, new[] { -9, 19 }, new[] { 33, 8 }, new[] { -47, -41 }
            };

            for (int s = 0; s < spots.Length; s++)
            {
                for (int cy = 0; cy < TerrainWorld.WorldHeightChunks; cy++)
                {
                    var blocks = new ushort[Chunk.Volume];
                    var coord = new ChunkCoord(spots[s][0], cy, spots[s][1]);
                    gen.Generate(coord, blocks);

                    for (int ly = 0; ly < Chunk.Size; ly++)
                    {
                        int wy = coord.Origin.y + ly;
                        for (int lz = 0; lz < Chunk.Size; lz++)
                        {
                            for (int lx = 0; lx < Chunk.Size; lx++)
                            {
                                var id = blocks[Chunk.Index(lx, ly, lz)];

                                if (id == coal) coalCount++;
                                else if (id == iron)
                                {
                                    ironCount++;
                                    if (wy < deepestIron) deepestIron = wy;
                                }
                                else if (id == tungsten)
                                {
                                    tungstenCount++;
                                    if (wy > shallowestTungsten) shallowestTungsten = wy;
                                }
                            }
                        }
                    }
                }
            }

            // TEMP PROBE
            Harness.Check(coalCount > 0, string.Format("coal generates ({0} voxels)", coalCount));
            Harness.Check(ironCount > 0, string.Format("iron generates ({0} voxels)", ironCount));
            Harness.Check(tungstenCount > 0, string.Format("tungsten generates ({0} voxels)", tungstenCount));

            // Rarer than iron, or the deep dig is not a dig.
            Harness.Check(tungstenCount < ironCount,
                string.Format("and is rarer than iron ({0} against {1})", tungstenCount, ironCount));

            // And genuinely deep. Digging a cellar must not turn it up.
            Harness.Check(shallowestTungsten < 32,
                string.Format("the shallowest tungsten is at y={0}", shallowestTungsten));
        }

        static void Coordinates()
        {
            Harness.Section("voxel: coordinate maths");

            // Floor division is what makes negative world coordinates land in the right
            // chunk; C# integer division truncates towards zero and would not.
            Harness.Equal(ChunkCoord.FloorDiv(-1, 16), -1, "FloorDiv(-1, 16) is -1, not 0");
            Harness.Equal(ChunkCoord.FloorDiv(-16, 16), -1, "FloorDiv(-16, 16) is -1");
            Harness.Equal(ChunkCoord.FloorDiv(-17, 16), -2, "FloorDiv(-17, 16) is -2");
            Harness.Equal(ChunkCoord.FloorMod(-1, 16), 15, "FloorMod(-1, 16) is 15");
            Harness.Equal(ChunkCoord.FloorMod(-16, 16), 0, "FloorMod(-16, 16) is 0");

            var coord = ChunkCoord.FromWorld(-1, 0, -1);
            Harness.Check(coord.Equals(new ChunkCoord(-1, 0, -1)), "world (-1,0,-1) maps to chunk (-1,0,-1)");
            Harness.Check(ChunkCoord.FromWorld(16, 16, 16).Equals(new ChunkCoord(1, 1, 1)), "world (16,16,16) maps to chunk (1,1,1)");

            var origin = new ChunkCoord(-2, 0, 3).Origin;
            Harness.Check(origin.x == -32 && origin.z == 48, "chunk origin is the corner in world space");

            // Hashing must not collide for neighbours, or the chunk dictionary degrades.
            var seen = new HashSet<int>();
            int collisions = 0;
            for (int x = -8; x <= 8; x++)
            for (int y = 0; y < 12; y++)
            for (int z = -8; z <= 8; z++)
            {
                if (!seen.Add(new ChunkCoord(x, y, z).GetHashCode())) collisions++;
            }
            Harness.Check(collisions == 0, string.Format("no hash collisions across {0} nearby chunks", seen.Count));
        }

        static void ChunkStorage()
        {
            Harness.Section("voxel: chunk storage");

            var chunk = new Chunk(new ChunkCoord(0, 0, 0));
            Harness.Equal(chunk.IsUniform, true, "a new chunk is uniform and holds no array");
            Harness.Equal(chunk.RawBlocks == null, true, "and allocates nothing");

            Harness.Equal(chunk.Set(1, 2, 3, 7), true, "setting a block reports a change");
            Harness.Equal(chunk.IsUniform, false, "which materialises the array");
            Harness.Equal(chunk.Get(1, 2, 3), (ushort)7, "the block reads back");
            Harness.Equal(chunk.Set(1, 2, 3, 7), false, "setting the same value reports no change");

            // Index packing must be collision-free across the whole volume.
            var indices = new HashSet<int>();
            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
            for (int x = 0; x < S; x++)
                indices.Add(Chunk.Index(x, y, z));
            Harness.Equal(indices.Count, Chunk.Volume, "Chunk.Index covers all 4096 cells uniquely");

            // Compaction reclaims the array once a chunk becomes uniform again.
            chunk.Set(1, 2, 3, 0);
            chunk.Compact();
            Harness.Equal(chunk.IsUniform, true, "a chunk that becomes uniform compacts back");

            var copy = new Chunk(new ChunkCoord(1, 1, 1));
            copy.SetUniform(5);
            var blocks = copy.CopyBlocks();
            Harness.Equal(blocks.Length, Chunk.Volume, "CopyBlocks returns a full-size array");
            bool allFive = true;
            for (int i = 0; i < blocks.Length; i++) if (blocks[i] != 5) allFive = false;
            Harness.Check(allFive, "a uniform chunk expands correctly for the save writer");
        }

        static int PadIndex(int x, int y, int z) { return ((y + 1) * P + (z + 1)) * P + (x + 1); }

        static BlockMeta[] Meta()
        {
            return new[]
            {
                new BlockMeta { Air = true,  Opaque = false, Solid = false },
                new BlockMeta { Air = false, Opaque = true,  Solid = true  },
                new BlockMeta { Air = false, Opaque = true,  Solid = true  },
            };
        }

        static void Mesher()
        {
            Harness.Section("voxel: greedy mesher");

            // A 3x3x3 solid cube in open air: six faces, each merged to a single quad.
            var cube = new ushort[P * P * P];
            for (int y = 5; y < 8; y++)
            for (int z = 5; z < 8; z++)
            for (int x = 5; x < 8; x++)
                cube[PadIndex(x, y, z)] = 1;

            var data = ChunkMesher.Build(cube, Meta());
            Harness.Equal(data.Vertices.Count, 24, "a 3x3x3 cube merges to six quads (24 vertices)");

            int indices = 0;
            foreach (var kv in data.Triangles) indices += kv.Value.Count;
            Harness.Equal(indices, 36, "six quads means 36 indices");
            Harness.Equal(data.BlockOrder.Count, 1, "one block type means one sub-mesh");

            var centre = new Vector3(6.5f, 6.5f, 6.5f);
            bool outward = true;
            for (int i = 0; i < data.Vertices.Count; i += 4)
            {
                var quadCentre = (data.Vertices[i] + data.Vertices[i + 1] + data.Vertices[i + 2] + data.Vertices[i + 3]) * 0.25f;
                if (Vector3.Dot(quadCentre - centre, data.Normals[i]) <= 0f) outward = false;
            }
            Harness.Check(outward, "every face points away from the cube");
            Harness.Check(WindingMatchesNormals(data), "triangle winding agrees with the face normals");

            // Ground with solid neighbours on every side: only the top is visible.
            var ground = new ushort[P * P * P];
            for (int y = -1; y < 8; y++)
            for (int z = -1; z <= S; z++)
            for (int x = -1; x <= S; x++)
                ground[PadIndex(x, y, z)] = 1;

            var groundData = ChunkMesher.Build(ground, Meta());
            Harness.Equal(groundData.Vertices.Count, 4, "flat ground with solid neighbours is one 16x16 quad");
            if (groundData.Vertices.Count == 4)
            {
                var n = groundData.Normals[0];
                Harness.Check(n.x == 0f && n.y == 1f && n.z == 0f, "and it faces up");
            }

            // Fully enclosed solid: nothing to draw at all.
            var buried = new ushort[P * P * P];
            for (int i = 0; i < buried.Length; i++) buried[i] = 1;
            Harness.Equal(ChunkMesher.Build(buried, Meta()).IsEmpty, true, "a fully buried chunk produces no geometry");

            // Air only: also nothing.
            Harness.Equal(ChunkMesher.Build(new ushort[P * P * P], Meta()).IsEmpty, true, "an empty chunk produces no geometry");

            // Two materials on one plane must not merge across the boundary.
            var mixed = new ushort[P * P * P];
            for (int z = 0; z < S; z++)
            for (int x = 0; x < S; x++)
                mixed[PadIndex(x, 4, z)] = (ushort)(x < 8 ? 1 : 2);

            var mixedData = ChunkMesher.Build(mixed, Meta());
            Harness.Equal(mixedData.BlockOrder.Count, 2, "two materials produce two sub-meshes");
            Harness.Check(mixedData.Triangles[1].Count > 0 && mixedData.Triangles[2].Count > 0, "both materials contribute faces");
            Harness.Check(WindingMatchesNormals(mixedData), "mixed-material winding is still correct");

            // A chunk exposed on all six sides against air.
            var exposed = new ushort[P * P * P];
            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
            for (int x = 0; x < S; x++)
                exposed[PadIndex(x, y, z)] = 1;

            var exposedData = ChunkMesher.Build(exposed, Meta());
            Harness.Equal(exposedData.Vertices.Count, 24, "a chunk exposed on all sides is six merged wall quads");
            Harness.Check(WindingMatchesNormals(exposedData), "border winding is correct");

            // Every triangle index must be in range, or Unity throws on upload.
            bool inRange = true;
            foreach (var kv in mixedData.Triangles)
            {
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    if (kv.Value[i] < 0 || kv.Value[i] >= mixedData.Vertices.Count) inRange = false;
                }
            }
            Harness.Check(inRange, "all triangle indices are within the vertex buffer");
            Harness.Equal(mixedData.Normals.Count, mixedData.Vertices.Count, "normals and vertices line up");
            Harness.Equal(mixedData.Uvs.Count, mixedData.Vertices.Count, "UVs and vertices line up");
        }

        static bool WindingMatchesNormals(ChunkMeshData data)
        {
            foreach (var kv in data.Triangles)
            {
                var tris = kv.Value;
                for (int i = 0; i < tris.Count; i += 3)
                {
                    var a = data.Vertices[tris[i]];
                    var b = data.Vertices[tris[i + 1]];
                    var c = data.Vertices[tris[i + 2]];
                    if (Vector3.Dot(Vector3.Cross(b - a, c - a), data.Normals[tris[i]]) <= 0f) return false;
                }
            }
            return true;
        }

        static void Terrain(ContentDatabase db)
        {
            Harness.Section("voxel: terrain generation");

            var gen = new TerrainGenerator(133742, db.blocks);

            int min = int.MaxValue, max = int.MinValue;
            for (int z = -600; z <= 600; z += 37)
            for (int x = -600; x <= 600; x += 37)
            {
                int h = gen.SurfaceHeight(x, z);
                if (h < min) min = h;
                if (h > max) max = h;
            }
            Harness.Check(min >= 4 && max < TerrainWorld.WorldHeight - 39, string.Format("surface stays in range: {0}..{1}", min, max));
            Harness.Check(max - min >= 12, string.Format("terrain has relief: spread {0}", max - min));

            // Determinism: the same seed must rebuild the same chunk exactly.
            var a = new ushort[Chunk.Volume];
            var b = new ushort[Chunk.Volume];
            var coord = new ChunkCoord(3, 3, -7);
            new TerrainGenerator(133742, db.blocks).Generate(coord, a);
            new TerrainGenerator(133742, db.blocks).Generate(coord, b);

            bool identical = true;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) identical = false;
            Harness.Check(identical, "the same seed rebuilds the same chunk");

            var other = new ushort[Chunk.Volume];
            new TerrainGenerator(987654, db.blocks).Generate(coord, other);
            bool differs = false;
            for (int i = 0; i < a.Length; i++) if (a[i] != other[i]) differs = true;
            Harness.Check(differs, "a different seed makes a different world");

            // Features: sample a 10x10 chunk area and confirm the world is not barren.
            int logs = 0, needles = 0, scrap = 0, coal = 0, iron = 0, caves = 0, grass = 0, bedrock = 0;
            var ids = new
            {
                Log = db.blocks.IdOf(BlockIds.PineLog),
                Needle = db.blocks.IdOf(BlockIds.PineNeedles),
                Scrap = db.blocks.IdOf(BlockIds.ScrapHeap),
                Coal = db.blocks.IdOf(BlockIds.CoalOre),
                Iron = db.blocks.IdOf(BlockIds.IronOre),
                Grass = db.blocks.IdOf(BlockIds.Grass),
                Bedrock = db.blocks.IdOf(BlockIds.Bedrock)
            };

            for (int cz = 0; cz < 10; cz++)
            for (int cx = 0; cx < 10; cx++)
            for (int cy = 0; cy < TerrainWorld.WorldHeightChunks; cy++)
            {
                var blocks = new ushort[Chunk.Volume];
                gen.Generate(new ChunkCoord(cx, cy, cz), blocks);

                for (int ly = 0; ly < S; ly++)
                for (int lz = 0; lz < S; lz++)
                for (int lx = 0; lx < S; lx++)
                {
                    ushort id = blocks[Chunk.Index(lx, ly, lz)];
                    if (id == ids.Log) logs++;
                    else if (id == ids.Needle) needles++;
                    else if (id == ids.Scrap) scrap++;
                    else if (id == ids.Coal) coal++;
                    else if (id == ids.Iron) iron++;
                    else if (id == ids.Grass) grass++;
                    else if (id == ids.Bedrock) bedrock++;

                    int wy = cy * S + ly;
                    if (id == 0 && wy > 4 && wy < gen.SurfaceHeight(cx * S + lx, cz * S + lz) - 2) caves++;
                }
            }

            Harness.Check(logs > 0 && needles > 0, string.Format("trees generate (logs {0}, needles {1})", logs, needles));
            Harness.Check(scrap > 0, string.Format("surface scrap generates ({0} voxels)", scrap));
            Harness.Check(coal > 0, string.Format("coal veins generate ({0} voxels)", coal));
            Harness.Check(iron > 0, string.Format("iron veins generate ({0} voxels)", iron));
            Harness.Check(caves > 0, string.Format("caves carve out underground air ({0} voxels)", caves));
            Harness.Check(grass > 0, string.Format("there is a walkable grass surface ({0} voxels)", grass));
            Harness.Check(bedrock > 0, "bedrock floors the world");

            // Ore must not be so common that mining is trivial, nor caves so common that
            // the ground is swiss cheese.
            const int sampled = 10 * 10 * TerrainWorld.WorldHeightChunks * Chunk.Volume;
            float coalShare = 100f * coal / sampled;
            float caveShare = 100f * caves / sampled;
            Harness.Check(coalShare < 3f, string.Format("coal is not everywhere: {0:0.00}% of sampled voxels", coalShare));
            Harness.Check(caveShare < 8f, string.Format("caves are not everywhere: {0:0.00}% of sampled voxels", caveShare));

            // Bedrock must be unbreakable, or players fall out of the world.
            var bedrockDef = db.blocks.ByStringId(BlockIds.Bedrock);
            Harness.Check(bedrockDef != null && bedrockDef.hardness < 0f, "bedrock is flagged indestructible");
        }
    }
}
