using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.World.Fields
{
    /// <summary>One plant the cover mesher has been asked to draw.</summary>
    public struct CoverPlant
    {
        public int X, Z;
        /// <summary>Y of the ground surface the plant stands on.</summary>
        public int SurfaceY;
        /// <summary>Which crop, and whether it is ripe. Decides the material.</summary>
        public byte CropIndex;
        public bool Ready;
        /// <summary>Metres, already bucketed by the caller.</summary>
        public float Height;
    }

    /// <summary>
    /// Vertex buffers for a patch of field cover, grouped by what material each plant
    /// wants. Mirrors <see cref="MadVoxel.World.Terrain.ChunkMeshData"/> deliberately:
    /// one sub-mesh per material, no texture atlas.
    /// </summary>
    public class CoverMeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>(1024);
        public readonly List<Vector3> Normals = new List<Vector3>(1024);
        public readonly List<Vector2> Uvs = new List<Vector2>(1024);

        /// <summary>Material key -> triangle indices. See <see cref="FieldCoverMesh.KeyFor"/>.</summary>
        public readonly Dictionary<int, List<int>> Triangles = new Dictionary<int, List<int>>();
        public readonly List<int> KeyOrder = new List<int>();

        public bool IsEmpty { get { return Vertices.Count == 0; } }

        public List<int> TrianglesFor(int key)
        {
            List<int> list;
            if (!Triangles.TryGetValue(key, out list))
            {
                list = new List<int>(256);
                Triangles.Add(key, list);
                KeyOrder.Add(key);
            }
            return list;
        }

        public void Clear()
        {
            Vertices.Clear();
            Normals.Clear();
            Uvs.Clear();
            Triangles.Clear();
            KeyOrder.Clear();
        }
    }

    /// <summary>
    /// Turns sown field cells into something you can see from the seat of a tractor.
    ///
    /// The field layer could always be *worked* - plowed ground turns the block to
    /// tilled soil - but a sown cell, a growing cell and a ripe cell all looked
    /// identical. You could sow an acre and have nothing to look at, and no way to tell
    /// by eye which half of a field was ready.
    ///
    /// A farm plot builds one GameObject per stalk and destroys them on every change.
    /// That is fine for a handful of plots and ruinous for ten thousand cells, so cover
    /// is meshed instead: one mesh per 16 m patch, one sub-mesh per crop-and-ripeness,
    /// and a patch is only rebuilt when something in it actually changed.
    ///
    /// The "actually changed" is the whole trick, and it is why height is bucketed.
    /// Growth is derived from the clock, so a plant's true height changes every frame;
    /// meshing that would rebuild every patch of every field forever. Bucketing the
    /// height means a patch settles until a plant crosses into the next bucket, which
    /// is also the point at which the change is visible.
    ///
    /// Pure and engine-free on purpose: a field that meshes wrong is either invisible
    /// or a wall of geometry, and both are cheaper to catch here than in an editor.
    /// </summary>
    public static class FieldCoverMesh
    {
        /// <summary>Metres square. Matches the terrain chunk footprint.</summary>
        public const int PatchSize = 16;

        /// <summary>
        /// Growth steps a plant is drawn at. Eight is enough that a field visibly comes
        /// on over its days without the mesher chasing every frame's fraction.
        /// </summary>
        public const int HeightBuckets = 8;

        /// <summary>Width of one plant's cross, in metres. Slightly under a cell so rows read.</summary>
        const float PlantWidth = 0.85f;

        /// <summary>How far a plant may wander from its cell centre, so a field is not a lattice.</summary>
        const float JitterMetres = 0.22f;

        // ------------------------------------------------------------------ patches

        public static Vector2Int PatchOf(int cellX, int cellZ)
        {
            return new Vector2Int(FloorDiv(cellX, PatchSize), FloorDiv(cellZ, PatchSize));
        }

        public static long PatchKey(int patchX, int patchZ)
        {
            return ((long)patchX << 32) ^ (uint)patchZ;
        }

        /// <summary>Floor division, so patches tile correctly through negative coordinates.</summary>
        static int FloorDiv(int value, int size)
        {
            return value >= 0 ? value / size : ((value + 1) / size) - 1;
        }

        // ------------------------------------------------------------------ growth

        /// <summary>
        /// Which of the <see cref="HeightBuckets"/> steps a progress fraction draws at.
        /// A cell that has only just been sown is bucket 0 and still gets a shoot: bare
        /// ground and sown ground have to look different the moment you sow them, or the
        /// drill gives no feedback at all.
        /// </summary>
        public static int BucketOf(float progress01)
        {
            int bucket = Mathf.FloorToInt(Mathf.Clamp01(progress01) * HeightBuckets);
            return Mathf.Clamp(bucket, 0, HeightBuckets - 1);
        }

        /// <summary>Metres a plant of this maturity stands at its bucket.</summary>
        public static float HeightFor(float matureHeight, int bucket)
        {
            float t = (bucket + 1) / (float)HeightBuckets;
            return Mathf.Max(0.1f, matureHeight * Mathf.Lerp(0.22f, 1f, t));
        }

        // ---------------------------------------------------------------- materials

        /// <summary>
        /// The sub-mesh a plant belongs in. Ripeness is part of the key rather than a
        /// tint applied later, because "which half of this field is ready" is the one
        /// question the cover exists to answer from a distance.
        /// </summary>
        public static int KeyFor(byte cropIndex, bool ready)
        {
            return cropIndex * 2 + (ready ? 1 : 0);
        }

        public static byte CropOfKey(int key) { return (byte)(key / 2); }
        public static bool ReadyOfKey(int key) { return (key & 1) == 1; }

        // ------------------------------------------------------------------- mesh

        /// <summary>
        /// Builds the buffers for one patch. Each plant is two crossed quads - the
        /// oldest trick there is for foliage, and the only one that stays cheap at ten
        /// thousand cells.
        /// </summary>
        public static void Build(IList<CoverPlant> plants, CoverMeshData into)
        {
            if (into == null) return;
            into.Clear();
            if (plants == null) return;

            for (int i = 0; i < plants.Count; i++)
            {
                var plant = plants[i];
                if (plant.CropIndex == 0 || plant.Height <= 0f) continue;

                // Deterministic from the cell, so a patch rebuilt after a reload puts
                // every plant back exactly where it was.
                float jitterX = (Hash(plant.X, plant.Z, 1) - 0.5f) * 2f * JitterMetres;
                float jitterZ = (Hash(plant.X, plant.Z, 2) - 0.5f) * 2f * JitterMetres;
                float scale = Mathf.Lerp(0.86f, 1.14f, Hash(plant.X, plant.Z, 3));

                var centre = new Vector3(plant.X + 0.5f + jitterX,
                                         plant.SurfaceY + 1f,
                                         plant.Z + 0.5f + jitterZ);

                var triangles = into.TrianglesFor(KeyFor(plant.CropIndex, plant.Ready));

                float height = plant.Height * scale;
                const float Diagonal = 0.70710678f;

                Quad(into, triangles, centre, new Vector3(Diagonal, 0f, Diagonal), height);
                Quad(into, triangles, centre, new Vector3(Diagonal, 0f, -Diagonal), height);
            }
        }

        /// <summary>
        /// One upright quad through the plant's centre, drawn from both sides.
        ///
        /// Both windings share the same four vertices and the same upward normal rather
        /// than doubling up with a flipped one: URP's Lit shader is single-sided, and a
        /// plant lit from above reads far better than one whose back face is black.
        /// </summary>
        static void Quad(CoverMeshData data, List<int> triangles, Vector3 centre, Vector3 across, float height)
        {
            int baseIndex = data.Vertices.Count;
            Vector3 half = across * (PlantWidth * 0.5f);
            Vector3 top = new Vector3(0f, height, 0f);

            data.Vertices.Add(centre - half);
            data.Vertices.Add(centre + half);
            data.Vertices.Add(centre + half + top);
            data.Vertices.Add(centre - half + top);

            for (int i = 0; i < 4; i++) data.Normals.Add(Vector3.up);

            data.Uvs.Add(new Vector2(0f, 0f));
            data.Uvs.Add(new Vector2(1f, 0f));
            data.Uvs.Add(new Vector2(1f, 1f));
            data.Uvs.Add(new Vector2(0f, 1f));

            triangles.Add(baseIndex); triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex); triangles.Add(baseIndex + 3); triangles.Add(baseIndex + 2);

            // The back faces, so a row does not vanish when you drive past it.
            triangles.Add(baseIndex); triangles.Add(baseIndex + 1); triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex); triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 3);
        }

        /// <summary>0..1 from a cell and a salt. Cheap, stable, and never saved.</summary>
        static float Hash(int x, int z, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ (uint)(salt * 83492791);
                h ^= h >> 13;
                h *= 0x85EBCA6Bu;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        // ----------------------------------------------------------------- staleness

        /// <summary>
        /// A number that changes exactly when a patch would look different. The cover
        /// world keeps the last one per patch and only remeshes when it moves, which is
        /// what stops derived growth from rebuilding every field every frame.
        /// </summary>
        public static int Signature(IList<CoverPlant> plants)
        {
            if (plants == null) return 0;

            unchecked
            {
                int hash = 17;
                for (int i = 0; i < plants.Count; i++)
                {
                    var plant = plants[i];
                    hash = hash * 31 + plant.X;
                    hash = hash * 31 + plant.Z;
                    hash = hash * 31 + plant.SurfaceY;
                    hash = hash * 31 + plant.CropIndex;
                    hash = hash * 31 + (plant.Ready ? 1 : 0);
                    // The bucketed height, not the raw one: same reason as above.
                    hash = hash * 31 + Mathf.RoundToInt(plant.Height * 100f);
                }
                return hash;
            }
        }
    }
}
