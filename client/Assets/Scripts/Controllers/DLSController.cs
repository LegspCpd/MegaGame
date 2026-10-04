using UnityEngine;
using System.Collections.Generic;
using Megame.Controllers;
using Megame.Systems;
using UnityEngine.UI;
using TMPro;
using Megame.Data;
using Megame.Client;
using Megame.Vehicles;

namespace Megame.Vehicles
{
    /// <summary>
    /// DLS (Dynamic Light System) Controller for emergency vehicles
    /// Integrates with DLSLightingSystem for pattern playback
    /// </summary>
    [RequireComponent(typeof(OptimizedVehicleController))]
    public class DLSController : MonoBehaviour
    {
        [Header("Configuration")]
        public string vehicleName = "";
        public ELSVehicleConfig config;
        
        [Header("Runtime")]
        public bool isActive = false;
        public string currentPattern = "";
        public int currentStep = 0;
        public float stepTimer = 0f;
        public bool sirenActive = false;
        public int currentSirenTone = 0;
        
        // Components
        private OptimizedVehicleController vehicleController;
        private List<ELSLightInstance> lightInstances = new List<ELSLightInstance>();
        private AudioSource sirenAudio;
        private AudioSource hornAudio;
        
        // Pattern state
        private int currentPatternIndex = 0;
        private int currentStepIndex = 0;
        private float patternTimer = 0f;
        private bool isPlaying = false;
        
        // Siren tones
        private string[] sirenTones = { "wail", "yelp", "hilo", "piercer", "airhorn" };
        
        private void Awake()
        {
            vehicleController = GetComponent<OptimizedVehicleController>();
            
            // Setup siren audio
            sirenAudio = gameObject.AddComponent<AudioSource>();
            sirenAudio.spatialBlend = 1f;
            sirenAudio.volume = 1f;
            sirenAudio.pitch = 1f;
            sirenAudio.loop = true;
            sirenAudio.playOnAwake = false;
            sirenAudio.rolloffMode = AudioRolloffMode.Linear;
            sirenAudio.maxDistance = 200f;
            
            // Horn audio
            hornAudio = gameObject.AddComponent<AudioSource>();
            hornAudio.spatialBlend = 1f;
            hornAudio.volume = 1f;
            hornAudio.playOnAwake = false;
        }
        
        private void Start()
        {
            // Auto-load config if vehicle name is set
            if (!string.IsNullOrEmpty(vehicleName) && config == null)
            {
                config = DLSLightingSystem.Instance?.GetVehicleConfig(vehicleName);
            }
            
            if (config != null)
            {
                Initialize(config);
            }
        }
        
        public void Initialize(ELSVehicleConfig vehicleConfig)
        {
            config = vehicleConfig;
            
            // Create light instances from config
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
                // sirenAudio.clip = Resources.Load<AudioClip>(config.sirens[0].audioClip);
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
            light.enabled = false; // Controlled by DLS
            light.renderMode = LightRenderMode.ForcePixel;
            
            if (data.lightType == "spot")
            {
                light.spotAngle = data.size * 10f;
            }
            
            if (data.isEmitter && !string.IsNullOrEmpty(data.texture))
            {
                // light.cookie = Resources.Load<Texture>(data.texture);
            }
            
            // Add phase materials for different colors
            if (data.phases.Count > 0)
            {
                // Store phase colors for pattern playback
            }
            
            return go;
        }
        
        // ============================================================================
        // PUBLIC API
        // ============================================================================
        
        public void SetActive(bool active)
        {
            isActive = active;
            
            if (!active)
            {
                // Turn off all lights
                foreach (var instance in lightInstances)
                {
                    instance.light.enabled = false;
                }
                isPlaying = false;
                StopSiren();
            }
        }
        
        public void SetPattern(string patternName)
        {
            if (config == null) return;
            
            var pattern = config.patterns.Find(p => p.name == patternName);
            if (pattern != null)
            {
                currentPattern = patternName;
                currentPatternIndex = config.patterns.IndexOf(pattern);
                currentStepIndex = 0;
                patternTimer = 0f;
                isPlaying = true;
            }
        }
        
        public void NextPattern()
        {
            if (config == null || config.patterns.Count == 0) return;
            
            currentPatternIndex = (currentPatternIndex + 1) % config.patterns.Count;
            var pattern = config.patterns[currentPatternIndex];
            currentPattern = pattern.name;
            currentStepIndex = 0;
            patternTimer = 0f;
            isPlaying = true;
        }
        
        public void PreviousPattern()
        {
            if (config == null || config.patterns.Count == 0) return;
            
            currentPatternIndex = (currentPatternIndex - 1 + config.patterns.Count) % config.patterns.Count;
            var pattern = config.patterns[currentPatternIndex];
            currentPattern = pattern.name;
            currentStepIndex = 0;
            patternTimer = 0f;
            isPlaying = true;
        }
        
        public void SetSiren(bool active)
        {
            sirenActive = active;
            
            if (active)
            {
                PlaySirenTone(currentSirenTone);
            }
            else
            {
                StopSiren();
            }
        }
        
        public void CycleSirenTone()
        {
            if (sirenTones.Length == 0) return;
            
            currentSirenTone = (currentSirenTone + 1) % sirenTones.Length;
            if (sirenActive)
            {
                PlaySirenTone(currentSirenTone);
            }
        }
        
        public void SetSirenTone(int toneIndex)
        {
            if (toneIndex >= 0 && toneIndex < sirenTones.Length)
            {
                currentSirenTone = toneIndex;
                if (sirenActive)
                {
                    PlaySirenTone(currentSirenTone);
                }
            }
        }
        
        private void PlaySirenTone(int toneIndex)
        {
            if (sirenAudio == null || toneIndex >= sirenTones.Length) return;
            
            string toneName = sirenTones[toneIndex];
            // sirenAudio.clip = Resources.Load<AudioClip>($"Sirens/{toneName}");
            if (sirenAudio.clip != null)
            {
                sirenAudio.Play();
            }
        }
        
        private void StopSiren()
        {
            if (sirenAudio != null)
            {
                sirenAudio.Stop();
            }
        }
        
        public void PlayHorn()
        {
            if (hornAudio != null && hornAudio.clip != null)
            {
                hornAudio.PlayOneShot(hornAudio.clip);
            }
        }
        
        // ============================================================================
        // PATTERN PLAYBACK
        // ============================================================================
        
        private void Update()
        {
            if (config == null || config.patterns.Count == 0) return;
            
            if (isPlaying)
            {
                var pattern = config.patterns[currentPatternIndex];
                if (pattern.steps.Count == 0) return;
                
                patternTimer += Time.deltaTime * pattern.speed;
                
                if (patternTimer >= pattern.steps[currentStepIndex].duration)
                {
                    patternTimer = 0f;
                    currentStepIndex = (currentStepIndex + 1) % pattern.steps.Count;
                    
                    var step = pattern.steps[currentStepIndex];
                    ApplyPatternStep(step);
                }
            }
        }
        
        private void ApplyPatternStep(ELSStep step)
        {
            // Update lights based on step
            for (int i = 0; i < lightInstances.Count; i++)
            {
                bool shouldBeOn = step.lightStates.Contains(i);
                lightInstances[i].light.enabled = shouldBeOn;
                
                // Apply phase colors if available
                if (shouldBeOn && step.lightStates.Count > 0)
                {
                    var instance = lightInstances[i];
                    var lightData = instance.data;
                    
                    // Find phase for this step
                    if (lightData.phases.Count > 0)
                    {
                        int phaseIndex = step.lightStates.IndexOf(i);
                        if (phaseIndex >= 0 && phaseIndex < lightData.phases.Count)
                        {
                            var phase = lightData.phases[phaseIndex];
                            instance.light.color = phase.color;
                            instance.light.intensity = lightData.intensity * phase.intensityMultiplier;
                        }
                    }
                }
            }
            
            // Handle siren tone change
            if (!string.IsNullOrEmpty(step.sirenTone))
            {
                int toneIndex = System.Array.IndexOf(sirenTones, step.sirenTone);
                if (toneIndex >= 0)
                {
                    SetSirenTone(toneIndex);
                }
            }
        }
        
        // ============================================================================
        // INPUT HANDLING
        // ============================================================================
        
        public void HandleInput()
        {
            // Pattern cycling
            if (Input.GetKeyDown(KeyCode.LeftBracket)) // [
            {
                PreviousPattern();
            }
            if (Input.GetKeyDown(KeyCode.RightBracket)) // ]
            {
                NextPattern();
            }
            
            // Siren toggle
            if (Input.GetKeyDown(KeyCode.LeftControl))
            {
                SetSiren(!sirenActive);
            }
            
            // Siren tone cycle
            if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                CycleSirenTone();
            }
            
            // Horn
            if (Input.GetKeyDown(KeyCode.H))
            {
                PlayHorn();
            }
        }
        
        // ============================================================================
        // INTEGRATION WITH POLICE EQUIPMENT
        // ============================================================================
        
        public void SyncWithPoliceEquipment(PoliceEquipmentSystem equipment)
        {
            if (equipment == null) return;
            
            // Sync siren speaker
            if (equipment.HasEquipment(PoliceEquipmentSystem.EquipmentSlot.SirenSpeaker))
            {
                var speaker = equipment.GetEquipped(PoliceEquipmentSystem.EquipmentSlot.SirenSpeaker);
                var audioSource = speaker?.GetComponent<AudioSource>();
                if (audioSource != null && sirenAudio != null)
                {
                    // Sync siren audio to speaker position
                    sirenAudio.transform.position = audioSource.transform.position;
                }
            }
            
            // Sync spotlights
            if (equipment.HasEquipment(PoliceEquipmentSystem.EquipmentSlot.SpotlightLeft))
            {
                var spotlight = equipment.GetEquipped(PoliceEquipmentSystem.EquipmentSlot.SpotlightLeft);
                var light = spotlight?.GetComponent<Light>();
                if (light != null)
                {
                    // Can be controlled by DLS pattern
                }
            }
        }
    }
    
    // ============================================================================
    // SUPPORTING CLASSES
    // ============================================================================
    
    [System.Serializable]
    public class ELSLightInstance
    {
        public ELSLight data;
        public GameObject gameObject;
        public Light light;
    }
    
    // ============================================================================
    // EXTENSION METHODS
    // ============================================================================
    
    public static class DLSControllerExtensions
    {
        public static void SetupFromVehicleDefinition(this DLSController controller, VehicleDefinitionData def)
        {
            if (def == null) return;
            
            // Set vehicle name for config lookup
            controller.vehicleName = def.modelName;
            
            // Auto-configure for emergency vehicles
            if (def.vehicleType == VehicleType.Police || def.vehicleType == VehicleType.Fire)
            {
                // Apply default police loadout
                controller.SetPattern("stage1"); // Default pattern
            }
        }
        
        public static void SetAgencyPattern(this DLSController controller, string agency)
        {
            string pattern = agency.ToLower() switch
            {
                "lspd" => "stage1",
                "lapd" => "stage2",
                "bcso" => "stage3",
                "sheriff" => "stage4",
                "state" => "stage5",
                "fbi" => "stealth",
                "noose" => "tactical",
                _ => "stage1",
            };
            controller.SetPattern(pattern);
        }
    }
}
