using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Megame.Data;
using Megame.Controllers;

namespace Megame.Editor
{
    /// <summary>
    /// Automatic Vehicle Prefab Generator
    /// Creates complete vehicle prefabs from imported FBX models
    /// </summary>
    public class VehiclePrefabGenerator : EditorWindow
    {
        private string modelsRootPath = "Assets/Imported/Vehicles";
        private VehicleDatabase database;
        private VehicleDefinitionData currentDefinition;
        private GameObject currentModelPrefab;
        
        // Wheel detection
        private float wheelRadiusFront = 0.35f;
        private float wheelRadiusRear = 0.35f;
        private float suspensionDistance = 0.2f;
        
        // Generated components
        private List<WheelCollider> wheelColliders = new List<WheelCollider>();
        private List<Transform> wheelTransforms = new List<Transform>();
        
        // Preview
        private Vector2 scrollPos;
        private bool showWheelSetup = true;
        private bool showPhysicsSetup = true;
        private bool showEmergencySetup = false;
        
        [MenuItem("MegaGame/Tools/Vehicle Prefab Generator")]
        public static void ShowWindow()
        {
            GetWindow<VehiclePrefabGenerator>("Vehicle Prefab Generator");
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Vehicle Prefab Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Models root
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Models Root:", GUILayout.Width(100));
            modelsRootPath = EditorGUILayout.TextField(modelsRootPath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Models Root", modelsRootPath, "");
                if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                {
                    modelsRootPath = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Scan for models
            if (GUILayout.Button("Scan for Models", GUILayout.Height(30)))
            {
                ScanModels();
            }
            
            EditorGUILayout.Space();
            
            if (currentDefinition == null)
            {
                EditorGUILayout.HelpBox("Select a model directory from the scan results, or drag a VehicleDefinitionData here.", MessageType.Info);
            }
            else
            {
                DrawDefinitionEditor();
            }
            
            EditorGUILayout.Space();
            
            // Batch operations
            GUILayout.Label("Batch Operations", EditorStyles.boldLabel);
            if (GUILayout.Button("Generate All Prefabs", GUILayout.Height(30)))
            {
                GenerateAllPrefabs();
            }
            
            if (GUILayout.Button("Validate All Prefabs", GUILayout.Height(25)))
            {
                ValidateAllPrefabs();
            }
        }
        
        private void ScanModels()
        {
            string fullPath = Path.Combine(Application.dataPath, modelsRootPath.Replace("Assets/", ""));
            if (!Directory.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Error", "Models root path not found!", "OK");
                return;
            }
            
            var dirs = Directory.GetDirectories(fullPath);
            var menu = new GenericMenu();
            
            foreach (var dir in dirs)
            {
                string dirName = Path.GetFileName(dir);
                var def = database?.Get(dirName);
                
                menu.AddItem(new GUIContent($"{dirName} {(def != null ? "✓" : "")}"), false, () => 
                {
                    LoadModel(dirName);
                });
            }
            
            if (dirs.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("No model directories found"));
            }
            
            menu.ShowAsContext();
        }
        
        private void LoadModel(string modelName)
        {
            string modelDir = Path.Combine(Application.dataPath, modelsRootPath.Replace("Assets/", ""), modelName);
            if (!Directory.Exists(modelDir)) return;
            
            // Find FBX
            var fbxFiles = Directory.GetFiles(modelDir, "*.fbx", SearchOption.AllDirectories);
            if (fbxFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", $"No FBX found in {modelName}", "OK");
                return;
            }
            
            // Load or create definition
            currentDefinition = database?.Get(modelName);
            if (currentDefinition == null && database != null)
            {
                currentDefinition = ScriptableObject.CreateInstance<VehicleDefinitionData>();
                currentDefinition.modelName = modelName;
                currentDefinition.displayName = modelName.Replace("_", " ").Replace("-", " ");
                
                string defPath = $"Assets/Resources/VehicleDefinitions/{modelName}.asset";
                Directory.CreateDirectory(Path.GetDirectoryName(defPath));
                AssetDatabase.CreateAsset(currentDefinition, defPath);
                AssetDatabase.SaveAssets();
            }
            
            // Load model prefab
            string fbxPath = fbxFiles[0].Replace(Application.dataPath, "Assets");
            currentModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            
            // Auto-detect wheels
            AutoDetectWheels();
            
            Repaint();
        }
        
        private void DrawDefinitionEditor()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Editing: {currentDefinition.displayName}", EditorStyles.boldLabel);
            
            // Model reference
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Model Prefab:", GUILayout.Width(100));
            currentModelPrefab = (GameObject)EditorGUILayout.ObjectField(currentModelPrefab, typeof(GameObject), false);
            EditorGUILayout.EndHorizontal();
            
            if (currentModelPrefab != null)
            {
                currentDefinition.modelPrefab = currentModelPrefab;
            }
            
            // Basic info
            EditorGUILayout.Space();
            GUILayout.Label("Basic Info", EditorStyles.boldLabel);
            currentDefinition.displayName = EditorGUILayout.TextField("Display Name", currentDefinition.displayName);
            currentDefinition.manufacturer = EditorGUILayout.TextField("Manufacturer", currentDefinition.manufacturer);
            currentDefinition.vehicleClass = (VehicleClass)EditorGUILayout.EnumPopup("Vehicle Class", currentDefinition.vehicleClass);
            currentDefinition.vehicleType = (VehicleType)EditorGUILayout.EnumPopup("Vehicle Type", currentDefinition.vehicleType);
            
            // Wheel Setup
            showWheelSetup = EditorGUILayout.Foldout(showWheelSetup, "Wheel Setup", true);
            if (showWheelSetup)
            {
                DrawWheelSetup();
            }
            
            // Physics Setup
            showPhysicsSetup = EditorGUILayout.Foldout(showPhysicsSetup, "Physics Setup", true);
            if (showPhysicsSetup)
            {
                DrawPhysicsSetup();
            }
            
            // Emergency Setup
            if (currentDefinition.vehicleType != VehicleType.Civilian)
            {
                showEmergencySetup = EditorGUILayout.Foldout(showEmergencySetup, "Emergency Setup", true);
                if (showEmergencySetup)
                {
                    DrawEmergencySetup();
                }
            }
            
            // Generate buttons
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Prefab", GUILayout.Height(30)))
            {
                GeneratePrefab();
            }
            if (GUILayout.Button("Save Definition", GUILayout.Height(30)))
            {
                EditorUtility.SetDirty(currentDefinition);
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawWheelSetup()
        {
            EditorGUI.indentLevel++;
            
            // Auto-detect button
            if (GUILayout.Button("Auto-Detect Wheels"))
            {
                AutoDetectWheels();
            }
            
            EditorGUILayout.Space();
            
            // Wheel list
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            
            for (int i = 0; i < wheelColliders.Count; i++)
            {
                var wc = wheelColliders[i];
                var wt = i < wheelTransforms.Count ? wheelTransforms[i] : null;
                
                EditorGUILayout.BeginHorizontal("box");
                
                EditorGUILayout.LabelField($"Wheel {i}", GUILayout.Width(60));
                
                wc.transform.position = EditorGUILayout.Vector3Field("Pos", wc.transform.localPosition);
                wc.radius = EditorGUILayout.FloatField("Radius", wc.radius);
                wc.suspensionDistance = EditorGUILayout.FloatField("Suspension", wc.suspensionDistance);
                
                // Wheel type
                bool isFront = wt != null && wt.localPosition.z > 0;
                EditorGUILayout.LabelField(isFront ? "Front" : "Rear", GUILayout.Width(50));
                
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    DestroyImmediate(wc.gameObject);
                    wheelColliders.RemoveAt(i);
                    if (i < wheelTransforms.Count) wheelTransforms.RemoveAt(i);
                    break;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            
            // Add wheel button
            if (GUILayout.Button("Add Wheel"))
            {
                AddWheel();
            }
            
            EditorGUI.indentLevel--;
        }
        
        private void DrawPhysicsSetup()
        {
            EditorGUI.indentLevel++;
            
            var serialized = new SerializedObject(currentDefinition);
            
            EditorGUILayout.PropertyField(serialized.FindProperty("mass"));
            EditorGUILayout.PropertyField(serialized.FindProperty("centerOfMass"));
            EditorGUILayout.PropertyField(serialized.FindProperty("dragCoefficient"));
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Engine", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty("maxSpeed"));
            EditorGUILayout.PropertyField(serialized.FindProperty("maxTorque"));
            EditorGUILayout.PropertyField(serialized.FindProperty("maxRPM"));
            EditorGUILayout.PropertyField(serialized.FindProperty("idleRPM"));
            EditorGUILayout.PropertyField(serialized.FindProperty("gearRatios"));
            EditorGUILayout.PropertyField(serialized.FindProperty("finalDriveRatio"));
            EditorGUILayout.PropertyField(serialized.FindProperty("driveBiasFront"));
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Steering", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty("maxSteerAngle"));
            EditorGUILayout.PropertyField(serialized.FindProperty("steerSpeed"));
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Braking", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty("brakeForce"));
            EditorGUILayout.PropertyField(serialized.FindProperty("handbrakeForce"));
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Suspension", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty("suspensionForce"));
            EditorGUILayout.PropertyField(serialized.FindProperty("suspensionDamper"));
            EditorGUILayout.PropertyField(serialized.FindProperty("suspensionTravel"));
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Aero", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty("downforceCoefficient"));
            
            serialized.ApplyModifiedProperties();
            EditorGUI.indentLevel--;
        }
        
        private void DrawEmergencySetup()
        {
            EditorGUI.indentLevel++;
            
            var serialized = new SerializedObject(currentDefinition);
            
            EditorGUILayout.PropertyField(serialized.FindProperty("hasSiren"));
            
            if (currentDefinition.hasSiren)
            {
                EditorGUILayout.PropertyField(serialized.FindProperty("sirenAudioHash"));
                EditorGUILayout.PropertyField(serialized.FindProperty("lightPattern"));
                EditorGUILayout.PropertyField(serialized.FindProperty("elsConfig"));
            }
            
            // Light setup
            if (GUILayout.Button("Setup Emergency Lights"))
            {
                SetupEmergencyLights();
            }
            
            serialized.ApplyModifiedProperties();
            EditorGUI.indentLevel--;
        }
        
        private void AutoDetectWheels()
        {
            wheelColliders.Clear();
            wheelTransforms.Clear();
            
            if (currentModelPrefab == null) return;
            
            // Find wheel meshes by name patterns
            var renderers = currentModelPrefab.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Concat(currentModelPrefab.GetComponentsInChildren<MeshRenderer>())
                .ToArray();
            
            var wheelPatterns = new[] { "wheel", "rim", "tire", "tyre", "hub" };
            
            foreach (var rend in renderers)
            {
                string name = rend.name.ToLower();
                if (wheelPatterns.Any(p => name.Contains(p)))
                {
                    // Create wheel collider at renderer position
                    var go = new GameObject($"WheelCollider_{rend.name}");
                    go.transform.SetParent(currentModelPrefab.transform);
                    go.transform.position = rend.bounds.center;
                    go.transform.rotation = rend.transform.rotation;
                    
                    var wc = go.AddComponent<WheelCollider>();
                    wc.radius = wheelRadiusFront;
                    wc.suspensionDistance = suspensionDistance;
                    wc.mass = 20f;
                    wc.wheelDampingRate = 0.25f;
                    
                    // Configure suspension
                    var spring = wc.suspensionSpring;
                    spring.spring = 35000f;
                    spring.damper = 4500f;
                    spring.targetPosition = 0.5f;
                    wc.suspensionSpring = spring;
                    
                    wheelColliders.Add(wc);
                    wheelTransforms.Add(rend.transform);
                }
            }
            
            // Classify front/rear and adjust
            foreach (var wc in wheelColliders)
            {
                bool isFront = wc.transform.localPosition.z > 0;
                wc.radius = isFront ? wheelRadiusFront : wheelRadiusRear;
                wc.transform.name = $"{(isFront ? "Front" : "Rear")}Wheel_{wc.transform.name}";
            }
            
            Debug.Log($"Auto-detected {wheelColliders.Count} wheels");
        }
        
        private void AddWheel()
        {
            var go = new GameObject($"WheelCollider_{wheelColliders.Count}");
            go.transform.SetParent(currentModelPrefab?.transform);
            go.transform.localPosition = new Vector3(0, 0.3f, 1.5f);
            
            var wc = go.AddComponent<WheelCollider>();
            wc.radius = wheelRadiusFront;
            wc.suspensionDistance = suspensionDistance;
            
            wheelColliders.Add(wc);
            wheelTransforms.Add(null);
            
            Repaint();
        }
        
        private void SetupEmergencyLights()
        {
            if (currentModelPrefab == null) return;
            
            // Find or create light container
            Transform lightContainer = currentModelPrefab.transform.Find("EmergencyLights");
            if (lightContainer == null)
            {
                lightContainer = new GameObject("EmergencyLights").transform;
                lightContainer.SetParent(currentModelPrefab.transform);
                lightContainer.localPosition = Vector3.zero;
            }
            
            // Common emergency light positions
            var lightConfigs = new[]
            {
                new { name = "Light_Front_Left", pos = new Vector3(-0.8f, 1.2f, 2.0f), color = Color.red },
                new { name = "Light_Front_Right", pos = new Vector3(0.8f, 1.2f, 2.0f), color = Color.blue },
                new { name = "Light_Rear_Left", pos = new Vector3(-0.8f, 1.2f, -2.0f), color = Color.red },
                new { name = "Light_Rear_Right", pos = new Vector3(0.8f, 1.2f, -2.0f), color = Color.blue },
                new { name = "Light_Roof_Center", pos = new Vector3(0, 1.8f, 0), color = Color.white },
            };
            
            foreach (var cfg in lightConfigs)
            {
                var lightObj = lightContainer.Find(cfg.name);
                if (lightObj == null)
                {
                    lightObj = new GameObject(cfg.name).transform;
                    lightObj.SetParent(lightContainer);
                }
                lightObj.localPosition = cfg.pos;
                
                var light = lightObj.GetComponent<Light>();
                if (light == null) light = lightObj.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = cfg.color;
                light.range = 20f;
                light.intensity = 3f;
                light.renderMode = LightRenderMode.ForcePixel;
            }
            
            // Add DLS controller
            var dls = currentModelPrefab.GetComponent<DLSController>();
            if (dls == null) dls = currentModelPrefab.AddComponent<DLSController>();
            
            EditorUtility.SetDirty(currentModelPrefab);
        }
        
        private void GeneratePrefab()
        {
            if (currentDefinition == null || currentModelPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Missing definition or model!", "OK");
                return;
            }
            
            // Create prefab instance
            var instance = PrefabUtility.InstantiatePrefab(currentModelPrefab) as GameObject;
            instance.name = currentDefinition.modelName;
            
            // Add OptimizedVehicleController
            var controller = instance.GetComponent<OptimizedVehicleController>();
            if (controller == null) controller = instance.AddComponent<OptimizedVehicleController>();
            
            // Apply definition to controller
            ApplyDefinitionToController(controller, currentDefinition);
            
            // Setup wheel colliders
            foreach (var wc in wheelColliders)
            {
                if (wc != null)
                {
                    // WheelCollider already on instance from instantiation
                }
            }
            
            // Save prefab
            string prefabPath = $"Assets/Imported/Vehicles/{currentDefinition.modelName}/{currentDefinition.modelName}.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
            
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            
            if (prefab != null)
            {
                currentDefinition.modelPrefab = prefab;
                EditorUtility.SetDirty(currentDefinition);
                AssetDatabase.SaveAssets();
                
                DestroyImmediate(instance);
                
                EditorUtility.DisplayDialog("Success", $"Prefab created at:\n{prefabPath}", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Failed to create prefab!", "OK");
            }
        }
        
        private void ApplyDefinitionToController(OptimizedVehicleController controller, VehicleDefinitionData def)
        {
            // Physics
            controller.MaxTorque = def.maxTorque;
            controller.MaxRPM = def.maxRPM;
            controller.IdleRPM = def.idleRPM;
            controller.GearRatios = def.gearRatios;
            controller.FinalDriveRatio = def.finalDriveRatio;
            controller.DriveBias = def.driveBiasFront;
            controller.MaxSteerAngle = def.maxSteerAngle;
            controller.BrakeForce = def.brakeForce;
            controller.HandbrakeForce = def.handbrakeForce;
            controller.DownforceCoefficient = def.downforceCoefficient;
            controller.DragCoefficient = def.dragCoefficient;
            
            // Wheel colliders
            controller.WheelColliders = wheelColliders.ToArray();
            controller.WheelTransforms = wheelTransforms.ToArray();
            
            // Emergency
            if (def.hasSiren)
            {
                // Setup siren audio
            }
        }
        
        private void GenerateAllPrefabs()
        {
            if (database == null || database.vehicles == null) return;
            
            int generated = 0;
            foreach (var def in database.vehicles)
            {
                if (def == null || def.modelPrefab == null) continue;
                
                // Load model
                currentDefinition = def;
                currentModelPrefab = def.modelPrefab;
                
                // Auto-detect if needed
                if (wheelColliders.Count == 0)
                    AutoDetectWheels();
                
                GeneratePrefab();
                generated++;
            }
            
            EditorUtility.DisplayDialog("Complete", $"Generated {generated} prefabs", "OK");
        }
        
        private void ValidateAllPrefabs()
        {
            if (database == null) return;
            
            int valid = 0, issues = 0;
            
            foreach (var def in database.vehicles)
            {
                if (def == null) continue;
                
                if (def.modelPrefab == null)
                {
                    Debug.LogError($"[Validate] {def.displayName}: Missing model prefab");
                    issues++;
                    continue;
                }
                
                var controller = def.modelPrefab.GetComponent<OptimizedVehicleController>();
                if (controller == null)
                {
                    Debug.LogWarning($"[Validate] {def.displayName}: Missing OptimizedVehicleController");
                    issues++;
                }
                
                var wheels = def.modelPrefab.GetComponentsInChildren<WheelCollider>();
                if (wheels.Length == 0)
                {
                    Debug.LogWarning($"[Validate] {def.displayName}: No wheel colliders");
                    issues++;
                }
                
                valid++;
            }
            
            EditorUtility.DisplayDialog("Validation Complete", 
                $"Valid: {valid}, Issues: {issues}", "OK");
        }
        
        private void OnEnable()
        {
            // Load database
            database = Resources.Load<VehicleDatabase>("VehicleDatabase");
        }
    }
    
    /// <summary>
    /// DLS Emergency Light Controller
    /// </summary>
    public class DLSController : MonoBehaviour
    {
        public string lightPattern = "lspd";
        public Light[] emergencyLights;
        public AudioSource sirenAudio;
        public string elsConfigFile = "";
        
        private int patternIndex = 0;
        private float patternTimer = 0f;
        private bool isActive = false;
        
        private void Start()
        {
            if (emergencyLights == null || emergencyLights.Length == 0)
            {
                emergencyLights = GetComponentsInChildren<Light>();
            }
            
            foreach (var light in emergencyLights)
            {
                light.enabled = false;
            }
        }
        
        public void SetActive(bool active)
        {
            isActive = active;
            if (!active)
            {
                foreach (var light in emergencyLights)
                    light.enabled = false;
            }
        }
        
        public void SetPattern(string patternName)
        {
            lightPattern = patternName;
            // Load pattern from ELS XML
        }
        
        private void Update()
        {
            if (!isActive) return;
            
            // Simple pattern cycling
            patternTimer += Time.deltaTime;
            if (patternTimer > 0.1f)
            {
                patternTimer = 0f;
                patternIndex = (patternIndex + 1) % emergencyLights.Length;
                
                for (int i = 0; i < emergencyLights.Length; i++)
                {
                    emergencyLights[i].enabled = (i == patternIndex || i == (patternIndex + 1) % emergencyLights.Length);
                }
            }
        }
    }
}