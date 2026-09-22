using UnityEngine;

namespace MadVoxel.Colony
{
    /// <summary>What is currently weighing on a colonist. Read straight off the world.</summary>
    public struct MoraleContext
    {
        public bool Hungry;
        public bool Thirsty;
        public bool HasBed;
        public bool InTheDark;
        public bool OutInBadWeather;
        public bool BaseWasBreached;
        public float WeatherPerHour;
    }

    /// <summary>
    /// Morale, as a single number with legible causes. Pure so the slide into a
    /// walk-out can be checked without waiting three in-game days for it.
    ///
    /// The shape that matters: everything bad is a per-hour drain, so a colony can be
    /// neglected for a while and recovered, but a colony that is neglected steadily
    /// will empty. There is no cliff and no hidden timer - a player who looks at the
    /// board can always see which line is pulling.
    /// </summary>
    public static class ColonyMorale
    {
        public const float Max = 100f;

        /// <summary>The strongest single complaint, for the one line on the board.</summary>
        public static MoraleReason Dominant(MoraleContext context, ColonistDefinition def)
        {
            float worst = 0f;
            var reason = MoraleReason.Fed;

            Consider(context.Hungry, MoraleReason.Hungry, def, ref worst, ref reason);
            Consider(context.Thirsty, MoraleReason.Thirsty, def, ref worst, ref reason);
            Consider(!context.HasBed, MoraleReason.NoBed, def, ref worst, ref reason);
            Consider(context.InTheDark, MoraleReason.Dark, def, ref worst, ref reason);
            Consider(context.BaseWasBreached, MoraleReason.Breached, def, ref worst, ref reason);
            Consider(context.OutInBadWeather, MoraleReason.Weather, def, ref worst, ref reason);

            return reason;
        }

        static void Consider(bool active, MoraleReason candidate, ColonistDefinition def,
                             ref float worst, ref MoraleReason reason)
        {
            if (!active || def == null) return;

            float weight = -def.MoralePerHour(candidate);
            if (weight <= worst) return;

            worst = weight;
            reason = candidate;
        }

        /// <summary>
        /// Advances morale one step. A colonist with nothing wrong recovers, which is
        /// what makes a rescued colony worth rescuing rather than a write-off.
        /// </summary>
        public static float Step(float morale, MoraleContext context, ColonistDefinition def, float gameHours)
        {
            if (def == null || gameHours <= 0f) return morale;

            float delta = 0f;

            if (context.Hungry) delta += def.MoralePerHour(MoraleReason.Hungry);
            if (context.Thirsty) delta += def.MoralePerHour(MoraleReason.Thirsty);
            if (!context.HasBed) delta += def.MoralePerHour(MoraleReason.NoBed);
            if (context.InTheDark) delta += def.MoralePerHour(MoraleReason.Dark);
            if (context.BaseWasBreached) delta += def.MoralePerHour(MoraleReason.Breached);
            if (context.OutInBadWeather) delta += context.WeatherPerHour;

            // Nothing wrong is not neutral: a fed, watered, rested person cheers up.
            bool settled = !context.Hungry && !context.Thirsty && context.HasBed
                        && !context.BaseWasBreached;
            if (settled) delta += def.MoralePerHour(MoraleReason.Rested);

            return Mathf.Clamp(morale + delta * gameHours, 0f, Max);
        }

        /// <summary>Below this they down tools, but they are still here.</summary>
        public static bool IsSulking(float morale, ColonistDefinition def)
        {
            return def != null && morale < def.sulkBelow;
        }

        /// <summary>Below this they pack up. The board should have warned you long before.</summary>
        public static bool IsLeaving(float morale, ColonistDefinition def)
        {
            return def != null && morale < def.leaveBelow;
        }

        /// <summary>One word for the board.</summary>
        public static string Describe(float morale, ColonistDefinition def)
        {
            if (def == null) return "";
            if (morale < def.leaveBelow) return "LEAVING";
            if (morale < def.sulkBelow) return "MISERABLE";
            if (morale < 55f) return "UNSETTLED";
            if (morale < 80f) return "STEADY";
            return "CONTENT";
        }
    }
}
