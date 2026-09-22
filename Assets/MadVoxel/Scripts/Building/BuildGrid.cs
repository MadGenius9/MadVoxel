using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Building
{
    /// <summary>Which part of a building cell a piece occupies.</summary>
    public enum BuildSlot
    {
        /// <summary>Foundation or floor: the horizontal surface at the bottom of a cell.</summary>
        Floor,
        /// <summary>A vertical panel on one of the cell's four edges.</summary>
        Wall,
        /// <summary>A roof or ceiling capping the cell.</summary>
        Ceiling,
        /// <summary>Stairs and other things filling the volume inside a cell.</summary>
        Interior,
        /// <summary>A ladder or similar hung on the inside face of a wall edge.</summary>
        Attachment
    }

    /// <summary>
    /// The building grid: 3 m cells in X and Z, free integer metres in Y.
    ///
    /// Cells are 3 m because that is the Rust foundation footprint and it makes walls,
    /// doorways and stairs read at the right scale. Y is left at 1 m so a foundation can
    /// sit on any height the player digs or flattens the terrain to - the terrain is a
    /// 1 m voxel volume and forcing building levels onto a 3 m vertical grid would make
    /// most dug pads unusable.
    /// </summary>
    public static class BuildGrid
    {
        public const float CellSize = 3f;
        /// <summary>Vertical step between stacked storeys.</summary>
        public const int LevelHeight = 3;

        public const int SideNorth = 0; // -Z
        public const int SideEast = 1;  // +X
        public const int SideSouth = 2; // +Z
        public const int SideWest = 3;  // -X

        public static int CellIndex(float world)
        {
            return Mathf.FloorToInt(world / CellSize);
        }

        public static Vector2Int CellOf(Vector3 world)
        {
            return new Vector2Int(CellIndex(world.x), CellIndex(world.z));
        }

        /// <summary>World position of the cell's minimum corner at the given level.</summary>
        public static Vector3 CellOrigin(int cellX, int y, int cellZ)
        {
            return new Vector3(cellX * CellSize, y, cellZ * CellSize);
        }

        public static Vector3 CellCentre(int cellX, int y, int cellZ)
        {
            return new Vector3((cellX + 0.5f) * CellSize, y, (cellZ + 0.5f) * CellSize);
        }

        public static Vector2Int Neighbour(int cellX, int cellZ, int side)
        {
            switch (side)
            {
                case SideNorth: return new Vector2Int(cellX, cellZ - 1);
                case SideEast: return new Vector2Int(cellX + 1, cellZ);
                case SideSouth: return new Vector2Int(cellX, cellZ + 1);
                default: return new Vector2Int(cellX - 1, cellZ);
            }
        }

        public static int OppositeSide(int side)
        {
            return (side + 2) % 4;
        }

        /// <summary>Yaw that points a piece's local +Z outward along the given side.</summary>
        public static float SideYaw(int side)
        {
            switch (side)
            {
                case SideNorth: return 180f;
                case SideEast: return 90f;
                case SideSouth: return 0f;
                default: return 270f;
            }
        }

        /// <summary>Which edge of a cell a world point is closest to.</summary>
        public static int NearestSide(int cellX, int cellZ, Vector3 world)
        {
            float localX = world.x - cellX * CellSize;
            float localZ = world.z - cellZ * CellSize;

            float toWest = localX;
            float toEast = CellSize - localX;
            float toNorth = localZ;
            float toSouth = CellSize - localZ;

            float best = toNorth;
            int side = SideNorth;
            if (toEast < best) { best = toEast; side = SideEast; }
            if (toSouth < best) { best = toSouth; side = SideSouth; }
            if (toWest < best) { side = SideWest; }
            return side;
        }

        /// <summary>
        /// Every slot a piece at this address touches. Connections are symmetric, so the
        /// flood fill can walk them in either direction.
        /// </summary>
        public static void EnumerateConnections(BuildAddress address, List<BuildAddress> results)
        {
            int x = address.X, y = address.Y, z = address.Z;
            int level = BuildGrid.LevelHeight;

            switch (address.Slot)
            {
                case BuildSlot.Floor:
                    // Walls standing on this floor, and walls underneath holding it up.
                    for (int side = 0; side < 4; side++)
                    {
                        results.Add(new BuildAddress(x, y, z, BuildSlot.Wall, side));
                        results.Add(new BuildAddress(x, y - level, z, BuildSlot.Wall, side));
                    }
                    // Neighbouring floors on the same level.
                    results.Add(new BuildAddress(x + 1, y, z, BuildSlot.Floor));
                    results.Add(new BuildAddress(x - 1, y, z, BuildSlot.Floor));
                    results.Add(new BuildAddress(x, y, z + 1, BuildSlot.Floor));
                    results.Add(new BuildAddress(x, y, z - 1, BuildSlot.Floor));
                    // Things sitting in or over this cell, and the stairs in the storey
                    // below that land on this floor.
                    results.Add(new BuildAddress(x, y, z, BuildSlot.Ceiling));
                    results.Add(new BuildAddress(x, y, z, BuildSlot.Interior));
                    results.Add(new BuildAddress(x, y - level, z, BuildSlot.Interior));
                    break;

                case BuildSlot.Wall:
                {
                    Vector2Int cellA, cellB;
                    address.EdgeCells(out cellA, out cellB);

                    // Floors this wall stands on, and floors it holds up.
                    results.Add(new BuildAddress(cellA.x, y, cellA.y, BuildSlot.Floor));
                    results.Add(new BuildAddress(cellB.x, y, cellB.y, BuildSlot.Floor));
                    results.Add(new BuildAddress(cellA.x, y + level, cellA.y, BuildSlot.Floor));
                    results.Add(new BuildAddress(cellB.x, y + level, cellB.y, BuildSlot.Floor));

                    // Walls stacked directly above and below this one.
                    results.Add(new BuildAddress(x, y + level, z, BuildSlot.Wall, address.Side));
                    results.Add(new BuildAddress(x, y - level, z, BuildSlot.Wall, address.Side));

                    // Walls meeting this one at a corner of either cell.
                    for (int side = 0; side < 4; side++)
                    {
                        results.Add(new BuildAddress(cellA.x, y, cellA.y, BuildSlot.Wall, side));
                        results.Add(new BuildAddress(cellB.x, y, cellB.y, BuildSlot.Wall, side));
                    }

                    // Roofs resting on this wall, on either side of it.
                    results.Add(new BuildAddress(cellA.x, y, cellA.y, BuildSlot.Ceiling));
                    results.Add(new BuildAddress(cellB.x, y, cellB.y, BuildSlot.Ceiling));

                    // Whatever is mounted on this wall.
                    results.Add(new BuildAddress(x, y, z, BuildSlot.Attachment, address.Side));
                    break;
                }

                case BuildSlot.Ceiling:
                    results.Add(new BuildAddress(x, y, z, BuildSlot.Floor));
                    for (int side = 0; side < 4; side++) results.Add(new BuildAddress(x, y, z, BuildSlot.Wall, side));
                    break;

                case BuildSlot.Interior:
                    results.Add(new BuildAddress(x, y, z, BuildSlot.Floor));
                    results.Add(new BuildAddress(x, y + BuildGrid.LevelHeight, z, BuildSlot.Floor));
                    break;

                case BuildSlot.Attachment:
                    results.Add(new BuildAddress(x, y, z, BuildSlot.Wall, address.Side));
                    break;
            }

            // A slot never connects to itself.
            for (int i = results.Count - 1; i >= 0; i--)
            {
                if (results[i].Equals(address)) results.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Identifies one placement slot. Wall and attachment addresses are canonicalised so
    /// the shared edge between two cells is a single slot rather than two overlapping
    /// walls.
    /// </summary>
    [Serializable]
    public struct BuildAddress : IEquatable<BuildAddress>
    {
        public int X;
        public int Y;
        public int Z;
        public BuildSlot Slot;
        public int Side;

        public BuildAddress(int x, int y, int z, BuildSlot slot, int side = 0)
        {
            X = x; Y = y; Z = z; Slot = slot; Side = side;
            this = Canonical(this);
        }

        /// <summary>
        /// Edges are shared: cell (x,z) side East is the same physical edge as cell
        /// (x+1,z) side West. Collapsing to the north and west sides keeps one slot per
        /// edge, so two players- or two placements- cannot stack walls in the same gap.
        /// </summary>
        public static BuildAddress Canonical(BuildAddress a)
        {
            if (a.Slot != BuildSlot.Wall && a.Slot != BuildSlot.Attachment) { a.Side = 0; return a; }

            if (a.Side == BuildGrid.SideEast)
            {
                a.X += 1;
                a.Side = BuildGrid.SideWest;
            }
            else if (a.Side == BuildGrid.SideSouth)
            {
                a.Z += 1;
                a.Side = BuildGrid.SideNorth;
            }
            return a;
        }

        public Vector2Int Cell { get { return new Vector2Int(X, Z); } }

        /// <summary>The two cells an edge slot sits between; both are the same for other slots.</summary>
        public void EdgeCells(out Vector2Int a, out Vector2Int b)
        {
            a = new Vector2Int(X, Z);
            if (Slot != BuildSlot.Wall && Slot != BuildSlot.Attachment) { b = a; return; }
            b = BuildGrid.Neighbour(X, Z, Side);
        }

        public BuildAddress WithSlot(BuildSlot slot, int side = 0)
        {
            return new BuildAddress(X, Y, Z, slot, side);
        }

        public BuildAddress AtLevel(int y)
        {
            var copy = this;
            copy.Y = y;
            return copy;
        }

        public bool Equals(BuildAddress other)
        {
            return X == other.X && Y == other.Y && Z == other.Z && Slot == other.Slot && Side == other.Side;
        }

        public override bool Equals(object obj)
        {
            return obj is BuildAddress && Equals((BuildAddress)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                uint h = (uint)X * 2654435761u;
                h ^= (uint)Y * 2246822519u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)Z * 3266489917u;
                h ^= (uint)((int)Slot * 31 + Side) * 668265263u;
                h ^= h >> 15;
                h *= 2654435761u;
                h ^= h >> 13;
                return (int)h;
            }
        }

        public override string ToString()
        {
            return string.Format("{0} ({1},{2},{3}) side {4}", Slot, X, Y, Z, Side);
        }
    }
}
