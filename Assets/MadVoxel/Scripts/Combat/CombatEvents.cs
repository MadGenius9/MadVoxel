using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.Combat
{
    /// <summary>What the HUD needs to know about a blow that landed.</summary>
    public struct HitReport
    {
        public IDamageable Victim;

        /// <summary>Damage actually dealt, after the zone multiplier.</summary>
        public float Damage;

        /// <summary>Where it landed on the body, or None for anything that is not one.</summary>
        public HitZone Zone;

        /// <summary>Whether that blow was the last one.</summary>
        public bool Killed;

        /// <summary>World space, for putting the sound and any marker in the right place.</summary>
        public Vector3 Point;
    }

    /// <summary>
    /// The one place a landed blow is announced.
    ///
    /// It started as a static event on PlayerInteraction, which was fine while melee
    /// was the only thing that could land one. Arrows made that wrong: a shot that
    /// connects has exactly as much claim on a hit marker as a swing, and hanging a
    /// second event off the projectile would leave the HUD subscribing to two things
    /// that mean the same thing and having to agree with itself about both.
    ///
    /// Raised by whatever dealt the damage, listened to by whatever draws or plays the
    /// consequence. Nothing here knows what a canvas is.
    /// </summary>
    public static class CombatEvents
    {
        /// <summary>Raised for every blow the player lands, melee or ranged.</summary>
        public static event System.Action<HitReport> HitLanded;

        public static void ReportHit(HitReport report)
        {
            if (HitLanded != null) HitLanded(report);
        }
    }
}
