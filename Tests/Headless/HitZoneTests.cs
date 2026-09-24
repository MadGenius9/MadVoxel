using MadVoxel.Combat;

namespace MadVoxel.Headless
{
    /// <summary>
    /// Where a shot counts as landing.
    ///
    /// Worth pinning precisely because the boundary is invisible in play: there are no
    /// head colliders to hit, only a height threshold, so a head that starts too high
    /// reads as the game refusing shots that plainly connected. That is a complaint a
    /// player will make and never be able to prove, and it is one number.
    /// </summary>
    public static class HitZoneTests
    {
        public static void Run()
        {
            Zones();
            Worth();
        }

        // A 1.8m shambler standing on the ground at y = 0.
        const float Height = 1.8f;

        static HitZone At(float y) { return HitZones.Classify(y, 0f, Height); }

        static void Zones()
        {
            Harness.Section("hit zones: where a shot lands");

            Harness.Equal(At(1.62f), HitZone.Head, "the drawn head is a head shot");
            Harness.Equal(At(1.5f), HitZone.Head, "and so is the bottom of it");
            Harness.Equal(At(1.1f), HitZone.Torso, "the chest is a body shot");
            Harness.Equal(At(0.9f), HitZone.Torso, "and so is the belt");
            Harness.Equal(At(0.4f), HitZone.Legs, "the legs are legs");
            Harness.Equal(At(0.05f), HitZone.Legs, "down to the boots");

            // Slightly outside the nominal body. The capsule is not the drawing, and a
            // shot just over the skull should read as a head shot rather than as a
            // miss that somehow did damage.
            Harness.Equal(At(Height + 0.1f), HitZone.Head, "just over the top is still the head");
            Harness.Equal(At(-0.1f), HitZone.Legs, "and just under the feet is still the legs");

            // A body whose height is unknown is still a body.
            Harness.Equal(HitZones.Classify(1f, 0f, 0f), HitZone.Torso, "an unknown body takes a body shot");
            Harness.Equal(HitZones.Classify(1f, 0f, -3f), HitZone.Torso, "and so does a nonsensical one");

            // A taller zombie's head is higher in world space, which is the whole
            // reason the classification is a fraction rather than a metre.
            Harness.Equal(HitZones.Classify(1.62f, 0f, 2.15f), HitZone.Torso,
                "a brute's chest is where a shambler's head was");
            Harness.Equal(HitZones.Classify(1.95f, 0f, 2.15f), HitZone.Head, "and its head is higher up");

            // Standing on a ledge.
            Harness.Equal(HitZones.Classify(11.62f, 10f, Height), HitZone.Head,
                "a body up a hill is measured from its own feet");
        }

        static void Worth()
        {
            Harness.Section("hit zones: what a shot is worth");

            Harness.Check(HitZones.Multiplier(HitZone.Head) > HitZones.Multiplier(HitZone.Torso),
                "a head shot beats a body shot");
            Harness.Check(HitZones.Multiplier(HitZone.Torso) > HitZones.Multiplier(HitZone.Legs),
                "and a body shot beats a leg shot");
            Harness.Equal(HitZones.Multiplier(HitZone.Torso), 1f, "a body shot is the baseline");

            // A leg shot has to still be worth taking. A panicked shot at something
            // closing on you is the commonest shot in the game.
            Harness.Check(HitZones.Multiplier(HitZone.Legs) > 0.5f, "a leg shot still hurts");

            // And a head shot has to change how a fight is fought, or nobody aims.
            Harness.Check(HitZones.Multiplier(HitZone.Head) >= 2f, "a head shot is worth aiming for");

            Harness.Equal(HitZones.Label(HitZone.Head), "HEADSHOT", "the head gets a word");
            Harness.Equal(HitZones.Label(HitZone.Torso), "", "and nothing else does");
        }
    }
}
