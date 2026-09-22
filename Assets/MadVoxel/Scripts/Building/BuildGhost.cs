using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.Building
{
    /// <summary>
    /// The two bits of build feedback: a highlight on the block you are looking at, and
    /// a translucent preview of what you are about to place.
    /// </summary>
    public class BuildGhost
    {
        static readonly Color ValidColour = new Color(0.45f, 0.95f, 0.5f, 0.32f);
        static readonly Color InvalidColour = new Color(0.95f, 0.30f, 0.25f, 0.32f);
        static readonly Color HighlightColour = new Color(1f, 1f, 1f, 0.18f);

        GameObject _ghost;
        GameObject _highlight;
        Material _ghostMaterial;
        Material _highlightMaterial;
        MeshRenderer _ghostRenderer;

        public void EnsureBuilt(Transform parent)
        {
            if (_ghost != null) return;

            _ghostMaterial = MakeTransparentMaterial(ValidColour);
            _highlightMaterial = MakeTransparentMaterial(HighlightColour);

            _ghost = MakeBox(parent, "BuildGhost", _ghostMaterial);
            _ghostRenderer = _ghost.GetComponent<MeshRenderer>();
            _highlight = MakeBox(parent, "TargetHighlight", _highlightMaterial);

            Hide();
        }

        static GameObject MakeBox(Transform parent, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = PrimitiveBuilder.CubeMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        static Material MakeTransparentMaterial(Color colour)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = MaterialLibrary.LitShader;

            var mat = new Material(shader);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            SetColour(mat, colour);
            return mat;
        }

        static void SetColour(Material mat, Color colour)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", colour);
        }

        public void ShowHighlight(Vector3Int cell, float miningProgress01)
        {
            if (_highlight == null) return;
            _highlight.SetActive(true);
            _highlight.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f);
            _highlight.transform.localScale = Vector3.one * 1.004f;

            var c = HighlightColour;
            c.a = Mathf.Lerp(0.14f, 0.55f, miningProgress01);
            SetColour(_highlightMaterial, c);
        }

        public void HideHighlight()
        {
            if (_highlight != null) _highlight.SetActive(false);
        }

        public void ShowPlacement(Vector3Int cell, Vector3Int footprint, int rotationSteps, bool valid)
        {
            if (_ghost == null) return;
            var size = PlacedStructure.RotatedFootprint(footprint, rotationSteps);
            size = new Vector3Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y), Mathf.Max(1, size.z));

            _ghost.SetActive(true);
            _ghost.transform.position = new Vector3(cell.x + size.x * 0.5f, cell.y + size.y * 0.5f, cell.z + size.z * 0.5f);
            _ghost.transform.localScale = new Vector3(size.x * 0.96f, size.y * 0.96f, size.z * 0.96f);
            SetColour(_ghostMaterial, valid ? ValidColour : InvalidColour);
        }

        public void HidePlacement()
        {
            if (_ghost != null) _ghost.SetActive(false);
        }

        public void Hide()
        {
            HideHighlight();
            HidePlacement();
        }

        /// <summary>The ghost lives at the scene root, so it has to be cleaned up explicitly.</summary>
        public void Dispose()
        {
            if (_ghost != null) Object.Destroy(_ghost);
            if (_highlight != null) Object.Destroy(_highlight);
            _ghost = null;
            _highlight = null;
        }
    }
}
