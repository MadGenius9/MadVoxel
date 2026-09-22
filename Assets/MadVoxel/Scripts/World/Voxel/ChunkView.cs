using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.World.Voxel
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
        }

        public void ClearMesh()
        {
            _mesh.Clear();
            _renderer.enabled = false;
            _collider.sharedMesh = null;
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
