using UnityEngine;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Linq;

namespace Megame.Systems
{
    /// <summary>
    /// DLS (Dynamic Light System) - Emergency lighting controller
    /// Parses ELS XML configs from GTA5 mods
    /// </summary>
    public class DLSLightingSystem : MonoBehaviour
    {
        public static DLSLightingSystem Instance { get; private set; }
        
        [Header("Configuration")]
        public string dlsConfigPath = "Assets/StreamingAssets/DLS/";
        public bool autoParseOnStart = true;
        
        private Dictionary<string, ELSPattern> patternCache = new Dictionary<string, ELSPattern>();
        private Dictionary<string, ELSVehicleConfig> vehicleConfigCache = new Dictionary<string, ELSVehicleConfig>();
        
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
            if (autoParseOnStart)
            {
                ParseAllConfigs();
            }
        }
        
        public void ParseAllConfigs()
        {
            patternCache.Clear();
            vehicleConfigCache.Clear();
            
            string fullPath = Path.Combine(Application.dataPath, dlsConfigPath.Replace("Assets/", ""));
            if (!Directory.Exists(fullPath))
            {
                Debug.LogWarning($"DLS config path not found: {fullPath}");
                return;
            }
            
            var xmlFiles = Directory.GetFiles(fullPath, "*.xml", SearchOption.AllDirectories);
            foreach (var file in xmlFiles)
            {
                try
                {
                    ParseELSXml(file);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to parse DLS config {file}: {e.Message}");
                }
            }
            
            Debug.Log($"DLS: Parsed {patternCache.Count} patterns, {vehicleConfigCache.Count} vehicle configs");
        }
        
        private void ParseELSXml(string filePath)
        {
            var doc = new XmlDocument();
            doc.Load(filePath);
            
            var root = doc.DocumentElement;
            if (root == null) return;
            
            // Parse vehicle config
            var vehicleName = Path.GetFileNameWithoutExtension(filePath);
            var vehicleConfig = new ELSVehicleConfig
            {
                vehicleName = vehicleName,
                sourceFile = filePath
            };
            
            // Parse lights
            var lightsNode = root.SelectSingleNode("Lights");
            if (lightsNode != null)
            {
                foreach (XmlNode lightNode in lightsNode.SelectNodes("Light"))
                {
                    var light = ParseLight(lightNode);
                    if (light != null)
                    {
                        vehicleConfig.lights.Add(light);
                    }
                }
            }
            
            // Parse stages (patterns)
            var stagesNode = root.SelectSingleNode("Stages");
            if (stagesNode != null)
            {
                foreach (XmlNode stageNode in stagesNode.SelectNodes("Stage"))
                {
                    var pattern = ParseStage(stageNode);
                    if (pattern != null)
                    {
                        string patternKey = $"{vehicleName}_{pattern.name}";
                        patternCache[patternKey] = pattern;
                        vehicleConfig.patterns.Add(pattern);
                    }
                }
            }
            
            // Parse sirens
            var sirensNode = root.SelectSingleNode("Sirens");
            if (sirensNode != null)
            {
                foreach (XmlNode sirenNode in sirensNode.SelectNodes("Siren"))
                {
                    var siren = ParseSiren(sirenNode);
                    if (siren != null)
                    {
                        vehicleConfig.sirens.Add(siren);
                    }
                }
            }
            
            vehicleConfigCache[vehicleName] = vehicleConfig;
        }
        
        private ELSLight ParseLight(XmlNode node)
        {
            try
            {
                var light = new ELSLight
                {
                    id = node.Attributes["id"]?.Value ?? "",
                    name = node.Attributes["name"]?.Value ?? "",
                    position = ParseVector3(node.Attributes["position"]?.Value),
                    rotation = ParseVector3(node.Attributes["rotation"]?.Value),
                    color = ParseColor(node.Attributes["color"]?.Value),
                    intensity = float.Parse(node.Attributes["intensity"]?.Value ?? "1"),
                    range = float.Parse(node.Attributes["range"]?.Value ?? "20"),
                    lightType = node.Attributes["type"]?.Value ?? "point",
                    isEmitter = bool.Parse(node.Attributes["emitter"]?.Value ?? "false"),
                    texture = node.Attributes["texture"]?.Value ?? "",
                    size = float.Parse(node.Attributes["size"]?.Value ?? "1"),
                };
                
                // Parse phases
                foreach (XmlNode phaseNode in node.SelectNodes("Phase"))
                {
                    light.phases.Add(ParsePhase(phaseNode));
                }
                
                return light;
            }
            catch
            {
                return null;
            }
        }
        
        private ELSPattern ParseStage(XmlNode node)
        {
            try
            {
                var pattern = new ELSPattern
                {
                    name = node.Attributes["name"]?.Value ?? "",
                    speed = float.Parse(node.Attributes["speed"]?.Value ?? "1"),
                    loop = bool.Parse(node.Attributes["loop"]?.Value ?? "true"),
                };
                
                foreach (XmlNode stepNode in node.SelectNodes("Step"))
                {
                    pattern.steps.Add(ParseStep(stepNode));
                }
                
                return pattern;
            }
            catch
            {
                return null;
            }
        }
        
        private ELSStep ParseStep(XmlNode node)
        {
            return new ELSStep
            {
                duration = float.Parse(node.Attributes["time"]?.Value ?? "0.1"),
                lightStates = node.Attributes["lights"]?.Value?.Split(',').Select(int.Parse).ToList() ?? new List<int>(),
                sirenTone = node.Attributes["siren"]?.Value ?? "",
            };
        }
        
        private ELSSiren ParseSiren(XmlNode node)
        {
            return new ELSSiren
            {
                id = node.Attributes["id"]?.Value ?? "",
                name = node.Attributes["name"]?.Value ?? "",
                audioClip = node.Attributes["audio"]?.Value ?? "",
                volume = float.Parse(node.Attributes["volume"]?.Value ?? "1"),
                pitch = float.Parse(node.Attributes["pitch"]?.Value ?? "1"),
                loop = bool.Parse(node.Attributes["loop"]?.Value ?? "true"),
            };
        }
        
        private ELSStep ParsePhase(XmlNode node)
        {
            return new ELSStep
            {
                duration = float.Parse(node.Attributes["time"]?.Value ?? "0.1"),
                lightStates = node.Attributes["lights"]?.Value?.Split(',').Select(int.Parse).ToList() ?? new List<int>(),
            };
        }
        
        private Vector3 ParseVector3(string value)
        {
            if (string.IsNullOrEmpty(value)) return Vector3.zero;
            var parts = value.Split(',', ' ');
            if (parts.Length >= 3)
            {
                return new Vector3(
                    float.Parse(parts[0]),
                    float.Parse(parts[1]),
                    float.Parse(parts[2])
                );
            }
            return Vector3.zero;
        }
        
        private Color ParseColor(string value)
        {
            if (string.IsNullOrEmpty(value)) return Color.white;
            if (value.StartsWith("#"))
            {
                ColorUtility.TryParseHtmlString(value, out Color c);
                return c;
            }
            var parts = value.Split(',', ' ');
            if (parts.Length >= 3)
            {
                return new Color(
                    float.Parse(parts[0]) / 255f,
                    float.Parse(parts[1]) / 255f,
                    float.Parse(parts[2]) / 255f
                );
            }
            return Color.white;
        }
        
        // Runtime API
        public ELSPattern GetPattern(string vehicleName, string patternName)
        {
            string key = $"{vehicleName}_{patternName}";
            patternCache.TryGetValue(key, out var pattern);
            return pattern;
        }
        
        public ELSVehicleConfig GetVehicleConfig(string vehicleName)
        {
            vehicleConfigCache.TryGetValue(vehicleName, out var config);
            return config;
        }
        
        public void ApplyConfigToVehicle(GameObject vehicle, string vehicleName)
        {
            var config = GetVehicleConfig(vehicleName);
            if (config == null) return;
            
            // Create lights
            foreach (var lightData in config.lights)
            {
                CreateLight(vehicle, lightData);
            }
            
            // Add controller
            var controller = vehicle.GetComponent<DLSController>();
            if (controller == null) controller = vehicle.AddComponent<DLSController>();
            controller.Initialize(config);
        }
        
        private void CreateLight(GameObject vehicle, ELSLight lightData)
        {
            var lightObj = new GameObject($"ELS_Light_{lightData.name}");
            lightObj.transform.SetParent(vehicle.transform);
            lightObj.transform.localPosition = lightData.position;
            lightObj.transform.localEulerAngles = lightData.rotation;
            
            var light = lightObj.AddComponent<Light>();
            light.type = lightData.lightType == "spot" ? LightType.Spot : LightType.Point;
            light.color = lightData.color;
            light.intensity = lightData.intensity;
            light.range = lightData.range;
            light.enabled = false; // Controlled by DLS
            
            if (lightData.lightType == "spot")
            {
                light.spotAngle = lightData.size * 10f;
            }
        }
    }
    
    // ============================================================================
    // ELS Data Structures
    // ============================================================================
    
    [System.Serializable]
    public class ELSVehicleConfig
    {
        public string vehicleName;
        public string sourceFile;
        public List<ELSLight> lights = new List<ELSLight>();
        public List<ELSPattern> patterns = new List<ELSPattern>();
        public List<ELSSiren> sirens = new List<ELSSiren>();
    }
    
    [System.Serializable]
    public class ELSLight
    {
        public string id;
        public string name;
        public Vector3 position;
        public Vector3 rotation;
        public Color color;
        public float intensity = 1f;
        public float range = 20f;
        public string lightType = "point";
        public bool isEmitter = false;
        public string texture = "";
        public float size = 1f;
        public List<ELSStep> phases = new List<ELSStep>();
    }
    
    [System.Serializable]
    public class ELSPattern
    {
        public string name;
        public float speed = 1f;
        public bool loop = true;
        public List<ELSStep> steps = new List<ELSStep>();
    }
    
    [System.Serializable]
    public class ELSStep
    {
        public float duration = 0.1f;
        public List<int> lightStates = new List<int>(); // Indices of lights to activate
        public string sirenTone = "";
    }
    
    [System.Serializable]
    public class ELSSiren
    {
        public string id;
        public string name;
        public string audioClip;
        public float volume = 1f;
        public float pitch = 1f;
        public bool loop = true;
    }
    
    // ============================================================================
    // Runtime DLS Controller
    // ============================================================================
    
    public class DLSController : MonoBehaviour
    {
        [Header("Configuration")]
        public string vehicleName;
        public ELSVehicleConfig config;
        
        [Header("Runtime")]
        public bool isActive = false;
        public string currentPattern = "";
        public int currentStep = 0;
        public float stepTimer = 0f;
        
        private List<ELSLightInstance> lightInstances = new List<ELSLightInstance>();
        private AudioSource sirenAudio;
        private int currentPatternIndex = 0;
        private int currentStepIndex = 0;
        private bool isPlaying = false;
        
        public void Initialize(ELSVehicleConfig vehicleConfig)
        {
            config = vehicleConfig;
            
            // Create light instances
            lightInstances.Clear();
            foreach (var lightData in config.lights)
            {
                var instance = new ELSLightInstance
                {
                    data = lightData,
                    gameObject = CreateLightGameObject(lightData),
                };
                lightInstances.Add(instance);
            }
            
            // Setup siren audio
            if (config.sirens.Count > 0)
            {
                sirenAudio = gameObject.AddComponent<AudioSource>();
                sirenAudio.spatialBlend = 1f;
                sirenAudio.volume = config.sirens[0].volume;
                sirenAudio.pitch = config.sirens[0].pitch;
                sirenAudio.loop = config.sirens[0].loop;
            }
        }
        
        private GameObject CreateLightGameObject(ELSLight data)
        {
            var go = new GameObject($"ELS_{data.name}");
            go.transform.SetParent(transform);
            go.transform.localPosition = data.position;
            go.transform.localEulerAngles = data.rotation;
            
            var light = go.AddComponent<Light>();
            light.type = data.lightType == "spot" ? LightType.Spot : LightType.Point;
            light.color = data.color;
            light.intensity = data.intensity;
            light.range = data.range;
            light.enabled = false;
            
            if (data.lightType == "spot")
                light.spotAngle = data.size * 10f;
            
            if (data.isEmitter && !string.IsNullOrEmpty(data.texture))
            {
                // Add cookie texture for pattern projection
                // light.cookie = Resources.Load<Texture>(data.texture);
            }
            
            return go;
        }
        
        public void SetActive(bool active)
        {
            foreach (var instance in lightInstances)
            {
                instance.light.enabled = false;
            }
        }
        
        public void SetPattern(string patternName)
        {
            var pattern = config.patterns.FirstOrDefault(p => p.name == patternName);
            if (pattern != null)
            {
                currentStepIndex = 0;
                stepTimer = 0f;
                isPlaying = true;
            }
        }
        
        public void SetSiren(int sirenIndex)
        {
            if (sirenIndex >= 0 && sirenIndex < config.sirens.Count && sirenAudio != null)
            {
                var siren = config.sirens[sirenIndex];
                // sirenAudio.clip = Resources.Load<AudioClip>(siren.audioClip);
                if (sirenAudio.clip != null)
                {
                    sirenAudio.volume = siren.volume;
                    sirenAudio.pitch = siren.pitch;
                    sirenAudio.loop = siren.loop;
                    sirenAudio.Play();
                }
            }
        }
        
        private void Update()
        {
            if (config == null || config.patterns.Count == 0) return;
            
            var pattern = config.patterns[currentPatternIndex];
            if (pattern.steps.Count == 0) return;
            
            stepTimer += Time.deltaTime * pattern.speed;
            
            if (stepTimer >= pattern.steps[currentStepIndex].duration)
            {
                stepTimer = 0f;
                currentStepIndex = (currentStepIndex + 1) % pattern.steps.Count;
                
                var step = pattern.steps[currentStepIndex];
                
                // Update lights
                for (int i = 0; i < lightInstances.Count; i++)
                {
                    bool shouldBeOn = step.lightStates.Contains(i);
                    lightInstances[i].light.enabled = shouldBeOn;
                }
                
                // Handle siren tone change
                if (!string.IsNullOrEmpty(step.sirenTone))
                {
                    var siren = config.sirens.FirstOrDefault(s => s.name == step.sirenTone);
                    if (siren != null)
                    {
                        SetSiren(config.sirens.IndexOf(siren));
                    }
                }
            }
        }
    }
    
    [System.Serializable]
    public class ELSLightInstance
    {
        public ELSLight data;
        public GameObject gameObject;
        public Light light;
    }
}
