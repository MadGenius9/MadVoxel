using System;
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
            // Each step is independent and every one of them pokes at a serialized
            // project setting whose name Unity is free to change between versions. A
            // throw in any single step used to abort the whole menu item, which left
            // the project half-configured and the scene missing from Build Settings -
            // with an exception that pointed at the step, not at what to do about it.
            // Now a failure costs you that one step and names the manual fallback.
            int failed = 0;

            failed += Step("product strings", EnsureProductStrings,
                "Set Company Name to 'MadGenius' and Product Name to 'MadVoxel' in Project Settings > Player.");

            failed += Step("input handling", EnsureInputHandling,
                "Set Active Input Handling to 'Both' in Project Settings > Player.");

            failed += Step("always-included shaders", EnsureAlwaysIncludedShaders,
                "Only affects builds, not the editor. Add the URP Lit/Unlit shaders by hand in "
                + "Project Settings > Graphics if you make a build.");

            failed += Step("build settings", SceneBuilder.AddSceneToBuildSettings,
                "Add Assets/MadVoxel/Scenes/MadVoxel.unity to File > Build Settings by hand.");

            failed += Step("render pipeline report", ReportRenderPipeline,
                "Cosmetic - this step only prints what the pipeline is.");

            Step("saving assets", AssetDatabase.SaveAssets, "Save the project with Ctrl+S.");

            if (failed == 0)
            {
                Debug.Log("MadVoxel: project configured. Open Assets/MadVoxel/Scenes/MadVoxel.unity and press Play.");
                return;
            }

            Debug.LogWarningFormat(
                "MadVoxel: {0} setup step(s) did not complete - see the warnings above for what to do by hand. "
                + "The game itself does not depend on any of them to run in the editor, so you can still open "
                + "Assets/MadVoxel/Scenes/MadVoxel.unity and press Play.", failed);
        }

        /// <summary>Runs one setup step. Returns 1 when it failed, 0 when it did not.</summary>
        static int Step(string what, Action action, string fallback)
        {
            try
            {
                action();
                return 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarningFormat("MadVoxel: could not configure {0} ({1}: {2}). {3}",
                    what, ex.GetType().Name, ex.Message, fallback);
                return 1;
            }
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

            // Qualified: 'using System' puts System.Object in scope too.
            var existing = new HashSet<UnityEngine.Object>();
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
