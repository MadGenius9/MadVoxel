using System.Collections.Generic;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The grass, as arithmetic.
    ///
    /// Whether it looks good is a playtest question. Whether it is *stable* is not,
    /// and stability is the whole game here: grass scattered from a hash has to give
    /// the same answer every time the same ground is meshed, or a hillside crawls as
    /// you walk past it and a patch dug up and refilled comes back different. That
    /// kind of fault is maddening to chase by eye and trivial to catch here.
    /// </summary>
    public static class GroundCoverTests
    {
        public static void Run()
        {
            Determinism();
            Density();
            Scatter();
        }

        static void Determinism()
        {
            Harness.Section("ground cover: the same ground grows the same grass");

            // The property everything else rests on.
            for (int i = 0; i < 64; i++)
            {
                float a = GroundCover.Hash(i * 7, i * 13, 3);
                float b = GroundCover.Hash(i * 7, i * 13, 3);
                if (a != b) { Harness.Check(false, "the hash is stable"); return; }
            }
            Harness.Check(true, "the hash is stable across repeated calls");

            // And it has to actually vary, or every tuft lands on the same spot.
            var seen = new HashSet<float>();
            for (int x = 0; x < 40; x++)
                for (int z = 0; z < 40; z++)
                    seen.Add(GroundCover.Hash(x, z, 1));

            Harness.Check(seen.Count > 1400,
                string.Format("and spreads: {0} distinct values over 1600 cells", seen.Count));

            // Channels must not agree with each other, or a blade's x and z offsets
            // would be identical and every tuft would sit on a diagonal.
            int agreements = 0;
            for (int x = 0; x < 50; x++)
                for (int z = 0; z < 50; z++)
                    if (GroundCover.Hash(x, z, 11) == GroundCover.Hash(x, z, 12)) agreements++;

            Harness.Check(agreements < 5,
                string.Format("and the channels differ ({0} collisions in 2500)", agreements));

            // Negative coordinates are half the world.
            Harness.Check(GroundCover.Hash(-40, -900, 1) >= 0f && GroundCover.Hash(-40, -900, 1) <= 1f,
                "negative ground is in range too");

            Harness.Equal(GroundCover.PatchOf(-1, -1).x, -1, "and lands in the patch below zero");
            Harness.Equal(GroundCover.PatchOf(0, 0).x, 0, "while the origin is patch zero");
        }

        static void Density()
        {
            Harness.Section("ground cover: thinning with distance");

            Harness.Equal(GroundCover.DensityAt(0f), 1f, "grass underfoot is full");
            Harness.Equal(GroundCover.DensityAt(GroundCover.FadeStart), 1f, "and stays full to the fade");
            Harness.Equal(GroundCover.DensityAt(GroundCover.DrawDistance), 0f, "and is gone at the draw distance");
            Harness.Equal(GroundCover.DensityAt(9999f), 0f, "well beyond it too");

            float mid = GroundCover.DensityAt((GroundCover.FadeStart + GroundCover.DrawDistance) * 0.5f);
            Harness.Check(mid > 0f && mid < 1f, "and thins rather than cutting off");

            // A hard edge sweeping across a hillside as you walk is far more noticeable
            // than sparse grass in the distance, so the fade has to have room to work.
            Harness.Check(GroundCover.DrawDistance - GroundCover.FadeStart > 8f,
                "with enough range for the fade to be gradual");
        }

        static void Scatter()
        {
            Harness.Section("ground cover: scattering a square metre");

            var tufts = new List<CoverTuft>();
            int added = GroundCover.Scatter(12, -40, 64f, GroundCover.KindGrass, 1f, tufts);

            Harness.Check(added > 0, "full ground grows something");
            Harness.Equal(tufts.Count, added, "and reports what it added");

            // Everything has to land inside the metre it belongs to, or patches would
            // overlap and a rebuild would leave grass behind.
            bool inside = true;
            for (int i = 0; i < tufts.Count; i++)
            {
                if (tufts[i].X < 12f || tufts[i].X >= 13f) inside = false;
                if (tufts[i].Z < -40f || tufts[i].Z >= -39f) inside = false;
                if (tufts[i].Height <= 0f) inside = false;
            }
            Harness.Check(inside, "and every tuft is inside its own cell, standing up");

            // Scattering twice gives the same grass.
            var again = new List<CoverTuft>();
            GroundCover.Scatter(12, -40, 64f, GroundCover.KindGrass, 1f, again);

            bool identical = again.Count == tufts.Count;
            for (int i = 0; identical && i < tufts.Count; i++)
            {
                identical = again[i].X == tufts[i].X
                         && again[i].Z == tufts[i].Z
                         && again[i].Height == tufts[i].Height;
            }
            Harness.Check(identical, "and the same cell scatters identically every time");

            // Thinning removes blades rather than rearranging them, so grass in the
            // distance does not visibly reshuffle as the player walks towards it.
            var thin = new List<CoverTuft>();
            GroundCover.Scatter(12, -40, 64f, GroundCover.KindGrass, 0.34f, thin);

            Harness.Check(thin.Count > 0 && thin.Count < tufts.Count, "thinner ground grows less");

            bool prefix = true;
            for (int i = 0; i < thin.Count; i++)
            {
                if (thin[i].X != tufts[i].X || thin[i].Z != tufts[i].Z) prefix = false;
            }
            Harness.Check(prefix, "and the survivors are the same blades, not a new arrangement");

            Harness.Equal(GroundCover.Scatter(0, 0, 10f, GroundCover.KindGrass, 0f, tufts), 0,
                "bare ground grows nothing");
            Harness.Equal(GroundCover.Scatter(0, 0, 10f, GroundCover.KindGrass, 1f, null), 0,
                "and nowhere to put it is not a crash");

            // The three kinds have to be tellable apart, or there is no point in them.
            Harness.Check(GroundCover.HeightFor(GroundCover.KindScrub, 1f)
                        > GroundCover.HeightFor(GroundCover.KindDry, 1f),
                "scrub stands taller than dry stubble");
        }
    }
}
