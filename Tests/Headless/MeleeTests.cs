using MadVoxel.Combat;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The swing.
    ///
    /// Melee is the one system whose failure the player cannot diagnose. A shot that
    /// misses leaves an arrow in the dirt; a swing that misses leaves nothing, and is
    /// indistinguishable from a swing the game ignored. So the arc gets pinned hard
    /// here, particularly the two cases a camera ray got wrong: a target pressed
    /// against the player, and a target whose visible body is wider than its collider.
    /// </summary>
    public static class MeleeTests
    {
        public static void Run()
        {
            Shape();
            Choice();
            Degenerate();
        }

        static MeleeArc.Candidate At(int id, float x, float y, float z, float radius = 0.4f)
        {
            return new MeleeArc.Candidate { Id = id, Centre = new Vector3(x, y, z), Radius = radius };
        }

        static MeleeArc.Result Swing(MeleeArc.Candidate[] candidates)
        {
            return MeleeArc.Resolve(Vector3.zero, Vector3.forward, candidates, candidates.Length);
        }

        // ------------------------------------------------------------------ shape

        static void Shape()
        {
            Harness.Section("melee: the shape of a swing");

            var ahead = new[] { At(1, 0f, 0f, 2f) };
            Harness.Check(Swing(ahead).Hit, "something dead ahead and in reach is hit");
            Harness.Equal(Swing(ahead).Id, 1, "and it is the thing that was hit");

            Harness.Check(!Swing(new[] { At(1, 0f, 0f, -2f) }).Hit, "something behind you is not");
            Harness.Check(!Swing(new[] { At(1, 0f, 0f, MeleeArc.Reach + 2f) }).Hit, "nor something out of reach");

            // The reach is to the body, not to its middle. A brute is wide, and a
            // swing that connects with its side is a swing that connected.
            float justOut = MeleeArc.Reach + 0.3f;
            Harness.Check(!Swing(new[] { At(1, 0f, 0f, justOut, 0.1f) }).Hit,
                "a narrow target just past the reach is missed");
            Harness.Check(Swing(new[] { At(1, 0f, 0f, justOut, 0.6f) }).Hit,
                "a wide one at the same distance is not - its near side is inside the arc");

            // The case a ray could never handle: the collider has swallowed the eye.
            Harness.Check(Swing(new[] { At(1, 0f, 0f, 0.2f, 0.5f) }).Hit,
                "a zombie pressed against you is hit, not cast through");
            Harness.Equal(Swing(new[] { At(1, 0.1f, 0f, -0.1f, 0.5f) }).Id, 1,
                "even when its centre has drifted behind you");

            // A shoulder off to the side. The old pinpoint ray's exact failure.
            Harness.Check(Swing(new[] { At(1, 0.45f, 0f, 1.2f, 0.35f) }).Hit,
                "a body off to one side but plainly in front is hit");
        }

        // ----------------------------------------------------------------- choice

        static void Choice()
        {
            Harness.Section("melee: which one a swing lands on");

            // Centred beats near. A swing should go to what the player is looking at,
            // not to whatever brushed their elbow.
            var offToTheSide = At(1, 1.5f, 0f, 0.9f);
            var straightAhead = At(2, 0f, 0f, 2.2f);
            Harness.Equal(MeleeArc.Resolve(Vector3.zero, Vector3.forward,
                new[] { offToTheSide, straightAhead }, 2).Id, 2,
                "the one you are looking at wins over the one closer to your elbow");

            // But proximity still counts for something when nothing is centred.
            var nearLeft = At(1, 0.8f, 0f, 1.0f);
            var farLeft = At(2, 2.0f, 0f, 2.5f);
            Harness.Equal(MeleeArc.Resolve(Vector3.zero, Vector3.forward,
                new[] { farLeft, nearLeft }, 2).Id, 1,
                "with both off-centre, the closer one takes the swing");

            // Order must not decide anything.
            Harness.Equal(MeleeArc.Resolve(Vector3.zero, Vector3.forward,
                new[] { straightAhead, offToTheSide }, 2).Id, 2,
                "and the answer does not depend on the order they were found in");

            // Only the first `count` entries are considered - the caller reuses a buffer.
            Harness.Equal(MeleeArc.Resolve(Vector3.zero, Vector3.forward,
                new[] { straightAhead, offToTheSide }, 1).Id, 2,
                "a stale tail of the buffer is not swung at");
        }

        // ------------------------------------------------------------- degenerate

        static void Degenerate()
        {
            Harness.Section("melee: nothing to hit");

            Harness.Check(!MeleeArc.Resolve(Vector3.zero, Vector3.forward, null, 0).Hit,
                "no candidates is a miss, not a crash");
            Harness.Check(!MeleeArc.Resolve(Vector3.zero, Vector3.forward, new MeleeArc.Candidate[0], 0).Hit,
                "an empty sweep is a miss");
            Harness.Equal(MeleeArc.Resolve(Vector3.zero, Vector3.forward, null, 0).Id, -1,
                "and a miss carries no id");

            // The rig can hand over a zero look direction for a frame while it builds.
            Harness.Check(!MeleeArc.Resolve(Vector3.zero, Vector3.zero, new[] { At(1, 0f, 0f, 1f) }, 1).Hit,
                "a degenerate look direction swings at nothing rather than throwing");
        }
    }
}
