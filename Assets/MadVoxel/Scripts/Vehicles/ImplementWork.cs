using UnityEngine;

namespace MadVoxel.Vehicles
{
    /// <summary>What one tick of work actually did, for the panel and the caller.</summary>
    public struct WorkResult
    {
        public int CellsWorked;
        public float LitresMoved;
        /// <summary>The hopper stopped the job before the swath ran out.</summary>
        public bool HopperLimited;

        public bool DidAnything { get { return CellsWorked > 0; } }
    }

    /// <summary>
    /// The accounting an implement does before it touches the ground: how many of the
    /// cells under it can actually be paid for this tick.
    ///
    /// It exists separately from the driving because the failure it prevents is
    /// invisible - a seeder that sows the last two cells of a swath with an empty
    /// hopper leaves a field that looks sown and comes up bare. The rule is that a
    /// cell is worked only if it can be paid for in full.
    /// </summary>
    public static class ImplementWork
    {
        /// <summary>
        /// How many of <paramref name="wanted"/> cells the hopper can cover.
        ///
        /// A seeder spends from the hopper, so it is limited by what is in it. A
        /// harvester fills the hopper, so it is limited by the room left. An implement
        /// with no hopper at all - a plough, a cultivator - is limited by nothing.
        /// </summary>
        public static int CellsAffordable(ImplementDefinition implement, float hopperLitres, int wanted)
        {
            if (implement == null || wanted <= 0) return 0;
            if (!implement.UsesHopper) return wanted;

            float perCell = Mathf.Max(0.0001f, implement.seedLitresPerCell);

            if (implement.FillsHopper)
            {
                // Harvesting: room left decides. Per-cell yield varies with the crop, so
                // this is a floor rather than an exact count - the caller stops early
                // when the hopper actually fills.
                float room = Mathf.Max(0f, implement.hopperCapacityLitres - hopperLitres);
                if (room <= 0f) return 0;
                return wanted;
            }

            // Sowing: only whole cells we can pay for.
            int affordable = Mathf.FloorToInt(hopperLitres / perCell);
            return Mathf.Clamp(affordable, 0, wanted);
        }

        /// <summary>
        /// Whether a hopper can take a whole sack's worth.
        ///
        /// All-or-nothing on purpose. A part-fill has to be either free seed or a whole
        /// item spent on a splash, and both are worse than saying the hopper is full -
        /// the few litres of headroom that will not take another sack round off, and
        /// nothing is lost either way.
        /// </summary>
        public static bool Accepts(ImplementDefinition implement, float hopperLitres, float litres)
        {
            if (implement == null || litres <= 0f) return false;
            return hopperLitres + litres <= implement.hopperCapacityLitres + 0.001f;
        }

        /// <summary>Litres a seeder spends putting seed in this many cells.</summary>
        public static float SeedCost(ImplementDefinition implement, int cells)
        {
            if (implement == null || cells <= 0 || implement.FillsHopper) return 0f;
            return cells * Mathf.Max(0f, implement.seedLitresPerCell);
        }

        /// <summary>
        /// Adds harvested litres to a hopper and reports what would not fit. The
        /// overflow is spilled rather than silently kept, so a full harvester in a
        /// standing crop is a decision the player has to make.
        /// </summary>
        public static float AddToHopper(ImplementDefinition implement, ref float hopperLitres, float litres)
        {
            if (implement == null || litres <= 0f) return 0f;

            float room = Mathf.Max(0f, implement.hopperCapacityLitres - hopperLitres);
            float taken = Mathf.Min(room, litres);

            hopperLitres += taken;
            return litres - taken;
        }

        /// <summary>True once a hopper is too full or too empty to keep working.</summary>
        public static bool IsBlocked(ImplementDefinition implement, float hopperLitres)
        {
            if (implement == null || !implement.UsesHopper) return false;

            if (implement.FillsHopper) return hopperLitres >= implement.hopperCapacityLitres - 0.001f;
            return hopperLitres < implement.seedLitresPerCell;
        }

        /// <summary>The one line the tractor panel shows about the implement.</summary>
        public static string Describe(ImplementDefinition implement, float hopperLitres, bool engaged)
        {
            if (implement == null) return "NO IMPLEMENT";

            string name = implement.displayName.ToUpperInvariant();
            if (!engaged) return name + "  RAISED";

            if (IsBlocked(implement, hopperLitres))
            {
                return name + (implement.FillsHopper ? "  HOPPER FULL" : "  OUT OF SEED");
            }

            return name + "  WORKING";
        }
    }
}
