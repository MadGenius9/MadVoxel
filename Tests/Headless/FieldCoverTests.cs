using System.Collections.Generic;
using MadVoxel.World.Fields;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The crop cover. Two things here are worth a test and neither is visible in a
    /// screenshot: that a patch settles instead of remeshing every frame, and that the
    /// geometry is actually somewhere a plant would stand.
    /// </summary>
    public static class FieldCoverTests
    {
        public static void Run()
        {
            Patches();
            Growth();
            Geometry();
            Staleness();
        }

        static CoverPlant Plant(int x, int z, byte crop, float height, bool ready = false, int surfaceY = 40)
        {
            return new CoverPlant { X = x, Z = z, SurfaceY = surfaceY, CropIndex = crop, Height = height, Ready = ready };
        }

        // ----------------------------------------------------------------- patches

        static void Patches()
        {
            Harness.Section("field cover: patches");

            int size = FieldCoverMesh.PatchSize;

            Harness.Check(FieldCoverMesh.PatchOf(0, 0) == new Vector2Int(0, 0), "the origin cell is in patch 0,0");
            Harness.Check(FieldCoverMesh.PatchOf(size - 1, size - 1) == new Vector2Int(0, 0),
                "and so is the last cell of that patch");
            Harness.Check(FieldCoverMesh.PatchOf(size, 0) == new Vector2Int(1, 0), "the next cell starts a new patch");

            // Negative coordinates are where naive integer division tiles wrongly: -1/16
            // is 0 in C#, which would put a cell west of the origin in the origin patch
            // and overlay two patches of crop on the same ground.
            Harness.Check(FieldCoverMesh.PatchOf(-1, -1) == new Vector2Int(-1, -1),
                "the cell west of the origin is in patch -1,-1, not 0,0");
            Harness.Check(FieldCoverMesh.PatchOf(-size, 0) == new Vector2Int(-1, 0), "and patches tile leftwards");
            Harness.Check(FieldCoverMesh.PatchOf(-size - 1, 0) == new Vector2Int(-2, 0), "all the way out");

            // Every cell of a patch must agree on which patch it is in.
            var first = FieldCoverMesh.PatchOf(-size, -size);
            bool agree = true;
            for (int z = -size; z < 0; z++)
            {
                for (int x = -size; x < 0; x++)
                {
                    if (FieldCoverMesh.PatchOf(x, z) != first) agree = false;
                }
            }
            Harness.Check(agree, "every cell of a negative patch maps to the same patch");

            // And distinct patches must not collide on their key.
            var keys = new HashSet<long>();
            bool unique = true;
            for (int pz = -3; pz <= 3; pz++)
            {
                for (int px = -3; px <= 3; px++)
                {
                    if (!keys.Add(FieldCoverMesh.PatchKey(px, pz))) unique = false;
                }
            }
            Harness.Check(unique, "49 neighbouring patches have 49 distinct keys");
        }

        // ------------------------------------------------------------------ growth

        static void Growth()
        {
            Harness.Section("field cover: growth");

            Harness.Equal(FieldCoverMesh.BucketOf(0f), 0, "a just-sown cell is in the first bucket");
            Harness.Equal(FieldCoverMesh.BucketOf(1f), FieldCoverMesh.HeightBuckets - 1,
                "a ripe one is in the last");
            Harness.Equal(FieldCoverMesh.BucketOf(2f), FieldCoverMesh.HeightBuckets - 1,
                "and past ripe stays there rather than running off the end");
            Harness.Equal(FieldCoverMesh.BucketOf(-1f), 0, "a negative fraction clamps too");

            bool monotonic = true;
            int last = -1;
            for (float p = 0f; p <= 1f; p += 0.02f)
            {
                int bucket = FieldCoverMesh.BucketOf(p);
                if (bucket < last) monotonic = false;
                last = bucket;
            }
            Harness.Check(monotonic, "buckets never go backwards as a crop grows");

            // A shoot has to be visible the moment it is sown, or the drill gives no
            // feedback at all and you cannot tell sown ground from bare.
            Harness.Check(FieldCoverMesh.HeightFor(1.35f, 0) > 0.1f, "a newly sown cell still shows a shoot");
            Harness.Check(FieldCoverMesh.HeightFor(1.35f, FieldCoverMesh.HeightBuckets - 1) > FieldCoverMesh.HeightFor(1.35f, 0) * 2f,
                "and a ripe plant is more than twice the height of a shoot");

            bool grows = true;
            for (int b = 1; b < FieldCoverMesh.HeightBuckets; b++)
            {
                if (FieldCoverMesh.HeightFor(1.35f, b) <= FieldCoverMesh.HeightFor(1.35f, b - 1)) grows = false;
            }
            Harness.Check(grows, "every bucket is taller than the one before it");

            // A crop with no mature height still gets geometry rather than a zero-area mesh.
            Harness.Check(FieldCoverMesh.HeightFor(0f, 4) > 0f, "even a zero-height crop draws something");

            Harness.Equal(FieldCoverMesh.KeyFor(3, false), FieldCoverMesh.KeyFor(3, false), "the material key is stable");
            Harness.Check(FieldCoverMesh.KeyFor(3, true) != FieldCoverMesh.KeyFor(3, false),
                "ripe and growing are different materials");
            Harness.Check(FieldCoverMesh.KeyFor(3, true) != FieldCoverMesh.KeyFor(4, false),
                "and so are different crops");
            Harness.Equal((int)FieldCoverMesh.CropOfKey(FieldCoverMesh.KeyFor(5, true)), 5, "the key round-trips the crop");
            Harness.Check(FieldCoverMesh.ReadyOfKey(FieldCoverMesh.KeyFor(5, true)), "and its ripeness");
            Harness.Check(!FieldCoverMesh.ReadyOfKey(FieldCoverMesh.KeyFor(5, false)), "both ways");
        }

        // ---------------------------------------------------------------- geometry

        static void Geometry()
        {
            Harness.Section("field cover: geometry");

            var data = new CoverMeshData();

            FieldCoverMesh.Build(new List<CoverPlant>(), data);
            Harness.Check(data.IsEmpty, "no plants means no mesh");

            // Bare ground must emit nothing at all, or a plowed field draws invisible
            // geometry across every cell of it.
            var bare = new List<CoverPlant> { Plant(0, 0, 0, 1f), Plant(1, 0, 2, 0f) };
            FieldCoverMesh.Build(bare, data);
            Harness.Check(data.IsEmpty, "an unsown cell and a zero-height plant draw nothing");

            var one = new List<CoverPlant> { Plant(4, 7, 2, 1.2f) };
            FieldCoverMesh.Build(one, data);

            Harness.Equal(data.Vertices.Count, 8, "one plant is two crossed quads: eight vertices");
            Harness.Equal(data.Normals.Count, 8, "with a normal each");
            Harness.Equal(data.Uvs.Count, 8, "and a uv each");
            Harness.Equal(data.KeyOrder.Count, 1, "in one sub-mesh");
            Harness.Equal(data.Triangles[data.KeyOrder[0]].Count, 24,
                "and twenty-four indices, because both sides of both quads are drawn");

            // Single-sided shading would make half of every row black from one side.
            var counts = new Dictionary<int, int>();
            var tris = data.Triangles[data.KeyOrder[0]];
            for (int i = 0; i < tris.Count; i++)
            {
                int v = tris[i];
                counts.TryGetValue(v, out int seen);
                counts[v] = seen + 1;
            }
            bool bothWays = true;
            foreach (var entry in counts)
            {
                if (entry.Value % 2 != 0) bothWays = false;
            }
            Harness.Check(bothWays, "every vertex appears an even number of times: each face has its twin");

            // The plant must stand on the ground, not float above it or sink into it.
            float minY = float.MaxValue, maxY = float.MinValue;
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < data.Vertices.Count; i++)
            {
                minY = Mathf.Min(minY, data.Vertices[i].y); maxY = Mathf.Max(maxY, data.Vertices[i].y);
                minX = Mathf.Min(minX, data.Vertices[i].x); maxX = Mathf.Max(maxX, data.Vertices[i].x);
            }
            Harness.Equal(minY, 41f, "the base sits on top of the surface block, not inside it");
            Harness.Check(maxY > minY, "and the plant has height");
            Harness.Check(maxY - minY < 1.2f * 1.2f, "which is near the height it was given, jitter aside");

            // Jitter must not walk a plant into the next cell, or a field pulls apart.
            Harness.Check(minX > 3.0f && maxX < 6.0f,
                string.Format("the plant stays near its own cell: x spans {0:0.00} to {1:0.00}", minX, maxX));

            // Deterministic: a patch rebuilt after a reload must look identical.
            var again = new CoverMeshData();
            FieldCoverMesh.Build(one, again);
            bool identical = again.Vertices.Count == data.Vertices.Count;
            for (int i = 0; identical && i < data.Vertices.Count; i++)
            {
                if (again.Vertices[i] != data.Vertices[i]) identical = false;
            }
            Harness.Check(identical, "the same cell always meshes to the same vertices");

            // Two crops in a patch must land in two sub-meshes, or one takes the other's
            // colour and you cannot tell wheat from corn.
            var mixed = new List<CoverPlant>
            {
                Plant(0, 0, 1, 1f), Plant(1, 0, 2, 1f), Plant(2, 0, 2, 1f, true)
            };
            FieldCoverMesh.Build(mixed, data);
            Harness.Equal(data.KeyOrder.Count, 3, "two crops and a ripe one make three sub-meshes");
            Harness.Equal(data.Vertices.Count, 24, "with eight vertices each");
        }

        // --------------------------------------------------------------- staleness

        static void Staleness()
        {
            Harness.Section("field cover: when a patch is rebuilt");

            var plants = new List<CoverPlant> { Plant(3, 3, 1, 0.5f), Plant(4, 3, 1, 0.5f) };
            int baseline = FieldCoverMesh.Signature(plants);

            Harness.Equal(FieldCoverMesh.Signature(plants), baseline, "the same field signs the same");

            // THE ONE THAT MATTERS. Growth is derived from the clock, so a plant's true
            // height changes every frame. If the signature followed the raw fraction,
            // every patch of every field would remesh forever. It follows the bucket.
            float mature = 1.35f;
            int sameBucketChanges = 0;
            int lastSignature = 0;
            var probe = new List<CoverPlant> { Plant(3, 3, 1, 0f) };

            for (float p = 0.26f; p < 0.36f; p += 0.005f)
            {
                probe[0] = Plant(3, 3, 1, FieldCoverMesh.HeightFor(mature, FieldCoverMesh.BucketOf(p)));
                int signature = FieldCoverMesh.Signature(probe);
                if (lastSignature != 0 && signature != lastSignature) sameBucketChanges++;
                lastSignature = signature;
            }
            Harness.Check(sameBucketChanges <= 1,
                string.Format("a crop growing across one bucket boundary remeshes {0} time(s), not twenty",
                    sameBucketChanges));

            // Over a whole life it must remesh a handful of times - not never.
            var seen = new HashSet<int>();
            for (float p = 0f; p <= 1f; p += 0.01f)
            {
                probe[0] = Plant(3, 3, 1, FieldCoverMesh.HeightFor(mature, FieldCoverMesh.BucketOf(p)));
                seen.Add(FieldCoverMesh.Signature(probe));
            }
            Harness.Equal(seen.Count, FieldCoverMesh.HeightBuckets,
                "a crop remeshes once per bucket over its whole life");

            // Everything that would look different has to move the signature.
            Harness.Check(FieldCoverMesh.Signature(new List<CoverPlant> { Plant(3, 3, 1, 0.5f), Plant(4, 3, 1, 0.6f) }) != baseline,
                "a plant growing into the next bucket rebuilds the patch");
            Harness.Check(FieldCoverMesh.Signature(new List<CoverPlant> { Plant(3, 3, 1, 0.5f), Plant(4, 3, 1, 0.5f, true) }) != baseline,
                "a plant ripening rebuilds it");
            Harness.Check(FieldCoverMesh.Signature(new List<CoverPlant> { Plant(3, 3, 1, 0.5f), Plant(4, 3, 2, 0.5f) }) != baseline,
                "sowing a different crop rebuilds it");
            Harness.Check(FieldCoverMesh.Signature(new List<CoverPlant> { Plant(3, 3, 1, 0.5f) }) != baseline,
                "harvesting a cell rebuilds it");
            Harness.Check(FieldCoverMesh.Signature(new List<CoverPlant> { Plant(3, 3, 1, 0.5f), Plant(4, 3, 1, 0.5f, false, 39) }) != baseline,
                "and digging the ground out from under one rebuilds it");

            Harness.Equal(FieldCoverMesh.Signature(null), 0, "nothing signs as nothing");
        }
    }
}
