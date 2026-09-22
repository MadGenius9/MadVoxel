using System.Collections.Generic;

namespace MadVoxel.Modding
{
    /// <summary>One mod's mod.json, plus where it was found.</summary>
    public class ModManifest
    {
        public string Id = "";
        public string Name = "";
        public string Version = "1.0.0";
        public string Author = "";
        public string Description = "";
        /// <summary>Lower loads first. Ties break on id, so load order is always deterministic.</summary>
        public int LoadOrder = 100;
        public readonly List<string> Dependencies = new List<string>();

        public string Directory = "";
        public bool Enabled = true;

        public string DisplayName
        {
            get { return string.IsNullOrEmpty(Name) ? Id : Name; }
        }

        public override string ToString()
        {
            return DisplayName + " " + Version;
        }

        /// <summary>
        /// Ids become the namespace prefix for everything a mod adds, so they are limited
        /// to what reads cleanly in a string id: lowercase letters, digits, underscore.
        /// </summary>
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok) return false;
            }
            return true;
        }

        public static ModManifest Parse(JsonValue root, string directory, ModLog log)
        {
            if (root == null || root.Kind != JsonKind.Object)
            {
                log.Error("mod.json in '{0}' is not a JSON object", directory);
                return null;
            }

            var manifest = new ModManifest { Directory = directory };
            var reader = new JsonReader(root, log, "mod.json");

            reader.Read("id", ref manifest.Id);
            reader.Read("name", ref manifest.Name);
            reader.Read("version", ref manifest.Version);
            reader.Read("author", ref manifest.Author);
            reader.Read("description", ref manifest.Description);
            reader.Read("loadOrder", ref manifest.LoadOrder);
            reader.Read("enabled", ref manifest.Enabled);
            reader.ReadStringList("dependencies", manifest.Dependencies);
            reader.ReportUnknownKeys();

            if (!IsValidId(manifest.Id))
            {
                log.Error("mod in '{0}' has an invalid id '{1}'. Use lowercase letters, digits and underscores.",
                    directory, manifest.Id);
                return null;
            }

            return manifest;
        }
    }
}
