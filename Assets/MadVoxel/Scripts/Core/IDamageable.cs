using UnityEngine;

namespace MadVoxel.Core
{
    public enum DamageKind
    {
        Melee,
        Tool,
        Fall,
        Starvation,
        Thirst,
        Explosion,
        Zombie,
        /// <summary>Storms and frost. Not anyone's fault, and not a horde's doing.</summary>
        Weather
    }

    public struct DamageInfo
    {
        public float Amount;
        public DamageKind Kind;
        public Vector3 Point;
        public Vector3 Direction;
        public GameObject Source;
        /// <summary>Tool tier of the attacker; structures resist tools below their tier.</summary>
        public int ToolTier;

        public static DamageInfo Simple(float amount, DamageKind kind)
        {
            return new DamageInfo { Amount = amount, Kind = kind };
        }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        void ApplyDamage(DamageInfo info);
    }

    /// <summary>Anything the player can press E on.</summary>
    public interface IInteractable
    {
        string InteractPrompt { get; }
        void Interact(GameObject interactor);
    }
}
