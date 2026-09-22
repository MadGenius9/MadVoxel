using System;
using System.Collections.Generic;

namespace MadVoxel.Save
{
    /// <summary>
    /// Plain serialisable records written as JSON. Vectors are stored as separate
    /// floats so the files stay readable and version-tolerant.
    /// </summary>
    [Serializable]
    public class WorldSaveData
    {
        public int version = 1;
        public string worldName = "World";
        public int seed;
        public double totalHours;
        public int hordeNumber;
        public string createdUtc = "";
        public string lastPlayedUtc = "";
    }

    [Serializable]
    public class ItemStackData
    {
        public string itemId = "";
        public int count;
        public int durability;
    }

    [Serializable]
    public class PerkRankData
    {
        public string perkId = "";
        public int rank;
    }

    [Serializable]
    public class PlayerSaveData
    {
        public float posX, posY, posZ;
        public float yaw, pitch;
        public float health, stamina, food, water;
        public int selectedHotbar;
        public List<ItemStackData> slots = new List<ItemStackData>();

        public int level = 1;
        public float xp;
        public int perkPoints;
        public List<string> unlockedRecipes = new List<string>();
        public List<PerkRankData> perkRanks = new List<PerkRankData>();

        public bool hasRespawn;
        public float respawnX, respawnY, respawnZ;
    }

    [Serializable]
    public class StructureSaveData
    {
        public string definitionId = "";
        public int cellX, cellY, cellZ;
        public int rotation;
        public float health;
        public bool doorOpen;
        public bool isDeathBackpack;
        public List<ItemStackData> contents = new List<ItemStackData>();

        // Farm plot state. Growth is derived from the planting hour, so a crop keeps
        // maturing across a save and reload.
        public string cropId = "";
        public double plantedAtHours;

        // Grain bin contents, in litres.
        public List<SiloEntryData> silo = new List<SiloEntryData>();
    }

    [Serializable]
    public class SiloEntryData
    {
        public string cropId = "";
        public float litres;
    }

    [Serializable]
    public class FieldCellSaveData
    {
        public int x, z;
        public byte state;
        public byte cropIndex;
        public float moisture;
        public float fertiliser;
        public float yieldFactor;
        public double changedAtHours;
    }

    [Serializable]
    public class BuildPieceSaveData
    {
        public string definitionId = "";
        public int x, y, z;
        public int slot;
        public int side;
        public float health;
        public bool open;
    }

    [Serializable]
    public class StructuresSaveData
    {
        public List<StructureSaveData> structures = new List<StructureSaveData>();
        public List<BuildPieceSaveData> pieces = new List<BuildPieceSaveData>();
        public List<FieldCellSaveData> fieldCells = new List<FieldCellSaveData>();
    }
}
