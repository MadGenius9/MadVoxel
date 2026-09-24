using UnityEngine;

namespace MadVoxel.Building
{
    /// <summary>
    /// What a spike trap does and what it costs to keep doing it.
    ///
    /// The horde already reads the ground: twelve scored lanes round a claim, a wave
    /// spread across them by how walkable each one is, every zombie given a way in
    /// before the base itself. That makes a dug channel a real instruction to the
    /// horde rather than a decoration - and until now there was nothing to put at the
    /// end of one. A blood moon was a question about whether a wall held.
    ///
    /// Spikes answer it differently. They are the unpowered defence: no grid, no
    /// watts, nothing to brown out, and - deliberately - no claim heat, because
    /// nothing about them runs. The blade trap is loud and draws the next wave
    /// larger; spikes are silent and charge you in iron instead. That is the whole
    /// trade, and it is why both exist.
    ///
    /// They also blunt. A spike that has been biting all night does less than a fresh
    /// one and eventually stops, which is where the upkeep lives: a killbox is
    /// something you maintain between blood moons rather than something you build
    /// once. It never wears to nothing, though - blunt spikes still stand, and can be
    /// hammered sharp again. Spikes that simply vanished after a good night would
    /// punish exactly the player whose trap worked.
    ///
    /// Pure and engine-free, because the interesting question - does a killbox pay
    /// for its own iron over a night of use - is arithmetic, and answering it by
    /// playing three blood moons is a bad use of a blood moon.
    /// </summary>
    public static class TrapRules
    {
        /// <summary>
        /// Condition at or below which spikes are too blunt to bite, as a fraction of
        /// full health. Above zero on purpose: the trap survives to be repaired.
        /// </summary>
        public const float BluntAt = 0.15f;

        /// <summary>
        /// Damage floor while still working, as a fraction of the trap's rating. A
        /// well-used spike is worth less than a fresh one but never nothing, or the
        /// last third of a trap's life would be wasted metal.
        /// </summary>
        public const float WornDamageFloor = 0.55f;

        /// <summary>Condition lost per bite, as a fraction of full health.</summary>
        public const float WearPerBite = 0.022f;

        /// <summary>
        /// Fraction of full condition one hammer repair restores. Four or five swings
        /// take a blunt trap back to sharp, which is a chore worth doing between
        /// blood moons and not one worth doing mid-fight.
        /// </summary>
        public const float RepairFraction = 0.2f;

        /// <summary>Is there enough edge left to hurt anything?</summary>
        public static bool IsBlunt(float conditionFraction)
        {
            return conditionFraction <= BluntAt;
        }

        /// <summary>
        /// What one bite takes off, given how sharp the spikes are.
        ///
        /// Tapers from full at perfect condition to <see cref="WornDamageFloor"/> at
        /// the blunt threshold, so a trap degrades noticeably before it stops.
        /// </summary>
        public static float BiteDamage(float ratedDamage, float conditionFraction)
        {
            if (ratedDamage <= 0f) return 0f;
            if (IsBlunt(conditionFraction)) return 0f;

            float sharp = Mathf.InverseLerp(BluntAt, 1f, Mathf.Clamp01(conditionFraction));
            return ratedDamage * Mathf.Lerp(WornDamageFloor, 1f, sharp);
        }

        /// <summary>
        /// Condition after a bite. Never falls below the blunt threshold: the trap
        /// stops working there, and taking it lower would only mean more hammer
        /// swings to bring back something that was already useless.
        /// </summary>
        public static float WearAfterBite(float conditionFraction)
        {
            if (IsBlunt(conditionFraction)) return Mathf.Clamp01(conditionFraction);
            return Mathf.Max(BluntAt, Mathf.Clamp01(conditionFraction) - WearPerBite);
        }

        /// <summary>Condition after one hammer repair.</summary>
        public static float AfterRepair(float conditionFraction)
        {
            return Mathf.Clamp01(conditionFraction + RepairFraction);
        }

        /// <summary>
        /// Roughly how many bites are left before it goes blunt. What the readout
        /// shows, so "nearly done" is a number rather than a feeling.
        /// </summary>
        public static int BitesRemaining(float conditionFraction)
        {
            if (IsBlunt(conditionFraction)) return 0;
            return Mathf.Max(0, Mathf.FloorToInt((Mathf.Clamp01(conditionFraction) - BluntAt) / WearPerBite));
        }
    }
}
