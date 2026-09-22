using System;
using System.Collections.Generic;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Quests
{
    public enum QuestKind
    {
        Fetch,
        Clear,
        SurviveNights,
        Deliver
    }

    [Serializable]
    public struct QuestReward
    {
        public ItemDefinition item;
        public int count;
    }

    /// <summary>
    /// A trader contract. Phase 0 ships the data and the reward tables; Phase 1 adds
    /// the tracker, the turn-in flow and the reputation hookup.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Quest", fileName = "Quest")]
    public class QuestDefinition : ScriptableObject
    {
        public string stringId = "madvoxel:quest";
        public string title = "Contract";
        [TextArea] public string description;
        public QuestKind kind = QuestKind.Fetch;

        [Header("Objective")]
        [Tooltip("Fetch / Deliver: the item wanted.")]
        public ItemDefinition objectiveItem;
        [Tooltip("Fetch / Deliver: how many. Clear: how many zombies. SurviveNights: how many nights.")]
        public int objectiveCount = 10;
        [Tooltip("Clear: restrict to this zombie type, or leave empty for any.")]
        public string objectiveZombieId = "";

        [Header("Requirements")]
        public int requiredPlayerLevel = 1;
        public int requiredReputationTier;

        [Header("Reward")]
        public float xpReward = 120f;
        public int reputationReward = 100;
        public List<QuestReward> rewards = new List<QuestReward>();
    }
}
