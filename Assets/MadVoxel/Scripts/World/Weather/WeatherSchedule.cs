using System.Collections.Generic;
using MadVoxel.World.Biomes;
using UnityEngine;

namespace MadVoxel.World.Weather
{
    /// <summary>
    /// Picks what the sky does next. Pure, so the odds can be checked without waiting
    /// three in-game days for a drought.
    ///
    /// Two rules do the real work. A night-only state can only start on a night, so
    /// frost never arrives at noon. And the state that just ended is pushed to the back
    /// of the queue unless it is the only thing the region rolls, which is what stops
    /// the dry flats from serving drought, drought, drought forever.
    /// </summary>
    public static class WeatherSchedule
    {
        /// <summary>
        /// Rolls the next state for a region. <paramref name="roll01"/> is the caller's
        /// random number, so the same roll always gives the same answer.
        /// </summary>
        public static WeatherKind Roll(BiomeDefinition biome, WeatherTable table,
                                       bool nightAhead, WeatherKind current, float roll01)
        {
            if (table == null || table.states.Count == 0) return WeatherKind.Clear;

            float total = 0f;
            var kinds = new List<WeatherKind>(table.states.Count);
            var weights = new List<float>(table.states.Count);

            for (int i = 0; i < table.states.Count; i++)
            {
                var state = table.states[i];
                if (state == null) continue;

                // Frost is a night. It does not roll at two in the afternoon.
                if (state.nightOnly && !nightAhead) continue;

                float weight = biome != null ? biome.WeightOf(state.kind) : 1f;
                if (weight <= 0f) continue;

                // Repeating what just ended reads as the weather being stuck.
                if (state.kind == current) weight *= 0.15f;

                kinds.Add(state.kind);
                weights.Add(weight);
                total += weight;
            }

            if (total <= 0f) return WeatherKind.Clear;

            float pick = Mathf.Clamp01(roll01) * total;
            for (int i = 0; i < kinds.Count; i++)
            {
                pick -= weights[i];
                if (pick <= 0f) return kinds[i];
            }
            return kinds[kinds.Count - 1];
        }

        /// <summary>How long a state lasts, from its own range.</summary>
        public static float Duration(WeatherDefinition state, float roll01)
        {
            if (state == null) return 6f;

            float min = Mathf.Max(0.5f, state.minHours);
            float max = Mathf.Max(min, state.maxHours);
            return Mathf.Lerp(min, max, Mathf.Clamp01(roll01));
        }

        /// <summary>
        /// The line the compass shows. Clear says nothing on purpose: a readout that
        /// announces the weather every day is wallpaper, and the word has to mean
        /// something when it appears.
        /// </summary>
        public static string ClockLine(int day, string clock, WeatherDefinition state)
        {
            string stamp = string.Format("DAY {0}   {1}", day, clock);
            if (state == null || string.IsNullOrEmpty(state.clockWord)) return stamp;

            return stamp + "   " + state.clockWord;
        }
    }
}
