using MadVoxel.Core;
using MadVoxel.Perks;
using UnityEngine;

namespace MadVoxel.Building
{
    /// <summary>
    /// A bed of spikes. The defence you can build before electricity.
    ///
    /// It uses the structure's own health as its condition, which is not a shortcut:
    /// it means a trap wears, takes damage from the horde, saves, loads and repairs
    /// through exactly one number and exactly one code path. A separate "sharpness"
    /// field would have needed its own save entry, its own clamp and its own bug.
    ///
    /// The bite is the same shape as <see cref="Power.PowerTrap"/>'s, and for the
    /// same reason: it looks on an interval rather than every frame, and it never
    /// bites the person who built it. A trap that killed you for walking over your
    /// own killbox is just a trap.
    /// </summary>
    public class SpikeTrapStructure : MonoBehaviour
    {
        /// <summary>How often it looks. Traps do not need frame-rate reflexes.</summary>
        const float LookInterval = 0.2f;

        public PlacedStructure Structure { get; private set; }

        float _nextLook;
        float _nextBite;
        bool _warnedBlunt;

        public void Bind(PlacedStructure structure)
        {
            Structure = structure;
        }

        /// <summary>
        /// The perk that scales trap damage applies here too, read off the world that
        /// owns this piece. Pulling it rather than having it pushed means a trap
        /// loaded from a save is wired the moment it exists.
        /// </summary>
        PlayerProgression Progression
        {
            get { return Structure != null && Structure.Owner != null ? Structure.Owner.Progression : null; }
        }

        /// <summary>Too worn to bite, but still standing and still repairable.</summary>
        public bool IsBlunt
        {
            get { return Structure == null || TrapRules.IsBlunt(Structure.HealthFraction); }
        }

        /// <summary>What the crosshair says about it.</summary>
        public string Readout
        {
            get
            {
                if (Structure == null) return "";
                if (IsBlunt) return "Blunt - repair with a hammer";
                return string.Format("Spikes - about {0} more", TrapRules.BitesRemaining(Structure.HealthFraction));
            }
        }

        void Update()
        {
            if (Structure == null || !Structure.IsAlive) return;
            if (Time.time < _nextLook) return;

            _nextLook = Time.time + LookInterval;

            if (IsBlunt)
            {
                // Said once per blunting, not once per look. A trap that complained
                // four times a second would be worse than one that said nothing.
                if (!_warnedBlunt)
                {
                    _warnedBlunt = true;
                    Notifications.PostFormat("{0} is blunt", Structure.Definition.displayName);
                }
                return;
            }

            _warnedBlunt = false;

            if (Time.time < _nextBite) return;

            var victim = Sweep();
            if (victim == null) return;

            _nextBite = Time.time + Mathf.Max(0.15f, Structure.Definition.trapIntervalSeconds);
            Bite(victim);
        }

        /// <summary>
        /// The nearest live hostile standing on the spikes.
        ///
        /// Reads the live zombie list rather than sweeping physics, for the reason
        /// melee had to: an overlap query in a dense spike bed fills its buffer with
        /// neighbouring traps, foundations and terrain before it reaches the zombie
        /// actually standing on this one, and the trap then silently never fires -
        /// worst exactly where the player built the most of them.
        /// </summary>
        AI.Zombie Sweep()
        {
            var spawns = Structure.Owner != null ? Structure.Owner.Spawns : null;
            if (spawns == null) return null;

            var alive = spawns.Alive;
            if (alive == null || alive.Count == 0) return null;

            float radius = Mathf.Max(0.4f, Structure.Definition.trapRadius);
            Vector3 centre = Centre;

            AI.Zombie best = null;
            float bestSqr = radius * radius;

            for (int i = 0; i < alive.Count; i++)
            {
                var zombie = alive[i];
                if (zombie == null || !zombie.IsAlive) continue;

                // Measured at the feet: a zombie is standing on the spikes or it is
                // not, and its chest being within reach of a bed on the floor below
                // would have traps biting through a ceiling.
                Vector3 feet = zombie.transform.position;
                float dy = feet.y - centre.y;
                if (dy < -0.6f || dy > 1.2f) continue;

                float dx = feet.x - centre.x;
                float dz = feet.z - centre.z;
                float sqr = dx * dx + dz * dz;

                if (sqr > bestSqr) continue;

                bestSqr = sqr;
                best = zombie;
            }

            return best;
        }

        /// <summary>
        /// The middle of the trap's cell. A structure's transform sits on the cell
        /// corner, so measuring from it skews the whole reach three quarters of a
        /// metre towards -X and -Z - biting things on the diagonal neighbour while
        /// barely covering its own far corner.
        /// </summary>
        Vector3 Centre
        {
            get { return transform.position + new Vector3(0.5f, 0.2f, 0.5f); }
        }

        void Bite(AI.Zombie victim)
        {
            var def = Structure.Definition;

            float damage = TrapRules.BiteDamage(def.trapDamage, Structure.HealthFraction);

            var perks = Progression;
            if (perks != null) damage *= perks.Effects.Multiplier(PerkEffectType.TrapDamageMultiplier);
            if (damage <= 0f) return;

            victim.ApplyDamage(new DamageInfo
            {
                Amount = damage,
                Kind = DamageKind.Melee,
                Point = Centre,
                Direction = Vector3.up,
                Source = gameObject,
                ToolTier = 2
            });

            Audio.GameAudio.PlayAt(Audio.Sound.MeleeHitFlesh, Centre, 0.18f, 0.8f);

            // Blunting goes through the structure's health, so a spike that has been
            // chewed on by the horde and one that has been busy are the same trap.
            float worn = TrapRules.WearAfterBite(Structure.HealthFraction);
            Structure.SetHealthDirect(worn * def.maxHealth);

            // Through the same tint a chewed-on wall gets. Without this a trap worn
            // out by working looks brand new while one the horde hit looks battered,
            // which is backwards.
            StructureVisuals.ShowDamage(Structure);
        }
    }
}
