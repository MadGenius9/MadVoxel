using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.Inventory
{
    public enum ToolType
    {
        None,
        Pickaxe,
        Axe,
        Shovel,
        Wrench,
        Hammer,
        Melee
    }

    public enum ItemCategory
    {
        Resource,
        Tool,
        Weapon,
        Block,
        Structure,
        Consumable,
        Ammo,
        Quest,
        Misc
    }

    /// <summary>
    /// Every item in the game. Blocks and snap pieces are items too - they simply carry
    /// a placement payload - so the hotbar never needs to special-case building.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Item", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string stringId = "madvoxel:unnamed";
        public string displayName = "Unnamed";
        [TextArea] public string description;
        public ItemCategory category = ItemCategory.Resource;
        public int maxStack = 64;

        [Header("Look")]
        public SurfaceFamily surfaceFamily = SurfaceFamily.Metal;
        public Color tint = Color.white;

        [Header("Tool / weapon")]
        public ToolType toolType = ToolType.None;
        [Tooltip("0 = fist/stone, 1 = stone tools, 2 = iron, 3 = steel.")]
        public int toolTier;
        [Tooltip("Multiplier on mining speed when used against its preferred block.")]
        public float harvestSpeed = 1f;
        public float meleeDamage = 5f;
        public float attackCooldown = 0.55f;
        [Tooltip("0 means the item never wears out.")]
        public int maxDurability;

        [Header("Placement")]
        public World.Terrain.BlockDefinition placeableBlock;
        public Building.StructureDefinition placeableStructure;
        [Tooltip("Rust-style snap piece. Always the Twig tier; the hammer upgrades from there.")]
        public Building.BuildPieceDefinition placeableBuildPiece;
        [Tooltip("Deploys a drivable machine rather than a static piece.")]
        public Vehicles.VehicleDefinition placeableVehicle;
        [Tooltip("Hitches to the back of a machine instead of standing on the ground.")]
        public Vehicles.ImplementDefinition hitchImplement;

        [Header("Consumable")]
        public float foodRestore;
        public float waterRestore;
        public float healthRestore;
        [Tooltip("Cooked food is what gets you through a night, so meals restore stamina too.")]
        public float staminaRestore;

        [Header("Economy / fuel")]
        [Tooltip("Seconds of campfire burn time. 0 means not a fuel.")]
        public float fuelSeconds;
        [Tooltip("Base trader price. Phase 1 uses this for buy/sell.")]
        public int tradeValue = 1;

        [Header("Spoilage")]
        [Tooltip("Game hours before this goes off in the open. Zero means it never does.")]
        public float spoilHours;
        [Tooltip("What it turns into when it does. Null means the stack is simply lost.")]
        public ItemDefinition spoiledInto;

        public bool IsPlaceable
        {
            get
            {
                return placeableBlock != null || placeableStructure != null
                    || placeableBuildPiece != null || placeableVehicle != null;
            }
        }

        public bool HasDurability
        {
            get { return maxDurability > 0; }
        }
    }
}
