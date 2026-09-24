namespace MadVoxel.Audio
{
    /// <summary>Everything the game can make a noise about.</summary>
    public enum Sound
    {
        /// <summary>The swing itself, whether or not it lands. Tells you the button worked.</summary>
        MeleeSwing,
        /// <summary>Flesh. The confirmation that a swing landed.</summary>
        MeleeHitFlesh,
        /// <summary>Wood, stone, metal - a swing that hit the world instead.</summary>
        MeleeHitHard,
        /// <summary>A body going down.</summary>
        ZombieDeath,
        /// <summary>Ambient menace, played on the horde at a distance.</summary>
        ZombieGroan,
        /// <summary>The player taking a bite.</summary>
        PlayerHurt,
        /// <summary>Too winded, wrong tier, nothing there. The sound of a refusal.</summary>
        Denied,
        /// <summary>A block coming loose.</summary>
        BlockBreak,
        /// <summary>Something going into the bag.</summary>
        Pickup,
        /// <summary>A recipe completing.</summary>
        Craft,
        /// <summary>A piece snapping into place.</summary>
        Place,
        /// <summary>The string going back.</summary>
        BowDraw,
        /// <summary>And letting go.</summary>
        BowRelease,
        /// <summary>An arrow arriving.</summary>
        ArrowHit
    }

    /// <summary>
    /// The recipe for every sound, as numbers.
    ///
    /// Kept apart from the synthesiser so that tuning how the game sounds never means
    /// touching how sound works, and kept out of the player so the whole bank can be
    /// rendered and inspected without an engine running.
    ///
    /// The numbers are guesses. Nobody has heard any of this - there is no audio
    /// hardware where it is being written - so what is verified is that every voice
    /// produces a finite, non-silent, non-clipping buffer that starts and ends at
    /// zero. Whether a shambler's groan is actually unsettling is a question for a
    /// playtest.
    /// </summary>
    public static class SoundBank
    {
        public static SoundSynth.Voice Voice(Sound sound)
        {
            switch (sound)
            {
                // Air moving. No pitch, bright at the start, closing as it passes.
                case Sound.MeleeSwing:
                    return new SoundSynth.Voice
                    {
                        Wave = SoundSynth.Wave.Noise,
                        Seconds = 0.16f, Attack = 0.25f, Decay = 2.2f,
                        StartCutoff = 4200f, EndCutoff = 700f,
                        Gain = 0.32f, Seed = 0x51ED27
                    };

                // A wet thud: low sine dropping fast, half of it gravel.
                case Sound.MeleeHitFlesh:
                    return new SoundSynth.Voice
                    {
                        Wave = SoundSynth.Wave.Sine,
                        StartHz = 190f, EndHz = 58f,
                        Seconds = 0.19f, Attack = 0.01f, Decay = 3.4f,
                        NoiseMix = 0.45f,
                        StartCutoff = 1800f, EndCutoff = 320f,
                        Gain = 0.75f, Seed = 0x2B7D19
                    };

                // Same shape, higher and harder, with the noise on top rather than through it.
                case Sound.MeleeHitHard:
                    return new SoundSynth.Voice
                    {
                        Wave = SoundSynth.Wave.Square,
                        StartHz = 420f, EndHz = 130f,
                        Seconds = 0.13f, Attack = 0.005f, Decay = 4.5f,
                        NoiseMix = 0.3f,
                        StartCutoff = 5200f, EndCutoff = 900f,
                        Gain = 0.6f, Seed = 0x7C1A44
                    };

                // Longer, lower, and it sags - a body rather than a blow.
                case Sound.ZombieDeath:
                    return new SoundSynth.Voice
                    {
                        Wave = SoundSynth.Wave.Saw,
                        StartHz = 150f, EndHz = 42f,
                        Seconds = 0.62f, Attack = 0.03f, Decay = 2.0f,
                        NoiseMix = 0.35f,
                        StartCutoff = 1100f, EndCutoff = 200f,
                        Vibrato = 0.25f, VibratoHz = 7f,
                        Gain = 0.62f, Seed = 0x1F9C02
                    };

                // Slow in, slow out, wobbling. The vibrato is doing all the work here.
                case Sound.ZombieGroan:
                    return new SoundSynth.Voice
                    {
                        Wave = SoundSynth.Wave.Saw,
                        StartHz = 96f, EndHz = 74f,
                        Seconds = 1.1f, Attack = 0.35f, Decay = 1.3f,
                        NoiseMix = 0.22f,
                        StartCutoff = 720f, EndCutoff = 420f,
                        Vibrato = 0.32f, VibratoHz = 5.5f,
                        Gain = 0.45f, Seed = 0x3E88D1
                    };

                // Short, breathy, and up in the chest rather than down in the floor, so
                // it never reads as another zombie landing a hit on something else.
                case Sound.PlayerHurt:
                    return new SoundSynth.Voice
                    {
                        Wave = SoundSynth.Wave.Saw,
                        StartHz = 240f, EndHz = 150f,
                        Seconds = 0.26f, Attack = 0.06f, Decay = 2.6f,
                        NoiseMix = 0.5f,
                        StartCutoff = 1500f, EndCutoff = 500f,
                        Gain = 0.7f, Seed = 0x6AB3F0
                    };

                // Two-tone down. Universally reads as "no".
                case Sound.Denied:
                    return new SoundSynth.Voice
                    {
                        Wave = SoundSynth.Wave.Square,
                        StartHz = 320f, EndHz = 160f,
                        Seconds = 0.13f, Attack = 0.05f, Decay = 3f,
                        StartCutoff = 2400f, EndCutoff = 1200f,
                        Gain = 0.3f, Seed = 0x4D2E77
                    };

                case Sound.BlockBreak:
                    return new SoundSynth.Voice
                    {
                        Wave = SoundSynth.Wave.Noise,
                        Seconds = 0.28f, Attack = 0.01f, Decay = 3.2f,
                        StartCutoff = 6000f, EndCutoff = 600f,
                        Gain = 0.5f, Seed = 0x15C9AE
                    };

                // Up, not down. The only difference between a pickup and a refusal.
                case Sound.Pickup:
                    return new SoundSynth.Voice
                    {
                        Wave = SoundSynth.Wave.Sine,
                        StartHz = 620f, EndHz = 990f,
                        Seconds = 0.11f, Attack = 0.1f, Decay = 3f,
                        StartCutoff = 7000f, EndCutoff = 7000f,
                        Gain = 0.3f, Seed = 0x20FA53
                    };

                case Sound.Craft:
                    return new SoundSynth.Voice
                    {
                        Wave = SoundSynth.Wave.Square,
                        StartHz = 260f, EndHz = 340f,
                        Seconds = 0.22f, Attack = 0.02f, Decay = 2.4f,
                        NoiseMix = 0.4f,
                        StartCutoff = 3200f, EndCutoff = 800f,
                        Gain = 0.45f, Seed = 0x0C7B36
                    };

                case Sound.Place:
                    return new SoundSynth.Voice
                    {
                        Wave = SoundSynth.Wave.Sine,
                        StartHz = 300f, EndHz = 170f,
                        Seconds = 0.14f, Attack = 0.01f, Decay = 3.6f,
                        NoiseMix = 0.35f,
                        StartCutoff = 2600f, EndCutoff = 700f,
                        Gain = 0.45f, Seed = 0x39E401
                    };

                // Creak. Rising cutoff is the string tightening.
                case Sound.BowDraw:
                    return new SoundSynth.Voice
                    {
                        Wave = SoundSynth.Wave.Saw,
                        StartHz = 70f, EndHz = 120f,
                        Seconds = 0.38f, Attack = 0.5f, Decay = 1.2f,
                        NoiseMix = 0.55f,
                        StartCutoff = 500f, EndCutoff = 1600f,
                        Gain = 0.25f, Seed = 0x5B1207
                    };

                case Sound.BowRelease:
                    return new SoundSynth.Voice
                    {
                        Wave = SoundSynth.Wave.Saw,
                        StartHz = 900f, EndHz = 220f,
                        Seconds = 0.15f, Attack = 0.004f, Decay = 4f,
                        NoiseMix = 0.3f,
                        StartCutoff = 6000f, EndCutoff = 1200f,
                        Gain = 0.5f, Seed = 0x72D5BC
                    };

                case Sound.ArrowHit:
                    return new SoundSynth.Voice
                    {
                        Wave = SoundSynth.Wave.Noise,
                        Seconds = 0.12f, Attack = 0.005f, Decay = 4.2f,
                        StartCutoff = 3000f, EndCutoff = 400f,
                        Gain = 0.55f, Seed = 0x48A96E
                    };

                default:
                    return Voice(Sound.Denied);
            }
        }

        /// <summary>Every sound, for building the bank and for checking all of them at once.</summary>
        public static readonly Sound[] All =
        {
            Sound.MeleeSwing, Sound.MeleeHitFlesh, Sound.MeleeHitHard, Sound.ZombieDeath,
            Sound.ZombieGroan, Sound.PlayerHurt, Sound.Denied, Sound.BlockBreak,
            Sound.Pickup, Sound.Craft, Sound.Place, Sound.BowDraw, Sound.BowRelease,
            Sound.ArrowHit
        };
    }
}
