using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Megame.Data;
using Megame.World;
using UnityEngine.UI;
using TMPro;
using Megame.Controllers;
using Megame.Client;
using Megame.Vehicles;

namespace Megame.Systems
{
    /// <summary>
    /// Traffic Flow System - Spawns and manages ambient traffic
    /// Based on region population config, road network, and time of day
    /// </summary>
    public class TrafficFlowSystem : MonoBehaviour
    {
        public static TrafficFlowSystem Instance { get; private set; }
        
        [Header("Configuration")]
        public TrafficFlowConfig config;
        public VehicleDatabase vehicleDatabase;
        public RoadNetwork roadNetwork;
        
        [Header("Runtime")]
        public int maxVehicles = 50;
        public float spawnRadius = 300f;
        public float despawnRadius = 500f;
        public float spawnInterval = 2f;
        
        [Header("Debug")]
        public bool showDebugGizmos = false;
        public bool logSpawns = false;
        
        private List<TrafficVehicle> activeVehicles = new List<TrafficVehicle>();
        private Queue<VehicleSpawnRequest> spawnQueue = new Queue<VehicleSpawnRequest>();
        private float spawnTimer;
        private int nextVehicleId = 1;
        
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
            if (config == null)
            {
                config = Resources.Load<TrafficFlowConfig>("TrafficFlowConfig");
            }
            if (vehicleDatabase == null)
            {
                vehicleDatabase = Resources.Load<VehicleDatabase>("VehicleDatabase");
            }
            if (roadNetwork == null)
            {
                roadNetwork = FindObjectOfType<RoadNetwork>();
            }
            
            // Initial spawn
            for (int i = 0; i < config.initialSpawnCount; i++)
            {
                TrySpawnRandomVehicle();
            }
        }
        
        private void Update()
        {
            spawnTimer += Time.deltaTime;
            
            // Process spawn queue
            if (spawnTimer >= spawnInterval && spawnQueue.Count > 0)
            {
                spawnTimer = 0f;
                ProcessSpawnQueue();
            }
            
            // Cleanup distant vehicles
            CleanupDistantVehicles();
            
            // Update traffic density based on time
            UpdateTrafficDensity();
        }
        
        private void ProcessSpawnQueue()
        {
            while (spawnQueue.Count > 0 && activeVehicles.Count < maxVehicles)
            {
                var request = spawnQueue.Dequeue();
                SpawnVehicle(request);
            }
        }
        
        private void TrySpawnRandomVehicle()
        {
            if (activeVehicles.Count >= maxVehicles) return;
            
            // Find spawn point on road network
            var spawnPoint = roadNetwork?.GetRandomSpawnPoint();
            if (spawnPoint == null)
            {
                // Fallback: spawn near player
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    Vector3 randomPos = player.transform.position + Random.insideUnitSphere * spawnRadius;
                    randomPos.y = 0;
                    SpawnVehicle(new VehicleSpawnRequest
                    {
                        position = randomPos,
                        rotation = Quaternion.LookRotation(Random.onUnitSphere),
                        vehicleClass = GetRandomVehicleClassForRegion(),
                    });
                }
                return;
            }
            
            SpawnVehicle(new VehicleSpawnRequest
            {
                position = spawnPoint.transform.position,
                rotation = spawnPoint.transform.rotation,
                vehicleClass = GetRandomVehicleClassForRegion(spawnPoint.region),
                roadSegment = spawnPoint.roadSegment,
                lane = spawnPoint.lane,
            });
        }
        
        private void SpawnVehicle(VehicleSpawnRequest request)
        {
            // Get appropriate vehicle definition
            var def = GetVehicleDefinition(request);
            if (def == null || def.modelPrefab == null) return;
            
            // Instantiate
            var go = Instantiate(def.modelPrefab, request.position, request.rotation);
            go.name = $"Traffic_{def.modelName}_{nextVehicleId++}";
            
            // Setup traffic AI
            var trafficVehicle = go.GetComponent<TrafficVehicle>();
            if (trafficVehicle == null) trafficVehicle = go.AddComponent<TrafficVehicle>();
            
            trafficVehicle.Initialize(def, request);
            
            // Apply random mods for variety
            ApplyRandomMods(trafficVehicle);
            
            // Apply random driver
            ApplyRandomDriver(trafficVehicle);
            
            activeVehicles.Add(trafficVehicle);
            
            if (logSpawns)
                Debug.Log($"Spawned traffic: {def.displayName} at {request.position}");
        }
        
        private VehicleDefinitionData GetVehicleDefinition(VehicleSpawnRequest request)
        {
            VehicleDefinitionData[] candidates = null;
            
            if (request.vehicleClass != VehicleClass.None)
            {
                candidates = vehicleDatabase.GetByClass(request.vehicleClass);
            }
            else
            {
                candidates = vehicleDatabase.GetCivilianVehicles();
            }
            
            if (candidates == null || candidates.Length == 0) return null;
            
            // Filter by region if specified
            if (!string.IsNullOrEmpty(request.region))
            {
                candidates = candidates.Where(v => v.requiredRegions.Contains(request.region)).ToArray();
            }
            
            if (candidates.Length == 0) return null;
            
            // Weighted random selection
            return candidates[Random.Range(0, candidates.Length)];
        }
        
        private VehicleClass GetRandomVehicleClassForRegion(string region = "")
        {
            if (config.regionVehicleDistribution.TryGetValue(region, out var dist))
            {
                float rand = Random.value;
                float cumulative = 0f;
                foreach (var kvp in dist)
                {
                    cumulative += kvp.Value;
                    if (rand <= cumulative) return kvp.Key;
                }
            }
            
            // Global default
            var globalDist = config.globalVehicleDistribution;
            float rand2 = Random.value;
            float cum2 = 0f;
            foreach (var kvp in globalDist)
            {
                cum2 += kvp.Value;
                if (rand2 <= cum2) return kvp.Key;
            }
            
            return VehicleClass.Sedan;
        }
        
        private void ApplyRandomMods(TrafficVehicle trafficVehicle)
        {
            if (!config.enableRandomMods) return;
            if (Random.value > config.modChance) return;
            
            var vehicleDef = trafficVehicle.definition;
            if (vehicleDef.modKitId <= 0) return;
            
            // Random visual mods
            if (Random.value < 0.3f) // Paint
            {
                trafficVehicle.SetRandomPaint();
            }
            
            if (Random.value < 0.2f) // Wheels
            {
                trafficVehicle.SetRandomWheels();
            }
            
            if (Random.value < 0.1f) // Body mods
            {
                trafficVehicle.SetRandomBodyMods();
            }
        }
        
        private void ApplyRandomDriver(TrafficVehicle trafficVehicle)
        {
            // Create random pedestrian driver
            var driverDef = vehicleDatabase.GetRandomPedestrian();
            if (driverDef == null) return;
            
            var driverGo = new GameObject("Driver");
            driverGo.transform.SetParent(trafficVehicle.transform);
            driverGo.transform.localPosition = new Vector3(0, 1f, 0.5f); // Approximate driver position
            
            // Add pedestrian component
            var ped = driverGo.AddComponent<PedestrianDriver>();
            ped.Initialize(driverDef, trafficVehicle);
        }
        
        private void CleanupDistantVehicles()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            
            for (int i = activeVehicles.Count - 1; i >= 0; i--)
            {
                var tv = activeVehicles[i];
                if (tv == null || tv.transform == null)
                {
                    activeVehicles.RemoveAt(i);
                    continue;
                }
                
                float dist = Vector3.Distance(tv.transform.position, player.transform.position);
                if (dist > despawnRadius)
                {
                    tv.Despawn();
                    activeVehicles.RemoveAt(i);
                }
            }
        }
        
        private void UpdateTrafficDensity()
        {
            // Adjust spawn rate based on time of day
            float timeOfDay = WorldManager.Instance?.CurrentTimeOfDay ?? 12f;
            float densityMultiplier = config.GetDensityForTime(timeOfDay);
            
            spawnInterval = Mathf.Lerp(config.minSpawnInterval, config.maxSpawnInterval, 1f - densityMultiplier);
            maxVehicles = Mathf.RoundToInt(config.baseMaxVehicles * densityMultiplier);
        }
        
        public void RequestSpawn(Vector3 position, VehicleClass vehicleClass = VehicleClass.None, string region = "")
        {
            if (activeVehicles.Count >= maxVehicles) return;
            
            spawnQueue.Enqueue(new VehicleSpawnRequest
            {
                position = position,
                rotation = Quaternion.identity,
                vehicleClass = vehicleClass,
                region = region,
            });
        }
        
        public void RequestPoliceResponse(Vector3 position, int severity)
        {
            // Spawn police vehicles
            int count = Mathf.Clamp(severity, 1, 4);
            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos = position + Random.insideUnitSphere * 50f;
                spawnPos.y = 0;
                
                RequestSpawn(spawnPos, VehicleClass.Emergency);
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;
            
            Gizmos.color = Color.green;
            foreach (var tv in activeVehicles)
            {
                if (tv != null && tv.transform != null)
                {
                    Gizmos.DrawWireSphere(tv.transform.position, 2f);
                }
            }
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(
                Camera.main?.transform.position ?? Vector3.zero,
                spawnRadius
            );
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(
                Camera.main?.transform.position ?? Vector3.zero,
                despawnRadius
            );
        }
    }
    
    // ============================================================================
    // Data Structures
    // ============================================================================
    
    [System.Serializable]
    public class TrafficFlowConfig : ScriptableObject
    {
        [Header("Spawn Limits")]
        public int baseMaxVehicles = 50;
        public int initialSpawnCount = 20;
        public float minSpawnInterval = 0.5f;
        public float maxSpawnInterval = 5f;
        
        [Header("Radius")]
        public float spawnRadius = 300f;
        public float despawnRadius = 500f;
        
        [Header("Variety")]
        public bool enableRandomMods = true;
        public float modChance = 0.3f;
        
        [Header("Time-based Density")]
        public AnimationCurve densityByTime = AnimationCurve.Linear(0, 0.3f, 24, 0.3f);
        
        public float GetDensityForTime(float hour)
        {
            return densityByTime.Evaluate(hour);
        }
        
        [Header("Vehicle Distribution")]
        public Dictionary<VehicleClass, float> globalVehicleDistribution = new Dictionary<VehicleClass, float>
        {
            { VehicleClass.Sedan, 0.35f },
            { VehicleClass.SUV, 0.25f },
            { VehicleClass.Compact, 0.15f },
            { VehicleClass.Coupe, 0.1f },
            { VehicleClass.Muscle, 0.05f },
            { VehicleClass.Sports, 0.05f },
            { VehicleClass.Vans, 0.03f },
            { VehicleClass.Truck, 0.02f },
        };
        
        public Dictionary<string, Dictionary<VehicleClass, float>> regionVehicleDistribution = 
            new Dictionary<string, Dictionary<VehicleClass, float>>();
    }
    
    public struct VehicleSpawnRequest
    {
        public Vector3 position;
        public Quaternion rotation;
        public VehicleClass vehicleClass;
        public string region;
        public RoadSegment roadSegment;
        public int lane;
        public bool isEmergency;
    }
    
    // ============================================================================
    // Traffic Vehicle AI
    // ============================================================================
    
    public class TrafficVehicle : MonoBehaviour
    {
        public VehicleDefinitionData definition;
        public VehicleSpawnRequest spawnRequest;
        public OptimizedVehicleController controller;
        public TrafficAIState currentState = TrafficAIState.Driving;
        public RoadSegment currentRoad;
        public int currentLane;
        public float targetSpeed;
        public float followDistance = 15f;
        
        private VehicleController targetVehicle;
        private float laneChangeTimer;
        private int laneChangeDirection = 0;
        
        public void Initialize(VehicleDefinitionData def, VehicleSpawnRequest request)
        {
            definition = def;
            spawnRequest = request;
            
            controller = GetComponent<OptimizedVehicleController>();
            if (controller != null)
            {
                controller.Initialize(def);
                controller.SetDriver(true);
            }
            
            // Set initial target speed
            targetSpeed = request.isEmergency ? def.maxSpeed : Random.Range(def.maxSpeed * 0.6f, def.maxSpeed * 0.9f);
            
            // Setup lights
            SetupLights(request.isEmergency);
        }
        
        private void SetupLights(bool isEmergency)
        {
            var lights = GetComponentsInChildren<Light>();
            foreach (var light in lights)
            {
                if (light.name.Contains("headlight") || light.name.Contains("head"))
                {
                    light.enabled = true;
                }
            }
            
            if (isEmergency)
            {
                // Enable emergency lights
                var dls = GetComponent<DLSController>();
                if (dls != null) dls.SetActive(true);
            }
        }
        
        private void Update()
        {
            if (controller == null) return;
            
            // Basic traffic AI
            UpdateDriving();
            UpdateLaneChanges();
            UpdateSpeedControl();
            CheckCollisions();
        }
        
        private void UpdateDriving()
        {
            // Follow road
            if (currentRoad != null)
            {
                // Calculate road-relative position
                Vector3 roadPos = currentRoad.GetClosestPointOnRoad(transform.position);
                Vector3 roadDir = currentRoad.GetDirectionAt(roadPos);
                
                // Steer toward road
                Vector3 toRoad = roadPos - transform.position;
                float steerAngle = Vector3.SignedAngle(transform.forward, roadDir, Vector3.up);
                steerAngle = Mathf.Clamp(steerAngle, -controller.maxSteerAngle, controller.maxSteerAngle);

                controller.SetInput(1f, 0f, steerAngle / controller.maxSteerAngle, false);
            }
            else
            {
                // Free driving - wander
                controller.SetInput(1f, 0f, Random.Range(-0.1f, 0.1f), false);
            }
        }
        
        private void UpdateLaneChanges()
        {
            laneChangeTimer += Time.deltaTime;
            
            if (laneChangeTimer > 10f && Random.value < 0.01f)
            {
                // Consider lane change
                if (CanChangeLane())
                {
                    laneChangeDirection = Random.value > 0.5f ? 1 : -1;
                    laneChangeTimer = 0f;
                }
            }
            
            if (laneChangeDirection != 0)
            {
                // Execute lane change
                float steerInput = laneChangeDirection * 0.5f;
                controller.SetInput(1f, 0f, steerInput, false);
                
                // Check if lane change complete
                if (IsLaneChangeComplete())
                {
                    laneChangeDirection = 0;
                }
            }
        }
        
        private void UpdateSpeedControl()
        {
            // Maintain target speed
            float currentSpeed = controller.SpeedKPH;
            float speedDiff = targetSpeed - currentSpeed;
            
            // Adjust throttle/brake
            float throttle = Mathf.Clamp(speedDiff / 20f, -1f, 1f);
            // Apply via controller input
        }
        
        private void CheckCollisions()
        {
            // Raycast forward
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out hit, followDistance))
            {
                var otherVehicle = hit.collider.GetComponentInParent<TrafficVehicle>();
                if (otherVehicle != null)
                {
                    // Slow down
                    targetSpeed = Mathf.Min(targetSpeed, otherVehicle.controller.SpeedKPH - 5f);
                    targetSpeed = Mathf.Max(targetSpeed, 10f);
                }
            }
            else
            {
                // Resume target speed
                targetSpeed = Mathf.Lerp(targetSpeed, definition.maxSpeed * 0.8f, Time.deltaTime * 0.1f);
            }
        }
        
        private bool CanChangeLane()
        {
            // Check adjacent lane for space
            return true; // Simplified
        }
        
        private bool IsLaneChangeComplete()
        {
            return true; // Simplified
        }
        
        public void SetRandomPaint()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var rend in renderers)
            {
                if (rend.name.Contains("body") || rend.name.Contains("paint"))
                {
                    var mat = rend.material;
                    mat.color = Random.ColorHSV(0f, 1f, 0.3f, 1f, 0.5f, 1f);
                }
            }
        }
        
        public void SetRandomWheels()
        {
            // Swap wheel meshes
        }
        
        public void SetRandomBodyMods()
        {
            // Enable/disable body parts
        }
        
        public void Despawn()
        {
            Destroy(gameObject);
        }
        
        public enum TrafficAIState
        {
            Driving,
            Stopping,
            Turning,
            LaneChanging,
            EmergencyResponse,
            Parked,
        }
    }
    
    // ============================================================================
    // Pedestrian Driver
    // ============================================================================
    
    public class PedestrianDriver : MonoBehaviour
    {
        public PedestrianDefinition definition;
        public TrafficVehicle vehicle;
        
        public void Initialize(PedestrianDefinition def, TrafficVehicle veh)
        {
            definition = def;
            vehicle = veh;
            
            // Setup appearance
            SetupAppearance();
            
            // Add animations
            var animator = gameObject.AddComponent<Animator>();
            // animator.runtimeAnimatorController = def.animatorController;
        }
        
        private void SetupAppearance()
        {
            // Apply clothing, accessories from definition
        }
        
        private void Update()
        {
            // Animate steering wheel, pedals
            if (vehicle?.controller != null)
            {
                float steer = vehicle.controller.GetSteerInput();
                transform.localRotation = Quaternion.Euler(0, steer * 90f, 0);
            }
        }
    }
    
    // ============================================================================
    // Supporting Types
    // ============================================================================
    
    
    
    
    
    
    [System.Serializable]
    public class PedestrianDefinition
    {
        public string id;
        public string modelName;
        public string[] clothingOptions;
        public string[] accessoryOptions;
        // public RuntimeAnimatorController animatorController;
    }
}
