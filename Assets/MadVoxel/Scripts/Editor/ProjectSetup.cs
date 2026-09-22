using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MadVoxel.EditorTools
{
    /// <summary>
    /// One-shot project configuration: legacy input handling, the shaders the runtime
    /// looks up by name, and the build scene list. Run it once after opening the
    /// project for the first time.
    /// </summary>
    public static class ProjectSetup
    {
        static readonly string[] RuntimeShaders =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Standard",
            "Unlit/Color"
        };

        [MenuItem("MadVoxel/Setup/Configure Project", priority = 1)]
        public static void Configure()
        {
            EnsureProductStrings();
            EnsureInputHandling();
            EnsureAlwaysIncludedShaders();
            SceneBuilder.AddSceneToBuildSettings();
            ReportRenderPipeline();

            AssetDatabase.SaveAssets();
            Debug.Log("MadVoxel: project configured. Open Assets/MadVoxel/Scenes/MadVoxel.unity and press Play.");
        }

        /// <summary>
        /// The save path is built from these, so they have to match the product string
        /// before anyone makes a world they want to keep.
        /// </summary>
        static void EnsureProductStrings()
        {
            if (PlayerSettings.companyName != "MadGenius") PlayerSettings.companyName = "MadGenius";
            if (PlayerSettings.productName != "MadVoxel") PlayerSettings.productName = "MadVoxel";
            Debug.LogFormat("MadVoxel: product set to {0} / {1}.", PlayerSettings.companyName, PlayerSettings.productName);
        }

        /// <summary>
        /// The game reads UnityEngine.Input, which needs the old input handling to be
        /// active. "Both" keeps the new Input System available for later work.
        /// </summary>
        static void EnsureInputHandling()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("MadVoxel: could not open ProjectSettings.asset. Set Active Input Handling to 'Both' by hand.");
                return;
            }

            var serialized = new SerializedObject(assets[0]);
            var property = serialized.FindProperty("activeInputHandler");
            if (property == null)
            {
                Debug.LogWarning("MadVoxel: activeInputHandler not found. Set Active Input Handling to 'Both' by hand.");
                return;
            }

            // 0 = Input Manager (Old), 1 = Input System Package (New), 2 = Both.
            if (property.intValue == 1)
            {
                property.intValue = 2;
                serialized.ApplyModifiedProperties();
                Debug.LogWarning("MadVoxel: Active Input Handling set to 'Both'. Unity will ask to restart - say yes.");
            }
        }

        /// <summary>Runtime materials use Shader.Find, so builds need these kept.</summary>
        static void EnsureAlwaysIncludedShaders()
        {
            var graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettings == null || graphicsSettings.Length == 0) return;

            var serialized = new SerializedObject(graphicsSettings[0]);
            var array = serialized.FindProperty("m_AlwaysIncludedShaders");
            if (array == null) return;

            var existing = new HashSet<Object>();
            for (int i = 0; i < array.arraySize; i++)
            {
                existing.Add(array.GetArrayElementAtIndex(i).objectReferenceValue);
            }

            for (int i = 0; i < RuntimeShaders.Length; i++)
            {
                var shader = Shader.Find(RuntimeShaders[i]);
                if (shader == null || existing.Contains(shader)) continue;

                array.InsertArrayElementAtIndex(array.arraySize);
                array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = shader;
                existing.Add(shader);
            }

            serialized.ApplyModifiedProperties();
        }

        static void ReportRenderPipeline()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            if (pipeline != null)
            {
                Debug.LogFormat("MadVoxel: render pipeline is '{0}'.", pipeline.name);
                return;
            }

            Debug.LogWarning(
                "MadVoxel: no Scriptable Render Pipeline asset is assigned, so the project is running on the Built-in pipeline. " +
                "The game still works (materials fall back to the Standard shader), but for the intended URP look create " +
                "'URP Asset (with Universal Renderer)' via Assets > Create > Rendering, then assign it in " +
                "Project Settings > Graphics > Default Render Pipeline and in Project Settings > Quality.");
        }
    }
}
