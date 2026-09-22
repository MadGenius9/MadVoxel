using System.Collections.Generic;
using System.IO;
using MadVoxel.Content;
using UnityEngine;

namespace MadVoxel.Modding
{
    /// <summary>
    /// Finds mods on disk, works out what order to load them in, and hands each one's
    /// JSON to the content applier.
    ///
    /// A mod is a folder. Nothing is compiled and nothing is executed: a mod describes
    /// content, and the game builds it. That means dropping in a folder from the
    /// internet cannot run code on your machine, and it is why the system is data-first.
    /// </summary>
    public static class ModLoader
    {
        public const string ModsFolderName = "Mods";
        public const string ManifestFileName = "mod.json";
        public const string ContentFolderName = "content";

        /// <summary>Where the player drops mods.</summary>
        public static string UserModsPath
        {
            get { return Path.Combine(Application.persistentDataPath, ModsFolderName); }
        }

        /// <summary>Mods shipped alongside the game.</summary>
        public static string BuiltInModsPath
        {
            get { return Path.Combine(Application.streamingAssetsPath, ModsFolderName); }
        }

        public static void EnsureUserModsFolder()
        {
            try
            {
                Directory.CreateDirectory(UserModsPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarningFormat("MadVoxel: could not create the mods folder: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Loads every enabled mod onto a database that already holds the base content.
        /// Returns the mods that actually applied, in load order.
        /// </summary>
        public static List<ModManifest> LoadAll(ContentDatabase database, ModLog log)
        {
            var applied = new List<ModManifest>();
            if (database == null) return applied;

            EnsureUserModsFolder();

            var discovered = new List<ModManifest>();
            Discover(BuiltInModsPath, discovered, log);
            Discover(UserModsPath, discovered, log);

            if (discovered.Count == 0)
            {
                log.CurrentModId = null;
                log.Info("No mods found. Drop a mod folder into {0}", UserModsPath);
                return applied;
            }

            var ordered = Order(discovered, log);
            var pending = new List<PendingLink>();

            // Pass one: every mod contributes its definitions.
            for (int i = 0; i < ordered.Count; i++)
            {
                var manifest = ordered[i];
                log.CurrentModId = manifest.Id;

                int errorsBefore = log.ErrorCount;
                ModContentApplier.ApplyDefinitions(database, manifest, log, pending);

                if (log.ErrorCount > errorsBefore)
                {
                    log.Warn("{0} loaded with errors; some of its content may be missing", manifest.DisplayName);
                }
                applied.Add(manifest);
            }

            // Pass two: wire references, now that everything exists. This is what lets a
            // mod's recipe use an item from a mod that loaded after it.
            ModContentApplier.ResolveLinks(database, pending, log);

            log.CurrentModId = null;
            log.Info("Loaded {0} mod(s)", applied.Count);
            return applied;
        }

        static void Discover(string root, List<ModManifest> into, ModLog log)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;

            string[] directories;
            try
            {
                directories = Directory.GetDirectories(root);
            }
            catch (System.Exception ex)
            {
                log.Error("could not read the mods folder '{0}': {1}", root, ex.Message);
                return;
            }

            System.Array.Sort(directories, System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < directories.Length; i++)
            {
                var manifestPath = Path.Combine(directories[i], ManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    log.CurrentModId = null;
                    log.Warn("'{0}' has no {1} and was skipped", Path.GetFileName(directories[i]), ManifestFileName);
                    continue;
                }

                string text;
                try
                {
                    text = File.ReadAllText(manifestPath);
                }
                catch (System.Exception ex)
                {
                    log.Error("could not read '{0}': {1}", manifestPath, ex.Message);
                    continue;
                }

                string error;
                var root2 = Json.Parse(text, out error);
                if (root2 == null)
                {
                    log.CurrentModId = null;
                    log.Error("{0}/{1} is not valid JSON - {2}", Path.GetFileName(directories[i]), ManifestFileName, error);
                    continue;
                }

                var manifest = ModManifest.Parse(root2, directories[i], log);
                if (manifest == null) continue;

                if (!manifest.Enabled)
                {
                    log.CurrentModId = null;
                    log.Info("{0} is disabled in its mod.json and was skipped", manifest.DisplayName);
                    continue;
                }

                // A user mod with the same id replaces a built-in one, so a player can
                // override a shipped mod by dropping their own copy in.
                int existing = into.FindIndex(m => m.Id == manifest.Id);
                if (existing >= 0)
                {
                    log.CurrentModId = null;
                    log.Info("{0} in '{1}' replaces the earlier copy", manifest.Id, manifest.Directory);
                    into[existing] = manifest;
                    continue;
                }

                into.Add(manifest);
            }
        }

        /// <summary>
        /// Sorts by dependency first, then by declared load order, then by id. A mod
        /// whose dependency is missing is dropped rather than half-applied, and a
        /// dependency cycle is reported instead of hanging.
        /// </summary>
        public static List<ModManifest> Order(List<ModManifest> mods, ModLog log)
        {
            var byId = new Dictionary<string, ModManifest>();
            for (int i = 0; i < mods.Count; i++) byId[mods[i].Id] = mods[i];

            // Drop anything whose dependencies are not present, and anything depending
            // on something already dropped, until the set settles.
            var usable = new List<ModManifest>(mods);
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = usable.Count - 1; i >= 0; i--)
                {
                    var mod = usable[i];
                    for (int d = 0; d < mod.Dependencies.Count; d++)
                    {
                        var need = mod.Dependencies[d];
                        if (byId.ContainsKey(need) && usable.Exists(m => m.Id == need)) continue;

                        log.CurrentModId = mod.Id;
                        log.Error("needs '{0}', which is not installed - skipping this mod", need);
                        byId.Remove(mod.Id);
                        usable.RemoveAt(i);
                        changed = true;
                        break;
                    }
                }
            }

            usable.Sort((a, b) =>
            {
                int order = a.LoadOrder.CompareTo(b.LoadOrder);
                return order != 0 ? order : string.CompareOrdinal(a.Id, b.Id);
            });

            // Stable topological pass over the load-order sort.
            var result = new List<ModManifest>(usable.Count);
            var placed = new HashSet<string>();
            var visiting = new HashSet<string>();

            System.Action<ModManifest> visit = null;
            visit = mod =>
            {
                if (placed.Contains(mod.Id)) return;
                if (!visiting.Add(mod.Id))
                {
                    log.CurrentModId = mod.Id;
                    log.Error("is part of a dependency cycle; loading it in declared order instead");
                    return;
                }

                for (int d = 0; d < mod.Dependencies.Count; d++)
                {
                    ModManifest dependency;
                    if (byId.TryGetValue(mod.Dependencies[d], out dependency)) visit(dependency);
                }

                visiting.Remove(mod.Id);
                if (placed.Add(mod.Id)) result.Add(mod);
            };

            for (int i = 0; i < usable.Count; i++) visit(usable[i]);

            log.CurrentModId = null;
            return result;
        }

        /// <summary>Every .json under a mod's content folder, in a stable order.</summary>
        public static List<string> ContentFiles(ModManifest manifest)
        {
            var results = new List<string>();
            var contentRoot = Path.Combine(manifest.Directory, ContentFolderName);
            if (!Directory.Exists(contentRoot)) return results;

            var files = Directory.GetFiles(contentRoot, "*.json", SearchOption.AllDirectories);
            System.Array.Sort(files, System.StringComparer.OrdinalIgnoreCase);
            results.AddRange(files);
            return results;
        }
    }
}
