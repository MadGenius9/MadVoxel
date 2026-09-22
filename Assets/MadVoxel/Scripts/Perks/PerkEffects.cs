using System;
using UnityEngine;

namespace MadVoxel.Perks
{
    /// <summary>
    /// The summed numeric result of every rank the player owns, recomputed whenever a
    /// rank changes rather than walked per swing. Pure and tree-driven: a mod that adds
    /// a perk with an existing effect type needs no code here.
    /// </summary>
    public class PerkEffects
    {
        static readonly int TypeCount = Enum.GetValues(typeof(PerkEffectType)).Length;

        readonly float[] _sums = new float[TypeCount];

        /// <summary>Raw sum of the effect across all owned ranks. Zero when nothing grants it.</summary>
        public float Bonus(PerkEffectType type)
        {
            int i = (int)type;
            return i >= 0 && i < _sums.Length ? _sums[i] : 0f;
        }

        /// <summary>
        /// The sum expressed as a scale factor. Clamped at 0.05 so a stack of negative
        /// effects (a mod handing out -1.0 per rank) can never invert a cost or a yield.
        /// </summary>
        public float Multiplier(PerkEffectType type)
        {
            return Mathf.Max(0.05f, 1f + Bonus(type));
        }

        /// <summary>Whole-number effects: block tier unlocks, extra slots.</summary>
        public int Tier(PerkEffectType type)
        {
            return Mathf.RoundToInt(Bonus(type));
        }

        /// <summary>Applies a multiplier to a count, keeping at least the base amount.</summary>
        public int ScaleCount(PerkEffectType type, int baseCount)
        {
            if (baseCount <= 0) return baseCount;
            return Mathf.Max(baseCount, Mathf.RoundToInt(baseCount * Multiplier(type)));
        }

        public void Clear()
        {
            for (int i = 0; i < _sums.Length; i++) _sums[i] = 0f;
        }

        /// <summary>Rebuilds the sums from the tree and the player's current ranks.</summary>
        public void Recompute(PerkTreeDefinition tree, Func<string, int> rankOf)
        {
            Clear();
            if (tree == null || rankOf == null) return;

            for (int p = 0; p < tree.perks.Count; p++)
            {
                var perk = tree.perks[p];
                if (perk == null) continue;

                int rank = Mathf.Clamp(rankOf(perk.stringId), 0, perk.maxRank);
                if (rank <= 0) continue;

                for (int e = 0; e < perk.effects.Count; e++)
                {
                    var effect = perk.effects[e];
                    int i = (int)effect.type;
                    if (i >= 0 && i < _sums.Length) _sums[i] += effect.valuePerRank * rank;
                }
            }
        }
    }
}
