using UnityEngine;

namespace MadVoxel.Claim
{
    /// <summary>
    /// What a claim contributes to its own heat. Kept as plain numbers rather than a
    /// scriptable object because this is balance, not content: a mod that wants a
    /// louder generator changes the generator, not the arithmetic.
    /// </summary>
    [System.Serializable]
    public struct HeatWeights
    {
        /// <summary>Per colonist living on the claim.</summary>
        public float perColonist;
        /// <summary>Per worked field cell, divided by a hundred - an acre, not a furrow.</summary>
        public float perHundredFieldCells;
        /// <summary>Per mature crop cell, on top of the acreage.</summary>
        public float perHundredMatureCells;
        /// <summary>One burst per shot, decaying like everything else.</summary>
        public float perGunshot;
        /// <summary>How fast heat falls back towards the floor, per game hour.</summary>
        public float coolPerHour;
        /// <summary>Dawn drags heat towards the floor this hard, on top of cooling.</summary>
        public float dawnPull;

        public static HeatWeights Default
        {
            get
            {
                return new HeatWeights
                {
                    perColonist = 4f,
                    perHundredFieldCells = 9f,
                    perHundredMatureCells = 5f,
                    perGunshot = 6f,
                    coolPerHour = 7f,
                    dawnPull = 12f
                };
            }
        }
    }

    /// <summary>
    /// One number, 0 to 100, for how loud a claim is. Not a second map layer and not a
    /// minigame: lights, a running generator, people, acreage and spinning traps push
    /// it up; switching things off and sheltering pull it down; dawn drags it back
    /// towards a floor that a working farm can never get under.
    ///
    /// The floor is the honest part. A farm with three colonists and an acre of wheat
    /// is never silent, so blacking out buys you less than moving would - and that is
    /// the trade the whole system exists to pose.
    /// </summary>
    public static class ClaimHeatMath
    {
        public const float Max = 100f;

        /// <summary>
        /// The quietest this claim can get. Population and acreage set it, the biome
        /// nudges it, and the Quiet Claim perk shaves it.
        /// </summary>
        public static float Floor(HeatWeights weights, int colonists, int workedFieldCells,
                                  float biomeBonus, float quietFraction)
        {
            float floor = colonists * weights.perColonist
                        + workedFieldCells / 100f * weights.perHundredFieldCells
                        + biomeBonus;

            floor *= Mathf.Clamp01(1f - quietFraction);
            return Mathf.Clamp(floor, 0f, Max);
        }

        /// <summary>
        /// Heat added per game hour by everything currently running. Traps only count
        /// while they are actually swinging, which is what makes a quiet night with
        /// armed defences possible.
        /// </summary>
        public static float Sources(float litLightHeat, float generatorHeat, float activeTrapHeat,
                                    float matureCells, HeatWeights weights, float quietFraction)
        {
            float raw = litLightHeat + generatorHeat + activeTrapHeat
                      + matureCells / 100f * weights.perHundredMatureCells;

            return raw * Mathf.Clamp01(1f - quietFraction);
        }

        /// <summary>
        /// Advances heat one step. Above the floor it always decays; the sources have
        /// to keep paying to hold it up, so switching the lights off is felt within the
        /// hour rather than at some later reset.
        /// </summary>
        public static float Step(float current, float floor, float sourcesPerHour,
                                 HeatWeights weights, float gameHours, bool dawnBroke)
        {
            if (gameHours <= 0f) return Mathf.Clamp(current, floor, Max);

            float next = current + sourcesPerHour * gameHours - weights.coolPerHour * gameHours;

            // Dawn is a reset towards the floor, not to zero: the night's noise fades
            // but the farm is still a farm.
            if (dawnBroke) next -= weights.dawnPull;

            return Mathf.Clamp(next, floor, Max);
        }

        /// <summary>One word for the board. Shape and wording, not just a colour.</summary>
        public static string Describe(float heat)
        {
            if (heat >= 75f) return "BEACON";
            if (heat >= 50f) return "LOUD";
            if (heat >= 25f) return "NOTICEABLE";
            return "QUIET";
        }

        /// <summary>
        /// Extra wandering zombies near the claim, scaled by heat. A quiet claim is not
        /// safe, it is merely not advertised.
        /// </summary>
        public static float WandererMultiplier(float heat)
        {
            return 1f + Mathf.Clamp01(heat / Max) * 1.5f;
        }

        /// <summary>Extra bodies on the blood moon. Heat is a budget, not a coin flip.</summary>
        public static int HordeBonus(float heat, int baseWaveSize)
        {
            float fraction = Mathf.Clamp01(heat / Max);
            return Mathf.RoundToInt(baseWaveSize * fraction * 0.6f);
        }
    }
}
