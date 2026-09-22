using System.Collections.Generic;
using MadVoxel.Core;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Building
{
    public enum BuildPieceKind
    {
        Foundation,
        Floor,
        Wall,
        WindowWall,
        Doorway,
        HalfWall,
        Stairs,
        Roof,
        Ladder,
        Hatch,
        Door,
        /// <summary>Low barrier for penning a garden without walling it in.</summary>
        Fence
    }

    /// <summary>Rust-style upgrade chain. Every piece starts at Twig and is hammered up.</summary>
    public enum BuildTier
    {
        Twig,
        Wood,
        Stone,
        Metal,
        Armored
    }

    /// <summary>
    /// One modular snap piece at one tier. Only the Twig tier is placeable; the rest are
    /// reached with the hammer, which is what makes "build the shape first, armour it
    /// later" the loop rather than a material gate on placement.
    /// </summary>
    [CreateAssetMenu(menuName = "MadVoxel/Build Piece", fileName = "BuildPiece")]
    public class BuildPieceDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string stringId = "madvoxel:piece";
        public string displayName = "Piece";
        public BuildPieceKind kind = BuildPieceKind.Foundation;
        public BuildTier tier = BuildTier.Twig;

        [Header("Placement")]
        public BuildSlot slot = BuildSlot.Floor;
        [Tooltip("Blocks the player and the AI. False for ladders, hatches and open doorways.")]
        public bool blocksMovement = true;
        [Tooltip("Foundations rest on terrain; everything else needs another piece to hang off.")]
        public bool restsOnTerrain;

        [Header("Mounting")]
        [Tooltip("Doors and ladders hang on an existing wall-slot piece rather than standing alone.")]
        public bool requiresHost;
        public BuildPieceKind hostKind = BuildPieceKind.Doorway;

        [Header("Durability")]
        public float maxHealth = 60f;
        [Tooltip("Multiplier applied to incoming damage. Higher tiers take less.")]
        public float damageResistance = 1f;

        [Header("Look")]
        public SurfaceFamily surfaceFamily = SurfaceFamily.Wood;
        public Color tint = new Color(0.45f, 0.35f, 0.22f);
        [Range(0f, 1f)] public float smoothness = 0.1f;
        [Range(0f, 1f)] public float metallic;

        [Header("Upgrade")]
        public BuildPieceDefinition upgradesTo;
        public List<RecipeIngredient> upgradeCost = new List<RecipeIngredient>();
        [Tooltip("Perk that must be ranked before this upgrade is allowed. Empty means always.")]
        public string requiredPerkId = "";
        public int requiredPerkRank = 1;

        [Header("Salvage")]
        [Tooltip("Item returned per salvage with the hammer.")]
        public ItemDefinition salvageItem;
        public int salvageCount = 1;

        public bool IsTopTier { get { return upgradesTo == null; } }
    }
}
