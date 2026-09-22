using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MadVoxel.Save
{
    /// <summary>
    /// Where saves live. One folder per world under the platform's persistent data
    /// path, with a chunks sub-folder holding only the chunks the player has edited.
    /// </summary>
    public static class SavePaths
    {
        public const string SavesFolder = "Saves";
        public const string ChunksFolder = "chunks";

        public const string WorldFile = "world.json";
        public const string PlayerFile = "player.json";
        public const string StructuresFile = "structures.json";

        public static string SavesRoot
        {
            get { return Path.Combine(Application.persistentDataPath, SavesFolder); }
        }

        public static string WorldDirectory(string worldName)
        {
            return Path.Combine(SavesRoot, Sanitise(worldName));
        }

        public static string ChunkDirectory(string worldName)
        {
            return Path.Combine(WorldDirectory(worldName), ChunksFolder);
        }

        public static string ChunkFile(string worldName, int cx, int cy, int cz)
        {
            return Path.Combine(ChunkDirectory(worldName), string.Format("c.{0}.{1}.{2}.mvc", cx, cy, cz));
        }

        public static void EnsureWorldDirectories(string worldName)
        {
            Directory.CreateDirectory(ChunkDirectory(worldName));
        }

        public static bool WorldExists(string worldName)
        {
            return File.Exists(Path.Combine(WorldDirectory(worldName), WorldFile));
        }

        public static List<string> ListWorlds()
        {
            var result = new List<string>();
            if (!Directory.Exists(SavesRoot)) return result;

            var dirs = Directory.GetDirectories(SavesRoot);
            for (int i = 0; i < dirs.Length; i++)
            {
                if (File.Exists(Path.Combine(dirs[i], WorldFile))) result.Add(Path.GetFileName(dirs[i]));
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        public static void DeleteWorld(string worldName)
        {
            var dir = WorldDirectory(worldName);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        public static string Sanitise(string name)
        {
            if (string.IsNullOrEmpty(name)) return "World";
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0) chars[i] = '_';
            }
            var cleaned = new string(chars).Trim();
            return cleaned.Length == 0 ? "World" : cleaned;
        }
    }
}
