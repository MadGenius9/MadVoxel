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
    public class SkillRankData
    {
        public string skillId = "";
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
        public int skillPoints;
        public List<string> unlockedRecipes = new List<string>();
        public List<SkillRankData> skillRanks = new List<SkillRankData>();

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
    }

    [Serializable]
    public class StructuresSaveData
    {
        public List<StructureSaveData> structures = new List<StructureSaveData>();
    }
}
