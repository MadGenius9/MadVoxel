using System;
using System.IO;
using UnityEngine;

namespace MadVoxel.Save
{
    /// <summary>Reads and writes the three JSON files that make up a world save.</summary>
    public static class WorldSaveIO
    {
        public static void WriteWorld(string worldName, WorldSaveData data)
        {
            Write(Path.Combine(SavePaths.WorldDirectory(worldName), SavePaths.WorldFile), data);
        }

        public static WorldSaveData ReadWorld(string worldName)
        {
            return Read<WorldSaveData>(Path.Combine(SavePaths.WorldDirectory(worldName), SavePaths.WorldFile));
        }

        public static void WritePlayer(string worldName, PlayerSaveData data)
        {
            Write(Path.Combine(SavePaths.WorldDirectory(worldName), SavePaths.PlayerFile), data);
        }

        public static PlayerSaveData ReadPlayer(string worldName)
        {
            return Read<PlayerSaveData>(Path.Combine(SavePaths.WorldDirectory(worldName), SavePaths.PlayerFile));
        }

        public static void WriteStructures(string worldName, StructuresSaveData data)
        {
            Write(Path.Combine(SavePaths.WorldDirectory(worldName), SavePaths.StructuresFile), data);
        }

        public static StructuresSaveData ReadStructures(string worldName)
        {
            return Read<StructuresSaveData>(Path.Combine(SavePaths.WorldDirectory(worldName), SavePaths.StructuresFile));
        }

        static void Write<T>(string path, T data) where T : class
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var json = JsonUtility.ToJson(data, true);

                // Write beside the target then swap, so a crash mid-write cannot eat the save.
                var temp = path + ".tmp";
                File.WriteAllText(temp, json);
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("Failed to write {0}: {1}", path, ex.Message);
            }
        }

        static T Read<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("Failed to read {0}: {1}", path, ex.Message);
                return null;
            }
        }
    }
}
