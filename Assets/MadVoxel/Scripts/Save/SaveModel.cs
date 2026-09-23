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

        /// <summary>The sky, so a reload does not hand you a different afternoon.</summary>
        public int weatherKind;
        public float weatherHoursRemaining;

        /// <summary>How loud the claim was. The floor is recomputed, the level is not.</summary>
        public float claimHeat;
        /// <summary>Mod ids active when this world was last played, so a missing one can be reported.</summary>
        public List<string> mods = new List<string>();
    }

    [Serializable]
    public class ItemStackData
    {
        public string itemId = "";
        public int count;
        public int durability;
        /// <summary>Game hours of shelf life left. -1 means "unset", which reads as fresh.</summary>
        public float spoilRemaining = -1f;
    }

    [Serializable]
    public class PerkRankData
    {
        public string perkId = "";
        public int rank;
    }

    /// <summary>
    /// One contract in progress. The counter is only meaningful for the kinds that
    /// tally; the rest recompute their progress, so there is nothing to store.
    /// </summary>
    [Serializable]
    public class QuestSaveData
    {
        public string questId = "";
        public string traderId = "";
        public double acceptedAtHours;
        public int counter;
    }

    [Serializable]
    public class PlayerSaveData
    {
        public List<QuestSaveData> activeQuests = new List<QuestSaveData>();
        public List<string> completedQuests = new List<string>();

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

        // Furnace: the hour its work is accounted up to. Everything after it is owed,
        // so a furnace loaded and left through a save comes back with the metal made.
        public double furnaceWorkedToHours;
        /// <summary>Burn already lit and unspent, so a save does not pocket half a lump of coal.</summary>
        public float furnaceBankedFuel;

        // Grain bin contents, in litres.
        public List<SiloEntryData> silo = new List<SiloEntryData>();

        // --- utilities. Node ids are saved so the wires can be put back; they are
        // remapped on load, because the graphs hand out fresh ids as pieces are placed.
        public int powerNodeId;
        public bool powerSwitchedOn = true;
        public float fuelLitres;
        public float storedWattHours;

        public int fluidNodeId;
        public bool fluidSwitchedOn = true;
        public float litres;
        public bool fluidBroken;
    }

    /// <summary>One wire or one hose, by the node ids the save recorded.</summary>
    [Serializable]
    public class LinkSaveData
    {
        public int fromId;
        public int toId;
    }

    /// <summary>A colonist, flattened.</summary>
    [Serializable]
    public class ColonistSaveData
    {
        public string name = "";
        public int job;
        public float food = 80f;
        public float water = 80f;
        public float morale = 65f;
    }

    /// <summary>The colony charter and everyone on it.</summary>
    [Serializable]
    public class ColonySaveData
    {
        public bool founded;
        public string colonyName = "Mad Colony";
        public bool sheltering;
        public List<ColonistSaveData> colonists = new List<ColonistSaveData>();

        /// <summary>
        /// Whoever is standing at the fence, and when the last one arrived. Without the
        /// arrival day a reload clears the three-day wait, which turns turning someone
        /// away from a decision into a reroll.
        /// </summary>
        public bool wandererWaiting;
        public string wandererName = "";
        public int lastArrivalDay;
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

    /// <summary>
    /// A parked machine. The implement is stored with it rather than as an object of
    /// its own, because an implement only exists hitched.
    /// </summary>
    [Serializable]
    public class VehicleSaveData
    {
        public string definitionId = "";
        public float posX, posY, posZ;
        public float yaw;
        public float fuelLitres;
        public float health;

        public List<ItemStackData> storage = new List<ItemStackData>();

        public string implementId = "";
        public string hopperCropId = "";
        public float hopperLitres;
    }

    /// <summary>
    /// A trader's books. The shelves are saved rather than recomputed, because a
    /// trader you cleared out is meant to stay cleared out until they restock.
    /// </summary>
    [Serializable]
    public class TraderSaveData
    {
        public string traderId = "";
        public int reputation;
        public double lastRestockHours;
        public List<int> stock = new List<int>();
    }

    [Serializable]
    public class StructuresSaveData
    {
        public List<TraderSaveData> traders = new List<TraderSaveData>();

        public List<VehicleSaveData> vehicles = new List<VehicleSaveData>();

        public List<StructureSaveData> structures = new List<StructureSaveData>();
        public List<BuildPieceSaveData> pieces = new List<BuildPieceSaveData>();
        public List<FieldCellSaveData> fieldCells = new List<FieldCellSaveData>();

        /// <summary>The grid and the plumbing, as pairs of saved node ids.</summary>
        public List<LinkSaveData> wires = new List<LinkSaveData>();
        public List<LinkSaveData> hoses = new List<LinkSaveData>();

        public ColonySaveData colony = new ColonySaveData();
    }
}
