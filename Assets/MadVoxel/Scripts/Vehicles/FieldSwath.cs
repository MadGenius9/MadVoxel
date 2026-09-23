using System.Collections.Generic;
using MadVoxel.World.Fields;
using UnityEngine;

namespace MadVoxel.Vehicles
{
    /// <summary>
    /// Which field cells an implement covers as it is dragged across the ground.
    ///
    /// This is the piece that is easy to get quietly wrong. A naive implement works
    /// only the cells under it *this frame*, so at 20 km/h on a 60 Hz frame you move
    /// nearly a cell per tick and at 30 fps you skip every other row - and the player
    /// finds out two in-game days later when the field comes up striped.
    ///
    /// So the swath is swept along the segment travelled since the last tick rather
    /// than sampled at a point, and the sweep is capped so a lag spike or a teleport
    /// cannot ask for a million cells.
    ///
    /// Pure and engine-free on purpose: striping is invisible in a screenshot and
    /// obvious in a test.
    /// </summary>
    public static class FieldSwath
    {
        /// <summary>Samples per cell along the direction of travel. Half a cell is plenty.</summary>
        const float StepMetres = FieldGrid.CellSize * 0.5f;

        /// <summary>
        /// A single tick can only work so much ground. Beyond this the machine was
        /// teleported or the frame hitched, and quietly ploughing the intervening
        /// hundred metres would be worse than missing it.
        /// </summary>
        public const float MaxSweepMetres = 12f;

        /// <summary>
        /// Fills <paramref name="into"/> with every field cell under an implement of
        /// <paramref name="width"/> metres dragged from <paramref name="from"/> to
        /// <paramref name="to"/>. Returns the number of cells added.
        ///
        /// Cells are deduplicated: a slow-moving machine returns the same handful of
        /// cells every tick, and the caller must not pay to work them twice.
        /// </summary>
        public static int Collect(Vector3 from, Vector3 to, float headingDegrees, float width,
                                  List<Vector2Int> into)
        {
            if (into == null) return 0;
            into.Clear();

            width = Mathf.Max(FieldGrid.CellSize, width);

            Vector3 travel = to - from;
            travel.y = 0f;

            float distance = travel.magnitude;
            if (distance > MaxSweepMetres)
            {
                // Work only the far end rather than a stripe across the county.
                from = to - travel.normalized * MaxSweepMetres;
                distance = MaxSweepMetres;
            }

            // The implement hangs square across the direction it is pointing, which is
            // the machine's heading rather than its direction of travel - reversing
            // drags the same width, it does not turn the implement round.
            float radians = headingDegrees * Mathf.Deg2Rad;
            var right = new Vector3(Mathf.Cos(radians), 0f, -Mathf.Sin(radians));

            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / StepMetres));
            int across = Mathf.Max(1, Mathf.CeilToInt(width / StepMetres));

            for (int s = 0; s <= steps; s++)
            {
                Vector3 centre = Vector3.Lerp(from, to, steps == 0 ? 0f : s / (float)steps);

                for (int a = 0; a <= across; a++)
                {
                    float offset = Mathf.Lerp(-width * 0.5f, width * 0.5f, across == 0 ? 0.5f : a / (float)across);
                    var cell = FieldGrid.CellOf(centre + right * offset);

                    if (!Contains(into, cell)) into.Add(cell);
                }
            }

            return into.Count;
        }

        /// <summary>
        /// Linear scan rather than a HashSet: a swath is tens of cells, not thousands,
        /// and this runs every physics tick on a machine that is already moving. A set
        /// would allocate more than it saves at this size.
        /// </summary>
        static bool Contains(List<Vector2Int> cells, Vector2Int cell)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] == cell) return true;
            }
            return false;
        }

        /// <summary>
        /// How many cells wide a swath of this width is, for the implement readout.
        /// </summary>
        public static int CellsWide(float width)
        {
            return Mathf.Max(1, Mathf.RoundToInt(width / FieldGrid.CellSize));
        }
    }
}
