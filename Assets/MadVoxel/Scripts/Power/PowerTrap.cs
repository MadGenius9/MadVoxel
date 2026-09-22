using MadVoxel.Core;
using MadVoxel.Perks;
using UnityEngine;

namespace MadVoxel.Power
{
    /// <summary>
    /// A blade trap or a fence post: armed for a trickle, expensive only while it is
    /// actually biting. It reports what it sees to its grid node, which is what lets
    /// the power solve decide whether it has the watts to swing - and what lets a
    /// small generator arm a fence it could never run flat out.
    ///
    /// It also means a trap adds nothing to claim heat until something walks into it.
    /// A defended base is quiet right up until it is not.
    /// </summary>
    public class PowerTrap : MonoBehaviour
    {
        /// <summary>How often it looks. Traps do not need frame-rate reflexes.</summary>
        const float LookInterval = 0.25f;

        static readonly Collider[] Hits = new Collider[16];

        PowerDeviceStructure _device;
        PlayerProgression _progression;

        float _nextLook;
        float _nextBite;

        public void Init(PowerDeviceStructure device, PlayerProgression progression)
        {
            _device = device;
            _progression = progression;
        }

        void Update()
        {
            if (_device == null || _device.Device == null) return;

            var node = _device.Node;
            if (node == null) return;

            if (Time.time >= _nextLook)
            {
                _nextLook = Time.time + LookInterval;
                node.Triggered = Sweep(node) != null;
            }

            if (!node.Triggered || !node.IsPowered) return;
            if (Time.time < _nextBite) return;

            var victim = Sweep(node);
            if (victim == null) return;

            _nextBite = Time.time + Mathf.Max(0.15f, _device.Device.trapIntervalSeconds);
            Bite(victim);
        }

        /// <summary>The nearest damageable thing in reach that is not the trap itself.</summary>
        IDamageable Sweep(PowerNode node)
        {
            float radius = _device.Device.trapRadius;
            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.6f, radius,
                Hits, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (Hits[i] == null) continue;
                if (Hits[i].transform.IsChildOf(transform)) continue;

                // Traps do not bite the player who built them. A grid that could kill
                // you for walking past your own fence would just be a trap for you.
                var zombie = Hits[i].GetComponentInParent<AI.Zombie>();
                if (zombie == null || !zombie.IsAlive) continue;

                return zombie;
            }
            return null;
        }

        void Bite(IDamageable victim)
        {
            float damage = _device.Device.trapDamage;
            if (_progression != null)
            {
                damage *= _progression.Effects.Multiplier(PerkEffectType.TrapDamageMultiplier);
            }

            victim.ApplyDamage(new DamageInfo
            {
                Amount = damage,
                Kind = DamageKind.Melee,
                Point = transform.position,
                Direction = Vector3.up,
                Source = gameObject,
                ToolTier = 2
            });
        }
    }
}
