using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.Building
{
    /// <summary>
    /// Turns "where the player is looking" into a grid slot.
    ///
    /// Foundations snap to the terrain height under the crosshair, which is why the loop
    /// is dig-and-flatten first: an uneven pad gives you foundations at mismatched
    /// levels. Everything else snaps off the piece already under the crosshair.
    /// </summary>
    public static class BuildPlacementSolver
    {
        public static bool TryResolve(BuildingWorld buildings, TerrainWorld terrain,
                                      BuildPieceDefinition def, Vector3 hitPoint, Vector3 hitNormal,
                                      BuildPiece hitPiece, out BuildAddress address)
        {
            address = default(BuildAddress);
            if (def == null) return false;

            var cell = BuildGrid.CellOf(hitPoint);


            switch (def.slot)
            {
                case BuildSlot.Floor:
                    return ResolveFloor(buildings, terrain, def, hitPoint, hitNormal, hitPiece, cell, out address);

                case BuildSlot.Wall:
                case BuildSlot.Attachment:
                    return ResolveEdge(buildings, def, hitPoint, hitPiece, cell, out address);

                case BuildSlot.Ceiling:
                case BuildSlot.Interior:
                    return ResolveInCell(buildings, def, hitPoint, hitPiece, cell, out address);
            }
            return false;
        }

        static bool ResolveFloor(BuildingWorld buildings, TerrainWorld terrain, BuildPieceDefinition def,
                                 Vector3 hitPoint, Vector3 hitNormal, BuildPiece hitPiece,
                                 Vector2Int cell, out BuildAddress address)
        {
            address = default(BuildAddress);

            if (hitPiece != null)
            {
                var hitAddress = hitPiece.Address;

                // Looking at a wall: the floor goes on top of it, in whichever cell the
                // player is aiming into.
                if (hitPiece.Definition.slot == BuildSlot.Wall)
                {
                    address = new BuildAddress(cell.x, hitAddress.Y + BuildGrid.LevelHeight, cell.y, BuildSlot.Floor);
                    return true;
                }

                // Looking at a floor: extend the deck sideways into the neighbouring cell.
                if (hitPiece.Definition.slot == BuildSlot.Floor)
                {
                    var target = cell;
                    if (target.x == hitAddress.X && target.y == hitAddress.Z)
                    {
                        // Aiming at the middle of the slab - step off in the facing direction.
                        if (Mathf.Abs(hitNormal.x) > Mathf.Abs(hitNormal.z))
                            target.x += hitNormal.x > 0f ? 1 : -1;
                        else if (Mathf.Abs(hitNormal.z) > 0.01f)
                            target.y += hitNormal.z > 0f ? 1 : -1;
                    }
                    address = new BuildAddress(target.x, hitAddress.Y, target.y, BuildSlot.Floor);
                    return true;
                }

                address = new BuildAddress(cell.x, hitAddress.Y, cell.y, BuildSlot.Floor);
                return true;
            }

            if (!def.restsOnTerrain) return false;

            // On terrain: sit the slab on the surface the player is pointing at, and use
            // the highest column in the cell so a foundation never sinks into a lip.
            int baseY = Mathf.FloorToInt(hitPoint.y + hitNormal.y * 0.5f + 0.01f);
            int highest = baseY;
            var origin = BuildGrid.CellOrigin(cell.x, 0, cell.y);
            for (int dz = 0; dz < 3; dz++)
            {
                for (int dx = 0; dx < 3; dx++)
                {
                    int surface = terrain.GetSurfaceY(Mathf.FloorToInt(origin.x) + dx, Mathf.FloorToInt(origin.z) + dz);
                    if (surface + 1 > highest) highest = surface + 1;
                }
            }

            address = new BuildAddress(cell.x, highest, cell.y, BuildSlot.Floor);
            return true;
        }

        static bool ResolveEdge(BuildingWorld buildings, BuildPieceDefinition def, Vector3 hitPoint,
                                BuildPiece hitPiece, Vector2Int cell, out BuildAddress address)
        {
            address = default(BuildAddress);

            // Mounting onto an existing wall (a door into a doorway, a ladder onto a wall).
            if (def.requiresHost)
            {
                if (hitPiece == null || hitPiece.Definition.slot != BuildSlot.Wall) return false;
                address = new BuildAddress(hitPiece.Address.X, hitPiece.Address.Y, hitPiece.Address.Z,
                                           def.slot, hitPiece.Address.Side);
                return true;
            }

            // Stacking a wall directly on the wall being looked at.
            if (hitPiece != null && hitPiece.Definition.slot == BuildSlot.Wall)
            {
                int level = hitPoint.y > hitPiece.Address.Y + BuildGrid.LevelHeight * 0.6f
                    ? hitPiece.Address.Y + BuildGrid.LevelHeight
                    : hitPiece.Address.Y;
                address = new BuildAddress(hitPiece.Address.X, level, hitPiece.Address.Z, def.slot, hitPiece.Address.Side);
                return true;
            }

            // Otherwise the wall goes on the nearest edge of whichever floor is under the
            // crosshair, which is how you wall in a foundation you just placed.
            var floor = buildings.FindFloorAtOrBelow(cell.x, cell.y, hitPoint.y + 0.5f);
            if (floor == null) return false;

            int side = BuildGrid.NearestSide(cell.x, cell.y, hitPoint);
            address = new BuildAddress(cell.x, floor.Address.Y, cell.y, def.slot, side);
            return true;
        }

        static bool ResolveInCell(BuildingWorld buildings, BuildPieceDefinition def, Vector3 hitPoint,
                                  BuildPiece hitPiece, Vector2Int cell, out BuildAddress address)
        {
            address = default(BuildAddress);

            var floor = buildings.FindFloorAtOrBelow(cell.x, cell.y, hitPoint.y + 0.5f);
            if (floor == null)
            {
                if (hitPiece == null) return false;
                address = new BuildAddress(cell.x, hitPiece.Address.Y, cell.y, def.slot);
                return true;
            }

            address = new BuildAddress(cell.x, floor.Address.Y, cell.y, def.slot);
            return true;
        }

        /// <summary>World-space box a ghost should occupy for this slot, for the preview.</summary>
        public static void GhostBounds(BuildPieceDefinition def, BuildAddress address, out Vector3 centre, out Vector3 size)
        {
            var origin = BuildGrid.CellOrigin(address.X, address.Y, address.Z);
            float cell = BuildGrid.CellSize;
            float level = BuildGrid.LevelHeight;

            switch (def.slot)
            {
                case BuildSlot.Floor:
                    centre = origin + new Vector3(cell * 0.5f, -0.2f, cell * 0.5f);
                    size = new Vector3(cell, 0.4f, cell);
                    return;

                case BuildSlot.Ceiling:
                    centre = origin + new Vector3(cell * 0.5f, level + 0.15f, cell * 0.5f);
                    size = new Vector3(cell, 0.3f, cell);
                    return;

                case BuildSlot.Interior:
                    centre = origin + new Vector3(cell * 0.5f, level * 0.5f, cell * 0.5f);
                    size = new Vector3(cell, level, cell);
                    return;

                default:
                {
                    float height = def.kind == BuildPieceKind.HalfWall ? level * 0.5f : level;
                    if (address.Side == BuildGrid.SideWest)
                    {
                        centre = origin + new Vector3(0.1f, height * 0.5f, cell * 0.5f);
                        size = new Vector3(0.28f, height, cell);
                    }
                    else
                    {
                        centre = origin + new Vector3(cell * 0.5f, height * 0.5f, 0.1f);
                        size = new Vector3(cell, height, 0.28f);
                    }
                    return;
                }
            }
        }
    }
}
