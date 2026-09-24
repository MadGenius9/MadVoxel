using UnityEngine;

namespace MadVoxel.World.Fields
{
    /// <summary>
    /// What working the ground does to it over time.
    ///
    /// The field layer shipped with fertility as a one-way ratchet. A harvest took
    /// 0.3, plowing the stubble back in returned 0.1, and nothing else touched it -
    /// so from a starting 0.25 a field was exhausted after two crops and then sat at
    /// the yield floor for the rest of the save. Permanently, with no way back and
    /// nothing on screen explaining why the same acre now paid a third less.
    ///
    /// That is worse than a missing feature: it is a mechanic that only ever takes.
    /// Rotation, muck and rest all needed somewhere to live, so they live here.
    ///
    /// Pure and engine-free, because a slow economic drift is close to impossible to
    /// judge by playing - you would have to farm twenty in-game days to find out
    /// whether the numbers converge or spiral, and a save that has already spiralled
    /// cannot be un-spiralled.
    /// </summary>
    public static class SoilRules
    {
        /// <summary>What a crop takes out of the ground when it is lifted.</summary>
        public const float HarvestDrain = 0.3f;

        /// <summary>What turning the stubble back in returns. Free, and never enough on its own.</summary>
        public const float StubbleReturn = 0.1f;

        /// <summary>What one dose of compost adds. Two doses take exhausted ground back to fresh.</summary>
        public const float CompostDose = 0.35f;

        /// <summary>
        /// Fertility recovered per in-game day a cell is left fallow.
        ///
        /// Slow on purpose. It has to be a real alternative to muck for someone with
        /// more land than compost, and never the obvious one - if resting a field beat
        /// working it, the spreader would be decoration.
        /// </summary>
        public const float FallowPerDay = 0.02f;

        /// <summary>A cell can only rest so far on its own; the rest has to be put back.</summary>
        public const float FallowCeiling = 0.5f;

        /// <summary>Yield multiplier at zero fertility and at full.</summary>
        public const float PoorYield = 0.75f;
        public const float RichYield = 1.15f;

        /// <summary>Litres multiplier for a cell's fertility.</summary>
        public static float YieldMultiplier(float fertiliser)
        {
            return Mathf.Lerp(PoorYield, RichYield, Mathf.Clamp01(fertiliser));
        }

        /// <summary>Fertility after lifting a crop.</summary>
        public static float AfterHarvest(float fertiliser)
        {
            return Mathf.Clamp01(fertiliser - HarvestDrain);
        }

        /// <summary>Fertility after breaking stubble back into the soil.</summary>
        public static float AfterPlow(float fertiliser)
        {
            return Mathf.Clamp01(fertiliser + StubbleReturn);
        }

        /// <summary>Fertility after a dose of compost.</summary>
        public static float AfterCompost(float fertiliser)
        {
            return Mathf.Clamp01(fertiliser + CompostDose);
        }

        /// <summary>
        /// Is there any point spreading here?
        ///
        /// Refusing ground that is already rich matters more than it sounds: a
        /// spreader crossing a field must not silently eat a hopper of compost
        /// putting 0.02 into cells that were already full.
        /// </summary>
        public static bool WantsCompost(float fertiliser)
        {
            return fertiliser < 1f - 0.01f;
        }

        /// <summary>
        /// Fertility after resting for a stretch of world-clock hours.
        ///
        /// Derived from the clock rather than ticked, like everything else here, so a
        /// field left alone over a save and reload comes back having actually rested
        /// instead of having been paused.
        /// </summary>
        public static float AfterFallow(float fertiliser, double restedHours)
        {
            if (restedHours <= 0.0) return Mathf.Clamp01(fertiliser);
            if (fertiliser >= FallowCeiling) return Mathf.Clamp01(fertiliser);

            float gained = fertiliser + (float)(restedHours / 24.0) * FallowPerDay;
            return Mathf.Clamp01(Mathf.Min(gained, FallowCeiling));
        }
    }
}
