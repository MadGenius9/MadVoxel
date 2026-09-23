using UnityEngine;

namespace MadVoxel.Combat
{
    /// <summary>
    /// What an arrow does between leaving the bow and hitting something.
    ///
    /// The whole design of ranged combat is in one decision: <b>draw</b>. A snapped
    /// shot leaves slowly, drops hard and hits softly; a full draw flies flat and
    /// hurts. That is the trade a player makes with a zombie closing on them, and it
    /// is the reason a bow is not just a melee swing at range.
    ///
    /// There is deliberately no damage falloff with distance on top of that. Drop is
    /// already the range limiter, and stacking a second one makes a bow feel like it
    /// is apologising for existing.
    ///
    /// Pure and engine-free so the arc can be checked without firing one: the aim dot
    /// and the arrow itself must integrate identically, or the game lies about where
    /// the shot is going.
    /// </summary>
    public static class Ballistics
    {
        /// <summary>Metres per second squared. Heavier than the world's gravity, so arrows read as arrows.</summary>
        public const float Gravity = 22f;

        /// <summary>A shot below this draw is not worth the arrow, and is refused.</summary>
        public const float MinimumDraw = 0.15f;

        /// <summary>What a fully drawn bow does to its damage. A snapped shot gets the floor.</summary>
        public const float MinDamageFraction = 0.3f;

        /// <summary>Seconds an arrow lives before it is given up on.</summary>
        public const float MaxFlightSeconds = 6f;

        /// <summary>How far through the draw, 0 to 1.</summary>
        public static float Draw01(float heldSeconds, float drawSeconds)
        {
            if (drawSeconds <= 0f) return 1f;
            return Mathf.Clamp01(heldSeconds / drawSeconds);
        }

        /// <summary>
        /// Launch speed for a draw. Linear rather than eased: the player is reading a
        /// bar and expecting the middle of it to mean the middle.
        /// </summary>
        public static float LaunchSpeed(float minSpeed, float maxSpeed, float draw01)
        {
            return Mathf.Lerp(Mathf.Max(0.1f, minSpeed), Mathf.Max(minSpeed, maxSpeed), Mathf.Clamp01(draw01));
        }

        /// <summary>
        /// Damage for a draw. Never zero at the bottom - an arrow that lands is an
        /// arrow that hurts - and never above the weapon's rating at the top.
        /// </summary>
        public static float Damage(float baseDamage, float draw01)
        {
            float scale = Mathf.Lerp(MinDamageFraction, 1f, Mathf.Clamp01(draw01));
            return Mathf.Max(0f, baseDamage) * scale;
        }

        public static bool CanRelease(float draw01)
        {
            return draw01 >= MinimumDraw;
        }

        /// <summary>
        /// One step of the arc. The arrow and the aim dot both call this, which is the
        /// only way the dot can be trusted.
        ///
        /// This is the exact solution for constant acceleration, not Euler. Stepping
        /// velocity first and then position - the obvious way - overshoots the drop by
        /// half a gravity-step each frame, which means an arrow falls further at thirty
        /// frames a second than at a hundred and twenty. A bow that shoots differently
        /// on a slower machine is not a bow anyone can learn.
        ///
        /// It also makes <see cref="DropOver"/> agree with the flight exactly, because
        /// they are now the same equation.
        /// </summary>
        public static void Step(ref Vector3 position, ref Vector3 velocity, float deltaSeconds)
        {
            position += velocity * deltaSeconds;
            position.y -= 0.5f * Gravity * deltaSeconds * deltaSeconds;

            velocity.y -= Gravity * deltaSeconds;
        }

        /// <summary>
        /// How far an arrow falls over a horizontal distance, ignoring drag. Used for
        /// the readout that tells a player how much to lead a far shot.
        /// </summary>
        public static float DropOver(float distance, float speed)
        {
            if (distance <= 0f || speed <= 0.01f) return 0f;

            float time = distance / speed;
            return 0.5f * Gravity * time * time;
        }

        /// <summary>
        /// The flat-ground range of a shot fired level from a given height. What the
        /// player actually means by "how far does this bow shoot".
        /// </summary>
        public static float LevelRange(float speed, float height)
        {
            if (speed <= 0.01f || height <= 0f) return 0f;

            float fall = Mathf.Sqrt(2f * Mathf.Max(0f, height) / Gravity);
            return speed * fall;
        }

        /// <summary>
        /// Walks the arc until something stops it, and returns where. The caller says
        /// what counts as solid, so this works the same against voxels, a structure, or
        /// a test's idea of a wall.
        ///
        /// Steps by a fixed slice rather than by frame time on purpose: the predicted
        /// path must not change shape when the frame rate does.
        /// </summary>
        public static Vector3 PredictImpact(Vector3 origin, Vector3 direction, float speed,
                                            float maxSeconds, System.Func<Vector3, bool> isSolid)
        {
            const float Slice = 1f / 30f;

            var position = origin;
            var velocity = direction.normalized * speed;

            int steps = Mathf.Max(1, Mathf.CeilToInt(maxSeconds / Slice));
            for (int i = 0; i < steps; i++)
            {
                var previous = position;
                var previousVelocity = velocity;

                Step(ref position, ref velocity, Slice);
                if (isSolid == null || !isSolid(position)) continue;

                return Refine(previous, previousVelocity, Slice, isSolid);
            }

            return position;
        }

        /// <summary>
        /// Narrows the last step down to where the arc actually meets the surface.
        ///
        /// Without this the dot sits wherever the previous whole step landed, which at
        /// forty metres a second is over a metre short of the wall - and a dot that is
        /// reliably wrong is worse than no dot, because the player aims off it.
        /// </summary>
        static Vector3 Refine(Vector3 start, Vector3 velocity, float slice, System.Func<Vector3, bool> isSolid)
        {
            float low = 0f;
            float high = slice;

            // Four halvings puts it within a sixteenth of a step - a few centimetres at
            // any speed a bow reaches.
            for (int i = 0; i < 4; i++)
            {
                float mid = (low + high) * 0.5f;

                var probe = start;
                var probeVelocity = velocity;
                Step(ref probe, ref probeVelocity, mid);

                if (isSolid(probe)) high = mid;
                else low = mid;
            }

            var result = start;
            var resultVelocity = velocity;
            Step(ref result, ref resultVelocity, low);
            return result;
        }

        /// <summary>The one line the HUD shows while a bow is drawn.</summary>
        public static string DrawLine(float draw01)
        {
            if (draw01 >= 0.999f) return "FULL DRAW";
            if (!CanRelease(draw01)) return "DRAWING";

            return string.Format("{0}%", Mathf.FloorToInt(Mathf.Clamp01(draw01) * 100f));
        }
    }
}
