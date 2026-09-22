using System.Collections.Generic;
using System.IO;
using System.Text;
using MadVoxel.Content;
using MadVoxel.Modding;
using UnityEditor;
using UnityEngine;

namespace MadVoxel.EditorTools
{
    /// <summary>
    /// Authoring helpers for mods. The important one is the id dump: the first thing a
    /// mod author needs is the exact string id of the thing they want to patch or point
    /// at, and guessing it is the main way mods fail silently.
    /// </summary>
    public static class ModTools
    {
        [MenuItem("MadVoxel/Mods/Open Mods Folder", priority = 40)]
        public static void OpenModsFolder()
        {
            ModLoader.EnsureUserModsFolder();
            EditorUtility.RevealInFinder(ModLoader.UserModsPath);
            Debug.Log("MadVoxel: mods folder is " + ModLoader.UserModsPath);
        }

        [MenuItem("MadVoxel/Mods/Write Content Id Reference", priority = 41)]
        public static void WriteIdReference()
        {
            var db = ContentDatabase.LoadOrBuild();
            db.Build();

            var sb = new StringBuilder();
            sb.AppendLine("MadVoxel content ids");
            sb.AppendLine("====================");
            sb.AppendLine();
            sb.AppendLine("Every id below can be patched by a mod, or referenced from one.");
            sb.AppendLine("Use them with \"$op\": \"patch\" to change a value, or \"remove\" to delete.");
            sb.AppendLine();

            Section(sb, "blocks", db.blocks.blocks, b => b.stringId, b => b.displayName);
            Section(sb, "items", db.items, i => i.stringId, i => i.displayName);
            Section(sb, "recipes", db.recipes, r => r.stringId, r => r.DisplayName);
            Section(sb, "structures", db.structures, s => s.stringId, s => s.displayName);
            Section(sb, "buildPieces", db.buildPieces, p => p.stringId, p => p.displayName);
            Section(sb, "crops", db.crops, c => c.stringId, c => c.displayName);
            Section(sb, "zombies", db.zombies, z => z.stringId, z => z.displayName);
            if (db.perkTree != null) Section(sb, "perks", db.perkTree.perks, p => p.stringId, p => p.displayName);
            Section(sb, "quests", db.quests, q => q.stringId, q => q.title);
            Section(sb, "traders", db.traders, t => t.stringId, t => t.displayName);
            Section(sb, "vehicles", db.vehicles, v => v.stringId, v => v.displayName);

            var path = Path.Combine(Application.dataPath, "../ContentIds.txt");
            path = Path.GetFullPath(path);

            try
            {
                File.WriteAllText(path, sb.ToString());
                Debug.Log("MadVoxel: wrote the content id reference to " + path);
                EditorUtility.RevealInFinder(path);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("MadVoxel: could not write the id reference: " + ex.Message);
            }
        }

        static void Section<T>(StringBuilder sb, string kind, List<T> items,
                               System.Func<T, string> id, System.Func<T, string> label) where T : Object
        {
            sb.AppendLine("## " + kind + "  (" + items.Count + ")");
            sb.AppendLine();

            var rows = new List<string>();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;
                rows.Add(string.Format("  {0,-46} {1}", id(items[i]), label(items[i])));
            }
            rows.Sort(System.StringComparer.Ordinal);

            for (int i = 0; i < rows.Count; i++) sb.AppendLine(rows[i]);
            sb.AppendLine();
        }

        [MenuItem("MadVoxel/Mods/Validate Installed Mods", priority = 42)]
        public static void ValidateMods()
        {
            var db = ContentDatabase.LoadOrBuild();
            db.Build();

            var log = new ModLog();
            var loaded = ModLoader.LoadAll(db, log);
            log.Flush();

            if (log.ErrorCount == 0 && log.WarningCount == 0)
            {
                Debug.LogFormat("MadVoxel: {0} mod(s) loaded cleanly.", loaded.Count);
                return;
            }

            Debug.LogWarningFormat("MadVoxel: {0} mod(s) loaded with {1} error(s) and {2} warning(s).",
                loaded.Count, log.ErrorCount, log.WarningCount);
        }
    }
}
