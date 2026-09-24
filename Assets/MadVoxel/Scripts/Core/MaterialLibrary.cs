using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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

        /// <summary>
        /// Whether a scriptable render pipeline is actually driving rendering.
        ///
        /// This is the question that matters, and asking the wrong one turned the whole
        /// game magenta. The old code tried the URP shader first and fell back to
        /// Standard "if it was not found" - but with the URP package installed the
        /// shader is always found, whether or not a pipeline asset is assigned. A found
        /// URP shader under the built-in pipeline does not fall back. It renders as the
        /// error colour, on every surface, with nothing in the console.
        ///
        /// So the test is the pipeline, not the shader.
        /// </summary>
        public static bool UsingScriptablePipeline
        {
            get
            {
                return GraphicsSettings.currentRenderPipeline != null
                    || GraphicsSettings.defaultRenderPipeline != null;
            }
        }

        public static Shader LitShader
        {
            get
            {
                if (_litShader == null) _litShader = FindLit();
                return _litShader;
            }
        }

        /// <summary>
        /// The project's own lit shader, or null when it is not there.
        ///
        /// It is the only thing that reads the ambient occlusion the mesher bakes, and
        /// it is deliberately optional: asked for by name, and simply absent if the
        /// file was stripped from a build or failed to compile. Terrain without AO is
        /// a disappointment; terrain that does not render is the magenta world this
        /// project has already shipped once.
        /// </summary>
        public const string VoxelLitName = "MadVoxel/VoxelLit";

        static Shader FindLit()
        {
            if (UsingScriptablePipeline)
            {
                var voxel = Shader.Find(VoxelLitName);
                if (voxel != null) return voxel;

                var urp = Shader.Find("Universal Render Pipeline/Lit");
                if (urp != null) return urp;
            }

            // Built-in, or a pipeline whose Lit shader is not URP's. Standard renders
            // everywhere the built-in pipeline does, which is the honest fallback.
            var standard = Shader.Find("Standard");
            if (standard != null) return standard;

            return Shader.Find("Diffuse");
        }

        public static Shader UnlitShader
        {
            get
            {
                if (_unlitShader == null) _unlitShader = FindUnlit();
                return _unlitShader;
            }
        }

        static Shader FindUnlit()
        {
            if (UsingScriptablePipeline)
            {
                var urp = Shader.Find("Universal Render Pipeline/Unlit");
                if (urp != null) return urp;
            }

            return Shader.Find("Unlit/Color");
        }

        /// <summary>
        /// Drops the cached shaders and materials.
        ///
        /// Assigning a pipeline asset while the editor is open changes which shaders
        /// are correct, and a cache from before that point is a screen full of magenta
        /// that survives pressing Play again.
        /// </summary>
        public static void Forget()
        {
            _litShader = null;
            _unlitShader = null;
            Cache.Clear();
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
