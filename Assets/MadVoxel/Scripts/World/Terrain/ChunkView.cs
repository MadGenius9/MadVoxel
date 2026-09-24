using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.World.Terrain
{
    /// <summary>The GameObject side of a chunk: mesh, renderer and collider.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class ChunkView : MonoBehaviour
    {
        public ChunkCoord Coord { get; private set; }

        MeshFilter _filter;
        MeshRenderer _renderer;
        MeshCollider _collider;
        Mesh _mesh;

        // Canopies, on their own object with no collider of any kind. They were never
        // solid and must not become solid because they are now drawn.
        MeshFilter _decorFilter;
        MeshRenderer _decorRenderer;
        Mesh _decorMesh;

        static readonly List<Material> MaterialScratch = new List<Material>(8);

        void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<MeshCollider>();

            _mesh = new Mesh();
            _mesh.name = "ChunkMesh";
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _mesh.MarkDynamic();
            _filter.sharedMesh = _mesh;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var decor = new GameObject("Canopy");
            decor.transform.SetParent(transform, false);

            _decorFilter = decor.AddComponent<MeshFilter>();
            _decorRenderer = decor.AddComponent<MeshRenderer>();

            _decorMesh = new Mesh { name = "ChunkCanopy" };
            _decorMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _decorMesh.MarkDynamic();
            _decorFilter.sharedMesh = _decorMesh;
        }

        public void Bind(ChunkCoord coord)
        {
            Coord = coord;
            var origin = coord.Origin;
            transform.position = new Vector3(origin.x, origin.y, origin.z);
            name = "Chunk " + coord;
        }

        public void Apply(ChunkMeshData data, BlockMaterialCache materials)
        {
            _mesh.Clear();

            if (data == null || data.IsEmpty)
            {
                _renderer.enabled = false;
                _collider.sharedMesh = null;
                return;
            }

            _mesh.SetVertices(data.Vertices);
            _mesh.SetNormals(data.Normals);
            _mesh.SetUVs(0, data.Uvs);

            // Baked ambient occlusion. Harmless if the shader in use ignores vertex
            // colours - which stock URP Lit does - so uploading it is never a risk.
            _mesh.SetColors(data.Colors);

            int subMeshCount = data.BlockOrder.Count;
            _mesh.subMeshCount = subMeshCount;

            MaterialScratch.Clear();
            for (int i = 0; i < subMeshCount; i++)
            {
                ushort blockId = data.BlockOrder[i];
                _mesh.SetTriangles(data.Triangles[blockId], i, false);
                MaterialScratch.Add(materials.Get(blockId));
            }

            _mesh.RecalculateBounds();
            _renderer.enabled = true;
            _renderer.sharedMaterials = MaterialScratch.ToArray();

            // Re-assigning the same mesh is how a MeshCollider is told to rebake.
            _collider.sharedMesh = null;
            _collider.sharedMesh = _mesh;

            ApplyDecoration(data.Decoration, materials);
        }

        void ApplyDecoration(ChunkMeshData decoration, BlockMaterialCache materials)
        {
            _decorMesh.Clear();

            if (decoration == null || decoration.IsEmpty)
            {
                _decorRenderer.enabled = false;
                return;
            }

            _decorMesh.SetVertices(decoration.Vertices);
            _decorMesh.SetNormals(decoration.Normals);
            _decorMesh.SetUVs(0, decoration.Uvs);
            _decorMesh.SetColors(decoration.Colors);

            int count = decoration.BlockOrder.Count;
            _decorMesh.subMeshCount = count;

            MaterialScratch.Clear();
            for (int i = 0; i < count; i++)
            {
                ushort blockId = decoration.BlockOrder[i];
                _decorMesh.SetTriangles(decoration.Triangles[blockId], i, false);
                MaterialScratch.Add(materials.Get(blockId));
            }

            _decorMesh.RecalculateBounds();
            _decorRenderer.sharedMaterials = MaterialScratch.ToArray();
            _decorRenderer.enabled = true;
        }

        public void ClearMesh()
        {
            _mesh.Clear();
            _renderer.enabled = false;
            _collider.sharedMesh = null;

            if (_decorMesh != null) _decorMesh.Clear();
            if (_decorRenderer != null) _decorRenderer.enabled = false;
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
