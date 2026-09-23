using MadVoxel.Colony;
using MadVoxel.Content;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// How people find the colony.
    ///
    /// The colony could be founded on day one and then filled from a button on its own
    /// board, which is not a system - it is a cheat with a label. These are the rules
    /// that replaced it, and the important ones are all about refusing: a wanderer
    /// arriving into a colony that cannot feed them is a problem dressed as a reward.
    /// </summary>
    public static class RecruitTests
    {
        public static void Run(ContentDatabase db)
        {
            Readiness();
            Arriving();
            Supply();
            if (db != null) Rules(db);
        }

        // --------------------------------------------------------------- readiness

        static void Readiness()
        {
            Harness.Section("colony: whether anyone would stay");

            // The happy case: founded, room, a spare bed, food and water put by.
            Harness.Check(ColonyRecruitment.Readiness(true, 1, 6, 2, 3f, 3f) == RecruitReadiness.Ready,
                "a colony with room, a spare bed and supplies is ready for someone");

            Harness.Check(ColonyRecruitment.Readiness(false, 0, 6, 9, 9f, 9f) == RecruitReadiness.NoColony,
                "without a charter there is nothing to join");

            Harness.Check(ColonyRecruitment.Readiness(true, 6, 6, 9, 9f, 9f) == RecruitReadiness.Full,
                "a full colony turns people away");

            // A bed each AND one spare. Two people and two beds is not room for a third.
            Harness.Check(ColonyRecruitment.Readiness(true, 2, 6, 2, 9f, 9f) == RecruitReadiness.NoBed,
                "a bed each is not a spare bed");
            Harness.Check(ColonyRecruitment.Readiness(true, 2, 6, 3, 9f, 9f) == RecruitReadiness.Ready,
                "one more than there are people is");

            // THE refusals that matter. Handing the player another mouth they cannot
            // feed is a punishment that looks like a gift.
            Harness.Check(ColonyRecruitment.Readiness(true, 1, 6, 2, 0.2f, 9f) == RecruitReadiness.NoFood,
                "nobody stays somewhere that cannot feed them");
            Harness.Check(ColonyRecruitment.Readiness(true, 1, 6, 2, 9f, 0.2f) == RecruitReadiness.NoWater,
                "or water them");

            // An empty founded camp with stores is a fine place to arrive at.
            Harness.Check(ColonyRecruitment.Readiness(true, 0, 6, 1, 3f, 3f) == RecruitReadiness.Ready,
                "the first person can walk into an empty camp that is stocked for them");

            // Every reason has words.
            var reasons = (RecruitReadiness[])System.Enum.GetValues(typeof(RecruitReadiness));
            var wordless = new System.Collections.Generic.List<string>();
            for (int i = 0; i < reasons.Length; i++)
            {
                if (string.IsNullOrEmpty(ColonyRecruitment.Describe(reasons[i], 9))) wordless.Add(reasons[i].ToString());
            }
            Harness.Check(wordless.Count == 0,
                "every reason nobody is coming has wording"
                + (wordless.Count > 0 ? ": " + string.Join(", ", wordless) : ""));
        }

        // ---------------------------------------------------------------- arriving

        static void Arriving()
        {
            Harness.Section("colony: whether anyone comes");

            // Nothing arrives at a colony that is not ready, however loud or however
            // long it has been.
            Harness.Check(!ColonyRecruitment.Arrives(RecruitReadiness.NoFood, 99, 100f, 0f),
                "a starving colony draws nobody, however loud");
            Harness.Check(!ColonyRecruitment.Arrives(RecruitReadiness.Full, 99, 100f, 0f),
                "and neither does a full one");

            // Nor immediately after the last one. People are not a resource tap.
            Harness.Check(!ColonyRecruitment.Arrives(RecruitReadiness.Ready, 0, 100f, 0f),
                "nobody turns up the morning after the last arrival");
            Harness.Check(!ColonyRecruitment.Arrives(RecruitReadiness.Ready, ColonyRecruitment.MinDaysBetween - 1, 100f, 0f),
                "nor a day early");
            Harness.Check(ColonyRecruitment.Arrives(RecruitReadiness.Ready, ColonyRecruitment.MinDaysBetween, 100f, 0f),
                "but on the day, with a good roll, someone does");

            // A loud claim is easier to find. The same thing that pulls a horde pulls a
            // survivor looking for walls - turtling in the dark is quiet both ways.
            Harness.Check(ColonyRecruitment.ChanceAt(100f) > ColonyRecruitment.ChanceAt(0f),
                "a loud claim is easier to find than a quiet one");
            Harness.Equal(ColonyRecruitment.ChanceAt(0f), ColonyRecruitment.BaseChance,
                "a silent farm is findable, barely");
            Harness.Equal(ColonyRecruitment.ChanceAt(100f), ColonyRecruitment.LoudChance,
                "and one you can hear from the treeline usually is");
            Harness.Equal(ColonyRecruitment.ChanceAt(500f), ColonyRecruitment.LoudChance,
                "past the top of the scale it stops climbing");

            // Never a certainty, at any heat - or a player could farm arrivals by
            // turning every light on.
            Harness.Check(ColonyRecruitment.LoudChance < 1f,
                "even the loudest claim is not a guarantee");
            Harness.Check(!ColonyRecruitment.Arrives(RecruitReadiness.Ready, 99, 100f, 1f),
                "so a bad roll at maximum heat still brings nobody");

            // And it has to actually happen sometimes at the bottom of the range.
            Harness.Check(ColonyRecruitment.Arrives(RecruitReadiness.Ready, 99, 0f, 0.01f),
                "a quiet claim still finds someone eventually");

            // Over a long stretch, a loud ready colony fills and a quiet one trickles.
            int loud = 0, quiet = 0;
            for (int i = 0; i < 100; i++)
            {
                float roll = i / 100f;
                if (ColonyRecruitment.Arrives(RecruitReadiness.Ready, 9, 100f, roll)) loud++;
                if (ColonyRecruitment.Arrives(RecruitReadiness.Ready, 9, 0f, roll)) quiet++;
            }

            Harness.Check(loud > quiet * 2,
                string.Format("over a hundred dawns a loud claim draws {0} against a quiet one's {1}", loud, quiet));
        }

        // ------------------------------------------------------------------ supply

        static void Supply()
        {
            Harness.Section("colony: days of supply");

            // Measured against one more mouth than there is now, because the question
            // is always whether the NEXT person can be fed.
            Harness.Equal(ColonyRecruitment.DaysOfSupply(6f, 1, 3f), 1f,
                "six food and one colonist is one day for two");
            Harness.Equal(ColonyRecruitment.DaysOfSupply(6f, 0, 3f), 2f,
                "and an empty camp counts the one who would arrive");

            Harness.Check(ColonyRecruitment.DaysOfSupply(0f, 1, 3f) < ColonyRecruitment.SpareDaysWanted,
                "an empty store is not enough for anyone");

            Harness.Check(ColonyRecruitment.DaysOfSupply(100f, 0, 0f) > 1000f,
                "a colony that needs nothing per day always has enough");
        }

        // ------------------------------------------------------------------- rules

        static void Rules(ContentDatabase db)
        {
            Harness.Section("colony: the cap as shipped");

            var rules = db.colonyRules;
            Harness.Check(rules != null, "there are colony rules");
            if (rules == null) return;

            // The brief said three to six. Three was the floor, not the ceiling.
            Harness.Check(rules.maxColonists >= 3 && rules.maxColonists <= 6,
                string.Format("the colony caps at {0}, inside the three-to-six the brief asked for",
                    rules.maxColonists));

            Harness.Check(rules.foodPerColonistPerDay > 0f, "a colonist eats");
            Harness.Check(rules.litresPerColonistPerDay > 0f, "and drinks");

            // A cap you could never reach would make the top of the range a lie: at the
            // full six, the beds and the supply the rules ask for have to be reachable.
            Harness.Check(ColonyRecruitment.Readiness(true, rules.maxColonists - 1, rules.maxColonists,
                    rules.maxColonists, 9f, 9f) == RecruitReadiness.Ready,
                "and the last place at the table can actually be filled");
        }
    }
}
