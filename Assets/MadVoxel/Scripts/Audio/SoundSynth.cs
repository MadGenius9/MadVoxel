using UnityEngine;

namespace MadVoxel.Audio
{
    /// <summary>
    /// Every sound in the game, generated a sample at a time.
    ///
    /// The project ships no assets - no meshes, no textures, no materials - and audio
    /// is no different. That is a constraint rather than a preference, and it turns
    /// out to be a workable one: a survival game's vocabulary is thuds, cracks,
    /// whooshes and groans, and all of those are a filtered noise burst or a swept
    /// oscillator under an envelope.
    ///
    /// One code path builds all of them. Ten bespoke generators would be ten places
    /// for a sound to clip, arrive a frame late, or come out silent, and the difference
    /// between them is a handful of numbers rather than a handful of algorithms.
    ///
    /// Pure and engine-free, for a reason that matters more here than elsewhere: a
    /// bad sample buffer is impossible to debug by ear. "It sounds wrong" does not
    /// tell you the envelope never closed, or that the filter blew up at low cutoffs,
    /// or that the last sample is a click because the tail did not reach zero. Those
    /// are all arithmetic, and arithmetic can be checked.
    /// </summary>
    public static class SoundSynth
    {
        /// <summary>
        /// Hz. Deliberately not 44100: these are short, noisy, low-fidelity sounds and
        /// the halved rate makes the generated buffers cheap enough to build during a
        /// load without anyone noticing.
        /// </summary>
        public const int SampleRate = 22050;

        /// <summary>Ceiling on generated length, so a bad number cannot allocate a minute of audio.</summary>
        public const float MaxSeconds = 4f;

        public enum Wave
        {
            /// <summary>Pure tone. Thuds, hums, the low half of an impact.</summary>
            Sine,
            /// <summary>Buzzy and vocal. Groans and engines.</summary>
            Saw,
            /// <summary>Hollow and hard. Clicks and snaps.</summary>
            Square,
            /// <summary>No pitch at all. Whooshes, gravel, breaking.</summary>
            Noise
        }

        /// <summary>
        /// One sound. Everything is a sweep - a sound whose pitch and brightness hold
        /// still reads as a beep, which is the one thing a survival game must not
        /// sound like.
        /// </summary>
        public struct Voice
        {
            public Wave Wave;

            /// <summary>Hz at the start and at the end. Equal values hold a pitch.</summary>
            public float StartHz;
            public float EndHz;

            /// <summary>Seconds. Clamped to <see cref="MaxSeconds"/>.</summary>
            public float Seconds;

            /// <summary>Fraction of the length spent rising, 0 to 1. A short attack is a hit; a long one is a groan.</summary>
            public float Attack;

            /// <summary>How sharply the tail falls. 1 is linear, higher is snappier.</summary>
            public float Decay;

            /// <summary>0 to 1 of noise blended over the oscillator. Impacts want both.</summary>
            public float NoiseMix;

            /// <summary>Low-pass cutoff in Hz at the start and end. 0 or negative means open.</summary>
            public float StartCutoff;
            public float EndCutoff;

            /// <summary>Depth of pitch wobble, 0 to 1, and its rate in Hz. Life, for a groan.</summary>
            public float Vibrato;
            public float VibratoHz;

            /// <summary>Peak level, 0 to 1.</summary>
            public float Gain;

            /// <summary>Deterministic noise seed, so the same sound is the same sound.</summary>
            public int Seed;
        }

        /// <summary>Samples a voice needs at <see cref="SampleRate"/>. Always at least one.</summary>
        public static int SampleCount(Voice voice)
        {
            float seconds = Mathf.Clamp(voice.Seconds, 1f / SampleRate, MaxSeconds);
            return Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
        }

        /// <summary>
        /// Renders a voice into <paramref name="buffer"/> and returns how many samples
        /// were written. A buffer shorter than the voice is filled as far as it goes
        /// rather than refused - the caller sized it from <see cref="SampleCount"/>,
        /// and a truncated sound beats an exception during a load.
        /// </summary>
        public static int Render(float[] buffer, Voice voice)
        {
            if (buffer == null || buffer.Length == 0) return 0;

            int count = Mathf.Min(buffer.Length, SampleCount(voice));

            float gain = Mathf.Clamp01(voice.Gain <= 0f ? 1f : voice.Gain);
            float attack = Mathf.Clamp(voice.Attack, 0f, 1f);
            float decay = Mathf.Max(0.1f, voice.Decay <= 0f ? 1f : voice.Decay);
            float noiseMix = Mathf.Clamp01(voice.NoiseMix);

            uint rng = voice.Seed == 0 ? 0x9E3779B9u : (uint)voice.Seed;

            // Running state: phase for the oscillator, and one pole of low-pass.
            float phase = 0f;
            float filtered = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 1f;

                float hz = Mathf.Lerp(voice.StartHz, voice.EndHz <= 0f ? voice.StartHz : voice.EndHz, t);
                if (voice.Vibrato > 0f && voice.VibratoHz > 0f)
                {
                    float wobble = Mathf.Sin(2f * Mathf.PI * voice.VibratoHz * i / SampleRate);
                    hz *= 1f + wobble * Mathf.Clamp01(voice.Vibrato) * 0.5f;
                }
                hz = Mathf.Max(0f, hz);

                rng = NextRandom(rng);
                float noise = (int)(rng >> 8) / 8388608f - 1f;

                float tone;
                if (voice.Wave == Wave.Noise)
                {
                    tone = noise;
                }
                else
                {
                    phase += hz / SampleRate;
                    if (phase >= 1f) phase -= (int)phase;

                    switch (voice.Wave)
                    {
                        case Wave.Saw: tone = phase * 2f - 1f; break;
                        case Wave.Square: tone = phase < 0.5f ? 1f : -1f; break;
                        default: tone = Mathf.Sin(phase * 2f * Mathf.PI); break;
                    }

                    if (noiseMix > 0f) tone = tone * (1f - noiseMix) + noise * noiseMix;
                }

                // One-pole low-pass. The coefficient is derived per sample because the
                // cutoff sweeps, and a swept filter is most of what makes a synthesised
                // thud sound like an impact rather than like a tone.
                float cutoff = Mathf.Lerp(voice.StartCutoff, voice.EndCutoff <= 0f ? voice.StartCutoff : voice.EndCutoff, t);
                if (cutoff > 0f)
                {
                    float k = Mathf.Clamp01(cutoff / (SampleRate * 0.5f));
                    filtered += (tone - filtered) * k;
                    tone = filtered;
                }

                tone *= Envelope(t, attack, decay) * gain;

                // Hard ceiling. A swept filter with resonance-adjacent settings can
                // overshoot, and a clipped sample is a click, which is exactly the
                // artefact these sounds are trying to avoid.
                buffer[i] = Mathf.Clamp(tone, -1f, 1f);
            }

            return count;
        }

        /// <summary>
        /// Rise then fall, always reaching zero at both ends.
        ///
        /// The tail landing exactly on zero is not a nicety: a one-shot that stops at a
        /// non-zero sample clicks every time it plays, and a click at the end of a hit
        /// sound is indistinguishable from a second, wronger hit.
        /// </summary>
        public static float Envelope(float t, float attack, float decay)
        {
            t = Mathf.Clamp01(t);

            // There is always a tail, however the voice was described. An envelope
            // that is all attack ends at full level, and a one-shot that stops on a
            // loud sample clicks - which is the exact artefact the shape exists to
            // avoid, and the one a caller is least likely to have meant.
            attack = Mathf.Clamp(attack, 0f, 0.95f);

            if (attack > 0f && t < attack) return t / attack;

            float remaining = (t - attack) / (1f - attack);
            return Mathf.Pow(Mathf.Clamp01(1f - remaining), decay);
        }

        /// <summary>xorshift32. Cheap, deterministic, and good enough to sound like gravel.</summary>
        static uint NextRandom(uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }
}
