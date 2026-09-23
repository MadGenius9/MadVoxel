using System;
using System.Collections.Generic;
using System.Reflection;
using MadVoxel.Save;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The shape of the save format, pinned.
    ///
    /// Saves go through <c>JsonUtility</c>, which is quiet in a way that matters: it
    /// serialises public instance fields and silently ignores everything else. Turn a
    /// save field into a property, mark it readonly, or give it a type it does not
    /// understand, and the data simply stops being written. Nothing throws. Nothing
    /// logs. The world just comes back missing something, and only on a reload - which
    /// is the last place anyone looks.
    ///
    /// Renaming a field is the same class of problem from the other side: the writer
    /// starts using a new name and every existing world silently loses that value,
    /// because a missing key reads back as a default.
    ///
    /// So the schema is written down here. A change to it is not forbidden - it is
    /// just not something that should happen by accident, and the failure message says
    /// what it costs.
    /// </summary>
    public static class SaveSchemaTests
    {
        public static void Run()
        {
            Serialisable();
            Schema();
        }

        static readonly Type[] SaveTypes =
        {
            typeof(WorldSaveData), typeof(ItemStackData), typeof(PerkRankData),
            typeof(QuestSaveData), typeof(PlayerSaveData), typeof(StructureSaveData),
            typeof(LinkSaveData), typeof(ColonistSaveData), typeof(ColonySaveData),
            typeof(SiloEntryData), typeof(FieldCellSaveData), typeof(BuildPieceSaveData),
            typeof(VehicleSaveData), typeof(TraderSaveData), typeof(StructuresSaveData)
        };

        // ---------------------------------------------------------- serialisability

        static void Serialisable()
        {
            Harness.Section("save: everything in the format is actually written");

            var notMarked = new List<string>();
            var unwritable = new List<string>();
            var hidden = new List<string>();

            for (int t = 0; t < SaveTypes.Length; t++)
            {
                var type = SaveTypes[t];

                if (type.GetCustomAttribute<SerializableAttribute>() == null) notMarked.Add(type.Name);

                // A property looks like state and is not saved. This is the trap.
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                for (int i = 0; i < properties.Length; i++)
                {
                    hidden.Add(type.Name + "." + properties[i].Name);
                }

                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                for (int i = 0; i < fields.Length; i++)
                {
                    if (fields[i].IsInitOnly) unwritable.Add(type.Name + "." + fields[i].Name + " (readonly)");
                    else if (!IsWritable(fields[i].FieldType)) unwritable.Add(type.Name + "." + fields[i].Name
                        + " (" + fields[i].FieldType.Name + ")");
                }
            }

            Harness.Check(notMarked.Count == 0,
                "every save record is [Serializable]" + Detail(notMarked));

            Harness.Check(hidden.Count == 0,
                "no save record has a property - JsonUtility ignores them without a word" + Detail(hidden));

            Harness.Check(unwritable.Count == 0,
                "every save field is a type JsonUtility can write" + Detail(unwritable));
        }

        /// <summary>JsonUtility's rules, as far as this format uses them.</summary>
        static bool IsWritable(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return true;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return IsWritable(type.GetGenericArguments()[0]);
            }

            return type.GetCustomAttribute<SerializableAttribute>() != null;
        }

        // ------------------------------------------------------------------ schema

        /// <summary>
        /// The format as it stands. Each entry is a record and the fields it writes.
        ///
        /// If a check here fails you have changed what a save file contains. That is
        /// allowed - but every world already on disk is about to read the missing or
        /// renamed field as a default, so it should be a decision rather than a
        /// side-effect of a rename.
        /// </summary>
        static readonly Dictionary<string, string[]> Expected = new Dictionary<string, string[]>
        {
            { "WorldSaveData", new[] {
                "version", "worldName", "seed", "totalHours", "hordeNumber", "createdUtc",
                "lastPlayedUtc", "weatherKind", "weatherHoursRemaining", "claimHeat", "mods" } },

            { "ItemStackData", new[] { "itemId", "count", "durability", "spoilRemaining" } },
            { "PerkRankData", new[] { "perkId", "rank" } },
            { "QuestSaveData", new[] { "questId", "traderId", "acceptedAtHours", "counter" } },

            { "PlayerSaveData", new[] {
                "activeQuests", "completedQuests", "posX", "posY", "posZ", "yaw", "pitch",
                "health", "stamina", "food", "water", "selectedHotbar", "slots",
                "level", "xp", "perkPoints", "unlockedRecipes", "perkRanks",
                "hasRespawn", "respawnX", "respawnY", "respawnZ" } },

            { "StructureSaveData", new[] {
                "definitionId", "cellX", "cellY", "cellZ", "rotation", "health", "doorOpen",
                "isDeathBackpack", "contents", "cropId", "plantedAtHours",
                "furnaceWorkedToHours", "furnaceBankedFuel", "silo",
                "powerNodeId", "powerSwitchedOn", "fuelLitres", "storedWattHours",
                "fluidNodeId", "fluidSwitchedOn", "litres", "fluidBroken" } },

            { "LinkSaveData", new[] { "fromId", "toId" } },
            { "ColonistSaveData", new[] { "name", "job", "food", "water", "morale" } },
            { "ColonySaveData", new[] { "founded", "colonyName", "sheltering", "colonists" } },
            { "SiloEntryData", new[] { "cropId", "litres" } },

            { "VehicleSaveData", new[] {
                "definitionId", "posX", "posY", "posZ", "yaw", "fuelLitres", "health",
                "storage", "implementId", "hopperCropId", "hopperLitres" } },

            { "TraderSaveData", new[] { "traderId", "reputation", "lastRestockHours", "stock" } },

            { "FieldCellSaveData", new[] { "x", "z", "state", "cropIndex", "changedAtHours",
                "moisture", "fertiliser", "yieldFactor" } },

            { "BuildPieceSaveData", new[] { "definitionId", "x", "y", "z",
                "slot", "side", "health", "open" } },

            { "StructuresSaveData", new[] { "vehicles", "traders", "structures", "pieces",
                "fieldCells", "wires", "hoses", "colony" } },
        };

        static void Schema()
        {
            Harness.Section("save: the format has not changed underneath old worlds");

            int checked_ = 0;

            for (int t = 0; t < SaveTypes.Length; t++)
            {
                var type = SaveTypes[t];

                string[] expected;
                if (!Expected.TryGetValue(type.Name, out expected)) continue;

                checked_++;

                var actual = new List<string>();
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                for (int i = 0; i < fields.Length; i++) actual.Add(fields[i].Name);

                var missing = new List<string>();
                for (int i = 0; i < expected.Length; i++)
                {
                    if (!actual.Contains(expected[i])) missing.Add(expected[i]);
                }

                var added = new List<string>();
                for (int i = 0; i < actual.Count; i++)
                {
                    if (Array.IndexOf(expected, actual[i]) < 0) added.Add(actual[i]);
                }

                // Gone or renamed: every world on disk loses that value on the next load.
                Harness.Check(missing.Count == 0,
                    type.Name + " still writes every field it used to" + Detail(missing));

                // New fields are fine - old saves read them as defaults - but the ledger
                // has to be updated, or it stops being a record of anything.
                Harness.Check(added.Count == 0,
                    type.Name + " writes nothing the ledger has not been told about" + Detail(added));
            }

            // Every type in the list, not merely most of them. A record with no ledger
            // entry is silently skipped, which is exactly the hole this is here to close.
            Harness.Equal(checked_, SaveTypes.Length,
                string.Format("all {0} save records are pinned, none skipped", SaveTypes.Length));

            // A save written before any of today's systems existed must still load. That
            // is what JsonUtility's missing-key-reads-as-default behaviour buys, and it
            // is worth stating rather than assuming.
            var old = new StructuresSaveData();
            Harness.Check(old.vehicles != null && old.vehicles.Count == 0,
                "a save with no machines in it reads as no machines, not as null");
            Harness.Check(old.traders != null && old.traders.Count == 0,
                "and a save from before traders existed reads as no traders");

            var player = new PlayerSaveData();
            Harness.Check(player.activeQuests != null && player.activeQuests.Count == 0,
                "and one from before contracts existed carries none");

            var structure = new StructureSaveData();
            Harness.Equal(structure.furnaceBankedFuel, 0f,
                "a furnace from before it banked fuel starts with none rather than a garbage figure");
        }

        static string Detail(List<string> items)
        {
            return items.Count > 0 ? ": " + string.Join(", ", items) : "";
        }
    }
}
