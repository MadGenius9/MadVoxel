using UnityEngine;

namespace MadVoxel.Combat
{
    /// <summary>
    /// Makes a body visibly register being hit.
    ///
    /// Before this existed, landing a swing changed exactly one thing: a float on a
    /// script. A shambler takes seven axe blows, and across all seven the player saw
    /// no flinch, no colour, no sound, nothing - which is indistinguishable from a
    /// game that is ignoring the button. Damage numbers alone do not fix it either;
    /// the eye is on the body, not on the corner of the screen.
    ///
    /// The flash goes through a <see cref="MaterialPropertyBlock"/> rather than
    /// touching the material. Every zombie of a given tint shares one material out of
    /// <see cref="Core.MaterialLibrary"/>, so setting a colour on it would flash the
    /// whole horde each time one of them got hit.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitFlash : MonoBehaviour
    {
        /// <summary>Seconds the flash lasts. Short enough to read as an impact, not a state.</summary>
        public const float Duration = 0.12f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        Renderer[] _renderers;
        Color[] _baseColours;
        MaterialPropertyBlock _block;
        float _until = -1f;
        Color _flashColour = Color.white;

        /// <summary>
        /// Caches the body's renderers. Call once the visuals exist - a body built
        /// after this runs will simply not flash, rather than throw.
        /// </summary>
        public void Capture()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
            _baseColours = new Color[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++)
            {
                var material = _renderers[i].sharedMaterial;
                _baseColours[i] = material == null
                    ? Color.white
                    : (material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId)
                        : material.HasProperty(ColorId) ? material.GetColor(ColorId)
                        : Color.white);
            }
        }

        /// <summary>Pulses the body. Repeated hits restart the flash rather than stacking.</summary>
        public void Flash(Color colour)
        {
            if (_renderers == null) Capture();
            _flashColour = colour;
            _until = Time.time + Duration;
            Apply(1f);
        }

        void LateUpdate()
        {
            if (_until < 0f) return;

            float remaining = _until - Time.time;
            if (remaining <= 0f)
            {
                Apply(0f);
                _until = -1f;
                return;
            }

            Apply(remaining / Duration);
        }

        void Apply(float strength)
        {
            if (_renderers == null || _block == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null) continue;

                Color colour = Color.Lerp(_baseColours[i], _flashColour, Mathf.Clamp01(strength));

                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, colour);
                _block.SetColor(ColorId, colour);
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
