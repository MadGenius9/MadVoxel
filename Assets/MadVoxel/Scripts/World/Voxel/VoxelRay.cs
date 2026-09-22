using UnityEngine;

namespace MadVoxel.World.Voxel
{
    public struct VoxelHit
    {
        public bool Hit;
        public Vector3Int Block;
        /// <summary>The empty cell the ray came from - where a new block goes.</summary>
        public Vector3Int Adjacent;
        public Vector3Int Normal;
        public float Distance;
    }

    /// <summary>
    /// Amanatides &amp; Woo voxel traversal. Used by the AI for line of sight and as a
    /// fallback when a chunk collider has not been baked yet.
    /// </summary>
    public static class VoxelRay
    {
        public static VoxelHit Cast(VoxelWorld world, Vector3 origin, Vector3 direction, float maxDistance)
        {
            var result = new VoxelHit();
            if (direction.sqrMagnitude < 1e-6f) return result;
            direction.Normalize();

            int x = Mathf.FloorToInt(origin.x);
            int y = Mathf.FloorToInt(origin.y);
            int z = Mathf.FloorToInt(origin.z);

            int stepX = direction.x > 0f ? 1 : -1;
            int stepY = direction.y > 0f ? 1 : -1;
            int stepZ = direction.z > 0f ? 1 : -1;

            float tDeltaX = direction.x != 0f ? Mathf.Abs(1f / direction.x) : float.PositiveInfinity;
            float tDeltaY = direction.y != 0f ? Mathf.Abs(1f / direction.y) : float.PositiveInfinity;
            float tDeltaZ = direction.z != 0f ? Mathf.Abs(1f / direction.z) : float.PositiveInfinity;

            float tMaxX = Boundary(origin.x, x, stepX, direction.x);
            float tMaxY = Boundary(origin.y, y, stepY, direction.y);
            float tMaxZ = Boundary(origin.z, z, stepZ, direction.z);

            int lastX = x, lastY = y, lastZ = z;
            float travelled = 0f;

            // maxDistance voxels is the worst case for an axis-aligned ray.
            int guard = Mathf.CeilToInt(maxDistance * 3f) + 3;
            for (int i = 0; i < guard; i++)
            {
                if (VoxelWorld.InVerticalRange(y) && world.Registry.IsSolid(world.GetBlock(x, y, z)))
                {
                    result.Hit = true;
                    result.Block = new Vector3Int(x, y, z);
                    result.Adjacent = new Vector3Int(lastX, lastY, lastZ);
                    result.Normal = new Vector3Int(lastX - x, lastY - y, lastZ - z);
                    result.Distance = travelled;
                    return result;
                }

                lastX = x; lastY = y; lastZ = z;

                if (tMaxX < tMaxY && tMaxX < tMaxZ)
                {
                    travelled = tMaxX; x += stepX; tMaxX += tDeltaX;
                }
                else if (tMaxY < tMaxZ)
                {
                    travelled = tMaxY; y += stepY; tMaxY += tDeltaY;
                }
                else
                {
                    travelled = tMaxZ; z += stepZ; tMaxZ += tDeltaZ;
                }

                if (travelled > maxDistance) break;
            }

            return result;
        }

        static float Boundary(float origin, int cell, int step, float dir)
        {
            if (dir == 0f) return float.PositiveInfinity;
            float next = step > 0 ? cell + 1f : cell;
            return (next - origin) / dir;
        }

        /// <summary>True when nothing solid blocks the straight line between two points.</summary>
        public static bool HasLineOfSight(VoxelWorld world, Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float dist = delta.magnitude;
            if (dist < 0.01f) return true;
            var hit = Cast(world, from, delta / dist, dist);
            return !hit.Hit;
        }
    }
}
