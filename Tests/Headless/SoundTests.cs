using MadVoxel.Audio;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The synthesiser.
    ///
    /// Audio is the one system where "it sounds fine" cannot be asserted from here -
    /// there is no audio hardware, and nobody has heard a single one of these. What
    /// can be asserted is everything underneath that: an envelope that closes, a tail
    /// that reaches zero, a buffer that is finite and audible and does not clip.
    ///
    /// Those are exactly the failures that are impossible to diagnose by ear. A click
    /// at the end of a hit sound is indistinguishable from a second, wronger hit; a
    /// voice that renders silent is indistinguishable from an unwired event. Both are
    /// arithmetic, and both are caught here instead.
    /// </summary>
    public static class SoundTests
    {
        public static void Run()
        {
            Envelope();
            Rendering();
            Bank();
        }

        // --------------------------------------------------------------- envelope

        static void Envelope()
        {
            Harness.Section("sound: the envelope");

            Harness.Equal(SoundSynth.Envelope(0f, 0.2f, 1f), 0f, "a sound starts at silence");
            Harness.Equal(SoundSynth.Envelope(0.2f, 0.2f, 1f), 1f, "and peaks at the end of the attack");
            Harness.Equal(SoundSynth.Envelope(1f, 0.2f, 1f), 0f, "and returns to silence at the end");

            Harness.Check(SoundSynth.Envelope(0.1f, 0.2f, 1f) > 0f
                          && SoundSynth.Envelope(0.1f, 0.2f, 1f) < 1f,
                "the attack ramps rather than jumping");

            // A sharper decay is quieter in the middle of the tail, never louder.
            Harness.Check(SoundSynth.Envelope(0.5f, 0.1f, 4f) < SoundSynth.Envelope(0.5f, 0.1f, 1f),
                "a snappier decay is further along by halfway");

            // An instant attack is the common case for an impact and must not divide by zero.
            Harness.Equal(SoundSynth.Envelope(0f, 0f, 1f), 1f, "a sound with no attack is loud immediately");
            Harness.Equal(SoundSynth.Envelope(1f, 0f, 1f), 0f, "and still lands on silence");

            // An envelope that is all attack has no tail, but must stay in range.
            Harness.Check(SoundSynth.Envelope(0.5f, 1f, 1f) >= 0f && SoundSynth.Envelope(0.5f, 1f, 1f) <= 1f,
                "an all-attack envelope stays in range");
        }

        // -------------------------------------------------------------- rendering

        static void Rendering()
        {
            Harness.Section("sound: rendering");

            var voice = SoundBank.Voice(Sound.MeleeHitFlesh);
            int count = SoundSynth.SampleCount(voice);

            Harness.Check(count > 0, "a voice has a length");
            Harness.Equal(count, Mathf.RoundToInt(voice.Seconds * SoundSynth.SampleRate),
                "and it is the length it asked for");

            var buffer = new float[count];
            Harness.Equal(SoundSynth.Render(buffer, voice), count, "rendering fills the whole buffer");

            // A buffer the caller sized wrong is truncated, not refused: a short sound
            // during a load beats an exception during a load.
            var small = new float[16];
            Harness.Equal(SoundSynth.Render(small, voice), 16, "a short buffer is filled as far as it goes");
            Harness.Equal(SoundSynth.Render(null, voice), 0, "and no buffer renders nothing rather than throwing");

            // Determinism. The noise is seeded, so the same voice is the same sound -
            // which is what makes any of this testable at all.
            var again = new float[count];
            SoundSynth.Render(again, voice);
            bool identical = true;
            for (int i = 0; i < count; i++) if (buffer[i] != again[i]) { identical = false; break; }
            Harness.Check(identical, "the same voice renders the same samples every time");

            // A zero-length request still produces something playable.
            var degenerate = new SoundSynth.Voice { Wave = SoundSynth.Wave.Sine, StartHz = 200f, Seconds = 0f };
            Harness.Check(SoundSynth.SampleCount(degenerate) >= 1, "a zero-length voice still has a sample");

            // And an absurd one cannot allocate a minute of audio.
            var huge = new SoundSynth.Voice { Wave = SoundSynth.Wave.Sine, StartHz = 200f, Seconds = 9999f };
            Harness.Equal(SoundSynth.SampleCount(huge),
                Mathf.RoundToInt(SoundSynth.MaxSeconds * SoundSynth.SampleRate),
                "and an absurd one is capped");
        }

        // ------------------------------------------------------------------- bank

        static void Bank()
        {
            Harness.Section("sound: every voice in the bank");

            for (int s = 0; s < SoundBank.All.Length; s++)
            {
                var sound = SoundBank.All[s];
                var voice = SoundBank.Voice(sound);

                int count = SoundSynth.SampleCount(voice);
                var buffer = new float[count];
                SoundSynth.Render(buffer, voice);

                float peak = 0f;
                bool finite = true;
                for (int i = 0; i < count; i++)
                {
                    float v = buffer[i];
                    if (float.IsNaN(v) || float.IsInfinity(v)) { finite = false; break; }
                    if (Mathf.Abs(v) > peak) peak = Mathf.Abs(v);
                }

                Harness.Check(finite, sound + " renders finite samples");

                // Silent is the failure that looks exactly like an unwired event.
                Harness.Check(peak > 0.02f, sound + " is actually audible");

                // And clipped is the failure that sounds like a different, worse sound.
                Harness.Check(peak <= 1f, sound + " does not clip");

                // Both ends at rest. A one-shot that stops on a non-zero sample clicks
                // every single time it plays.
                Harness.Check(Mathf.Abs(buffer[0]) < 0.02f, sound + " starts from silence");
                Harness.Check(Mathf.Abs(buffer[count - 1]) < 0.02f, sound + " ends in silence");
            }

            // The two that have to be told apart in the middle of a fight.
            var flesh = SoundBank.Voice(Sound.MeleeHitFlesh);
            var hard = SoundBank.Voice(Sound.MeleeHitHard);
            Harness.Check(hard.StartHz > flesh.StartHz, "a hit on stone is brighter than a hit on a body");

            var pickup = SoundBank.Voice(Sound.Pickup);
            var denied = SoundBank.Voice(Sound.Denied);
            Harness.Check(pickup.EndHz > pickup.StartHz, "a pickup rises");
            Harness.Check(denied.EndHz < denied.StartHz, "and a refusal falls");
        }
    }
}
