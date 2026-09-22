using System;
using System.Collections.Generic;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Colony
{
    /// <summary>
    /// What a colonist spends their day doing. Deliberately five: this is a farm with
    /// people on it, not a colony sim with a scheduling grid.
    /// </summary>
    public enum ColonyJob
    {
        Idle,
        /// <summary>Tends the garden: harvests ready plots into a box.</summary>
        Farm,
        /// <summary>Stands a post near the fence or a trap.</summary>
        Guard,
        /// <summary>Hammers damaged pieces, slowly.</summary>
        Repair,
        /// <summary>Turns crops into meals at a stove, when there is one.</summary>
        Cook
    }

    /// <summary>What a colonist is doing this instant. A small state machine, on purpose.</summary>
    public enum ColonyState
    {
        Working,
        Eating,
        Drinking,
        Sleeping,
        Sheltering,
        Leaving,
        Dead
    }

    /// <summary>Why morale moved. Shown on the board so a player can act on it.</summary>
    public enum MoraleReason
    {
        Fed,
        Hungry,
        Thirsty,
        Rested,
        NoBed,
        Dark,
        Breached,
        Weather,
        Death
    }

    [Serializable]
    public struct MoraleEvent
    {
        public MoraleReason reason;
        public float perHour;
        public string line;
    }

    /// <summary>
    /// A person. Everything about them that is content rather than state: the names
    /// they can be drawn from, what they eat, and how fast they wear down.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Colonist", fileName = "Colonist")]
    public class ColonistDefinition : ScriptableObject
    {
        public string stringId = "madvoxel:colonist";
        public string displayName = "Survivor";

        [Header("Names")]
        [Tooltip("Drawn from when a recruit arrives. Never repeated while one is alive.")]
        public List<string> names = new List<string>();

        [Header("Body")]
        public float maxHealth = 90f;
        public float moveSpeed = 3.1f;
        [Tooltip("Hunger per game hour. They eat at roughly a third full.")]
        public float hungerPerHour = 2.2f;
        public float thirstPerHour = 2.8f;
        [Tooltip("What one meal restores.")]
        public float mealRestores = 45f;
        public float drinkRestores = 40f;

        [Header("Work")]
        [Tooltip("Game hours a repair job takes per piece. They are not fast.")]
        public float repairHoursPerPiece = 0.6f;
        public float repairFraction = 0.25f;
        [Tooltip("How far they will walk from the claim stake to work.")]
        public float workRadius = 26f;

        [Header("Morale")]
        [Range(0f, 100f)] public float startingMorale = 65f;
        [Tooltip("Below this they stop working.")]
        public float sulkBelow = 30f;
        [Tooltip("Below this they pack up and leave.")]
        public float leaveBelow = 10f;
        public List<MoraleEvent> moraleEvents = new List<MoraleEvent>();

        [Header("Fight")]
        [Tooltip("Guard job only. They are not soldiers.")]
        public float guardDamage = 7f;
        public float guardReach = 2.0f;
        public float guardCooldown = 1.3f;

        public float MoralePerHour(MoraleReason reason)
        {
            for (int i = 0; i < moraleEvents.Count; i++)
            {
                if (moraleEvents[i].reason == reason) return moraleEvents[i].perHour;
            }
            return 0f;
        }

        public string LineFor(MoraleReason reason)
        {
            for (int i = 0; i < moraleEvents.Count; i++)
            {
                if (moraleEvents[i].reason == reason && !string.IsNullOrEmpty(moraleEvents[i].line))
                    return moraleEvents[i].line;
            }
            return "";
        }
    }

    /// <summary>The colony's own rules: how many, what founding costs, what it eats.</summary>
    [CreateAssetMenu(menuName = "MadVoxel/Colony Rules", fileName = "ColonyRules")]
    public class ColonyRules : ScriptableObject
    {
        [Header("Size")]
        [Tooltip("This pass caps at three. A colony sim is a different game.")]
        public int maxColonists = 3;

        [Header("Founding")]
        [Tooltip("Beds needed before the board will accept a charter.")]
        public int bedsRequired = 1;
        [Tooltip("Food items in a box or fridge inside the claim.")]
        public int foodRequired = 1;

        [Header("Supply")]
        [Tooltip("Food items one colonist is reckoned to eat in a day, for the board.")]
        public float foodPerColonistPerDay = 3f;
        [Tooltip("Litres one colonist drinks a day, for the board.")]
        public float litresPerColonistPerDay = 4f;

        [Header("People")]
        public ColonistDefinition colonist;
    }
}
