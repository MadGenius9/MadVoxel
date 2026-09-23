using System.Collections.Generic;
using MadVoxel.Building;
using MadVoxel.Colony;
using MadVoxel.Core;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Content
{
    /// <summary>
    /// The people. Six of them, at most, and the numbers that decide how long they
    /// put up with you.
    /// </summary>
    public static partial class ContentLibrary
    {
        static MoraleEvent Morale(MoraleReason reason, float perHour, string line)
        {
            return new MoraleEvent { reason = reason, perHour = perHour, line = line };
        }

        static ColonyRules BuildColony(Dictionary<string, ItemDefinition> items,
                                       Dictionary<string, StructureDefinition> structures)
        {
            var person = ScriptableObject.CreateInstance<ColonistDefinition>();
            person.name = "Colonist";
            person.stringId = "madvoxel:colonist_survivor";
            person.displayName = "Survivor";

            // Plain names. These people are hands on a farm, not characters with arcs.
            person.names.AddRange(new[]
            {
                "Jules", "Mara", "Odell", "Wren", "Cass", "Boone", "Tam", "Rue", "Hollis", "Sig"
            });

            person.maxHealth = 90f;
            person.moveSpeed = 3.1f;

            // They eat about three times a day and drink about four, which is what the
            // board's "days of food" line is reckoned against.
            person.hungerPerHour = 2.2f;
            person.thirstPerHour = 2.8f;
            person.mealRestores = 45f;
            person.drinkRestores = 40f;

            person.repairHoursPerPiece = 0.6f;
            person.repairFraction = 0.25f;
            person.workRadius = 26f;

            person.startingMorale = 65f;
            person.sulkBelow = 30f;
            person.leaveBelow = 10f;

            // Every bad thing is a per-hour drain, so neglect is recoverable and steady
            // neglect is not. Hunger and thirst pull hardest; a dark camp is a nag.
            person.moraleEvents.AddRange(new[]
            {
                Morale(MoraleReason.Rested, 1.4f, "settled"),
                Morale(MoraleReason.Hungry, -3.2f, "hungry"),
                Morale(MoraleReason.Thirsty, -4.0f, "thirsty"),
                Morale(MoraleReason.NoBed, -1.8f, "nowhere to sleep"),
                Morale(MoraleReason.Dark, -1.1f, "working in the dark"),
                Morale(MoraleReason.Breached, -2.6f, "the walls came down"),
                Morale(MoraleReason.Weather, -1.0f, "out in the weather"),
                Morale(MoraleReason.Death, -14f, "someone died")
            });

            person.guardDamage = 7f;
            person.guardReach = 2f;
            person.guardCooldown = 1.3f;

            var rules = ScriptableObject.CreateInstance<ColonyRules>();
            rules.name = "ColonyRules";
            // The brief said three to six. Three was the floor: the hard cap is six and
            // what you have actually built - beds, food put by - decides the rest.
            rules.maxColonists = 6;
            rules.bedsRequired = 1;
            rules.foodRequired = 1;
            rules.foodPerColonistPerDay = 3f;
            rules.litresPerColonistPerDay = 4f;
            rules.colonist = person;

            // The board itself: a plaque, not a building.
            var board = Structure(StructureIds.ColonyBoard, "Colony Board", StructureKind.ColonyBoard,
                Vector3Int.one, SurfaceFamily.Plank, new Color(0.42f, 0.33f, 0.22f), 120f);
            board.blocksMovement = false;
            Register(items, structures, ItemIds.PieceColonyBoard, StructureIds.ColonyBoard, board);

            return rules;
        }

        static void AddColonyRecipes(Dictionary<string, ItemDefinition> it, List<RecipeDefinition> list)
        {
            list.Add(Recipe("madvoxel:craft_colony_board", it[ItemIds.PieceColonyBoard], 1,
                CraftStation.Workbench, 4f,
                Ing(it[ItemIds.Plank], 8), Ing(it[ItemIds.ScrapMetal], 4), Ing(it[ItemIds.Cloth], 2)));
        }
    }
}
