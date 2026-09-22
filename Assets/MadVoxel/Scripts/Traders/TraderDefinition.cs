using System;
using System.Collections.Generic;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Traders
{
    [Serializable]
    public struct TraderStockEntry
    {
        public ItemDefinition item;
        public int stockCount;
        [Tooltip("Multiplier on the item's base trade value when this trader sells it.")]
        public float priceMultiplier;
        [Tooltip("Reputation tier needed before this appears in the list.")]
        public int minReputationTier;
    }

    /// <summary>
    /// A trader outpost. Phase 0 places the POI and the data; Phase 1 wires the
    /// buy/sell UI, the restock timer and reputation gains.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Trader", fileName = "Trader")]
    public class TraderDefinition : ScriptableObject
    {
        public string stringId = "madvoxel:trader";
        public string displayName = "Trader";
        [TextArea] public string greeting = "Cash on the counter, no credit.";

        [Header("Economy")]
        [Tooltip("Multiplier applied to an item's trade value when the trader sells to the player.")]
        public float sellMarkup = 1.6f;
        [Tooltip("Multiplier applied when the trader buys from the player.")]
        public float buyRate = 0.45f;
        public float restockHours = 24f;
        public ItemDefinition currencyItem;

        [Header("Placement")]
        [Tooltip("Outposts are placed on a deterministic grid this many metres apart.")]
        public int outpostGridSpacing = 700;
        [Tooltip("Index within the outpost grid cell, so two traders never share a spot.")]
        public int outpostVariant;

        [Header("Stock")]
        public List<TraderStockEntry> stock = new List<TraderStockEntry>();

        [Header("Quests")]
        public List<Quests.QuestDefinition> questBoard = new List<Quests.QuestDefinition>();
        public int reputationTiers = 5;
        public int reputationPerTier = 300;
    }
}
