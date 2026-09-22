using System.Collections.Generic;
using System.IO;
using MadVoxel.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MadVoxel.EditorTools
{
    /// <summary>
    /// Rebuilds the single gameplay scene. The scene holds one object: everything else
    /// is created at runtime by <see cref="GameBootstrap"/>.
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/MadVoxel/Scenes/MadVoxel.unity";

        [MenuItem("MadVoxel/Setup/Rebuild Scene", priority = 2)]
        public static void Rebuild()
        {
            if (!EditorUtility.DisplayDialog("Rebuild MadVoxel scene?",
                "Overwrites " + ScenePath + " with a fresh bootstrap scene.", "Rebuild", "Cancel")) return;

            var directory = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("MadVoxel");
            go.AddComponent<GameBootstrap>();

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.36f, 0.37f);
            RenderSettings.fog = true;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();

            Debug.Log("MadVoxel: scene rebuilt at " + ScenePath);
        }

        public static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == ScenePath) { scenes[i].enabled = true; EditorBuildSettings.scenes = scenes.ToArray(); return; }
            }
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [MenuItem("MadVoxel/Setup/Open Scene", priority = 3)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogWarning("MadVoxel: scene missing. Run MadVoxel > Setup > Rebuild Scene.");
                return;
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
