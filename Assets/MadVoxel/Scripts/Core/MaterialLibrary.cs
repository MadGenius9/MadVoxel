using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Core
{
    /// <summary>
    /// Runtime material cache. Every material is the same lit shader with a procedural
    /// grime texture and a tint, which keeps the project free of authored .mat assets
    /// until real art lands.
    /// </summary>
    public static class MaterialLibrary
    {
        struct Key
        {
            public SurfaceFamily Family;
            public int Colour;
            public int Smoothness;
            public int Metallic;
            public bool Transparent;
        }

        static readonly Dictionary<Key, Material> Cache = new Dictionary<Key, Material>();
        static Shader _litShader;
        static Shader _unlitShader;

        public static Shader LitShader
        {
            get
            {
                if (_litShader == null)
                {
                    _litShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (_litShader == null) _litShader = Shader.Find("Standard");
                    if (_litShader == null) _litShader = Shader.Find("Diffuse");
                }
                return _litShader;
            }
        }

        public static Shader UnlitShader
        {
            get
            {
                if (_unlitShader == null)
                {
                    _unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (_unlitShader == null) _unlitShader = Shader.Find("Unlit/Color");
                }
                return _unlitShader;
            }
        }

        public static Material Get(SurfaceFamily family, Color tint, float smoothness = 0.12f, float metallic = 0f, bool transparent = false)
        {
            var key = new Key
            {
                Family = family,
                Colour = ((Color32)tint).r << 24 | ((Color32)tint).g << 16 | ((Color32)tint).b << 8 | ((Color32)tint).a,
                Smoothness = Mathf.RoundToInt(smoothness * 255f),
                Metallic = Mathf.RoundToInt(metallic * 255f),
                Transparent = transparent
            };

            Material mat;
            if (Cache.TryGetValue(key, out mat) && mat != null) return mat;

            mat = new Material(LitShader);
            mat.name = string.Format("MV_{0}_{1}", family, ColorUtility.ToHtmlStringRGB(tint));
            var tex = SurfaceTextureFactory.Get(family);

            SetTexture(mat, tex);
            SetColour(mat, tint);
            SetFloatIfPresent(mat, "_Smoothness", smoothness);
            SetFloatIfPresent(mat, "_Glossiness", smoothness);
            SetFloatIfPresent(mat, "_Metallic", metallic);

            if (transparent) MakeTransparent(mat);

            mat.enableInstancing = true;
            Cache[key] = mat;
            return mat;
        }

        public static Material GetUnlit(Color tint)
        {
            var mat = new Material(UnlitShader);
            SetColour(mat, tint);
            return mat;
        }

        static void SetTexture(Material mat, Texture tex)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        }

        static void SetColour(Material mat, Color c)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        }

        static void SetFloatIfPresent(Material mat, string prop, float value)
        {
            if (mat.HasProperty(prop)) mat.SetFloat(prop, value);
        }

        static void SetIntIfPresent(Material mat, string prop, int value)
        {
            if (mat.HasProperty(prop)) mat.SetInt(prop, value);
        }

        static void MakeTransparent(Material mat)
        {
            SetFloatIfPresent(mat, "_Surface", 1f);
            SetFloatIfPresent(mat, "_Blend", 0f);
            SetFloatIfPresent(mat, "_ZWrite", 0f);
            SetIntIfPresent(mat, "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            SetIntIfPresent(mat, "_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
