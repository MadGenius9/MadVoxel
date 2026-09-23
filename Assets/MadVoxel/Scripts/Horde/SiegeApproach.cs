using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Horde
{
    /// <summary>One way in, and how hard it looks from outside.</summary>
    public struct SiegeLane
    {
        /// <summary>Where on the ring the lane starts. Zombies are sent here first.</summary>
        public Vector3 Point;

        /// <summary>Blocking metres between the ring and the base. Zero is a clear run.</summary>
        public int Blocks;

        /// <summary>How likely this lane is to be chosen. Never zero.</summary>
        public float Weight;
    }

    /// <summary>
    /// Where a horde comes from.
    ///
    /// Every horde zombie used to walk at the same point - the tool cupboard - and chew
    /// through whatever happened to be in the way. That meant the strongest wall took
    /// the same beating as the doorway ten metres along it, and nothing a player built
    /// or dug changed where the pressure landed. A base was a health pool, not a shape.
    ///
    /// So the ring around a claim is sampled into lanes, each one scored by what
    /// actually stands between it and the base, and the wave is spread across them by
    /// weight. Leave a ramp up out of your trench and they will use the ramp. Leave a
    /// doorway and they will find the doorway. Wall yourself in completely and they
    /// will still come - at the thinnest place, and slowly - because a base you can
    /// turtle in forever is a base with nothing to defend.
    ///
    /// Pure and engine-free: the caller says what counts as blocking, so the same
    /// scoring works against voxels, against build pieces, and against a test's idea of
    /// a wall.
    /// </summary>
    public static class SiegeApproach
    {
        /// <summary>Lanes sampled around a claim. Enough to find a gap, few enough to stay cheap.</summary>
        public const int DefaultLanes = 12;

        /// <summary>
        /// How sharply a clear lane is preferred. Squared, so one open doorway pulls
        /// most of a wave without making a walled approach impossible.
        /// </summary>
        const float Sharpness = 2f;

        /// <summary>
        /// Samples the ring and scores each lane.
        ///
        /// <paramref name="countBlocking"/> is given the outside point and the centre
        /// and returns how many metres of the run between them are obstructed.
        /// </summary>
        public static int Plan(Vector3 centre, float radius, int lanes,
                               System.Func<Vector3, Vector3, int> countBlocking, List<SiegeLane> into)
        {
            if (into == null) return 0;
            into.Clear();

            lanes = Mathf.Max(1, lanes);
            radius = Mathf.Max(1f, radius);

            for (int i = 0; i < lanes; i++)
            {
                float angle = i / (float)lanes * Mathf.PI * 2f;
                var point = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                int blocks = countBlocking != null ? Mathf.Max(0, countBlocking(point, centre)) : 0;

                into.Add(new SiegeLane
                {
                    Point = point,
                    Blocks = blocks,
                    Weight = WeightFor(blocks)
                });
            }

            return into.Count;
        }

        /// <summary>
        /// A lane's pull. Never zero - a fully enclosed base still gets attacked, at its
        /// thinnest wall - and falls off fast enough that one gap dominates.
        /// </summary>
        public static float WeightFor(int blocks)
        {
            float clearance = 1f + Mathf.Max(0, blocks);
            return 1f / Mathf.Pow(clearance, Sharpness);
        }

        /// <summary>
        /// Picks a lane by weight. <paramref name="roll"/> is 0..1, so the caller owns
        /// the randomness and a test can ask for a specific outcome.
        /// </summary>
        public static int Pick(IList<SiegeLane> lanes, float roll)
        {
            if (lanes == null || lanes.Count == 0) return -1;

            float total = 0f;
            for (int i = 0; i < lanes.Count; i++) total += Mathf.Max(0f, lanes[i].Weight);

            if (total <= 0f) return Mathf.Clamp(Mathf.FloorToInt(roll * lanes.Count), 0, lanes.Count - 1);

            float target = Mathf.Clamp01(roll) * total;
            float running = 0f;

            for (int i = 0; i < lanes.Count; i++)
            {
                running += Mathf.Max(0f, lanes[i].Weight);
                if (target <= running) return i;
            }

            return lanes.Count - 1;
        }

        /// <summary>The clearest way in, for the readout and for tests.</summary>
        public static int Easiest(IList<SiegeLane> lanes)
        {
            if (lanes == null || lanes.Count == 0) return -1;

            int best = 0;
            for (int i = 1; i < lanes.Count; i++)
            {
                if (lanes[i].Blocks < lanes[best].Blocks) best = i;
            }
            return best;
        }

        /// <summary>
        /// True when the base has a way in that needs no chewing at all. The director
        /// uses it to decide whether the wave is a breach or a siege.
        /// </summary>
        public static bool HasOpenLane(IList<SiegeLane> lanes)
        {
            if (lanes == null) return false;

            for (int i = 0; i < lanes.Count; i++)
            {
                if (lanes[i].Blocks <= 0) return true;
            }
            return false;
        }

        /// <summary>
        /// What fraction of a wave the clearest lane should draw. Only used for the
        /// horde readout, but it is the number that tells a player whether their wall
        /// is doing anything.
        /// </summary>
        public static float ShareOfEasiest(IList<SiegeLane> lanes)
        {
            if (lanes == null || lanes.Count == 0) return 0f;

            float total = 0f;
            for (int i = 0; i < lanes.Count; i++) total += Mathf.Max(0f, lanes[i].Weight);
            if (total <= 0f) return 0f;

            int best = Easiest(lanes);
            return Mathf.Clamp01(lanes[best].Weight / total);
        }
    }
}
