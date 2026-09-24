using System.Collections.Generic;
using UnityEngine;

namespace MadVoxel.Audio
{
    /// <summary>
    /// Plays the synthesised bank.
    ///
    /// Everything is built once at startup and then reused, because
    /// <see cref="AudioClip.SetData"/> during a fight is a frame hitch in the one
    /// moment the player can least afford one. The whole bank is well under a second
    /// of 22kHz mono, so the memory is not worth thinking about.
    ///
    /// Voices are pooled and round-robin. A blood moon can ask for a dozen sounds in
    /// a frame, and allocating an <see cref="AudioSource"/> per one-shot would leave
    /// the garbage collector to clean up after the horde.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        const int VoiceCount = 12;

        /// <summary>
        /// Enough that a swing, a hit and a pickup can overlap without stealing each
        /// other's pitch, and few enough that nothing is stacking up unheard.
        /// </summary>
        const int FlatVoiceCount = 4;

        /// <summary>
        /// Metres. Past this a sound is inaudible, which keeps a distant horde from
        /// drowning out the one that is actually in the room.
        /// </summary>
        const float MaxDistance = 38f;

        static GameAudio _instance;

        readonly Dictionary<Sound, AudioClip> _clips = new Dictionary<Sound, AudioClip>();
        AudioSource[] _voices;
        AudioSource[] _flat;
        int _next;
        int _nextFlat;

        /// <summary>
        /// Builds the bank and takes over as the game's audio.
        ///
        /// An existing instance is torn down rather than reused. Reusing it looks
        /// safe and is not: <see cref="Object.Destroy"/> is deferred to the end of the
        /// frame, so a world restarted in place hands back an instance that is already
        /// doomed, and the game plays no sound at all for the rest of the process
        /// because nothing calls this again. Rebuilding costs well under a second of
        /// 22kHz mono and happens during a load.
        /// </summary>
        public static GameAudio Install(Transform parent)
        {
            if (_instance != null)
            {
                var stale = _instance;
                _instance = null;
                Destroy(stale.gameObject);
            }

            var go = new GameObject("GameAudio");
            if (parent != null) go.transform.SetParent(parent, false);

            _instance = go.AddComponent<GameAudio>();
            _instance.Build();
            return _instance;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Build()
        {
            for (int i = 0; i < SoundBank.All.Length; i++)
            {
                var sound = SoundBank.All[i];
                _clips[sound] = Render(sound);
            }

            _voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                // Each voice needs its own transform. Twelve AudioSources on one
                // object share one position, so moving a voice to where a zombie died
                // would drag every other sound in flight along with it.
                var holder = new GameObject("Voice" + i);
                holder.transform.SetParent(transform, false);

                var source = holder.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;              // positional
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 2.5f;
                source.maxDistance = MaxDistance;

                // Doppler off. These voices teleport across the map between one-shots,
                // and Unity reads that jump as velocity - which detunes an impact into
                // something that sounds like a completely different, wronger sound.
                source.dopplerLevel = 0f;

                _voices[i] = source;
            }

            // Separate, because anything the player does themselves has no position -
            // it happens at their hands, and panning it would be wrong. Pooled for the
            // same reason as the rest: pitch belongs to the source, not to the clip,
            // so retuning one for a new swing would retune the last one still ringing.
            _flat = new AudioSource[FlatVoiceCount];
            for (int i = 0; i < FlatVoiceCount; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _flat[i] = source;
            }
        }

        static AudioClip Render(Sound sound)
        {
            var voice = SoundBank.Voice(sound);
            int count = SoundSynth.SampleCount(voice);

            var samples = new float[count];
            SoundSynth.Render(samples, voice);

            var clip = AudioClip.Create(sound.ToString(), count, 1, SoundSynth.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // ------------------------------------------------------------------ play

        /// <summary>
        /// At the player's own hands: swings, refusals, pickups.
        ///
        /// <paramref name="pitchSpread"/> keeps a repeated action from sounding like a
        /// loop - seven identical axe blows in five seconds is the exact case, and it
        /// is the difference between a weapon and a machine.
        /// </summary>
        public static void Play(Sound sound, float pitchSpread = 0.08f, float volume = 1f)
        {
            if (_instance == null) return;
            _instance.PlayFlat(sound, pitchSpread, volume);
        }

        /// <summary>Somewhere in the world: impacts, bodies, blocks coming loose.</summary>
        public static void PlayAt(Sound sound, Vector3 position, float pitchSpread = 0.08f, float volume = 1f)
        {
            if (_instance == null) return;
            _instance.PlayPositional(sound, position, pitchSpread, volume);
        }

        void PlayFlat(Sound sound, float pitchSpread, float volume)
        {
            AudioClip clip;
            if (_flat == null || !_clips.TryGetValue(sound, out clip)) return;

            var source = _flat[_nextFlat];
            _nextFlat = (_nextFlat + 1) % _flat.Length;

            source.pitch = Pitch(pitchSpread);
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        void PlayPositional(Sound sound, Vector3 position, float pitchSpread, float volume)
        {
            AudioClip clip;
            if (_voices == null || !_clips.TryGetValue(sound, out clip)) return;

            // Round-robin rather than hunting for a free voice: with twelve of them the
            // one being stolen is the oldest, which is the one nobody is listening to.
            var source = _voices[_next];
            _next = (_next + 1) % _voices.Length;

            source.transform.position = position;
            source.clip = clip;
            source.pitch = Pitch(pitchSpread);
            source.volume = Mathf.Clamp01(volume);
            source.Play();
        }

        static float Pitch(float spread)
        {
            if (spread <= 0f) return 1f;
            return 1f + Random.Range(-spread, spread);
        }
    }
}
