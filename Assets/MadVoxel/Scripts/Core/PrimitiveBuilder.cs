using UnityEngine;

namespace MadVoxel.Core
{
    /// <summary>
    /// Builds props out of boxes at runtime. Placeholder art with clear silhouettes;
    /// swapping in real meshes later means replacing these calls, nothing else.
    /// </summary>
    public static class PrimitiveBuilder
    {
        static Mesh _cubeMesh;
        static Mesh _cylinderMesh;

        public static Mesh CubeMesh
        {
            get
            {
                if (_cubeMesh == null)
                {
                    var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                    Object.Destroy(temp);
                }
                return _cubeMesh;
            }
        }

        public static Mesh CylinderMesh
        {
            get
            {
                if (_cylinderMesh == null)
                {
                    var temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    _cylinderMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                    Object.Destroy(temp);
                }
                return _cylinderMesh;
            }
        }

        public static GameObject Box(Transform parent, Vector3 localCentre, Vector3 size, Material material, string name = "Part")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.transform.localScale = size;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = CubeMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return go;
        }

        public static GameObject Cylinder(Transform parent, Vector3 localCentre, Vector3 size, Material material, string name = "Part")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.transform.localScale = size;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = CylinderMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return go;
        }

        /// <summary>Strips shadows and colliders from a whole hierarchy - used for build ghosts.</summary>
        public static void MakeGhost(GameObject root, Material ghostMaterial)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial = ghostMaterial;
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[i].receiveShadows = false;
            }
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) Object.Destroy(colliders[i]);
        }
    }
}
