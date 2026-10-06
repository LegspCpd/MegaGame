using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Drives the sun, sky, ambient light and fog over a day/night cycle, and
    /// flips the emissive materials on at night.
    ///
    /// Without this the city sits under one fixed mid-morning light forever.
    /// A cycle costs very little and changes how the whole place reads: long
    /// shadows at dawn, a warm sunset, and windows plus street lamps lighting
    /// up after dark.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        [Header("Timing")]
        public float DayLengthMinutes = 16f;   // real minutes for a full 24h
        public float StartHour = 8.5f;

        [Header("Sun")]
        public float SunPeakIntensity = 1.35f;
        public float SunMinIntensity = 0.04f;
        public float SunTiltDegrees = 48f;

        [Header("Sky")]
        public Color DayZenith = new Color(0.42f, 0.52f, 0.68f);
        public Color DayHorizon = new Color(0.62f, 0.68f, 0.74f);
        public Color NightZenith = new Color(0.04f, 0.05f, 0.09f);
        public Color NightHorizon = new Color(0.10f, 0.11f, 0.16f);
        public Color DuskZenith = new Color(0.28f, 0.22f, 0.30f);
        public Color DuskHorizon = new Color(0.85f, 0.45f, 0.22f);

        private Light _sun;
        private float _hour;
        private float _accumulated;

        /// <summary>0 at midnight, 1 at noon. Read by anything that wants to react.</summary>
        public float Daylight01 { get; private set; }

        /// <summary>True once the sun is down and emissives should be on.</summary>
        public bool IsNight { get; private set; }

        public float Hour => _hour;

        public static DayNightCycle Create()
        {
            var go = new GameObject("DayNightCycle");
            var c = go.AddComponent<DayNightCycle>();
            Instance = c;
            return c;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            _hour = StartHour;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Resolve the sun once. Apply() runs every frame, so leaving _sun
            // null would turn the fallback search into a scene-wide walk on
            // every single frame.
            var lights = FindObjectsOfType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional) { _sun = lights[i]; break; }
            }

            Apply(_hour);
        }

        private void Update()
        {
            if (DayLengthMinutes <= 0.01f) return;

            _accumulated += Time.deltaTime;
            float secondsPerDay = DayLengthMinutes * 60f;
            float hoursPerSecond = 24f / secondsPerDay;

            _hour += _accumulated * hoursPerSecond;
            _accumulated = 0f;

            if (_hour >= 24f) _hour -= 24f;

            Apply(_hour);
        }

        private void Apply(float hour)
        {
            // Sun elevation: rises at 06:00, sets at 18:00.
            float dayT = Mathf.Clamp01((hour - 6f) / 12f);
            float elevation = Mathf.Sin(dayT * Mathf.PI);

            Daylight01 = elevation;
            IsNight = IsNightHour(hour);

            if (_sun != null)
            {
                _sun.transform.rotation =
                    Quaternion.Euler(SunTiltDegrees * (1f - elevation * 2f), 145f, 0f);
                _sun.intensity = Mathf.Lerp(SunMinIntensity, SunPeakIntensity, elevation);
                _sun.color = SunColorAt(hour);
            }

            // Sky and ambient follow the sky gradient.
            Color zenith = SkyZenithAt(hour);
            Color horizon = SkyHorizonAt(hour);

            RenderSettings.ambientSkyColor = zenith;
            RenderSettings.ambientEquatorColor = Color.Lerp(zenith, horizon, 0.55f);
            RenderSettings.ambientGroundColor = horizon * 0.35f;

            if (RenderSettings.fog)
            {
                RenderSettings.fogColor = Color.Lerp(horizon, zenith, 0.35f);
            }

            Camera.main.backgroundColor = Color.Lerp(zenith, horizon, 0.5f);
        }

        private static Color SunColorAt(float hour)
        {
            // Warm at the horizon, neutral at noon, cold-blue moonlight at night.
            if (hour < 5f || hour > 19f) return new Color(0.55f, 0.62f, 0.85f);

            float t = Mathf.InverseLerp(6f, 12f, hour);
            if (hour <= 12f)
            {
                // Dawn ramp.
                return Color.Lerp(new Color(1f, 0.62f, 0.35f), new Color(1f, 0.97f, 0.90f), t);
            }

            // Dusk ramp.
            float d = Mathf.InverseLerp(12f, 19f, hour);
            return Color.Lerp(new Color(1f, 0.97f, 0.90f), new Color(0.95f, 0.55f, 0.30f), d);
        }

        private Color SkyZenithAt(float hour)
        {
            if (IsNightHour(hour)) return NightZenith;
            if (hour < 8f || hour > 17f)
            {
                float k = hour < 8f
                    ? Mathf.InverseLerp(5f, 8f, hour)
                    : Mathf.InverseLerp(20f, 17f, hour);
                return Color.Lerp(NightZenith, DayZenith, k);
            }

            // Dusk tint near the horizon crossing.
            if (hour > 16f || hour < 7.5f) return DuskZenith;
            return DayZenith;
        }

        private Color SkyHorizonAt(float hour)
        {
            if (IsNightHour(hour)) return NightHorizon;
            if (hour > 16f || hour < 7.5f) return DuskHorizon;
            return DayHorizon;
        }

        private static bool IsNightHour(float hour) => hour < 5.6f || hour > 18.6f;
    }
}