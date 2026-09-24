using UnityEngine;

namespace MadVoxel.Combat
{
    /// <summary>
    /// Which target a swing lands on.
    ///
    /// Melee used to be the same pinpoint camera ray that mining uses, and that is
    /// the wrong shape for a swing. Mining wants precision - you are naming one
    /// block out of a wall of identical blocks. A swing wants the opposite: the
    /// player has committed to a direction and an animation, and the game should
    /// find them something to hit.
    ///
    /// A ray also cannot express the two things that decide most real melee
    /// exchanges. A shambler's collider is a 0.32m capsule under a body whose arms
    /// reach out past 0.43m, so the crosshair can sit on a visible shoulder and pass
    /// through empty space. And once something is close enough to chew on you its
    /// collider can swallow the camera, at which point a ray cast from inside it
    /// reports nothing at all. Both read to the player as "my swing did nothing",
    /// which is the least recoverable feeling a melee game can hand someone.
    ///
    /// So a swing is a cone, not a line: everything within <see cref="Reach"/> and
    /// inside the arc is a candidate, and the best one is picked by a score rather
    /// than by raw distance. Nearest-wins is tempting and wrong - it hands the swing
    /// to whatever brushed your elbow instead of the zombie you are looking at.
    ///
    /// Pure and engine-free, because "did my swing land" is exactly the question
    /// that is miserable to answer by playing the game and easy to answer here.
    /// </summary>
    public static class MeleeArc
    {
        /// <summary>
        /// Metres from the eye. Comfortably past the 0.72m two character capsules
        /// settle at, so a zombie pressed against you is inside the swing, not behind it.
        /// </summary>
        public const float Reach = 3.2f;

        /// <summary>
        /// Half-angle of the swing, in degrees. Generous by shooter standards and
        /// deliberately so: a club is not a rifle, and 60 degrees is roughly "in front
        /// of me", which is what the player believes they are aiming at.
        /// </summary>
        public const float HalfAngleDegrees = 60f;

        /// <summary>
        /// How much the score prefers centred over close, 0 to 1.
        ///
        /// At 0 the nearest candidate always wins and the swing gets stolen by
        /// whatever is underfoot. At 1 distance stops mattering and a far target
        /// dead ahead beats one in your face. Two thirds aim, one third proximity
        /// puts the swing on what the player is looking at while still letting an
        /// adjacent target win when nothing is properly centred.
        /// </summary>
        public const float AimWeight = 0.66f;

        /// <summary>A candidate for a swing. Positions are world space.</summary>
        public struct Candidate
        {
            /// <summary>Caller's handle - an index, an id, whatever it needs back.</summary>
            public int Id;

            /// <summary>
            /// The point the swing is judged against. The centre of mass, not the
            /// origin: a zombie's transform sits at its feet, and judging a swing
            /// against someone's ankles makes every hit look like a miss.
            /// </summary>
            public Vector3 Centre;

            /// <summary>
            /// Radius of the body, for the reach test. A swing that clips the edge of
            /// something still lands, which is the whole point of the wider arc.
            /// </summary>
            public float Radius;
        }

        /// <summary>How a swing resolved, and why.</summary>
        public struct Result
        {
            public bool Hit;
            public int Id;

            /// <summary>
            /// Higher is better. Up to 1 for a body in front of you, and above 1 for
            /// one that has closed far enough to overlap the eye, so a target in your
            /// face always outranks one at arm's length however well aimed.
            /// </summary>
            public float Score;

            /// <summary>Metres from the eye to the candidate's surface.</summary>
            public float Distance;

            public static Result Miss { get { return new Result { Hit = false, Id = -1 }; } }
        }

        /// <summary>
        /// Picks what a swing lands on, or reports a miss.
        ///
        /// <paramref name="forward"/> need not be normalised; a zero-length one is a
        /// miss rather than a crash, because a look direction can be degenerate for a
        /// frame while the rig is being built.
        /// </summary>
        public static Result Resolve(Vector3 eye, Vector3 forward, Candidate[] candidates, int count,
                                     float reach = Reach, float halfAngleDegrees = HalfAngleDegrees)
        {
            if (candidates == null || count <= 0) return Result.Miss;

            float forwardLength = forward.magnitude;
            if (forwardLength < 1e-4f) return Result.Miss;
            Vector3 aim = forward / forwardLength;

            // Dot product beats an acos per candidate, and the comparison is the same one.
            float minDot = Mathf.Cos(Mathf.Clamp(halfAngleDegrees, 1f, 179f) * Mathf.Deg2Rad);

            var best = Result.Miss;
            float bestScore = 0f;

            int limit = Mathf.Min(count, candidates.Length);
            for (int i = 0; i < limit; i++)
            {
                var candidate = candidates[i];
                float radius = Mathf.Max(0f, candidate.Radius);

                Vector3 toTarget = candidate.Centre - eye;
                float centreDistance = toTarget.magnitude;

                // Surface distance, so a wide body counts as reachable from further out.
                float surfaceDistance = Mathf.Max(0f, centreDistance - radius);
                if (surfaceDistance > reach) continue;

                // Overlapping the eye. The player unquestionably means to hit it, so it
                // outranks anything merely in front of them - but it does not end the
                // scan. Two bodies can be inside you at once during a siege, and
                // returning the first one found hands the swing to whichever spawned
                // earlier no matter which way the player is facing.
                if (centreDistance <= radius || centreDistance < 1e-4f)
                {
                    // Still scored by facing, so the tie between two of them is broken
                    // by the thing the player is actually looking at.
                    float facing = centreDistance < 1e-4f
                        ? 0.5f
                        : Mathf.Clamp01(Vector3.Dot(aim, toTarget / centreDistance) * 0.5f + 0.5f);

                    float overlapScore = 1f + facing;
                    if (best.Hit && overlapScore <= bestScore) continue;

                    bestScore = overlapScore;
                    best = new Result { Hit = true, Id = candidate.Id, Score = overlapScore, Distance = 0f };
                    continue;
                }

                Vector3 direction = toTarget / centreDistance;
                float dot = Vector3.Dot(aim, direction);

                // The arc is measured to the body, not to its centre: a target close
                // enough that its own width spans the arc is in front of you whatever
                // the angle to its middle says.
                float angularRadius = centreDistance > radius
                    ? Mathf.Asin(Mathf.Clamp01(radius / centreDistance))
                    : Mathf.PI * 0.5f;
                float widenedMinDot = Mathf.Cos(Mathf.Min(Mathf.PI,
                    Mathf.Acos(Mathf.Clamp(minDot, -1f, 1f)) + angularRadius));

                if (dot < widenedMinDot) continue;

                // Both terms are 0-to-1 and higher-is-better, so the weighting reads
                // as written rather than as a pair of opposed units.
                float aimScore = Mathf.Clamp01((dot - widenedMinDot) / Mathf.Max(1e-4f, 1f - widenedMinDot));
                float nearScore = 1f - Mathf.Clamp01(surfaceDistance / Mathf.Max(1e-4f, reach));
                float score = aimScore * AimWeight + nearScore * (1f - AimWeight);

                if (best.Hit && score <= bestScore) continue;

                bestScore = score;
                best = new Result
                {
                    Hit = true,
                    Id = candidate.Id,
                    Score = score,
                    Distance = surfaceDistance
                };
            }

            return best;
        }
    }
}
