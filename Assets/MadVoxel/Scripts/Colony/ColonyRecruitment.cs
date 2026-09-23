using UnityEngine;

namespace MadVoxel.Colony
{
    /// <summary>Why a wanderer did or did not turn up, in the words the board uses.</summary>
    public enum RecruitReadiness
    {
        Ready,
        NoColony,
        Full,
        /// <summary>Nowhere for another person to sleep.</summary>
        NoBed,
        /// <summary>You cannot feed the ones you have, let alone another.</summary>
        NoFood,
        NoWater
    }

    /// <summary>
    /// How people find you.
    ///
    /// The colony could be founded from the first day and then filled from a button on
    /// its own board, which is not a system - it is a cheat with a label. The brief
    /// always said a wanderer at the fence and a word from a trader were how the first
    /// person arrives, and both of those things exist now.
    ///
    /// Two rules do the work:
    ///
    /// <b>They come to a claim worth coming to.</b> A wanderer will not walk into a
    /// place with no spare bed and no food, because the colony would immediately be
    /// worse off and the player would have been handed a problem dressed as a reward.
    /// What you have built decides whether anyone arrives at all.
    ///
    /// <b>A loud claim is easier to find.</b> Claim heat already measures how much
    /// attention a base draws, and it draws people as well as zombies. Lights, a
    /// generator, acreage - the same things that pull a horde pull a survivor looking
    /// for somewhere with walls. Turtling in the dark is quiet in both directions.
    ///
    /// Pure and engine-free, because "does a stranger show up" is exactly the kind of
    /// rule that is impossible to test by playing and easy to get subtly wrong.
    /// </summary>
    public static class ColonyRecruitment
    {
        /// <summary>In-game days between arrivals, at best. People are not a resource tap.</summary>
        public const int MinDaysBetween = 3;

        /// <summary>Days of food and water a colony needs spare before anyone else will stay.</summary>
        public const float SpareDaysWanted = 1f;

        /// <summary>The chance per eligible dawn at zero heat. A quiet farm is findable, barely.</summary>
        public const float BaseChance = 0.18f;

        /// <summary>And at a claim you can hear from the treeline.</summary>
        public const float LoudChance = 0.62f;

        /// <summary>
        /// Whether the colony could take another person right now, and why not.
        ///
        /// <paramref name="foodDays"/> and <paramref name="waterDays"/> are what the
        /// store holds measured against what the current population eats - so the test
        /// is "could you feed one more", not "do you have any food at all".
        /// </summary>
        public static RecruitReadiness Readiness(bool founded, int population, int cap,
                                                 int beds, float foodDays, float waterDays)
        {
            if (!founded) return RecruitReadiness.NoColony;
            if (population >= Mathf.Max(1, cap)) return RecruitReadiness.Full;

            // A bed each, and one going spare for whoever turns up.
            if (beds <= population) return RecruitReadiness.NoBed;

            if (foodDays < SpareDaysWanted) return RecruitReadiness.NoFood;
            if (waterDays < SpareDaysWanted) return RecruitReadiness.NoWater;

            return RecruitReadiness.Ready;
        }

        /// <summary>
        /// The chance a wanderer finds you this dawn. Heat is 0..100, as the claim
        /// tracker keeps it.
        /// </summary>
        public static float ChanceAt(float heat)
        {
            float loudness = Mathf.Clamp01(heat / 100f);
            return Mathf.Lerp(BaseChance, LoudChance, loudness);
        }

        /// <summary>
        /// Whether someone turns up. <paramref name="roll"/> is 0..1 and belongs to the
        /// caller, so a test can ask for a specific answer and the world can use its
        /// own randomness.
        /// </summary>
        public static bool Arrives(RecruitReadiness readiness, int daysSinceLast, float heat, float roll)
        {
            if (readiness != RecruitReadiness.Ready) return false;
            if (daysSinceLast < MinDaysBetween) return false;

            return roll <= ChanceAt(heat);
        }

        /// <summary>The line the board shows about whether anyone is likely to come.</summary>
        public static string Describe(RecruitReadiness readiness, int daysSinceLast)
        {
            switch (readiness)
            {
                case RecruitReadiness.NoColony: return "NO COLONY";
                case RecruitReadiness.Full: return "NO ROOM FOR ANOTHER";
                case RecruitReadiness.NoBed: return "NO SPARE BED";
                case RecruitReadiness.NoFood: return "NOT ENOUGH FOOD PUT BY";
                case RecruitReadiness.NoWater: return "NOT ENOUGH WATER PUT BY";
                default:
                    int wait = MinDaysBetween - daysSinceLast;
                    return wait > 0
                        ? string.Format("WORD TRAVELS SLOWLY  -  {0} DAY{1}", wait, wait == 1 ? "" : "S")
                        : "SOMEONE MAY COME";
            }
        }

        /// <summary>
        /// Days of supply a store holds for a population. Zero people is not infinite
        /// days - it is a colony that has not started eating yet, and a wanderer should
        /// still be able to walk into a stocked empty camp.
        /// </summary>
        public static float DaysOfSupply(float held, int population, float perPersonPerDay)
        {
            if (perPersonPerDay <= 0f) return float.MaxValue;

            // Measured against one more mouth than there is now, because the question
            // is always whether the *next* person can be fed.
            float draw = Mathf.Max(1, population + 1) * perPersonPerDay;
            return held / draw;
        }
    }
}
