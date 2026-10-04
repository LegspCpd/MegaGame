using UnityEngine;
using System.Collections.Generic;
using Megame.Data;

namespace Megame.World
{
    /// <summary>
    /// World Manager - handles time, weather, and world state
    /// </summary>
    public class WorldManager : MonoBehaviour
    {
        public static WorldManager Instance { get; private set; }
        
        [Header("Time")]
        public float timeScale = 1f; // 1 = real-time, 24 = 1 hour = 1 day
        public float currentHour = 12f;
        public int currentDay = 1;
        
        [Header("Weather")]
        public WeatherState currentWeather = WeatherState.Clear;
        public float weatherTransition = 0f;
        public float windSpeed = 5f;
        public Vector3 windDirection = Vector3.forward;
        
        [Header("References")]
        public Light sunLight;
        public Material skyboxMaterial;
        public ParticleSystem rainParticles;
        public ParticleSystem snowParticles;
        public ParticleSystem fogParticles;
        public AudioSource ambientAudio;
        
        [Header("Settings")]
        public float dayLengthMinutes = 30f; // Real minutes per game day
        public AnimationCurve sunIntensityByHour = AnimationCurve.Linear(0, 0, 12, 1, 24, 0);
        public AnimationCurve sunColorByHour;
        public AnimationCurve fogDensityByHour;
        
        private float dayLengthSeconds;
        private float timeAccumulator;
        
        public float CurrentTimeOfDay => currentHour;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            dayLengthSeconds = dayLengthMinutes * 60f;
            InitializeSunColorCurve();
            UpdateSun();
        }
        
        private void InitializeSunColorCurve()
        {
            if (sunColorByHour.length == 0)
            {
                sunColorByHour = new AnimationCurve(
                    new Keyframe(0, 0f),      // Midnight - dark blue
                    new Keyframe(5, 0.2f),    // Dawn
                    new Keyframe(6, 0.5f),    // Sunrise
                    new Keyframe(12, 1f),     // Noon - white
                    new Keyframe(18, 0.5f),   // Sunset
                    new Keyframe(19, 0.2f),   // Dusk
                    new Keyframe(24, 0f)      // Midnight
                );
            }
        }
        
        private void Update()
        {
            // Time progression
            timeAccumulator += Time.deltaTime * timeScale;
            if (timeAccumulator >= 1f)
            {
                float hoursToAdd = timeAccumulator / (dayLengthSeconds / 24f);
                currentHour += hoursToAdd;
                timeAccumulator = 0f;
                
                while (currentHour >= 24f)
                {
                    currentHour -= 24f;
                    currentDay++;
                }
            }
            
            // Weather transition
            if (weatherTransition > 0f)
            {
                weatherTransition -= Time.deltaTime / 300f; // 5 min transition
                weatherTransition = Mathf.Max(0f, weatherTransition);
            }
            
            UpdateSun();
            UpdateWeather();
            UpdateAmbient();
        }
        
        private void UpdateSun()
        {
            if (sunLight == null) return;
            
            // Sun rotation based on hour
            float sunAngle = (currentHour - 6f) / 24f * 360f - 90f;
            sunLight.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0f);
            
            // Intensity
            sunLight.intensity = sunIntensityByHour.Evaluate(currentHour) * 1.5f;
            
            // Color
            if (sunColorByHour.length > 0)
            {
                float colorEval = sunColorByHour.Evaluate(currentHour);
                sunLight.color = Color.Lerp(Color.blue, Color.white, colorEval);
            }
            
            // Shadows
            sunLight.shadows = currentHour > 6f && currentHour < 20f ? LightShadows.Soft : LightShadows.None;
        }
        
        private void UpdateWeather()
        {
            // Rain
            if (rainParticles != null)
            {
                var emission = rainParticles.emission;
                emission.enabled = currentWeather == WeatherState.Rain || currentWeather == WeatherState.Thunderstorm;
                
                if (emission.enabled)
                {
                    emission.rateOverTime = currentWeather == WeatherState.Thunderstorm ? 5000f : 2000f;
                }
            }
            
            // Snow
            if (snowParticles != null)
            {
                var emission = snowParticles.emission;
                emission.enabled = currentWeather == WeatherState.Snow || currentWeather == WeatherState.Blizzard;
                
                if (emission.enabled)
                {
                    emission.rateOverTime = currentWeather == WeatherState.Blizzard ? 3000f : 1000f;
                }
            }
            
            // Fog
            if (fogParticles != null && fogDensityByHour.length > 0)
            {
                float fogDensity = fogDensityByHour.Evaluate(currentHour);
                var emission = fogParticles.emission;
                emission.rateOverTime = fogDensity * 100f;
            }
            
            // Lightning
            if (currentWeather == WeatherState.Thunderstorm && Random.value < 0.001f)
            {
                TriggerLightning();
            }
        }
        
        private void TriggerLightning()
        {
            // Flash sun light
            if (sunLight != null)
            {
                StartCoroutine(LightningFlash());
            }
            
            // Play thunder sound
            // AudioSource.PlayClipAtPoint(thunderClip, Camera.main.transform.position);
        }
        
        private System.Collections.IEnumerator LightningFlash()
        {
            float originalIntensity = sunLight.intensity;
            for (int i = 0; i < 3; i++)
            {
                sunLight.intensity = originalIntensity * 3f;
                yield return new WaitForSeconds(0.05f);
                sunLight.intensity = originalIntensity * 0.5f;
                yield return new WaitForSeconds(0.1f);
            }
            sunLight.intensity = originalIntensity;
        }
        
        private void UpdateAmbient()
        {
            // Ambient light based on time
            RenderSettings.ambientIntensity = Mathf.Lerp(0.2f, 1f, sunIntensityByHour.Evaluate(currentHour));
            
            // Ambient audio
            if (ambientAudio != null)
            {
                // Could change audio based on time/weather
            }
        }
        
        // ============================================================================
        // PUBLIC API
        // ============================================================================
        
        public void SetTime(float hour)
        {
            currentHour = Mathf.Clamp(hour, 0f, 24f);
            UpdateSun();
        }
        
        public void SetWeather(WeatherState weather, float transitionTime = 300f)
        {
            currentWeather = weather;
            weatherTransition = 1f;
        }
        
        public void SetWind(float speed, Vector3 direction)
        {
            windSpeed = speed;
            windDirection = direction.normalized;
            
            // Update particle systems
            if (rainParticles != null)
            {
                var main = rainParticles.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                // windDirection would affect particle velocity
            }
        }
        
        public string GetTimeString()
        {
            int hour = Mathf.FloorToInt(currentHour);
            int minute = Mathf.FloorToInt((currentHour - hour) * 60);
            return $"{hour:D2}:{minute:D2}";
        }
        
        public string GetDateString()
        {
            return $"Day {currentDay}";
        }
    }
    
    // ============================================================================
    // WEATHER STATES
    // ============================================================================
    
    public enum WeatherState
    {
        Clear,
        Clouds,
        Rain,
        Thunderstorm,
        Snow,
        Blizzard,
        Fog,
        Overcast,
    }
    
    // ============================================================================
    // REGION SYSTEM
    // ============================================================================
    
    public class RegionManager : MonoBehaviour
    {
        public static RegionManager Instance { get; private set; }
        
        public List<Region> regions = new List<Region>();
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        public Region GetRegionAt(Vector3 position)
        {
            foreach (var region in regions)
            {
                if (region.Contains(position))
                    return region;
            }
            return null;
        }
        
        public Region GetCurrentRegion()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                return GetRegionAt(player.transform.position);
            return null;
        }
    }
    
    [System.Serializable]
    public class Region
    {
        public string regionId;
        public string displayName;
        public string shortName;
        public RegionKind type;
        public Bounds bounds;
        public int populationDensity = 50;
        public int trafficDensity = 50;
        public int policePatrolFrequency = 50;
        public string gangFaction = "";
        public string backgroundMusic = "";
        public bool isSafeZone = false;
        public bool isRestricted = false;
        public float wantedLevelMultiplier = 1f;
        public List<string> connectedRegions = new List<string>();
        
        public bool Contains(Vector3 position)
        {
            return bounds.Contains(position);
        }
    }
    
    public enum RegionKind
    {
        Downtown,
        Residential,
        Industrial,
        Commercial,
        Rural,
        Wilderness,
        Beach,
        Airport,
        Port,
        Military,
        University,
        Park,
        Suburbs,
        Slums,
        Rich,
    }
    
    // ============================================================================
    // POI SYSTEM
    // ============================================================================
    
    public class POIManager : MonoBehaviour
    {
        public static POIManager Instance { get; private set; }
        
        public List<POI> pois = new List<POI>();
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        public POI GetNearestPOI(Vector3 position, POIPointType type = POIPointType.None, float maxDistance = 1000f)
        {
            POI nearest = null;
            float nearestDist = maxDistance;
            
            foreach (var poi in pois)
            {
                if (type != POIPointType.None && poi.type != type) continue;
                
                float dist = Vector3.Distance(position, poi.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = poi;
                }
            }
            
            return nearest;
        }
        
        public List<POI> GetPOIsInRadius(Vector3 position, float radius, POIPointType type = POIPointType.None)
        {
            var result = new List<POI>();
            foreach (var poi in pois)
            {
                if (type != POIPointType.None && poi.type != type) continue;
                
                if (Vector3.Distance(position, poi.position) <= radius)
                {
                    result.Add(poi);
                }
            }
            return result;
        }
    }
    
    [System.Serializable]
    public class POI
    {
        public string poiId;
        public string displayName;
        public POIPointType type;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale = Vector3.one;
        public string modelPath = "";
        public List<string> tags = new List<string>();
        public string interiorId = "";
        public bool isLandmark = false;
        public bool showOnMap = true;
        public int unlockLevel = 0;
        public string blipSprite = "";
        public Color blipColor = Color.white;
        public WorldOpeningHours hours = new WorldOpeningHours();
    }
    
    public enum POIPointType
    {
        None,
        ShopClothing,
        ShopWeapons,
        ShopVehicles,
        ShopMods,
        ShopTattoo,
        ShopBarber,
        ShopFood,
        ShopConvenience,
        ShopLiquor,
        Safehouse,
        Hotel,
        Hospital,
        PoliceStation,
        FireStation,
        Garage,
        Helipad,
        Dock,
        StripClub,
        Bar,
        Restaurant,
        Gym,
        Golf,
        Tennis,
        Darts,
        ArmWrestling,
        ShootingRange,
        FlightSchool,
        DrivingSchool,
        Cinema,
        ComedyClub,
        BaseJump,
        Parachute,
        RaceTrack,
        StuntJump,
        Collectible,
        HiddenPackage,
        LetterScrap,
        SpaceshipPart,
        SubmarinePart,
        NuclearWaste,
        EpsilonTract,
        Rampage,
        Assassination,
        Bounty,
        GangAttack,
        ArmoredTruck,
        CrateDrop,
        DeadDrop,
        PlayerSpawn,
        MissionStart,
        Stranger,
        RandomEvent,
        Property,
        Business,
    }
    
    [System.Serializable]
    public class WorldOpeningHours
    {
        public int openHour = 0;
        public int openMinute = 0;
        public int closeHour = 23;
        public int closeMinute = 59;
        public bool is24h = false;
        public List<int> closedDays = new List<int>(); // 0=Sunday
        
        public bool IsOpen(float currentHour, int currentDay)
        {
            if (is24h) return true;
            if (closedDays.Contains(currentDay)) return false;
            
            float openTime = openHour + openMinute / 60f;
            float closeTime = closeHour + closeMinute / 60f;
            
            if (openTime < closeTime)
            {
                return currentHour >= openTime && currentHour <= closeTime;
            }
            else // Overnight
            {
                return currentHour >= openTime || currentHour <= closeTime;
            }
        }
    }
}