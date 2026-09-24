using MadVoxel.Core;
using MadVoxel.Core.Player;
using UnityEngine;
using UnityEngine.UI;

namespace MadVoxel.UI
{
    /// <summary>
    /// The half of combat that happens on the screen rather than in the numbers.
    ///
    /// For a long time this layer did not exist, and the result was a game you could
    /// not fight in. A shambler takes seven axe blows; across all seven nothing
    /// flashed, nothing moved, no number appeared, and the only evidence a swing had
    /// landed was the zombie eventually falling over. Being mauled was worse -
    /// <see cref="PlayerStats.Damaged"/> had no subscribers anywhere in the project,
    /// so the first news of a fight going badly was the death screen.
    ///
    /// None of that is a balance problem, and none of it shows up in a test that
    /// checks arithmetic. It is the difference between a combat system that works and
    /// one that a player believes is broken.
    ///
    /// Lives on its own canvas just above the HUD so none of this has to be threaded
    /// through <see cref="HudView"/>, and dies with the gameplay UI.
    /// </summary>
    public class CombatFeedbackView : MonoBehaviour
    {
        const int DamageNumberCount = 8;
        const float DamageNumberSeconds = 0.85f;
        const float HitMarkerSeconds = 0.18f;
        const float PlayerFlashSeconds = 0.45f;

        /// <summary>Health fraction below which the edges stay lit and breathing.</summary>
        const float LowHealth = 0.3f;

        Canvas _canvas;
        PlayerStats _stats;

        RectTransform _hitMarker;
        Graphic[] _hitMarkerParts;
        float _hitMarkerUntil;
        bool _hitMarkerWasKill;
        bool _hitMarkerWasHeadshot;

        Image _vignette;
        float _flashUntil;
        float _flashStrength;

        Text[] _numbers;
        float[] _numberUntil;
        Vector2[] _numberDrift;
        bool[] _numberIsLoud;
        int _nextNumber;

        public void Init(PlayerStats stats)
        {
            _stats = stats;

            _canvas = UIKit.CreateCanvas("CombatFeedback", 1, transform);
            // Nothing here is clickable, and a full-screen image that eats the mouse
            // would be a miserable bug to track down from the symptom.
            var raycaster = _canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;

            BuildVignette();
            BuildHitMarker();
            BuildDamageNumbers();

            Combat.CombatEvents.HitLanded += OnHitLanded;
            if (_stats != null) _stats.Damaged += OnPlayerDamaged;
        }

        void OnDestroy()
        {
            // Static event, so this is not optional: a stale subscriber would keep a
            // destroyed view alive and start throwing on the next world load.
            Combat.CombatEvents.HitLanded -= OnHitLanded;
            if (_stats != null) _stats.Damaged -= OnPlayerDamaged;
        }

        // ------------------------------------------------------------------ build

        void BuildVignette()
        {
            _vignette = UIKit.Image(_canvas.transform, "Vignette", new Color(0f, 0f, 0f, 0f));
            _vignette.sprite = VignetteSprite();
            _vignette.raycastTarget = false;
            UIKit.Stretch(_vignette.rectTransform);
        }

        /// <summary>
        /// A radial fade, built rather than authored - the project ships no textures.
        /// Transparent in the middle so it never sits between the player and what they
        /// are aiming at, opaque at the corners where the eye still catches it.
        /// </summary>
        static Sprite _vignetteSprite;

        static Sprite VignetteSprite()
        {
            if (_vignetteSprite != null) return _vignetteSprite;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color32[size * size];
            float centre = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - centre) / centre;
                    float dy = (y - centre) / centre;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;

                    // Empty well past the crosshair, then rising hard into the corners.
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, d));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            _vignetteSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            return _vignetteSprite;
        }

        /// <summary>
        /// Four ticks leaning away from the centre. Deliberately not the crosshair
        /// itself - the crosshair means "where", the marker means "yes".
        /// </summary>
        void BuildHitMarker()
        {
            _hitMarker = UIKit.Rect(_canvas.transform, "HitMarker");
            UIKit.Place(_hitMarker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));

            _hitMarkerParts = new Graphic[4];
            for (int i = 0; i < 4; i++)
            {
                var tick = UIKit.Image(_hitMarker, "Tick" + i, Color.white);
                tick.raycastTarget = false;

                var rect = tick.rectTransform;
                rect.sizeDelta = new Vector2(3f, 13f);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

                // Corners of an X, each rotated to point outwards.
                float angle = 45f + i * 90f;
                rect.anchoredPosition = Quaternion.Euler(0f, 0f, -angle) * new Vector3(0f, 15f, 0f);
                rect.localRotation = Quaternion.Euler(0f, 0f, angle);

                _hitMarkerParts[i] = tick;
            }

            _hitMarker.gameObject.SetActive(false);
        }

        void BuildDamageNumbers()
        {
            _numbers = new Text[DamageNumberCount];
            _numberUntil = new float[DamageNumberCount];
            _numberDrift = new Vector2[DamageNumberCount];
            _numberIsLoud = new bool[DamageNumberCount];

            for (int i = 0; i < DamageNumberCount; i++)
            {
                var label = UIKit.Label(_canvas.transform, "Damage" + i, "", 30, TextAnchor.MiddleCenter, Color.white);
                UIKit.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(220f, 44f));
                label.gameObject.SetActive(false);
                _numbers[i] = label;
                _numberUntil[i] = -1f;
            }
        }

        // ----------------------------------------------------------------- events

        void OnHitLanded(Combat.HitReport hit)
        {
            bool headshot = hit.Zone == Combat.HitZone.Head;

            _hitMarkerUntil = Time.time + (hit.Killed || headshot ? HitMarkerSeconds * 2f : HitMarkerSeconds);
            _hitMarkerWasKill = hit.Killed;
            _hitMarkerWasHeadshot = headshot;
            _hitMarker.gameObject.SetActive(true);

            // A number only where it is the truth. Bodies take the damage they are
            // dealt, but a build piece divides by its own resistance before applying
            // it, so a figure floating over a wall would overstate the blow by the
            // whole resistance factor. The marker and the impact still land; only the
            // number, which would be a lie, is withheld.
            if (hit.Victim is IMeleeTarget)
            {
                ShowDamage(Mathf.Max(1, Mathf.RoundToInt(hit.Damage)), hit.Killed, hit.Zone);
            }
        }

        void OnPlayerDamaged(DamageInfo info)
        {
            // Only things that hit you. Starvation and thirst tick once a second, so
            // treating them as blows means a grunt every second and a vignette that
            // never clears - at which point the flash stops meaning "something is
            // attacking me", which is the only thing it is for. Going hungry already
            // shows up through the low-health pulse below, as health actually falls.
            if (!IsAttack(info.Kind)) return;

            _flashUntil = Time.time + PlayerFlashSeconds;

            // Scaled by the bite, so a shambler's swipe and a brute's do not read the
            // same. Floored well above nothing: every hit has to be noticed.
            float share = _stats != null && _stats.MaxHealth > 0f ? info.Amount / _stats.MaxHealth : 0.1f;
            _flashStrength = Mathf.Clamp(share * 3.2f, 0.35f, 1f);

            Audio.GameAudio.Play(Audio.Sound.PlayerHurt, 0.1f, _flashStrength);
        }

        static bool IsAttack(DamageKind kind)
        {
            return kind == DamageKind.Zombie
                || kind == DamageKind.Melee
                || kind == DamageKind.Explosion
                || kind == DamageKind.Fall;
        }

        void ShowDamage(int amount, bool killed, Combat.HitZone zone)
        {
            int slot = _nextNumber;
            _nextNumber = (_nextNumber + 1) % DamageNumberCount;

            string tag = Combat.HitZones.Label(zone);
            bool loud = killed || zone == Combat.HitZone.Head;

            var label = _numbers[slot];
            label.text = killed ? amount + "  KILL"
                       : tag.Length > 0 ? amount + "  " + tag
                       : amount.ToString();
            label.fontSize = loud ? 38 : 30;
            _numberIsLoud[slot] = loud;
            label.gameObject.SetActive(true);

            _numberUntil[slot] = Time.time + DamageNumberSeconds;

            // Scattered so a flurry of hits does not stack into one illegible column.
            _numberDrift[slot] = new Vector2(Random.Range(-70f, 70f), Random.Range(34f, 62f));
            label.rectTransform.anchoredPosition = new Vector2(_numberDrift[slot].x * 0.35f, 40f);
        }

        // ------------------------------------------------------------------- tick

        void Update()
        {
            TickHitMarker();
            TickNumbers();
            TickVignette();
        }

        void TickHitMarker()
        {
            if (!_hitMarker.gameObject.activeSelf) return;

            float duration = _hitMarkerWasKill || _hitMarkerWasHeadshot
                ? HitMarkerSeconds * 2f
                : HitMarkerSeconds;
            float remaining = _hitMarkerUntil - Time.time;
            if (remaining <= 0f)
            {
                _hitMarker.gameObject.SetActive(false);
                return;
            }

            float t = remaining / duration;

            // Punches out and fades, which reads as impact rather than as a widget.
            _hitMarker.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, t);

            Color colour = _hitMarkerWasKill ? ClaimSlate.OxideRust
                         : _hitMarkerWasHeadshot ? ClaimSlate.SodiumGold
                         : Color.white;
            colour.a = t;
            for (int i = 0; i < _hitMarkerParts.Length; i++) _hitMarkerParts[i].color = colour;
        }

        void TickNumbers()
        {
            for (int i = 0; i < _numbers.Length; i++)
            {
                if (_numberUntil[i] < 0f) continue;

                float remaining = _numberUntil[i] - Time.time;
                if (remaining <= 0f)
                {
                    _numbers[i].gameObject.SetActive(false);
                    _numberUntil[i] = -1f;
                    continue;
                }

                float t = 1f - remaining / DamageNumberSeconds;

                var rect = _numbers[i].rectTransform;
                rect.anchoredPosition = new Vector2(
                    _numberDrift[i].x * (0.35f + t * 0.65f),
                    40f + _numberDrift[i].y * t);

                // Holds full strength for the first third, then goes. A number that
                // starts fading immediately is one the player never quite reads.
                var colour = _numberIsLoud[i] ? ClaimSlate.OxideRust : ClaimSlate.SodiumGold;
                colour.a = Mathf.Clamp01((1f - t) * 1.5f);
                _numbers[i].color = colour;
            }
        }

        void TickVignette()
        {
            float alpha = 0f;

            // The hit itself.
            if (Time.time < _flashUntil)
            {
                float t = (_flashUntil - Time.time) / PlayerFlashSeconds;
                alpha = t * t * 0.85f * _flashStrength;
            }

            // And the standing warning underneath it, breathing so it cannot be
            // mistaken for part of the scenery.
            if (_stats != null && _stats.IsAlive && _stats.MaxHealth > 0f)
            {
                float fraction = _stats.Health / _stats.MaxHealth;
                if (fraction < LowHealth)
                {
                    float severity = 1f - fraction / LowHealth;
                    float pulse = 0.72f + 0.28f * Mathf.Sin(Time.time * Mathf.Lerp(2.4f, 6f, severity));
                    alpha = Mathf.Max(alpha, severity * 0.5f * pulse);
                }
            }

            if (alpha <= 0.001f)
            {
                if (_vignette.enabled) _vignette.enabled = false;
                return;
            }

            if (!_vignette.enabled) _vignette.enabled = true;
            _vignette.color = new Color(ClaimSlate.Blood.r, ClaimSlate.Blood.g, ClaimSlate.Blood.b, Mathf.Clamp01(alpha));
        }
    }
}
