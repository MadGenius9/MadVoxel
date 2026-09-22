using System.Collections.Generic;
using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.World.Terrain
{
    public enum PoiKind
    {
        FarmRuin,
        TownFragment,
        TraderOutpost
    }

    public struct Poi
    {
        public PoiKind Kind;
        public int CentreX;
        public int CentreZ;
        public int HalfX;
        public int HalfZ;
        /// <summary>Flattened ground level. Everything is stamped from here upward.</summary>
        public int PadY;
        public int Variant;

        public int MinX { get { return CentreX - HalfX; } }
        public int MaxX { get { return CentreX + HalfX; } }
        public int MinZ { get { return CentreZ - HalfZ; } }
        public int MaxZ { get { return CentreZ + HalfZ; } }

        public bool Contains(int wx, int wz)
        {
            return wx >= MinX && wx <= MaxX && wz >= MinZ && wz <= MaxZ;
        }

        /// <summary>The pad is wider than the build so the approach is level too.</summary>
        public bool ContainsPad(int wx, int wz, int margin)
        {
            return wx >= MinX - margin && wx <= MaxX + margin
                && wz >= MinZ - margin && wz <= MaxZ + margin;
        }
    }

    /// <summary>
    /// Decides where the points of interest sit. The map is finite, so the whole list is
    /// computed once up front and then read from worker threads - no locking, and the
    /// same seed always lays the world out the same way.
    /// </summary>
    public class PoiPlanner
    {
        public const int CellSize = 384;
        public const int PadMargin = 3;

        readonly Dictionary<long, Poi> _byCell = new Dictionary<long, Poi>();
        readonly List<Poi> _all = new List<Poi>();
        readonly List<Poi> _traders = new List<Poi>();

        public IReadOnlyList<Poi> All { get { return _all; } }
        public IReadOnlyList<Poi> TraderOutposts { get { return _traders; } }

        public PoiPlanner(int seed, int worldExtentMetres, System.Func<int, int, int> baseHeight)
        {
            int cells = Mathf.Max(1, worldExtentMetres / CellSize);

            // Two trader outposts, placed first so nothing else can take their cells.
            PlaceTraders(seed, cells, baseHeight);

            for (int cz = -cells; cz < cells; cz++)
            {
                for (int cx = -cells; cx < cells; cx++)
                {
                    long key = Key(cx, cz);
                    if (_byCell.ContainsKey(key)) continue;

                    uint roll = Noise.Hash(cx, 77, cz, seed + 5309);
                    // Roughly a third of cells carry something; the rest stay wild.
                    uint bucket = roll % 100u;
                    if (bucket >= 34u) continue;

                    var kind = bucket < 24u ? PoiKind.FarmRuin : PoiKind.TownFragment;
                    var poi = Make(kind, cx, cz, seed, baseHeight);
                    _byCell.Add(key, poi);
                    _all.Add(poi);
                }
            }
        }

        void PlaceTraders(int seed, int cells, System.Func<int, int, int> baseHeight)
        {
            // Two outposts, on opposite sides of spawn and far enough out to be a trip.
            for (int i = 0; i < 2; i++)
            {
                uint h = Noise.Hash(i * 31, 991, i * 17, seed + 7717);
                float angle = (i * Mathf.PI) + (h % 1000u) / 1000f * 1.2f - 0.6f;

                int ringCells = Mathf.Clamp(cells / 2, 1, Mathf.Max(1, cells - 1));
                int cx = Mathf.RoundToInt(Mathf.Cos(angle) * ringCells);
                int cz = Mathf.RoundToInt(Mathf.Sin(angle) * ringCells);

                // Never sit on spawn.
                if (cx == 0 && cz == 0) cx = 1;

                long key = Key(cx, cz);
                if (_byCell.ContainsKey(key)) continue;

                var poi = Make(PoiKind.TraderOutpost, cx, cz, seed, baseHeight);
                _byCell.Add(key, poi);
                _all.Add(poi);
                _traders.Add(poi);
            }
        }

        Poi Make(PoiKind kind, int cellX, int cellZ, int seed, System.Func<int, int, int> baseHeight)
        {
            uint h = Noise.Hash(cellX, 13, cellZ, seed + 8831);

            // Jitter inside the cell so the grid never reads as a grid.
            int jitterX = (int)(h % 120u) - 60;
            int jitterZ = (int)((h >> 8) % 120u) - 60;

            int centreX = cellX * CellSize + CellSize / 2 + jitterX;
            int centreZ = cellZ * CellSize + CellSize / 2 + jitterZ;

            int halfX, halfZ;
            switch (kind)
            {
                case PoiKind.TownFragment: halfX = 19; halfZ = 19; break;
                case PoiKind.TraderOutpost: halfX = 14; halfZ = 14; break;
                default: halfX = 11; halfZ = 8; break;
            }

            return new Poi
            {
                Kind = kind,
                CentreX = centreX,
                CentreZ = centreZ,
                HalfX = halfX,
                HalfZ = halfZ,
                PadY = baseHeight(centreX, centreZ),
                Variant = (int)((h >> 16) % 4u)
            };
        }

        static long Key(int cellX, int cellZ)
        {
            return ((long)cellX << 32) ^ (uint)cellZ;
        }

        /// <summary>The POI whose pad covers this column, if any. Thread-safe after construction.</summary>
        public bool TryGetAt(int wx, int wz, out Poi poi)
        {
            // A POI can spill past its own cell, so check the neighbours too.
            int cellX = Mathf.FloorToInt(wx / (float)CellSize);
            int cellZ = Mathf.FloorToInt(wz / (float)CellSize);

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    Poi candidate;
                    if (!_byCell.TryGetValue(Key(cellX + dx, cellZ + dz), out candidate)) continue;
                    if (candidate.ContainsPad(wx, wz, PadMargin))
                    {
                        poi = candidate;
                        return true;
                    }
                }
            }

            poi = default(Poi);
            return false;
        }

        /// <summary>Every POI whose footprint touches a chunk-sized window.</summary>
        public void Overlapping(int minX, int minZ, int maxX, int maxZ, List<Poi> results)
        {
            int cellMinX = Mathf.FloorToInt((minX - PadMargin) / (float)CellSize) - 1;
            int cellMaxX = Mathf.FloorToInt((maxX + PadMargin) / (float)CellSize) + 1;
            int cellMinZ = Mathf.FloorToInt((minZ - PadMargin) / (float)CellSize) - 1;
            int cellMaxZ = Mathf.FloorToInt((maxZ + PadMargin) / (float)CellSize) + 1;

            for (int cz = cellMinZ; cz <= cellMaxZ; cz++)
            {
                for (int cx = cellMinX; cx <= cellMaxX; cx++)
                {
                    Poi poi;
                    if (!_byCell.TryGetValue(Key(cx, cz), out poi)) continue;
                    if (poi.MaxX + PadMargin < minX || poi.MinX - PadMargin > maxX) continue;
                    if (poi.MaxZ + PadMargin < minZ || poi.MinZ - PadMargin > maxZ) continue;
                    results.Add(poi);
                }
            }
        }

        public bool TryFindNearestTrader(Vector3 from, out Poi poi)
        {
            poi = default(Poi);
            if (_traders.Count == 0) return false;

            float best = float.MaxValue;
            for (int i = 0; i < _traders.Count; i++)
            {
                float dx = _traders[i].CentreX - from.x;
                float dz = _traders[i].CentreZ - from.z;
                float d2 = dx * dx + dz * dz;
                if (d2 >= best) continue;
                best = d2;
                poi = _traders[i];
            }
            return true;
        }
    }
}
