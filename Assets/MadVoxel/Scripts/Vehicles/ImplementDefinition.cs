using MadVoxel.Farming.Crops;
using MadVoxel.Inventory;
using UnityEngine;

namespace MadVoxel.Vehicles
{
    /// <summary>
    /// What an implement does to the ground it is dragged over. One operation each -
    /// a combine that ploughs, sows and harvests in one pass would collapse the whole
    /// tillage cycle into a single button.
    /// </summary>
    public enum ImplementKind
    {
        /// <summary>Wild or stubble into plowed. The first pass on new ground.</summary>
        Plow,
        /// <summary>Plowed into cultivated, ready to take seed.</summary>
        Cultivator,
        /// <summary>Sows a cultivated cell. Draws from the hopper.</summary>
        Seeder,
        /// <summary>Lifts a ready cell into the hopper, in litres.</summary>
        Harvester,
        /// <summary>Puts fertility back into worked ground. Draws from the hopper.</summary>
        Spreader
    }

    [CreateAssetMenu(menuName = "MadVoxel/Implement", fileName = "Implement")]
    public class ImplementDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string stringId = "madvoxel:implement";
        public string displayName = "Implement";
        public ImplementKind kind = ImplementKind.Plow;

        [Header("Working")]
        [Tooltip("Metres across. This is the number that decides whether an acre is an afternoon or a week.")]
        public float workingWidth = 3f;
        [Tooltip("Scales the tractor's top speed while engaged. A plough is heavy.")]
        [Range(0.1f, 1f)] public float speedMultiplier = 0.55f;
        [Tooltip("Extra litres of fuel per hour while engaged and moving.")]
        public float fuelLitresPerHour = 2.5f;

        [Header("Hopper")]
        [Tooltip("Seeders and spreaders draw from it, harvesters fill it. Zero means it has none.")]
        public float hopperCapacityLitres;
        [Tooltip("Seeders: litres of seed one cell costs. Spreaders: litres of muck.")]
        public float seedLitresPerCell = 0.05f;
        [Tooltip("Seeders: litres one seed item fills the hopper with. Spreaders: one compost.")]
        public float litresPerSeedItem = 8f;

        [Header("Carrying")]
        [Tooltip("The item you carry it as. An unhitched implement lives in a bag, like every other deployable.")]
        public ItemDefinition item;

        public bool UsesHopper { get { return hopperCapacityLitres > 0f; } }

        /// <summary>Seeders and spreaders empty their hopper; harvesters fill it.</summary>
        public bool FillsHopper { get { return kind == ImplementKind.Harvester; } }

        /// <summary>
        /// Carries one thing and needs no cargo identity for it. A seeder has to
        /// remember which crop is in the box; a spreader only ever holds muck.
        /// </summary>
        public bool CarriesMuck { get { return kind == ImplementKind.Spreader; } }

        public string VerbFor(CropDefinition crop)
        {
            switch (kind)
            {
                case ImplementKind.Plow: return "PLOWING";
                case ImplementKind.Cultivator: return "CULTIVATING";
                case ImplementKind.Seeder: return crop != null ? "SOWING " + crop.displayName.ToUpperInvariant() : "SOWING";
                case ImplementKind.Spreader: return "SPREADING";
                default: return "HARVESTING";
            }
        }
    }
}
