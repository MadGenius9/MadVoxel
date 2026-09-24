using UnityEngine;

namespace MadVoxel.Combat
{
    /// <summary>Where on a body a shot landed.</summary>
    public enum HitZone
    {
        /// <summary>Anything that is not a body at all - a wall, a door, a vehicle.</summary>
        None,
        Legs,
        Torso,
        Head
    }

    /// <summary>
    /// Which part of a body a shot hit, and what that is worth.
    ///
    /// This exists to give the bow something melee cannot have. A swing is a cone -
    /// it finds the player a target rather than a point, which is the right shape for
    /// a swing and the wrong one for precision. A shot is a line, aimed, with drop to
    /// solve and a draw to commit to, and rewarding where it lands is the only thing
    /// that makes those choices matter. So headshots are a ranged mechanic on
    /// purpose: applying them to a melee cone would be handing out a bonus at random.
    ///
    /// Zones come from the height of the impact rather than from colliders, because
    /// there are none to hit. A zombie's whole body is one capsule, and the rendered
    /// head, torso and limbs have no colliders at all - so a shot that visually hits a
    /// skull is, to physics, a hit on a featureless cylinder. Measuring the height is
    /// the only thing that can tell the difference.
    ///
    /// Pure and engine-free: "did that count as a headshot" is a question about one
    /// number, and answering it by shooting zombies until it feels right is how you
    /// end up with a head that starts below the shoulders.
    /// </summary>
    public static class HitZones
    {
        /// <summary>
        /// Fraction of body height at which the head starts.
        ///
        /// The rendered head is a 0.26m cube centred at 0.90 of height, so it runs
        /// from about 0.83 upwards. Set at the bottom of the drawn head rather than
        /// above it: a shot that visibly hits the skull has to count, and a player who
        /// sees the arrow strike a face and reads "torso" will conclude the game is
        /// lying to them - which is worse than the bonus being slightly generous.
        /// </summary>
        public const float HeadFrom = 0.82f;

        /// <summary>Fraction of body height below which it is legs. The torso's box bottoms out near 0.44.</summary>
        public const float LegsBelow = 0.44f;

        /// <summary>What a head shot is worth. Enough to change how a fight is fought.</summary>
        public const float HeadMultiplier = 2.5f;

        /// <summary>A body shot is the baseline; a leg shot is a miss you got away with.</summary>
        public const float TorsoMultiplier = 1f;
        public const float LegsMultiplier = 0.7f;

        /// <summary>
        /// Classifies an impact by how far up the body it landed.
        ///
        /// <paramref name="impactY"/> and <paramref name="baseY"/> are world-space
        /// heights: where the shot hit, and where the body's feet are. A zero or
        /// negative <paramref name="height"/> is a torso hit rather than a division by
        /// zero - an unknown body is still a body.
        /// </summary>
        public static HitZone Classify(float impactY, float baseY, float height)
        {
            if (height <= 0.01f) return HitZone.Torso;

            float fraction = (impactY - baseY) / height;

            // Above the head or below the feet. A shot can land slightly outside the
            // nominal body - the capsule is not the drawing - and the sensible reading
            // of "just over the top of the skull" is a head shot, not a miss that
            // somehow did damage.
            if (fraction >= HeadFrom) return HitZone.Head;
            if (fraction < LegsBelow) return HitZone.Legs;
            return HitZone.Torso;
        }

        /// <summary>What a zone does to the damage.</summary>
        public static float Multiplier(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return HeadMultiplier;
                case HitZone.Legs: return LegsMultiplier;
                default: return TorsoMultiplier;
            }
        }

        /// <summary>The word the HUD puts on it, or empty for an unremarkable hit.</summary>
        public static string Label(HitZone zone)
        {
            return zone == HitZone.Head ? "HEADSHOT" : "";
        }
    }
}
