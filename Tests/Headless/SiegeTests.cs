using System.Collections.Generic;
using MadVoxel.Horde;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// Where a horde comes from.
    ///
    /// The question every one of these asks is the same: does what the player built or
    /// dug change where the pressure lands? If it does not, a base is a health pool and
    /// the whole building layer is decoration.
    /// </summary>
    public static class SiegeTests
    {
        public static void Run()
        {
            Sampling();
            Scoring();
            Choosing();
            Shapes();
        }

        /// <summary>A wall everywhere: every lane costs the same to come through.</summary>
        static System.Func<Vector3, Vector3, int> Walled(int thickness)
        {
            return (from, to) => thickness;
        }

        /// <summary>
        /// A base walled all round except for one gap, at the given angle. This is a
        /// player who left a doorway.
        /// </summary>
        static System.Func<Vector3, Vector3, int> WithGapAt(Vector3 centre, float gapAngleDegrees, int wall)
        {
            return (from, to) =>
            {
                Vector3 offset = from - centre;
                float angle = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;

                float delta = Mathf.Abs(Mathf.DeltaAngle(angle, gapAngleDegrees));
                return delta < 20f ? 0 : wall;
            };
        }

        // ---------------------------------------------------------------- sampling

        static void Sampling()
        {
            Harness.Section("siege: sampling the ring");

            var lanes = new List<SiegeLane>();
            var centre = new Vector3(100f, 40f, -60f);

            int count = SiegeApproach.Plan(centre, 30f, 12, Walled(0), lanes);
            Harness.Equal(count, 12, "twelve lanes are sampled around a claim");

            // Every lane must sit on the ring, or a zombie is sent somewhere that is not
            // an approach at all.
            bool onRing = true;
            for (int i = 0; i < lanes.Count; i++)
            {
                Vector3 offset = lanes[i].Point - centre;
                offset.y = 0f;
                if (Mathf.Abs(offset.magnitude - 30f) > 0.01f) onRing = false;
            }
            Harness.Check(onRing, "and every one of them sits on the ring");

            // And they must be spread, not bunched.
            bool distinct = true;
            for (int i = 0; i < lanes.Count; i++)
            {
                for (int j = i + 1; j < lanes.Count; j++)
                {
                    if ((lanes[i].Point - lanes[j].Point).sqrMagnitude < 1f) distinct = false;
                }
            }
            Harness.Check(distinct, "no two lanes land on the same spot");

            Harness.Equal(SiegeApproach.Plan(centre, 30f, 0, Walled(0), lanes), 1,
                "asking for no lanes still gives one, so a wave always has somewhere to go");
            Harness.Equal(SiegeApproach.Plan(centre, 30f, 12, null, lanes), 12,
                "and a world that cannot be probed reads as open rather than crashing");
        }

        // ----------------------------------------------------------------- scoring

        static void Scoring()
        {
            Harness.Section("siege: what a wall is worth");

            Harness.Check(SiegeApproach.WeightFor(0) > SiegeApproach.WeightFor(1),
                "a clear lane pulls harder than a blocked one");
            Harness.Check(SiegeApproach.WeightFor(1) > SiegeApproach.WeightFor(8),
                "and a thin wall pulls harder than a thick one");

            // THE rule that keeps turtling from working. A base with no way in still
            // gets attacked, at its thinnest point, or a player can wall up on day
            // seven and never be threatened again.
            Harness.Check(SiegeApproach.WeightFor(50) > 0f,
                "no wall is ever thick enough to make a lane impossible");
            Harness.Check(SiegeApproach.WeightFor(9999) > 0f, "however thick");

            // Falls off fast enough that a gap matters, gently enough that a wall is
            // not simply ignored.
            float clear = SiegeApproach.WeightFor(0);
            float thin = SiegeApproach.WeightFor(2);
            Harness.Check(clear / thin > 4f, "an open lane is worth several walled ones");
            Harness.Check(clear / thin < 50f, "but a wall is not completely ignored");

            Harness.Equal(SiegeApproach.WeightFor(-5), SiegeApproach.WeightFor(0),
                "a negative count reads as open rather than infinite");
        }

        // ---------------------------------------------------------------- choosing

        static void Choosing()
        {
            Harness.Section("siege: choosing a way in");

            var lanes = new List<SiegeLane>();
            var centre = Vector3.zero;

            // All equal: the wave must spread rather than pile onto lane zero.
            SiegeApproach.Plan(centre, 20f, 8, Walled(3), lanes);

            var used = new HashSet<int>();
            for (int i = 0; i < 64; i++) used.Add(SiegeApproach.Pick(lanes, i / 64f));

            Harness.Check(used.Count >= 6,
                string.Format("with every side equal, a wave spreads over {0} of 8 lanes", used.Count));

            // One doorway: most of the wave must find it. This is the whole feature.
            SiegeApproach.Plan(centre, 20f, 12, WithGapAt(centre, 0f, 4), lanes);

            int open = SiegeApproach.Easiest(lanes);
            Harness.Equal(lanes[open].Blocks, 0, "the easiest lane is the one with no wall");
            Harness.Check(SiegeApproach.HasOpenLane(lanes), "and the base reads as breached");

            int throughGap = 0;
            for (int i = 0; i < 200; i++)
            {
                int picked = SiegeApproach.Pick(lanes, i / 200f);
                if (lanes[picked].Blocks == 0) throughGap++;
            }

            float share = throughGap / 200f;
            Harness.Check(share > 0.5f,
                string.Format("{0:0}% of the wave comes through the doorway", share * 100f));
            Harness.Check(share < 1f,
                "but not all of it - something always tries the wall, so a gap is not a safe funnel");

            Harness.Check(SiegeApproach.ShareOfEasiest(lanes) > 0.2f,
                "and the readout agrees the gap is where the pressure is");

            // Sealed: no open lane, but still a target.
            SiegeApproach.Plan(centre, 20f, 12, Walled(6), lanes);
            Harness.Check(!SiegeApproach.HasOpenLane(lanes), "a sealed base has no open lane");
            Harness.Check(SiegeApproach.Pick(lanes, 0.5f) >= 0, "and is still attacked somewhere");

            Harness.Equal(SiegeApproach.Pick(null, 0.5f), -1, "no lanes is no choice");
            Harness.Equal(SiegeApproach.Pick(new List<SiegeLane>(), 0.5f), -1, "nor an empty list");

            // The roll is the caller's, so the ends of it must be safe.
            SiegeApproach.Plan(centre, 20f, 5, Walled(1), lanes);
            Harness.Check(SiegeApproach.Pick(lanes, 0f) >= 0, "a roll of zero picks a lane");
            Harness.Check(SiegeApproach.Pick(lanes, 1f) >= 0, "and so does a roll of one");
            Harness.Check(SiegeApproach.Pick(lanes, 5f) < lanes.Count, "an out-of-range roll stays in bounds");
        }

        // ------------------------------------------------------------------ shapes

        /// <summary>
        /// The cases a player actually builds. Each one should move the pressure
        /// somewhere a person would expect it to go.
        /// </summary>
        static void Shapes()
        {
            Harness.Section("siege: the shapes people build");

            var lanes = new List<SiegeLane>();
            var centre = Vector3.zero;

            // A trench with one bridge left across it. The bridge is the way in.
            SiegeApproach.Plan(centre, 24f, 12, WithGapAt(centre, 90f, 5), lanes);

            int easiest = SiegeApproach.Easiest(lanes);
            Vector3 point = lanes[easiest].Point;
            float angle = Mathf.Atan2(point.z, point.x) * Mathf.Rad2Deg;

            Harness.Check(Mathf.Abs(Mathf.DeltaAngle(angle, 90f)) < 25f,
                string.Format("they come over the bridge, at {0:0} degrees rather than through the trench", angle));

            // Wall one side thicker than the other and the pressure moves to the thin
            // side, which is what makes upgrading a wall a decision rather than a chore.
            System.Func<Vector3, Vector3, int> uneven = (from, to) => from.x > 0f ? 8 : 2;
            SiegeApproach.Plan(centre, 24f, 12, uneven, lanes);

            int thinSide = 0;
            for (int i = 0; i < 100; i++)
            {
                if (lanes[SiegeApproach.Pick(lanes, i / 100f)].Point.x <= 0f) thinSide++;
            }

            Harness.Check(thinSide > 60,
                string.Format("{0}% of a wave goes at the thin wall rather than the thick one", thinSide));

            // An open base draws evenly - there is nothing to funnel through, and the
            // horde should not invent a preference out of floating-point noise.
            SiegeApproach.Plan(centre, 24f, 8, Walled(0), lanes);

            var spread = new HashSet<int>();
            for (int i = 0; i < 80; i++) spread.Add(SiegeApproach.Pick(lanes, i / 80f));

            Harness.Equal(spread.Count, 8, "an unwalled base is approached from every side equally");
        }
    }
}
