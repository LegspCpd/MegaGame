using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Megame.Data;

namespace Megame.Editor
{
    /// <summary>
    /// Import vehicle definitions from pipeline JSON output
    /// </summary>
    public class VehicleDefinitionImporter : EditorWindow
    {
        private string jsonFolderPath = "Assets/Resources/VehicleDatabase";
        private VehicleDatabase database;
        private List<string> importLog = new List<string>();
        private Vector2 scrollPos;
        
        [MenuItem("MegaGame/Tools/Vehicle Definition Importer")]
        public static void ShowWindow()
        {
            GetWindow<VehicleDefinitionImporter>("Vehicle Importer");
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Vehicle Definition Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("JSON Folder:", GUILayout.Width(100));
            jsonFolderPath = EditorGUILayout.TextField(jsonFolderPath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select JSON Folder", jsonFolderPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    // Make relative to project
                    if (path.StartsWith(Application.dataPath))
                    {
                        jsonFolderPath = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        jsonFolderPath = path;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target Database:", GUILayout.Width(120));
            database = (VehicleDatabase)EditorGUILayout.ObjectField(database, typeof(VehicleDatabase), false);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Import All JSON Definitions", GUILayout.Height(30)))
            {
                ImportAll();
            }
            
            if (GUILayout.Button("Create New Database", GUILayout.Height(25)))
            {
                CreateNewDatabase();
            }
            
            if (GUILayout.Button("Validate Database", GUILayout.Height(25)))
            {
                ValidateDatabase();
            }
            
            EditorGUILayout.Space();
            GUILayout.Label("Import Log:", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(300));
            foreach (var log in importLog)
            {
                GUILayout.Label(log, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
            
            if (GUILayout.Button("Clear Log"))
            {
                importLog.Clear();
            }
        }
        
        private void ImportAll()
        {
            importLog.Clear();
            
            if (database == null)
            {
                LogError("No database selected!");
                return;
            }
            
            string fullPath = Path.Combine(Application.dataPath, jsonFolderPath.Replace("Assets/", ""));
            if (!Directory.Exists(fullPath))
            {
                LogError($"Folder not found: {fullPath}");
                return;
            }
            
            var jsonFiles = Directory.GetFiles(fullPath, "*.json");
            Log($"Found {jsonFiles.Length} JSON files");
            
            var definitions = new List<VehicleDefinitionData>();
            int imported = 0;
            int skipped = 0;
            int errors = 0;
            
            foreach (var jsonFile in jsonFiles)
            {
                try
                {
                    string json = File.ReadAllText(jsonFile);
                    var data = JsonUtility.FromJson<VehicleDefinitionJson>(json);
                    
                    if (data == null)
                    {
                        LogError($"Failed to parse: {Path.GetFileName(jsonFile)}");
                        errors++;
                        continue;
                    }
                    
                    // Check if already exists
                    var existing = database.vehicles?.FirstOrDefault(v => v != null && v.modelName == data.modelName);
                    if (existing != null)
                    {
                        UpdateDefinition(existing, data);
                        Log($"Updated: {data.displayName} ({data.modelName})");
                        imported++;
                    }
                    else
                    {
                        var def = CreateDefinition(data);
                        if (def != null)
                        {
                            definitions.Add(def);
                            Log($"Created: {data.displayName} ({data.modelName})");
                            imported++;
                        }
                        else
                        {
                            errors++;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    LogError($"Error importing {Path.GetFileName(jsonFile)}: {e.Message}");
                    errors++;
                }
            }
            
            // Update database
            if (definitions.Count > 0)
            {
                var allDefs = new List<VehicleDefinitionData>();
                if (database.vehicles != null)
                    allDefs.AddRange(database.vehicles.Where(v => v != null));
                allDefs.AddRange(definitions);
                
                // Remove duplicates
                var unique = allDefs.GroupBy(d => d.modelName).Select(g => g.First()).ToArray();
                database.vehicles = unique;
                
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
            }
            
            Log($"Import complete: {imported} imported/updated, {skipped} skipped, {errors} errors");
            AssetDatabase.Refresh();
        }
        
        private VehicleDefinitionData CreateDefinition(VehicleDefinitionJson data)
        {
            var def = CreateInstance<VehicleDefinitionData>();
            def.modelName = data.modelName;
            def.displayName = data.displayName;
            def.manufacturer = data.manufacturer;
            def.vehicleClass = (VehicleClass)System.Enum.Parse(typeof(VehicleClass), data.vehicleClass);
            def.vehicleType = (VehicleType)System.Enum.Parse(typeof(VehicleType), data.vehicleType);
            
            // Specs
            if (data.specs != null)
            {
                var s = data.specs;
                def.length = s.length;
                def.width = s.width;
                def.height = s.height;
                def.wheelbase = s.wheelbase;
                def.trackWidthFront = s.trackWidthFront;
                def.trackWidthRear = s.trackWidthRear;
                def.groundClearance = s.groundClearance;
                def.mass = s.mass;
                def.dragCoefficient = s.dragCoefficient;
                def.centerOfMass = new Vector3(s.centerOfMass[0], s.centerOfMass[1], s.centerOfMass[2]);
                def.inertiaMultiplier = new Vector3(s.inertiaMultiplier[0], s.inertiaMultiplier[1], s.inertiaMultiplier[2]);
                def.maxSpeed = s.maxSpeed;
                def.maxRPM = s.maxRPM;
                def.idleRPM = s.idleRPM;
                def.maxTorque = s.maxTorque;
                def.gearRatios = s.gearRatios;
                def.finalDriveRatio = s.finalDriveRatio;
                def.driveBiasFront = s.driveBiasFront;
                def.maxSteerAngle = s.maxSteerAngle;
                def.steerSpeed = s.steerSpeed;
                def.brakeForce = s.brakeForce;
                def.handbrakeForce = s.handbrakeForce;
                def.suspensionForce = s.suspensionForce;
                def.suspensionDamper = s.suspensionDamper;
                def.suspensionTravel = s.suspensionTravel;
                def.springLength = s.springLength;
                def.downforceCoefficient = s.downforceCoefficient;
                def.wheelRadiusFront = s.wheelRadiusFront;
                def.wheelRadiusRear = s.wheelRadiusRear;
                def.wheelWidthFront = s.wheelWidthFront;
                def.wheelWidthRear = s.wheelWidthRear;
                def.engineAudioHash = s.engineAudioHash;
                def.hornAudioHash = s.hornAudioHash;
                def.modKitId = s.modKitId;
                def.hasLivery = s.hasLivery;
                def.hasSiren = s.hasSiren;
                def.sirenAudioHash = s.sirenAudioHash;
                def.lightPattern = s.lightPattern;
                def.elsConfig = s.elsConfig;
            }
            
            // Save as asset
            string assetPath = $"Assets/Resources/VehicleDefinitions/{def.modelName}.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            AssetDatabase.CreateAsset(def, assetPath);
            
            return def;
        }
        
        private void UpdateDefinition(VehicleDefinitionData def, VehicleDefinitionJson data)
        {
            def.displayName = data.displayName;
            def.manufacturer = data.manufacturer;
            def.vehicleClass = (VehicleClass)System.Enum.Parse(typeof(VehicleClass), data.vehicleClass);
            def.vehicleType = (VehicleType)System.Enum.Parse(typeof(VehicleType), data.vehicleType);
            
            if (data.specs != null)
            {
                var s = data.specs;
                def.length = s.length;
                def.width = s.width;
                def.height = s.height;
                def.wheelbase = s.wheelbase;
                def.trackWidthFront = s.trackWidthFront;
                def.trackWidthRear = s.trackWidthRear;
                def.groundClearance = s.groundClearance;
                def.mass = s.mass;
                def.dragCoefficient = s.dragCoefficient;
                def.centerOfMass = new Vector3(s.centerOfMass[0], s.centerOfMass[1], s.centerOfMass[2]);
                def.inertiaMultiplier = new Vector3(s.inertiaMultiplier[0], s.inertiaMultiplier[1], s.inertiaMultiplier[2]);
                def.maxSpeed = s.maxSpeed;
                def.maxRPM = s.maxRPM;
                def.idleRPM = s.idleRPM;
                def.maxTorque = s.maxTorque;
                def.gearRatios = s.gearRatios;
                def.finalDriveRatio = s.finalDriveRatio;
                def.driveBiasFront = s.driveBiasFront;
                def.maxSteerAngle = s.maxSteerAngle;
                def.steerSpeed = s.steerSpeed;
                def.brakeForce = s.brakeForce;
                def.handbrakeForce = s.handbrakeForce;
                def.suspensionForce = s.suspensionForce;
                def.suspensionDamper = s.suspensionDamper;
                def.suspensionTravel = s.suspensionTravel;
                def.springLength = s.springLength;
                def.downforceCoefficient = s.downforceCoefficient;
                def.wheelRadiusFront = s.wheelRadiusFront;
                def.wheelRadiusRear = s.wheelRadiusRear;
                def.wheelWidthFront = s.wheelWidthFront;
                def.wheelWidthRear = s.wheelWidthRear;
                def.engineAudioHash = s.engineAudioHash;
                def.hornAudioHash = s.hornAudioHash;
                def.modKitId = s.modKitId;
                def.hasLivery = s.hasLivery;
                def.hasSiren = s.hasSiren;
                def.sirenAudioHash = s.sirenAudioHash;
                def.lightPattern = s.lightPattern;
                def.elsConfig = s.elsConfig;
            }
            
            EditorUtility.SetDirty(def);
        }
        
        private void CreateNewDatabase()
        {
            var db = CreateInstance<VehicleDatabase>();
            string path = "Assets/Resources/VehicleDatabase.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            database = db;
            Selection.activeObject = db;
            Log($"Created new database at {path}");
        }
        
        private void ValidateDatabase()
        {
            if (database == null || database.vehicles == null)
            {
                LogError("No database to validate");
                return;
            }
            
            Log($"Validating {database.vehicles.Length} vehicles...");
            
            int valid = 0;
            int missingModel = 0;
            int missingMaterials = 0;
            int duplicateNames = 0;
            
            var seenNames = new HashSet<string>();
            
            foreach (var v in database.vehicles)
            {
                if (v == null) continue;
                
                if (!seenNames.Add(v.modelName))
                {
                    LogError($"Duplicate model name: {v.modelName}");
                    duplicateNames++;
                }
                
                if (v.modelPrefab == null)
                {
                    LogError($"Missing model prefab: {v.displayName} ({v.modelName})");
                    missingModel++;
                }
                
                if (v.materials == null || v.materials.Length == 0)
                {
                    LogError($"Missing materials: {v.displayName} ({v.modelName})");
                    missingMaterials++;
                }
                
                valid++;
            }
            
            Log($"Validation complete: {valid} valid, {missingModel} missing models, {missingMaterials} missing materials, {duplicateNames} duplicates");
        }
        
        private void Log(string msg)
        {
            importLog.Add($"[{System.DateTime.Now:HH:mm:ss}] {msg}");
            Debug.Log(msg);
        }
        
        private void LogError(string msg)
        {
            importLog.Add($"[{System.DateTime.Now:HH:mm:ss}] ERROR: {msg}");
            Debug.LogError(msg);
        }
    }
    
    // JSON-serializable version matching pipeline output
    [System.Serializable]
    public class VehicleDefinitionJson
    {
        public string modelName;
        public string displayName;
        public string manufacturer;
        public string vehicleClass;
        public string vehicleType;
        public SpecsJson specs;
        public MetadataJson metadata;
        public string prefabPath;
        public string modelPath;
        public string texturePath;
    }
    
    [System.Serializable]
    public class SpecsJson
    {
        public float length;
        public float width;
        public float height;
        public float wheelbase;
        public float trackWidthFront;
        public float trackWidthRear;
        public float groundClearance;
        public float mass;
        public float dragCoefficient;
        public float[] centerOfMass;
        public float[] inertiaMultiplier;
        public float maxSpeed;
        public float maxRPM;
        public float idleRPM;
        public float maxTorque;
        public float[] torqueCurve;
        public float[] gearRatios;
        public float finalDriveRatio;
        public float driveBiasFront;
        public float maxSteerAngle;
        public float steerSpeed;
        public float brakeForce;
        public float handbrakeForce;
        public float suspensionForce;
        public float suspensionDamper;
        public float suspensionTravel;
        public float springLength;
        public float downforceCoefficient;
        public float wheelRadiusFront;
        public float wheelRadiusRear;
        public float wheelWidthFront;
        public float wheelWidthRear;
        public string engineAudioHash;
        public string hornAudioHash;
        public int modKitId;
        public bool hasLivery;
        public bool hasSiren;
        public string sirenAudioHash;
        public string lightPattern;
        public string elsConfig;
    }
    
    [System.Serializable]
    public class MetadataJson
    {
        public string sourceFile;
        public string modType;
        public string displayName;
        public string author;
        public string version;
        public string[] tags;
        public string[] dependencies;
    }
}