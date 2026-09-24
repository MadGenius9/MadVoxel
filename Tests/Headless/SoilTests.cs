using MadVoxel.World.Fields;

namespace MadVoxel.Headless
{
    /// <summary>
    /// Soil fertility over the long run.
    ///
    /// This is the system least suited to being judged by playing it. Finding out
    /// whether an acre converges or spirals takes twenty in-game days of farming, and
    /// by the time a save has spiralled it cannot be un-spiralled. So the economics
    /// get asserted here: that working land without feeding it still falls, that
    /// feeding it recovers, and that neither direction runs away.
    /// </summary>
    public static class SoilTests
    {
        public static void Run()
        {
            Bounds();
            TheRatchet();
            Recovery();
            Fallow();
        }

        // ----------------------------------------------------------------- bounds

        static void Bounds()
        {
            Harness.Section("soil: nothing leaves the rails");

            Harness.Equal(SoilRules.AfterHarvest(0.1f), 0f, "a poor cell cannot go below empty");
            Harness.Equal(SoilRules.AfterCompost(0.9f), 1f, "and a rich one cannot go above full");
            Harness.Equal(SoilRules.AfterPlow(0.98f), 1f, "plowing rich ground does not overflow it");

            Harness.Equal(SoilRules.YieldMultiplier(0f), SoilRules.PoorYield, "empty ground pays the floor");
            Harness.Equal(SoilRules.YieldMultiplier(1f), SoilRules.RichYield, "and full ground the ceiling");
            Harness.Check(SoilRules.YieldMultiplier(-5f) == SoilRules.PoorYield
                          && SoilRules.YieldMultiplier(5f) == SoilRules.RichYield,
                "a nonsense fertility is clamped rather than extrapolated");

            Harness.Check(SoilRules.WantsCompost(0f), "empty ground wants muck");
            Harness.Check(!SoilRules.WantsCompost(1f), "full ground does not");
        }

        // ---------------------------------------------------------------- ratchet

        static void TheRatchet()
        {
            Harness.Section("soil: working land without feeding it");

            // Harvest takes more than the stubble gives back, so continuous cropping
            // runs the ground down. It does not reach zero, though: the drain clamps
            // at empty and the plow always returns its 0.1, so unfed land settles on a
            // poor steady state rather than dying outright. That floor is worth
            // pinning - it is the difference between a hard field and a dead one.
            float f = 0.25f;
            for (int crop = 0; crop < 6; crop++)
            {
                f = SoilRules.AfterHarvest(f);
                f = SoilRules.AfterPlow(f);
            }

            Harness.Equal(f, SoilRules.StubbleReturn,
                "unfed land settles at what the stubble alone puts back");

            float fed = 0.8f;
            for (int crop = 0; crop < 6; crop++)
            {
                fed = SoilRules.AfterHarvest(fed);
                fed = SoilRules.AfterPlow(fed);
                fed = SoilRules.AfterCompost(fed);
            }

            // The penalty has to be worth avoiding, or nobody will ever make compost.
            Harness.Check(SoilRules.YieldMultiplier(fed) - SoilRules.YieldMultiplier(f) > 0.25f,
                "and pays markedly less than land that is fed");

            // What was missing is that it could never come back. It can now.
            Harness.Check(SoilRules.AfterCompost(f) > f, "but that floor is no longer the end of it");
        }

        // --------------------------------------------------------------- recovery

        static void Recovery()
        {
            Harness.Section("soil: putting it back");

            // Two doses take fully exhausted ground back to better than it started.
            float f = SoilRules.AfterCompost(SoilRules.AfterCompost(0f));
            Harness.Check(f >= 0.7f, "two doses bring dead ground back to rich");
            Harness.Check(f <= 1f, "and not past full");

            // The loop has to be sustainable: one dose per crop must hold a field
            // steady rather than merely slow its decline. That is the whole point.
            float held = 0.6f;
            for (int crop = 0; crop < 12; crop++)
            {
                held = SoilRules.AfterHarvest(held);
                held = SoilRules.AfterPlow(held);
                held = SoilRules.AfterCompost(held);
            }
            Harness.Check(held >= 0.6f, "one dose a crop holds a field indefinitely");

            // And it must not be free money either - muck alone cannot push an acre
            // above what the ceiling allows.
            float piled = 0.5f;
            for (int i = 0; i < 20; i++) piled = SoilRules.AfterCompost(piled);
            Harness.Equal(piled, 1f, "and piling it on stops at full");
        }

        // ----------------------------------------------------------------- fallow

        static void Fallow()
        {
            Harness.Section("soil: leaving it alone");

            Harness.Equal(SoilRules.AfterFallow(0.2f, 0.0), 0.2f, "no time rested is no change");
            Harness.Equal(SoilRules.AfterFallow(0.2f, -50.0), 0.2f, "and neither is negative time");

            Harness.Check(SoilRules.AfterFallow(0.1f, 24.0) > 0.1f, "a day of rest recovers something");
            Harness.Check(SoilRules.AfterFallow(0.1f, 24.0) < 0.2f, "but not much - muck is the real answer");

            // Rest alone must never reach what feeding it does, or the spreader is a
            // decoration and the compost heap is a chore nobody needs to do.
            Harness.Equal(SoilRules.AfterFallow(0f, 24.0 * 365.0), SoilRules.FallowCeiling,
                "a year fallow still stops halfway");
            Harness.Check(SoilRules.FallowCeiling < 1f, "rest alone never reaches rich");
            Harness.Check(SoilRules.AfterFallow(0.8f, 24.0 * 30.0) == 0.8f,
                "and resting ground that is already better than the ceiling does nothing");
        }
    }
}
