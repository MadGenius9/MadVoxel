using System.Collections.Generic;
using MadVoxel.Core;
using MadVoxel.Farming.Crops;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.World.Fields
{
    /// <summary>
    /// Draws the standing crop across the field grid.
    ///
    /// The field layer could be worked long before it could be seen: plowing turned the
    /// block to tilled soil, but a sown cell, a growing cell and a ripe cell all looked
    /// the same. Now that a tractor can sow an acre in a minute that is the weakest
    /// thing in the loop, so this is the other half of the field layer - the half you
    /// look at.
    ///
    /// The shape of it:
    ///
    /// - <b>Patches, not objects.</b> One mesh per 16 m patch, not one GameObject per
    ///   plant. A garden plot builds five boxes per plot and destroys them on every
    ///   change; the same approach over ten thousand cells would be tens of thousands
    ///   of transforms.
    /// - <b>Signatures, not timers.</b> Growth is derived from the clock, so every
    ///   plant's true height changes every frame. A patch is remeshed only when its
    ///   <see cref="FieldCoverMesh.Signature"/> moves, which bucketed height makes
    ///   happen a handful of times over a crop's whole life.
    /// - <b>Ripeness from progress, not from stored state.</b> A cell's stored state
    ///   only advances when something touches it, but the hours since sowing are always
    ///   true. So a field goes visibly gold on time whether or not anyone has walked
    ///   past it.
    /// - <b>Only what is near.</b> Patches outside the radius are dropped, because a
    ///   farm you left behind two hundred metres ago is not worth a draw call.
    /// </summary>
    public class FieldCoverView : MonoBehaviour
    {
        /// <summary>Metres. Beyond this a patch is dropped rather than drawn.</summary>
        public const float ViewRadius = 96f;

        /// <summary>Seconds between scans. Crops grow over in-game hours; this is plenty.</summary>
        const float ScanInterval = 0.35f;

        /// <summary>Patches rebuilt per frame, so sowing a whole field never spikes.</summary>
        const int RebuildBudget = 2;

        class Patch
        {
            public GameObject Root;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public int Signature;
            public bool Seen;
        }

        FieldWorld _fields;
        TerrainWorld _terrain;
        WorldClock _clock;
        Transform _viewer;
        Transform _root;

        readonly Dictionary<long, Patch> _patches = new Dictionary<long, Patch>();
        readonly Dictionary<long, List<CoverPlant>> _gathered = new Dictionary<long, List<CoverPlant>>();
        readonly Stack<List<CoverPlant>> _listPool = new Stack<List<CoverPlant>>();
        readonly List<long> _dirty = new List<long>();
        readonly List<long> _dead = new List<long>();

        /// <summary>Surface height per column. Walking the chunks per cell per scan would not do.</summary>
        readonly Dictionary<long, int> _surface = new Dictionary<long, int>();

        readonly Dictionary<int, Material> _materials = new Dictionary<int, Material>();
        readonly CoverMeshData _mesh = new CoverMeshData();

        float _sinceScan;

        public int DrawnPatches { get { return _patches.Count; } }

        public void Init(FieldWorld fields, TerrainWorld terrain, WorldClock clock, Transform viewer)
        {
            _fields = fields;
            _terrain = terrain;
            _clock = clock;
            _viewer = viewer;

            var rootGo = new GameObject("FieldCover");
            rootGo.transform.SetParent(transform, false);
            _root = rootGo.transform;

            if (_terrain != null) _terrain.BlockChanged += OnBlockChanged;
        }

        void OnDestroy()
        {
            if (_terrain != null) _terrain.BlockChanged -= OnBlockChanged;

            foreach (var patch in _patches.Values)
            {
                if (patch.Mesh != null) Destroy(patch.Mesh);
            }
            _patches.Clear();
        }

        /// <summary>
        /// Digging under a field moves the ground the crop stands on. The cached height
        /// for that column goes, and the patch will pick the new one up on its next scan.
        /// </summary>
        void OnBlockChanged(Vector3Int cell, ushort oldId, ushort newId)
        {
            _surface.Remove(FieldGrid.Key(cell.x, cell.z));
        }

        // -------------------------------------------------------------------- scan

        void Update()
        {
            if (_fields == null || _fields.Grid == null || _viewer == null) return;

            _sinceScan += Time.deltaTime;
            if (_sinceScan < ScanInterval) return;
            _sinceScan = 0f;

            Gather();
            Reconcile();
        }

        /// <summary>
        /// Walks every sown cell once and buckets it into its patch. This is the only
        /// pass over the grid, and everything the mesher needs is decided here.
        /// </summary>
        void Gather()
        {
            Recycle();

            Vector3 eye = _viewer.position;
            float radiusSqr = ViewRadius * ViewRadius;
            double now = _clock != null ? _clock.TotalHours : 0.0;

            foreach (var entry in _fields.Grid.Cells)
            {
                var data = entry.Value;
                if (!data.HasCrop) continue;

                var crop = _fields.CropAt(data.CropIndex);
                if (crop == null) continue;

                int x, z;
                FieldGrid.Decode(entry.Key, out x, out z);

                float dx = x + 0.5f - eye.x;
                float dz = z + 0.5f - eye.z;
                if (dx * dx + dz * dz > radiusSqr) continue;

                // Derived, so a field ripens on the clock rather than when someone
                // happens to touch it. The stored state can lag; the hours cannot.
                float progress = Mathf.Clamp01((float)((now - data.ChangedAtHours) / Mathf.Max(0.1f, crop.HoursToMature)));
                int bucket = FieldCoverMesh.BucketOf(progress);

                var patch = FieldCoverMesh.PatchOf(x, z);
                long key = FieldCoverMesh.PatchKey(patch.x, patch.y);

                List<CoverPlant> list;
                if (!_gathered.TryGetValue(key, out list))
                {
                    list = _listPool.Count > 0 ? _listPool.Pop() : new List<CoverPlant>(64);
                    list.Clear();
                    _gathered.Add(key, list);
                }

                list.Add(new CoverPlant
                {
                    X = x,
                    Z = z,
                    SurfaceY = SurfaceAt(x, z),
                    CropIndex = data.CropIndex,
                    Ready = progress >= 1f,
                    Height = FieldCoverMesh.HeightFor(crop.matureHeight, bucket)
                });
            }
        }

        int SurfaceAt(int x, int z)
        {
            long key = FieldGrid.Key(x, z);

            int y;
            if (_surface.TryGetValue(key, out y)) return y;

            y = _terrain != null ? _terrain.GetSurfaceY(x, z) : 0;
            _surface[key] = y;
            return y;
        }

        void Recycle()
        {
            foreach (var list in _gathered.Values) _listPool.Push(list);
            _gathered.Clear();
        }

        // ------------------------------------------------------------- reconciling

        /// <summary>
        /// Compares what was gathered against what is drawn: drops patches that emptied
        /// or fell out of range, and queues the ones whose signature moved.
        /// </summary>
        void Reconcile()
        {
            foreach (var patch in _patches.Values) patch.Seen = false;

            _dirty.Clear();
            foreach (var entry in _gathered)
            {
                int signature = FieldCoverMesh.Signature(entry.Value);

                Patch patch;
                if (_patches.TryGetValue(entry.Key, out patch))
                {
                    patch.Seen = true;
                    if (patch.Signature == signature) continue;
                }

                _dirty.Add(entry.Key);
            }

            _dead.Clear();
            foreach (var entry in _patches)
            {
                if (!entry.Value.Seen) _dead.Add(entry.Key);
            }
            for (int i = 0; i < _dead.Count; i++) Drop(_dead[i]);

            int built = 0;
            for (int i = 0; i < _dirty.Count && built < RebuildBudget; i++)
            {
                Rebuild(_dirty[i]);
                built++;
            }
        }

        void Drop(long key)
        {
            Patch patch;
            if (!_patches.TryGetValue(key, out patch)) return;

            if (patch.Mesh != null) Destroy(patch.Mesh);
            if (patch.Root != null) Destroy(patch.Root);
            _patches.Remove(key);
        }

        void Rebuild(long key)
        {
            List<CoverPlant> plants;
            if (!_gathered.TryGetValue(key, out plants) || plants.Count == 0)
            {
                Drop(key);
                return;
            }

            FieldCoverMesh.Build(plants, _mesh);
            if (_mesh.IsEmpty)
            {
                Drop(key);
                return;
            }

            Patch patch;
            if (!_patches.TryGetValue(key, out patch))
            {
                patch = new Patch();
                patch.Root = new GameObject("CoverPatch");
                patch.Root.transform.SetParent(_root, false);
                patch.Root.AddComponent<MeshFilter>();
                patch.Renderer = patch.Root.AddComponent<MeshRenderer>();
                patch.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                patch.Renderer.receiveShadows = false;
                _patches.Add(key, patch);
            }

            if (patch.Mesh == null)
            {
                patch.Mesh = new Mesh();
                patch.Mesh.name = "FieldCover";
                patch.Mesh.MarkDynamic();
                patch.Root.GetComponent<MeshFilter>().sharedMesh = patch.Mesh;
            }

            Upload(patch);
            patch.Signature = FieldCoverMesh.Signature(plants);
            patch.Seen = true;
        }

        void Upload(Patch patch)
        {
            var mesh = patch.Mesh;
            mesh.Clear();

            // A big field patch can pass 65k vertices at two crossed quads a plant.
            mesh.indexFormat = _mesh.Vertices.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(_mesh.Vertices);
            mesh.SetNormals(_mesh.Normals);
            mesh.SetUVs(0, _mesh.Uvs);
            mesh.subMeshCount = _mesh.KeyOrder.Count;

            var materials = new Material[_mesh.KeyOrder.Count];
            for (int i = 0; i < _mesh.KeyOrder.Count; i++)
            {
                int key = _mesh.KeyOrder[i];
                mesh.SetTriangles(_mesh.Triangles[key], i, false);
                materials[i] = MaterialFor(key);
            }

            mesh.RecalculateBounds();
            patch.Renderer.sharedMaterials = materials;
        }

        /// <summary>
        /// One material per crop-and-ripeness, cached. Ripe goes warm and bright for the
        /// same reason a ready garden plot does - it has to read from across the field.
        /// </summary>
        Material MaterialFor(int key)
        {
            Material material;
            if (_materials.TryGetValue(key, out material) && material != null) return material;

            var crop = _fields.CropAt(FieldCoverMesh.CropOfKey(key));
            Color tint = crop != null ? crop.plantTint : new Color(0.36f, 0.52f, 0.22f);

            if (FieldCoverMesh.ReadyOfKey(key))
            {
                tint = Color.Lerp(tint, new Color(0.85f, 0.72f, 0.24f), 0.6f);
            }

            material = MaterialLibrary.Get(SurfaceFamily.Foliage, tint, 0.05f);
            _materials[key] = material;
            return material;
        }
    }
}
