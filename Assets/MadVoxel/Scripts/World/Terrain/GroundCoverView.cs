using System.Collections.Generic;
using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.World.Terrain
{
    /// <summary>
    /// Puts the grass on the ground.
    ///
    /// Built the same way the field cover is, and for the same reasons: one mesh per
    /// 16 m patch rather than a GameObject per plant, a rebuild budget so walking into
    /// a meadow never spikes a frame, and patches dropped once they are behind you.
    /// Those decisions were argued out in <see cref="Fields.FieldCoverView"/> and none
    /// of them change for grass.
    ///
    /// What is different is that there is no state to read. A crop is sown and the
    /// field grid remembers it; grass is not planted by anyone, so a patch is scattered
    /// from a hash of world position and the terrain under it. That means nothing to
    /// save, nothing to load, and ground that is dug up and filled back in grows the
    /// same grass it had before.
    ///
    /// It also means the whole thing can be switched off and the game is unchanged.
    /// Nothing gameplay-facing reads it; it does not block, it does not collide, and
    /// no rule anywhere asks whether a cell has grass on it.
    /// </summary>
    public class GroundCoverView : MonoBehaviour
    {
        /// <summary>Seconds between scans. Grass does not move, so this is generous.</summary>
        const float ScanInterval = 0.5f;

        /// <summary>Patches rebuilt per frame, so cresting a hill never spikes.</summary>
        const int RebuildBudget = 2;

        class Patch
        {
            public GameObject Root;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public int Signature;
            public bool Seen;
        }

        readonly Dictionary<Vector2Int, Patch> _patches = new Dictionary<Vector2Int, Patch>();
        readonly List<Vector2Int> _stale = new List<Vector2Int>();
        readonly List<CoverTuft> _tufts = new List<CoverTuft>(2048);

        readonly List<Vector3> _vertices = new List<Vector3>(4096);
        readonly List<Vector3> _normals = new List<Vector3>(4096);
        readonly List<Vector2> _uvs = new List<Vector2>(4096);
        readonly List<Color32> _colors = new List<Color32>(4096);
        readonly List<int> _triangles = new List<int>(6144);

        TerrainWorld _voxels;
        Transform _viewer;
        Material _material;
        float _nextScan;

        /// <summary>Turns the whole layer off. Nothing else in the game notices.</summary>
        public bool Enabled = true;

        public void Init(TerrainWorld voxels, Transform viewer)
        {
            _voxels = voxels;
            _viewer = viewer;

            // One material for every blade. Tint variation rides on vertex colour, the
            // same channel the terrain's baked occlusion uses, so a meadow is one draw
            // call per patch instead of one per shade of green.
            _material = MaterialLibrary.Get(SurfaceFamily.Foliage, Color.white, 0.04f);
        }

        void Update()
        {
            if (_voxels == null || _viewer == null) return;

            if (!Enabled)
            {
                if (_patches.Count > 0) DropAll();
                return;
            }

            if (Time.time < _nextScan) return;
            _nextScan = Time.time + ScanInterval;

            Scan();
        }

        void Scan()
        {
            foreach (var kv in _patches) kv.Value.Seen = false;

            Vector3 eye = _viewer.position;
            int reach = Mathf.CeilToInt(GroundCover.DrawDistance / GroundCover.PatchSize);

            var centre = GroundCover.PatchOf(Mathf.FloorToInt(eye.x), Mathf.FloorToInt(eye.z));
            int built = 0;

            for (int pz = -reach; pz <= reach; pz++)
            for (int px = -reach; px <= reach; px++)
            {
                var key = new Vector2Int(centre.x + px, centre.y + pz);

                // Measured to the patch's middle, so a patch does not pop in at one
                // corner and out at the other.
                float cx = (key.x + 0.5f) * GroundCover.PatchSize;
                float cz = (key.y + 0.5f) * GroundCover.PatchSize;
                float distance = Mathf.Sqrt((cx - eye.x) * (cx - eye.x) + (cz - eye.z) * (cz - eye.z));

                if (distance > GroundCover.DrawDistance) continue;

                Patch patch;
                bool existed = _patches.TryGetValue(key, out patch);
                if (existed) patch.Seen = true;

                if (built >= RebuildBudget && !existed) continue;

                int signature = Gather(key, eye);

                if (existed && patch.Signature == signature) continue;
                if (built >= RebuildBudget) continue;

                Rebuild(key, signature);
                built++;
            }

            DropUnseen();
        }

        /// <summary>
        /// Fills the tuft list for a patch and returns a signature for it.
        ///
        /// The signature is what stops a patch rebuilding every scan: it folds in the
        /// ground height, what is growing and the bucketed density, so walking towards
        /// a hillside remeshes it a handful of times rather than twice a second.
        /// </summary>
        int Gather(Vector2Int key, Vector3 eye)
        {
            _tufts.Clear();

            int baseX = key.x * GroundCover.PatchSize;
            int baseZ = key.y * GroundCover.PatchSize;

            unchecked
            {
                int signature = 17;

                for (int z = 0; z < GroundCover.PatchSize; z++)
                for (int x = 0; x < GroundCover.PatchSize; x++)
                {
                    int wx = baseX + x;
                    int wz = baseZ + z;

                    byte kind;
                    int surface = _voxels.GetSurfaceY(wx, wz);
                    if (!GrowsHere(wx, surface, wz, out kind)) continue;

                    float dx = wx + 0.5f - eye.x;
                    float dz = wz + 0.5f - eye.z;
                    float distance = Mathf.Sqrt(dx * dx + dz * dz);

                    // Bucketed, so the signature does not change on every step.
                    float density = GroundCover.DensityAt(distance);
                    int bucket = Mathf.RoundToInt(density * 4f);
                    if (bucket <= 0) continue;

                    GroundCover.Scatter(wx, wz, surface + 1f, kind, bucket / 4f, _tufts);

                    signature = signature * 31 + surface;
                    signature = signature * 31 + kind;
                    signature = signature * 31 + bucket;
                }

                return signature;
            }
        }

        /// <summary>
        /// Does anything grow on this column, and what?
        ///
        /// Only on open natural ground. Nothing grows under a roof, on a built floor,
        /// or on stone - which also means a base's footprint clears itself, since the
        /// blocks a player puts down are not ground.
        /// </summary>
        bool GrowsHere(int wx, int surfaceY, int wz, out byte kind)
        {
            kind = GroundCover.KindGrass;

            var def = _voxels.GetBlockDef(wx, surfaceY, wz);
            if (def == null || def.isAir) return false;

            // Under something. A cellar floor does not grow grass.
            if (_voxels.IsSolid(wx, surfaceY + 1, wz)) return false;

            if (def.stringId == BlockIds.Grass) { kind = GroundCover.KindGrass; return true; }
            if (def.stringId == BlockIds.Dirt) { kind = GroundCover.KindScrub; return true; }
            if (def.stringId == BlockIds.Sand) { kind = GroundCover.KindDry; return true; }

            return false;
        }

        void Rebuild(Vector2Int key, int signature)
        {
            Patch patch;
            if (!_patches.TryGetValue(key, out patch))
            {
                patch = NewPatch(key);
                _patches[key] = patch;
            }

            patch.Seen = true;
            patch.Signature = signature;

            BuildMesh(patch);
        }

        Patch NewPatch(Vector2Int key)
        {
            var go = new GameObject(string.Format("Grass {0},{1}", key.x, key.y));
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(key.x * GroundCover.PatchSize, 0f, key.y * GroundCover.PatchSize);

            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();

            // Grass casting shadows is a great deal of shadow map for very little, and
            // it is the first thing to turn off if a meadow costs frames.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sharedMaterial = _material;

            var mesh = new Mesh { name = "GrassPatch" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;

            return new Patch { Root = go, Mesh = mesh, Renderer = renderer };
        }

        void BuildMesh(Patch patch)
        {
            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _colors.Clear();
            _triangles.Clear();

            Vector3 origin = patch.Root.transform.position;

            for (int i = 0; i < _tufts.Count; i++)
            {
                var tuft = _tufts[i];
                var centre = new Vector3(tuft.X - origin.x, tuft.Y, tuft.Z - origin.z);

                // Two crossed quads, the same trick the crop cover uses: readable from
                // any angle for four triangles, and no billboarding work per frame.
                const float Half = 0.17f;
                Blade(centre, new Vector3(Half, 0f, Half), tuft);
                Blade(centre, new Vector3(Half, 0f, -Half), tuft);
            }

            patch.Mesh.Clear();
            if (_vertices.Count == 0)
            {
                patch.Renderer.enabled = false;
                return;
            }

            patch.Mesh.SetVertices(_vertices);
            patch.Mesh.SetNormals(_normals);
            patch.Mesh.SetUVs(0, _uvs);
            patch.Mesh.SetColors(_colors);
            patch.Mesh.SetTriangles(_triangles, 0, true);
            patch.Mesh.RecalculateBounds();

            patch.Renderer.enabled = true;
        }

        void Blade(Vector3 centre, Vector3 across, CoverTuft tuft)
        {
            int b = _vertices.Count;

            _vertices.Add(centre - across);
            _vertices.Add(centre + across);
            _vertices.Add(centre + across + Vector3.up * tuft.Height);
            _vertices.Add(centre - across + Vector3.up * tuft.Height);

            // Facing up rather than out: grass lit by its own facing looks like paper
            // standing on edge, and lit by the ground it grows from looks like grass.
            for (int i = 0; i < 4; i++) _normals.Add(Vector3.up);

            _uvs.Add(new Vector2(0f, 0f));
            _uvs.Add(new Vector2(1f, 0f));
            _uvs.Add(new Vector2(1f, 1f));
            _uvs.Add(new Vector2(0f, 1f));

            // Dark at the root, full at the tip. This is the grass equivalent of the
            // terrain's baked occlusion and it does the same job: a flat green quad
            // reads as a sticker, and one that darkens into the ground reads as growing
            // out of it.
            var tip = TintFor(tuft.Kind);
            var root = Darken(tip, 0.55f);

            _colors.Add(root);
            _colors.Add(root);
            _colors.Add(tip);
            _colors.Add(tip);

            _triangles.Add(b); _triangles.Add(b + 2); _triangles.Add(b + 1);
            _triangles.Add(b); _triangles.Add(b + 3); _triangles.Add(b + 2);
        }

        static Color32 TintFor(byte kind)
        {
            switch (kind)
            {
                case GroundCover.KindScrub: return new Color32(150, 150, 110, 255);
                case GroundCover.KindDry: return new Color32(185, 175, 130, 255);
                default: return new Color32(120, 165, 95, 255);
            }
        }

        static Color32 Darken(Color32 c, float by)
        {
            return new Color32(
                (byte)(c.r * by), (byte)(c.g * by), (byte)(c.b * by), c.a);
        }

        void DropUnseen()
        {
            _stale.Clear();
            foreach (var kv in _patches)
            {
                if (!kv.Value.Seen) _stale.Add(kv.Key);
            }

            for (int i = 0; i < _stale.Count; i++) Drop(_stale[i]);
        }

        void DropAll()
        {
            _stale.Clear();
            foreach (var kv in _patches) _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++) Drop(_stale[i]);
        }

        void Drop(Vector2Int key)
        {
            Patch patch;
            if (!_patches.TryGetValue(key, out patch)) return;

            if (patch.Mesh != null) Destroy(patch.Mesh);
            if (patch.Root != null) Destroy(patch.Root);
            _patches.Remove(key);
        }

        void OnDestroy()
        {
            DropAll();
        }
    }
}
