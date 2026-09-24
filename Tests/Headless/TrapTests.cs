using MadVoxel.Building;

namespace MadVoxel.Headless
{
    /// <summary>
    /// Spike traps, and whether a killbox is worth building.
    ///
    /// The question that decides it - does a trap kill enough over a night to pay for
    /// the iron it costs to keep sharp - is arithmetic, and the only other way to ask
    /// it is to play three blood moons and form an impression. So it gets asked here,
    /// where the answer is the same every time.
    /// </summary>
    public static class TrapTests
    {
        public static void Run()
        {
            Sharpness();
            Wearing();
            Upkeep();
        }

        // -------------------------------------------------------------- sharpness

        static void Sharpness()
        {
            Harness.Section("traps: how much edge is left");

            Harness.Equal(TrapRules.BiteDamage(100f, 1f), 100f, "a fresh trap does its full rating");
            Harness.Check(TrapRules.BiteDamage(100f, 0.5f) < 100f, "a half-worn one does less");
            Harness.Check(TrapRules.BiteDamage(100f, 0.5f) > 50f, "but not proportionally less");

            // The last of its working life still has to be worth something, or a third
            // of every trap is metal nobody ever gets value from.
            Harness.Check(TrapRules.BiteDamage(100f, TrapRules.BluntAt + 0.01f) >= 55f,
                "the last usable bite still hurts");

            Harness.Equal(TrapRules.BiteDamage(100f, TrapRules.BluntAt), 0f, "a blunt trap does nothing");
            Harness.Equal(TrapRules.BiteDamage(100f, 0f), 0f, "and neither does a destroyed one");
            Harness.Equal(TrapRules.BiteDamage(0f, 1f), 0f, "a trap with no rating does nothing whatever its condition");

            Harness.Check(!TrapRules.IsBlunt(1f), "a fresh trap is not blunt");
            Harness.Check(TrapRules.IsBlunt(0f), "an empty one is");
        }

        // ---------------------------------------------------------------- wearing

        static void Wearing()
        {
            Harness.Section("traps: blunting");

            Harness.Check(TrapRules.WearAfterBite(1f) < 1f, "biting wears a trap");

            // It stops at blunt rather than at nothing. A trap that destroyed itself
            // after a good night would punish exactly the player whose trap worked.
            float f = 1f;
            for (int i = 0; i < 500; i++) f = TrapRules.WearAfterBite(f);

            Harness.Equal(f, TrapRules.BluntAt, "wear bottoms out at blunt, not at zero");
            Harness.Check(f > 0f, "so the trap is still standing and can be repaired");

            // And how many bites that took has to be a sane number of zombies.
            int bites = TrapRules.BitesRemaining(1f);
            Harness.Check(bites >= 25 && bites <= 60,
                string.Format("a fresh trap is good for {0} bites - a night's worth, not a week's", bites));

            Harness.Equal(TrapRules.BitesRemaining(TrapRules.BluntAt), 0, "a blunt trap has none left");
            Harness.Check(TrapRules.BitesRemaining(0.5f) < bites, "a worn one has fewer than a fresh one");
        }

        // ----------------------------------------------------------------- upkeep

        static void Upkeep()
        {
            Harness.Section("traps: keeping them sharp");

            Harness.Check(TrapRules.AfterRepair(TrapRules.BluntAt) > TrapRules.BluntAt, "a repair puts edge back");
            Harness.Equal(TrapRules.AfterRepair(0.95f), 1f, "and cannot overfill");

            // Repairing a blunt trap has to be a chore between blood moons rather than
            // something anyone does mid-fight.
            int swings = 0;
            float f = TrapRules.BluntAt;
            while (f < 1f && swings < 100) { f = TrapRules.AfterRepair(f); swings++; }

            Harness.Check(swings >= 3 && swings <= 8,
                string.Format("blunt to sharp takes {0} swings", swings));

            // The loop closes: a trap can be brought back and worn down again forever,
            // which is what makes a killbox maintenance rather than a consumable.
            for (int cycle = 0; cycle < 5; cycle++)
            {
                for (int i = 0; i < 500; i++) f = TrapRules.WearAfterBite(f);
                Harness.Check(TrapRules.IsBlunt(f), "worn blunt again");

                for (int i = 0; i < 10; i++) f = TrapRules.AfterRepair(f);
                Harness.Check(f >= 1f, "and hammered back to sharp");
            }
        }
    }
}
