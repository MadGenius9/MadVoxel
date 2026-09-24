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

    /// <summary>
    /// Something a swing should actively seek out.
    ///
    /// A melee swing is a volume, not a line, so it needs to know what it is allowed
    /// to find - and "anything with health" is the wrong answer. Walls, doors,
    /// vehicles and colonists are all <see cref="IDamageable"/>, and a swing that
    /// hunts for the nearest one turns a fight next to your own base into demolishing
    /// it, or into clubbing the person you just recruited.
    ///
    /// So the sweep only chases things that opt in here. Your own build pieces stay
    /// hittable the honest way, through the crosshair, where you have to mean it.
    /// </summary>
    public interface IMeleeTarget : IDamageable
    {
        /// <summary>
        /// World-space middle of the body, which is not the transform. A character
        /// stands at its feet, and a swing judged against someone's ankles reads as a
        /// miss every time.
        /// </summary>
        Vector3 CentreOfMass { get; }

        /// <summary>
        /// Roughly how wide the body is. Generous is right: this is what lets a swing
        /// clip a shoulder that the physics capsule does not actually cover.
        /// </summary>
        float BodyRadius { get; }
    }

    /// <summary>Anything the player can press E on.</summary>
    public interface IInteractable
    {
        string InteractPrompt { get; }
        void Interact(GameObject interactor);
    }
}
