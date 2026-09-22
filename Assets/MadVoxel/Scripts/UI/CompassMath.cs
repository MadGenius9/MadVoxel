using UnityEngine;

namespace MadVoxel.UI
{
    /// <summary>
    /// The compass arithmetic, kept away from the canvas so it can be checked without
    /// one. Bearings are the part that is silently wrong by ninety degrees, and a
    /// compass that lies is worse than no compass.
    /// </summary>
    public static class CompassMath
    {
        /// <summary>
        /// Compass bearing from one world point to another, in degrees, matching Unity's
        /// yaw: 0 is +Z (north), 90 is +X (east). Returns 0 for a point on top of you.
        /// </summary>
        public static float Bearing(Vector3 from, Vector3 to)
        {
            float dx = to.x - from.x;
            float dz = to.z - from.z;
            if (dx * dx + dz * dz < 0.0001f) return 0f;

            return Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Signed degrees from where the player is looking to a bearing, wrapped to
        /// (-180, 180]. Positive is to the right.
        /// </summary>
        public static float Offset(float headingDegrees, float bearingDegrees)
        {
            return Mathf.DeltaAngle(headingDegrees, bearingDegrees);
        }

        /// <summary>
        /// Where a bearing lands on the tape, or false when it falls outside the arc the
        /// strip shows. The caller hides the marker rather than clamping it to the edge -
        /// a pip pinned to the end of the tape is a lie about direction.
        /// </summary>
        public static bool TryPlace(float headingDegrees, float bearingDegrees,
                                    float visibleDegrees, float pixelsPerDegree, out float x)
        {
            float offset = Offset(headingDegrees, bearingDegrees);
            if (Mathf.Abs(offset) > visibleDegrees * 0.5f)
            {
                x = 0f;
                return false;
            }

            x = offset * pixelsPerDegree;
            return true;
        }
    }
}
