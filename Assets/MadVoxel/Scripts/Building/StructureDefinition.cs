using MadVoxel.Core;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Building
{
    /// <summary>
    /// Deployables only. Doors, ladders, walls and floors are snap pieces on the build
    /// grid (see <see cref="BuildPieceDefinition"/>), not deployables.
    /// </summary>
    public enum StructureKind
    {
        Generic,
        Storage,
        CraftStation,
        Campfire,
        ToolCupboard,
        Bedroll,
        FarmPlot,
        Silo,
        /// <summary>Anything on the electrical grid: banks, relays, lights, traps, the pump.</summary>
        PowerDevice,
        /// <summary>Anything on the water side: pipes, tanks, taps, sprinklers.</summary>
        FluidDevice,
        /// <summary>The colony charter. One per claim.</summary>
        ColonyBoard
    }

    /// <summary>
    /// A snap piece: an object that sits in the voxel grid but is not a voxel. Doors,
    /// ladders, crates, benches and the claim stake are all this one type.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Structure", fileName = "Structure")]
    public class StructureDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string stringId = "madvoxel:structure";
        public string displayName = "Structure";
        public StructureKind kind = StructureKind.Generic;

        [Header("Footprint")]
        [Tooltip("How many voxel cells the piece occupies, in its unrotated orientation.")]
        public Vector3Int footprint = new Vector3Int(1, 1, 1);
        [Tooltip("Must sit on something solid.")]
        public bool requiresSupport = true;
        [Tooltip("Ladders and bedrolls do not block movement.")]
        public bool blocksMovement = true;

        [Header("Look")]
        public SurfaceFamily surfaceFamily = SurfaceFamily.Plank;
        public Color tint = new Color(0.45f, 0.33f, 0.21f);

        [Header("Durability")]
        public float maxHealth = 180f;
        [Tooltip("0 = wood, 1 = cobble, 2 = iron, 3 = steel. Phase 2 upgrade chain.")]
        public int buildTier;
        public StructureDefinition upgradesTo;

        [Header("Behaviour")]
        [Tooltip("Storage only: number of slots.")]
        public int storageSlots = 24;
        [Tooltip("Craft station only: which recipes it enables.")]
        public CraftStation craftStation = CraftStation.Workbench;
        [Tooltip("Campfire only.")]
        public float lightRange = 9f;
        public Color lightColour = new Color(1f, 0.62f, 0.28f);
        [Tooltip("Tool cupboard only: building privilege radius in metres. 0 uses the game config value.")]
        public float claimRadius;

        [Header("Farming")]
        [Tooltip("Farm plot only: must be placed on soil rather than stone or concrete.")]
        public bool requiresSoil;
        [Tooltip("Silo only: how many litres of produce it holds (Phase 1).")]
        public float siloCapacityLitres = 20000f;

        [Header("Utilities")]
        [Tooltip("Set on a PowerDevice: which electrical device this deployable carries.")]
        public Power.PowerDeviceDefinition powerDevice;
        [Tooltip("Set on a FluidDevice: which fitting this deployable carries.")]
        public Fluid.FluidDeviceDefinition fluidDevice;
        [Tooltip("A pump can be both: it sits on the water and draws from the grid.")]
        public bool drawsPower;

        [Header("Salvage")]
        [Tooltip("Item returned when the piece is removed with a wrench.")]
        public ItemDefinition salvageItem;
        public int salvageCount = 1;
    }
}
