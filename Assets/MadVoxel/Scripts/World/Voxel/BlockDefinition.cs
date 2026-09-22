using MadVoxel.Core;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.World.Voxel
{
    /// <summary>
    /// One voxel type. Everything the mesher, the mining code, the loot roll and the
    /// upgrade path need lives here, so adding a block is a data change.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Block", fileName = "Block")]
    public class BlockDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id written into save files. Never reuse or rename after release.")]
        public string stringId = "madvoxel:unnamed";
        public string displayName = "Unnamed";

        [Header("Physical")]
        public bool isAir;
        [Tooltip("Blocks movement and is written into the chunk collider.")]
        public bool solid = true;
        [Tooltip("Hides the neighbouring face. Glass and foliage are solid but not opaque.")]
        public bool opaque = true;
        [Tooltip("Base seconds to mine with a bare hand at tool tier 0.")]
        public float hardness = 1.0f;
        [Tooltip("Tools below this tier cannot harvest the block at all.")]
        public int requiredToolTier;
        public ToolType preferredTool = ToolType.None;
        [Tooltip("Structural hit points when a zombie chews on it.")]
        public float structureHealth = 60f;

        [Header("Look")]
        public SurfaceFamily surfaceFamily = SurfaceFamily.Stone;
        public Color tint = Color.grey;
        [Range(0f, 1f)] public float smoothness = 0.1f;
        [Range(0f, 1f)] public float metallic;
        public bool transparent;
        [Range(0f, 1f)] public float lightEmission;

        [Header("Harvest")]
        public ItemDefinition dropItem;
        public int dropMin = 1;
        public int dropMax = 1;
        [Tooltip("XP awarded for breaking this block. Player-placed blocks award none.")]
        public float harvestXp = 1f;

        [Header("Building")]
        [Tooltip("0 = natural, 1 = wood, 2 = cobble, 3 = iron, 4 = steel. Used by Phase 2 upgrades.")]
        public int buildTier;
        [Tooltip("Block this one becomes when upgraded. Null means it is the top tier.")]
        public BlockDefinition upgradesTo;

        /// <summary>Runtime index in the <see cref="BlockRegistry"/>. Not serialised.</summary>
        [System.NonSerialized] public ushort RuntimeId;
    }
}
