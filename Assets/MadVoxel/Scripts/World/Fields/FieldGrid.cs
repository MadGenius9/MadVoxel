using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.World.Fields
{
    /// <summary>
    /// The FS25-style acreage layer: a sparse one-metre cell grid laid over the terrain.
    ///
    /// It is deliberately plain C# and holds no Unity references, so the tractor and
    /// implements Phase 1 adds are just callers that work many cells at once - the state
    /// machine, the growth timing and the litre yield all already live here and are
    /// exercised by a single hoe today.
    /// </summary>
    public class FieldGrid
    {
        /// <summary>Cells are one metre, matching the terrain voxel footprint.</summary>
        public const float CellSize = 1f;

        readonly Dictionary<long, FieldCell> _cells = new Dictionary<long, FieldCell>();

        public int WorkedCellCount { get { return _cells.Count; } }

        public static long Key(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }

        public static Vector2Int CellOf(Vector3 world)
        {
            return new Vector2Int(Mathf.FloorToInt(world.x / CellSize), Mathf.FloorToInt(world.z / CellSize));
        }

        /// <summary>Unworked ground is not stored, so anything unknown reads as wild.</summary>
        public FieldCell Get(int x, int z)
        {
            FieldCell cell;
            return _cells.TryGetValue(Key(x, z), out cell) ? cell : FieldCell.Wild;
        }

        public void Set(int x, int z, FieldCell cell)
        {
            if (cell.State == FieldCellState.Wild)
            {
                _cells.Remove(Key(x, z));
                return;
            }
            _cells[Key(x, z)] = cell;
        }

        public bool IsWorked(int x, int z)
        {
            return _cells.ContainsKey(Key(x, z));
        }

        // ------------------------------------------------------------ operations

        /// <summary>Breaks wild ground or stubble into plowed soil. Returns false if it was already plowed.</summary>
        public bool Plow(int x, int z, double nowHours)
        {
            var cell = Get(x, z);
            if (cell.State != FieldCellState.Wild && cell.State != FieldCellState.Stubble && cell.State != FieldCellState.Plowed)
                return false;
            if (cell.State == FieldCellState.Plowed) return false;

            cell.State = FieldCellState.Plowed;
            cell.CropIndex = 0;
            cell.ChangedAtHours = nowHours;
            // Turning stubble back in returns a little fertility.
            cell.Fertiliser = Mathf.Clamp01(cell.Fertiliser + 0.1f);
            Set(x, z, cell);
            return true;
        }

        /// <summary>Second pass: breaks the clods down so a seeder can work.</summary>
        public bool Cultivate(int x, int z, double nowHours)
        {
            var cell = Get(x, z);
            if (cell.State != FieldCellState.Plowed && cell.State != FieldCellState.Stubble) return false;

            cell.State = FieldCellState.Cultivated;
            cell.ChangedAtHours = nowHours;
            Set(x, z, cell);
            return true;
        }

        public bool Sow(int x, int z, byte cropIndex, double nowHours)
        {
            if (cropIndex == 0) return false;

            var cell = Get(x, z);
            if (cell.State != FieldCellState.Cultivated && cell.State != FieldCellState.Plowed) return false;

            cell.State = FieldCellState.Seeded;
            cell.CropIndex = cropIndex;
            cell.ChangedAtHours = nowHours;
            Set(x, z, cell);
            return true;
        }

        /// <summary>
        /// Advances sown cells. Growth is derived from the clock rather than ticked, so a
        /// field matures correctly across a save and reload however long it was closed.
        /// </summary>
        public void Refresh(int x, int z, double nowHours, float hoursToMature)
        {
            var cell = Get(x, z);
            if (cell.State != FieldCellState.Seeded && cell.State != FieldCellState.Growing) return;

            double grown = nowHours - cell.ChangedAtHours;
            if (grown <= 0.0) return;

            float progress = Mathf.Clamp01((float)(grown / Mathf.Max(0.1f, hoursToMature)));
            var next = progress >= 1f
                ? FieldCellState.Ready
                : (progress >= 0.25f ? FieldCellState.Growing : FieldCellState.Seeded);

            if (next == cell.State) return;

            // Keep ChangedAtHours pointing at the sowing, so progress stays monotonic.
            cell.State = next;
            _cells[Key(x, z)] = cell;
        }

        public float Progress01(int x, int z, double nowHours, float hoursToMature)
        {
            var cell = Get(x, z);
            if (!cell.HasCrop) return 0f;
            return Mathf.Clamp01((float)((nowHours - cell.ChangedAtHours) / Mathf.Max(0.1f, hoursToMature)));
        }

        /// <summary>Harvests a ready cell and returns the litres it yielded.</summary>
        public float Harvest(int x, int z, float litresPerCell, double nowHours, out byte cropIndex)
        {
            cropIndex = 0;

            var cell = Get(x, z);
            if (cell.State != FieldCellState.Ready) return 0f;

            cropIndex = cell.CropIndex;
            float litres = litresPerCell * Mathf.Max(0f, cell.YieldFactor)
                         * Mathf.Lerp(0.75f, 1.15f, cell.Fertiliser);

            cell.State = FieldCellState.Stubble;
            cell.CropIndex = 0;
            cell.ChangedAtHours = nowHours;
            // A crop takes fertility with it.
            cell.Fertiliser = Mathf.Clamp01(cell.Fertiliser - 0.3f);
            Set(x, z, cell);

            return litres;
        }

        /// <summary>Zombies trampling a field knock it back to stubble and cost the yield.</summary>
        public bool Trample(int x, int z, double nowHours)
        {
            var cell = Get(x, z);
            if (!cell.HasCrop) return false;

            cell.State = FieldCellState.Stubble;
            cell.CropIndex = 0;
            cell.ChangedAtHours = nowHours;
            cell.YieldFactor = Mathf.Max(0.4f, cell.YieldFactor - 0.15f);
            Set(x, z, cell);
            return true;
        }

        // ---------------------------------------------------------------- swaths

        /// <summary>
        /// Works a rectangle of cells. Implements in Phase 1 call this with their working
        /// width; the hoe calls the single-cell versions above.
        /// </summary>
        public int Apply(RectInt swath, System.Func<int, int, bool> operation)
        {
            int worked = 0;
            for (int z = swath.yMin; z < swath.yMax; z++)
            {
                for (int x = swath.xMin; x < swath.xMax; x++)
                {
                    if (operation(x, z)) worked++;
                }
            }
            return worked;
        }

        public IEnumerable<KeyValuePair<long, FieldCell>> Cells { get { return _cells; } }

        public void Clear()
        {
            _cells.Clear();
        }

        public static void Decode(long key, out int x, out int z)
        {
            x = (int)(key >> 32);
            z = (int)(key & 0xFFFFFFFFL);
        }
    }
}
