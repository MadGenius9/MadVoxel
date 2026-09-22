using UnityEngine;

namespace MadVoxel.Core
{
    /// <summary>
    /// Drives the sun, ambient and fog from the clock. Grimy sodium-orange dusk,
    /// cold blue moonlight, heavy haze at range so 50-100m silhouettes stay readable.
    /// </summary>
    public class SkyController : MonoBehaviour
    {
        WorldClock _clock;
        Light _sun;
        Light _moon;

        static readonly Color NightSky = new Color(0.035f, 0.045f, 0.065f);
        static readonly Color DawnSky = new Color(0.36f, 0.24f, 0.16f);
        static readonly Color DaySky = new Color(0.44f, 0.47f, 0.48f);
        static readonly Color DuskSky = new Color(0.42f, 0.22f, 0.10f);

        static readonly Color SunDay = new Color(1.0f, 0.96f, 0.86f);
        static readonly Color SunLow = new Color(1.0f, 0.62f, 0.30f);
        static readonly Color MoonColour = new Color(0.55f, 0.66f, 0.95f);

        public bool BloodMoon { get; set; }

        public void Init(WorldClock clock)
        {
            _clock = clock;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            _sun = sunGo.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.82f;

            var moonGo = new GameObject("Moon");
            moonGo.transform.SetParent(transform, false);
            _moon = moonGo.AddComponent<Light>();
            _moon.type = LightType.Directional;
            _moon.shadows = LightShadows.None;
            _moon.color = MoonColour;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        }

        void LateUpdate()
        {
            if (_clock == null) return;

            float hour = _clock.HourOfDay;

            // Sun sweeps from sunrise at 06:00 to sunset at 18:00 (visually; gameplay
            // dawn/dusk come from the config and may be slightly offset).
            float sunAngle = (hour / 24f) * 360f - 90f;
            _sun.transform.rotation = Quaternion.Euler(sunAngle, 35f, 0f);
            _moon.transform.rotation = Quaternion.Euler(sunAngle + 180f, 35f, 0f);

            float sunHeight = Mathf.Sin((hour / 24f) * Mathf.PI * 2f - Mathf.PI * 0.5f);
            float dayFactor = Mathf.Clamp01(sunHeight * 3f);
            float horizon = Mathf.Clamp01(1f - Mathf.Abs(sunHeight) * 4f);

            _sun.intensity = Mathf.Lerp(0f, 1.35f, dayFactor);
            _sun.color = Color.Lerp(SunLow, SunDay, Mathf.Clamp01(sunHeight * 2.2f));
            _sun.enabled = _sun.intensity > 0.01f;

            float moonBase = Mathf.Clamp01(-sunHeight * 2.4f);
            _moon.intensity = moonBase * (BloodMoon ? 0.55f : 0.30f);
            _moon.color = BloodMoon ? new Color(0.85f, 0.28f, 0.22f) : MoonColour;
            _moon.enabled = _moon.intensity > 0.01f;

            Color sky = Color.Lerp(NightSky, DaySky, dayFactor);
            Color warm = hour < 12f ? DawnSky : DuskSky;
            sky = Color.Lerp(sky, warm, horizon * 0.75f);
            if (BloodMoon && moonBase > 0.05f)
            {
                sky = Color.Lerp(sky, new Color(0.19f, 0.045f, 0.045f), moonBase * 0.8f);
            }

            RenderSettings.ambientLight = sky * 1.15f;
            RenderSettings.fogColor = sky;
            RenderSettings.fogDensity = Mathf.Lerp(0.014f, 0.0055f, dayFactor);

            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = sky;
        }
    }
}
