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
            AmbientOcclusion(db);
            SmoothTerrain(db);
            SmoothAlignment(db);
            Trees(db);
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
            int shallowestTungsten = int.MaxValue;
            int shallowestIron = int.MinValue;

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
                                    if (wy > shallowestIron) shallowestIron = wy;
                                }
                                else if (id == tungsten)
                                {
                                    tungstenCount++;
                                    if (wy < shallowestTungsten) shallowestTungsten = wy;
                                }
                            }
                        }
                    }
                }
            }

            Harness.Check(coalCount > 0, string.Format("coal generates ({0} voxels)", coalCount));
            Harness.Check(ironCount > 0, string.Format("iron generates ({0} voxels)", ironCount));
            Harness.Check(tungstenCount > 0, string.Format("tungsten generates ({0} voxels)", tungstenCount));

            // Rarer than iron, or the deep dig is not a dig.
            Harness.Check(tungstenCount < ironCount,
                string.Format("and is rarer than iron ({0} against {1})", tungstenCount, ironCount));

            // And genuinely deep. Guarded on having found any at all: seeded with a
            // sentinel, "no tungsten anywhere" would satisfy a depth bound vacuously
            // and the test would pass hardest exactly when the ore was missing.
            if (tungstenCount > 0)
            {
                Harness.Check(shallowestTungsten < 22,
                    string.Format("the shallowest tungsten is at y={0}, under its ceiling", shallowestTungsten));
            }

            // The bands are in the right order: tungsten's ceiling is below where iron
            // is still being found, so going deeper is what reaches it.
            if (tungstenCount > 0 && ironCount > 0)
            {
                Harness.Check(shallowestIron > shallowestTungsten,
                    string.Format("iron reaches up to y={0}, above tungsten's y={1}",
                        shallowestIron, shallowestTungsten));
            }
        }

        /// <summary>
        /// Baked ambient occlusion, checked as geometry rather than looked at.
        ///
        /// AO is the one part of the look that is arithmetic all the way down: a
        /// vertex is darker because three specific neighbours are solid, and whether
        /// the mesher agrees is a question with an answer. What cannot be checked here
        /// is whether it looks good - only that an inside corner is darker than an
        /// open plain, which is the entire point of having it.
        /// </summary>
        static void AmbientOcclusion(ContentDatabase db)
        {
            Harness.Section("voxel: baked ambient occlusion");

            var meta = BlockMeta.Snapshot(db.blocks);

            // Planks, not stone. Stone is natural ground now and goes through surface
            // nets, so building this out of it would quietly test the smooth shading
            // twice and leave the cube mesher's corner AO - the merge key, the
            // three-neighbour test - with no coverage at all.
            ushort stone = db.blocks.IdOf(BlockIds.Planks);
            Harness.Check(!meta[stone].Smooth, "the AO fixtures are built from a hard block");

            // A single block alone in the void: every corner of every face is open.
            var lone = Padded();
            Set(lone, 8, 8, 8, stone);

            var loneMesh = ChunkMesher.Build(lone, meta);
            Harness.Check(loneMesh.Colors.Count == loneMesh.Vertices.Count,
                "every vertex carries a shade");
            Harness.Check(loneMesh.Colors.Count > 0, "and a lone block produces some");

            Harness.Equal(Darkest(loneMesh), Brightest(loneMesh),
                "a block with nothing near it is evenly lit");

            // An inside corner. Two neighbours meeting at a right angle is the shape
            // the eye reads as depth, and it has to come out darker than open ground.
            var corner = Padded();
            for (int x = 4; x < 12; x++)
                for (int z = 4; z < 12; z++)
                    Set(corner, x, 6, z, stone);          // a floor

            for (int y = 7; y < 11; y++)
                for (int z = 4; z < 12; z++)
                    Set(corner, 4, y, z, stone);          // a wall rising out of it

            var cornerMesh = ChunkMesher.Build(corner, meta);
            Harness.Check(Darkest(cornerMesh) < Brightest(cornerMesh),
                "a floor meeting a wall is shaded, not flat");

            // A flat plain has nothing to occlude anything, so it must stay evenly and
            // fully lit - otherwise the whole world is tinted rather than only its
            // corners. Filled right across the padding, because a slab that stops
            // inside the sampled area has a rim, and a rim is a real edge.
            var plain = Padded();
            for (int x = -1; x <= 16; x++)
                for (int z = -1; z <= 16; z++)
                    Set(plain, x, 6, z, stone);

            var plainMesh = ChunkMesher.Build(plain, meta);
            Harness.Equal(Darkest(plainMesh), Brightest(plainMesh), "open ground is evenly lit");
            Harness.Equal(Brightest(plainMesh), 255, "and lit fully, not tinted");

            // And the darkest shade is never black: a buried corner in a survival game
            // is still lit by something, and crushing it reads as a hole in the render.
            Harness.Check(Darkest(cornerMesh) > 0, "the deepest corner is dark, not black");

            // Merging must respect it. Two faces only join when their corners match, so
            // a wall that is shaded along its length cannot be merged into one quad
            // carrying a single shade.
            Harness.Check(cornerMesh.Vertices.Count > loneMesh.Vertices.Count,
                "a shaded surface is not merged flat");
        }

        /// <summary>
        /// That natural ground comes out smooth and built blocks do not.
        ///
        /// The whole effect is the contrast, so both halves have to be checked: a
        /// world where everything is rounded looks like clay, and one where everything
        /// is cubes looks like the thing this is trying not to look like. What cannot
        /// be checked here is whether it is pretty - only that a slope stops being a
        /// staircase, which is a question about vertex positions.
        /// </summary>
        static void SmoothTerrain(ContentDatabase db)
        {
            Harness.Section("voxel: natural ground is smoothed, construction is not");

            var meta = BlockMeta.Snapshot(db.blocks);
            ushort stone = db.blocks.IdOf(BlockIds.Stone);
            ushort planks = db.blocks.IdOf(BlockIds.Planks);

            Harness.Check(meta[stone].Smooth, "stone is ground");
            Harness.Check(!meta[planks].Smooth, "planks are not");

            // A staircase of stone. Cube-meshed it is all axis-aligned quads; smoothed
            // it has to produce vertices that sit off the lattice.
            var steps = Padded();
            for (int x = 0; x < 12; x++)
                for (int z = 0; z < 12; z++)
                    for (int y = 0; y <= 2 + x / 3; y++)
                        Set(steps, x, y, z, stone);

            var stepMesh = ChunkMesher.Build(steps, meta);
            Harness.Check(stepMesh.Vertices.Count > 0, "a slope meshes");

            int offLattice = 0;
            for (int i = 0; i < stepMesh.Vertices.Count; i++)
            {
                var vert = stepMesh.Vertices[i];
                if (!OnLattice(vert.x) || !OnLattice(vert.y) || !OnLattice(vert.z)) offLattice++;
            }

            Harness.Check(offLattice > 0,
                string.Format("{0} of {1} vertices sit off the block lattice - the slope is not a staircase",
                    offLattice, stepMesh.Vertices.Count));

            // Normals have to be real. They are computed here rather than recalculated
            // from the triangles, because the cube half shares these buffers and its
            // normals are already exact - averaging them would inflate every built
            // wall in the chunk. A field of identical up-vectors is what a placeholder
            // looks like, and it would light the whole slope as though it were flat.
            int upright = 0;
            int unit = 0;
            for (int i = 0; i < stepMesh.Normals.Count; i++)
            {
                var n = stepMesh.Normals[i];
                if (Mathf.Abs(n.magnitude - 1f) < 0.01f) unit++;
                if (n.y > 0.999f) upright++;
            }

            Harness.Equal(unit, stepMesh.Normals.Count, "every smooth normal is a unit vector");
            Harness.Check(upright < stepMesh.Normals.Count / 2,
                string.Format("and a slope's normals lean ({0} of {1} point straight up)",
                    upright, stepMesh.Normals.Count));

            // Built blocks keep their edges. Every vertex of a plank structure must
            // land exactly on the lattice, or a wall would sag.
            var built = Padded();
            for (int x = 2; x < 8; x++)
                for (int y = 2; y < 6; y++)
                    Set(built, x, y, 4, planks);

            var builtMesh = ChunkMesher.Build(built, meta);
            Harness.Check(builtMesh.Vertices.Count > 0, "a wall meshes");

            int sagging = 0;
            for (int i = 0; i < builtMesh.Vertices.Count; i++)
            {
                var vert = builtMesh.Vertices[i];
                if (!OnLattice(vert.x) || !OnLattice(vert.y) || !OnLattice(vert.z)) sagging++;
            }

            Harness.Equal(sagging, 0, "and every vertex of a built wall is exactly on the lattice");

            // The two halves must not fight over a face. A wall standing in dirt keeps
            // the faces that meet the ground, because the ground is no longer a cube
            // that could cull them.
            var both = Padded();
            for (int x = -1; x <= 16; x++)
                for (int z = -1; z <= 16; z++)
                    for (int y = 0; y < 5; y++)
                        Set(both, x, y, z, stone);

            for (int x = 4; x < 10; x++)
                for (int y = 5; y < 9; y++)
                    Set(both, x, y, 8, planks);

            var mixed = ChunkMesher.Build(both, meta);
            Harness.Check(mixed.Triangles.ContainsKey(stone), "the ground is in the mesh");
            Harness.Check(mixed.Triangles.ContainsKey(planks), "and so is the wall");
            Harness.Check(mixed.Triangles[planks].Count > 0, "with faces of its own");

            // Buried construction is a fair test of the same thing from the other side.
            var buried = Padded();
            for (int x = -1; x <= 16; x++)
                for (int z = -1; z <= 16; z++)
                    for (int y = 0; y < 8; y++)
                        Set(buried, x, y, z, stone);
            Set(buried, 8, 4, 8, planks);

            var buriedMesh = ChunkMesher.Build(buried, meta);
            Harness.Check(buriedMesh.Triangles.ContainsKey(planks),
                "a block walled into the ground still draws, since smooth ground cannot cull a cube");
        }

        /// <summary>
        /// That smooth ground sits where the block grid says it does, and that a
        /// chunk of air is empty.
        ///
        /// Both of these produce symptoms a playtester would misread. Ground half a
        /// block low looks like floating trees and a camera sunk into the floor;
        /// chunks that are not empty but have nothing to draw look like a performance
        /// problem. Neither looks like what it is.
        /// </summary>
        static void SmoothAlignment(ContentDatabase db)
        {
            Harness.Section("voxel: smooth ground lines up with the block grid");

            var meta = BlockMeta.Snapshot(db.blocks);
            ushort stone = db.blocks.IdOf(BlockIds.Stone);

            // Ground filled solid to y=6 inclusive. The block top is y=7, which is
            // where every tree, bush, plot and deployable in the game is placed.
            var ground = Padded();
            for (int x = -1; x <= 16; x++)
                for (int z = -1; z <= 16; z++)
                    for (int y = -1; y <= 6; y++)
                        Set(ground, x, y, z, stone);

            var mesh = ChunkMesher.Build(ground, meta);
            Harness.Check(mesh.Vertices.Count > 0, "flat ground meshes");

            float highest = float.MinValue;
            float lowest = float.MaxValue;
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                float y = mesh.Vertices[i].y;
                if (y > highest) highest = y;
                if (y < lowest) lowest = y;
            }

            Harness.Check(Mathf.Abs(highest - 7f) < 0.001f,
                string.Format("its surface is at y={0}, where the block top is", highest));
            Harness.Check(Mathf.Abs(lowest - 7f) < 0.001f, "and it is flat");

            // A vertical face lines up the same way. Solid where x < 8 means the wall
            // of earth stands at x = 8, not half a metre inside it.
            var cliff = Padded();
            for (int x = -1; x < 8; x++)
                for (int z = -1; z <= 16; z++)
                    for (int y = -1; y <= 10; y++)
                        Set(cliff, x, y, z, stone);

            var cliffMesh = ChunkMesher.Build(cliff, meta);
            float furthest = float.MinValue;
            for (int i = 0; i < cliffMesh.Vertices.Count; i++)
            {
                if (cliffMesh.Vertices[i].x > furthest) furthest = cliffMesh.Vertices[i].x;
            }

            Harness.Check(Mathf.Abs(furthest - 8f) < 0.001f,
                string.Format("a cliff face stands at x={0}, on the block boundary", furthest));

            // Air above ground. Surface nets places vertices in the border cells this
            // chunk shares with the one below, so it is not vertex-free - but it has
            // no triangles, and a chunk with nothing to draw has to read as empty or
            // the streamer keeps a renderer and a collider alive for every one.
            var air = Padded();
            for (int x = -1; x <= 16; x++)
                for (int z = -1; z <= 16; z++)
                    Set(air, x, -1, z, stone);

            var airMesh = ChunkMesher.Build(air, meta);
            Harness.Check(airMesh.IsEmpty, "a chunk of air above ground counts as empty");
        }

        /// <summary>
        /// That a tree draws as a tree and stays a block.
        ///
        /// The whole reason this is done in the mesher rather than by replacing trees
        /// with props is that the blocks must not change - a log has to still be a
        /// log, choppable and saved and the same answer to every rule that asks. So
        /// what is checked here is both halves: that the geometry is no longer cubic,
        /// and that nothing about the block itself moved.
        /// </summary>
        static void Trees(ContentDatabase db)
        {
            Harness.Section("voxel: trees draw as trees");

            var meta = BlockMeta.Snapshot(db.blocks);
            ushort log = db.blocks.IdOf(BlockIds.PineLog);
            ushort needles = db.blocks.IdOf(BlockIds.PineNeedles);

            Harness.Equal(meta[log].Foliage, FoliageForm.Trunk, "a log draws as a trunk");
            Harness.Equal(meta[needles].Foliage, FoliageForm.Frond, "and needles as fronds");

            // The block is untouched. This is the contract the whole approach rests on.
            var logDef = db.blocks.ByStringId(BlockIds.PineLog);
            Harness.Check(logDef.solid, "a log is still solid");
            Harness.Check(logDef.dropItem != null, "still drops wood");
            Harness.Check(logDef.hardness > 0f, "and can still be chopped");

            // A trunk. Round, so almost nothing sits on the lattice, and it must not
            // be axis-aligned the way a cube is.
            var trunk = Padded();
            for (int y = 0; y < 6; y++) Set(trunk, 8, y, 8, log);

            var trunkMesh = ChunkMesher.Build(trunk, meta);
            Harness.Check(trunkMesh.Vertices.Count > 0, "a trunk meshes");
            Harness.Check(trunkMesh.Triangles.ContainsKey(log), "into the log's own sub-mesh");

            int offLattice = 0;
            for (int i = 0; i < trunkMesh.Vertices.Count; i++)
            {
                var v = trunkMesh.Vertices[i];
                if (!OnLattice(v.x) || !OnLattice(v.z)) offLattice++;
            }
            Harness.Check(offLattice > trunkMesh.Vertices.Count / 2,
                string.Format("and is round, not square ({0} of {1} vertices off the lattice)",
                    offLattice, trunkMesh.Vertices.Count));

            // Same trunk, meshed twice, has to be the same trunk - or a forest would
            // reshuffle itself every time a chunk came back into view.
            var again = ChunkMesher.Build(trunk, meta);
            bool identical = again.Vertices.Count == trunkMesh.Vertices.Count;
            for (int i = 0; identical && i < again.Vertices.Count; i++)
            {
                identical = again.Vertices[i] == trunkMesh.Vertices[i];
            }
            Harness.Check(identical, "and meshes identically every time");

            // A canopy. It must not land in the main buffers at all: that mesh is also
            // the collider, needles were never solid, and a player has always been
            // able to walk through a treetop. Drawing them as fronds in the same mesh
            // would have silently made every tree in the world solid.
            var canopy = Padded();
            Set(canopy, 8, 8, 8, needles);

            var canopyMesh = ChunkMesher.Build(canopy, meta);
            Harness.Check(canopyMesh.Decoration != null, "a chunk has somewhere to put decoration");
            Harness.Equal(canopyMesh.Vertices.Count, 0,
                "and a canopy puts nothing in the mesh that becomes the collider");

            var fronds = canopyMesh.Decoration;
            Harness.Check(fronds.Vertices.Count > 0, "while the fronds themselves are meshed");

            float lowest = float.MaxValue;
            for (int i = 0; i < fronds.Vertices.Count; i++)
            {
                if (fronds.Vertices[i].y < lowest) lowest = fronds.Vertices[i].y;
            }
            Harness.Check(lowest < 8f,
                string.Format("and hang below the block (lowest y={0:0.00})", lowest));

            // Fronds must stay close enough to their own block that aiming at one
            // resolves to the cell it belongs to. Mining and placing both derive the
            // cell from where the ray lands, so foliage that sprawls into a neighbour
            // is foliage you cannot chop.
            float reach = 0f;
            for (int i = 0; i < fronds.Vertices.Count; i++)
            {
                var v = fronds.Vertices[i];
                reach = Mathf.Max(reach, Mathf.Abs(v.x - 8.5f));
                reach = Mathf.Max(reach, Mathf.Abs(v.z - 8.5f));
            }
            Harness.Check(reach < 1f,
                string.Format("and stay within a block of their own centre (reach {0:0.00}m)", reach));

            // A lone trunk is a stump, and a stump is something a player stands on.
            var stump = Padded();
            Set(stump, 8, 4, 8, log);

            var stumpMesh = ChunkMesher.Build(stump, meta);
            float top = float.MinValue;
            for (int i = 0; i < stumpMesh.Vertices.Count; i++)
            {
                if (stumpMesh.Vertices[i].y > top) top = stumpMesh.Vertices[i].y;
            }

            int atTop = 0;
            for (int i = 0; i < stumpMesh.Vertices.Count; i++)
            {
                if (Mathf.Abs(stumpMesh.Vertices[i].y - top) < 0.001f) atTop++;
            }
            Harness.Check(atTop > 4,
                string.Format("a stump is capped, not an open pipe ({0} vertices on its top face)", atTop));

            // Where a trunk continues, it is not capped - a disc inside the tree at
            // every metre is triangles nobody ever sees.
            var column = Padded();
            for (int y = 2; y < 9; y++) Set(column, 8, y, 8, log);

            var columnMesh = ChunkMesher.Build(column, meta);
            Harness.Check(columnMesh.Vertices.Count < stumpMesh.Vertices.Count * 7,
                "and a seven-block trunk is cheaper than seven stumps");

            // Where the tree stands decides how it looks, not where it sits in its
            // chunk. Two identical columns in different chunks must not be identical
            // trees, or every chunk grows the same forest at the same offsets.
            var here = ChunkMesher.Build(column, meta, new Vector3Int(0, 0, 0));
            var there = ChunkMesher.Build(column, meta, new Vector3Int(64, 0, 112));

            bool same = here.Vertices.Count == there.Vertices.Count;
            for (int i = 0; same && i < here.Vertices.Count; i++)
            {
                same = here.Vertices[i] == there.Vertices[i];
            }
            Harness.Check(!same, "and the same column in another chunk grows a different tree");

            // A tree must not shade the ground under it like a wall would. Trunks have
            // air all round them and canopies are mostly gaps, so treating either as
            // solid would stamp a hard square shadow under every pine in the world.
            ushort stone = db.blocks.IdOf(BlockIds.Stone);

            var lit = Padded();
            for (int x = -1; x <= 16; x++)
                for (int z = -1; z <= 16; z++)
                    Set(lit, x, 5, z, stone);

            var bare = ChunkMesher.Build(lit, meta);

            for (int y = 6; y < 12; y++) Set(lit, 8, y, 8, log);
            var wooded = ChunkMesher.Build(lit, meta);

            // Only the ground's own vertices. Measuring the whole mesh would pick up
            // the trunk's facet shading, which is the trunk looking round rather than
            // the ground being shadowed - two different things that happen to be dark.
            Harness.Equal(DarkestOf(wooded, stone), DarkestOf(bare, stone),
                "and a tree standing on ground does not shade it");
        }

        /// <summary>Is this coordinate exactly on a block boundary?</summary>
        static bool OnLattice(float value)
        {
            return Mathf.Abs(value - Mathf.Round(value)) < 0.0001f;
        }

        static ushort[] Padded()
        {
            const int p = Chunk.Size + 2;
            return new ushort[p * p * p];
        }

        static void Set(ushort[] padded, int x, int y, int z, ushort id)
        {
            const int p = Chunk.Size + 2;
            padded[((y + 1) * p + (z + 1)) * p + (x + 1)] = id;
        }

        /// <summary>Darkest shade among the vertices one block's faces actually use.</summary>
        static int DarkestOf(ChunkMeshData data, ushort blockId)
        {
            List<int> tris;
            if (!data.Triangles.TryGetValue(blockId, out tris)) return -1;

            int min = 255;
            for (int i = 0; i < tris.Count; i++)
            {
                int shade = data.Colors[tris[i]].r;
                if (shade < min) min = shade;
            }
            return min;
        }

        static int Darkest(ChunkMeshData data)
        {
            int min = 255;
            for (int i = 0; i < data.Colors.Count; i++) if (data.Colors[i].r < min) min = data.Colors[i].r;
            return min;
        }

        static int Brightest(ChunkMeshData data)
        {
            int max = 0;
            for (int i = 0; i < data.Colors.Count; i++) if (data.Colors[i].r > max) max = data.Colors[i].r;
            return max;
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
