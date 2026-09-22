using System.Collections.Generic;
using System.IO;
using MadVoxel.Content;
using UnityEditor;
using UnityEngine;

namespace MadVoxel.EditorTools
{
    /// <summary>
    /// Writes the code-defined content out as real ScriptableObject assets so designers
    /// can tune blocks, items, recipes, skills and quests without touching C#.
    /// The game runs fine without these assets; once they exist they take priority.
    /// </summary>
    public static class ContentAssetGenerator
    {
        const string ContentRoot = "Assets/MadVoxel/Content";
        const string ResourcesRoot = "Assets/MadVoxel/Resources/MadVoxel";

        [MenuItem("MadVoxel/Content/Generate ScriptableObject Assets", priority = 20)]
        public static void Generate()
        {
            bool overwrite = !AssetDatabase.IsValidFolder(ContentRoot) ||
                             EditorUtility.DisplayDialog(
                                 "Regenerate MadVoxel content?",
                                 "This deletes and rewrites everything under " + ContentRoot +
                                 " and the ContentDatabase in Resources.\n\nHand edits to those assets will be lost.",
                                 "Regenerate", "Cancel");
            if (!overwrite) return;

            if (AssetDatabase.IsValidFolder(ContentRoot)) AssetDatabase.DeleteAsset(ContentRoot);
            if (AssetDatabase.IsValidFolder(ResourcesRoot)) AssetDatabase.DeleteAsset(ResourcesRoot);

            EnsureFolder(ContentRoot);
            EnsureFolder(ResourcesRoot);

            var db = ContentLibrary.BuildRuntime();

            SaveAsset(db.config, ContentRoot + "/GameConfig.asset");

            SaveAll(db.blocks.blocks, ContentRoot + "/Blocks");
            SaveAsset(db.blocks, ContentRoot + "/BlockRegistry.asset");

            SaveAll(db.items, ContentRoot + "/Items");
            SaveAll(db.structures, ContentRoot + "/Structures");
            SaveAll(db.recipes, ContentRoot + "/Recipes");
            SaveAll(db.zombies, ContentRoot + "/Zombies");
            SaveAsset(db.hordeSchedule, ContentRoot + "/HordeSchedule.asset");

            SaveAll(db.perkTree.perks, ContentRoot + "/Perks");
            SaveAsset(db.perkTree, ContentRoot + "/PerkTree.asset");

            SaveAll(db.quests, ContentRoot + "/Quests");
            SaveAll(db.traders, ContentRoot + "/Traders");
            SaveAll(db.vehicles, ContentRoot + "/Vehicles");

            SaveAsset(db, ResourcesRoot + "/ContentDatabase.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.LogFormat("MadVoxel: generated {0} blocks, {1} items, {2} recipes, {3} structures, {4} skills, {5} quests.",
                db.blocks.blocks.Count, db.items.Count, db.recipes.Count, db.structures.Count,
                db.perkTree.perks.Count, db.quests.Count);

            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
        }

        [MenuItem("MadVoxel/Content/Delete Generated Assets", priority = 21)]
        public static void DeleteGenerated()
        {
            if (!EditorUtility.DisplayDialog("Delete generated content?",
                "Removes " + ContentRoot + " and the generated ContentDatabase. The game falls back to the code-defined content.",
                "Delete", "Cancel")) return;

            AssetDatabase.DeleteAsset(ContentRoot);
            AssetDatabase.DeleteAsset(ResourcesRoot);
            AssetDatabase.Refresh();
        }

        static void SaveAll<T>(List<T> assets, string folder) where T : ScriptableObject
        {
            if (assets == null || assets.Count == 0) return;
            EnsureFolder(folder);

            var used = new HashSet<string>();
            for (int i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                if (asset == null) continue;

                string baseName = string.IsNullOrEmpty(asset.name) ? typeof(T).Name + i : asset.name;
                string fileName = baseName;
                int suffix = 2;
                while (!used.Add(fileName)) fileName = baseName + "_" + suffix++;

                SaveAsset(asset, folder + "/" + fileName + ".asset");
            }
        }

        static void SaveAsset(Object asset, string path)
        {
            if (asset == null) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateAsset(asset, path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
