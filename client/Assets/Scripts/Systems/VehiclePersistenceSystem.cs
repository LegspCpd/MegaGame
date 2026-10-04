using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Megame.Data;
using Megame.Vehicles;
using UnityEngine.UI;
using TMPro;
using Grpc.Net.Client;
using Megame.Controllers;
using Megame.Client;

namespace Megame.Systems
{
    /// <summary>
    /// Vehicle Persistence System
    /// Handles player garage, vehicle saves, mod persistence
    /// </summary>
    public class VehiclePersistenceSystem : MonoBehaviour
    {
        public static VehiclePersistenceSystem Instance { get; private set; }
        
        [Header("Configuration")]
        public int maxGarageSlots = 50;
        public string saveDirectory = "Saves/Vehicles";
        
        [Header("References")]
        public VehicleDatabase vehicleDatabase;
        
        // Runtime
        private List<SavedVehicle> garage = new List<SavedVehicle>();
        private string savePath;
        
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
            
            savePath = Path.Combine(Application.persistentDataPath, saveDirectory);
            Directory.CreateDirectory(savePath);
        }
        
        private void Start()
        {
            LoadGarage();
        }
        
        // ============================================================================
        // Public API
        // ============================================================================
        
        /// <summary>
        /// Save current player vehicle to garage
        /// </summary>
        public bool SaveVehicleToGarage(GameObject vehicle, string customName = "")
        {
            var controller = vehicle.GetComponent<OptimizedVehicleController>();
            if (controller == null || controller.definition == null) return false;
            
            if (garage.Count >= maxGarageSlots)
            {
                Debug.LogWarning("Garage full!");
                return false;
            }
            
            var saved = new SavedVehicle
            {
                id = System.Guid.NewGuid().ToString(),
                vehicleModel = controller.definition.modelName,
                customName = string.IsNullOrEmpty(customName) ? controller.definition.displayName : customName,
                mods = controller.currentMods.Clone(),
                condition = controller.currentCondition.Clone(),
                fuel = controller.currentFuel,
                engineHealth = controller.currentEngineHealth,
                bodyHealth = controller.currentBodyHealth,
                plateText = controller.currentPlateText,
                plateStyle = controller.currentPlateStyle,
                mileage = controller.totalDistanceDriven,
                acquisitionTime = System.DateTime.UtcNow.ToString("O"),
                lastUsedTime = System.DateTime.UtcNow.ToString("O"),
                position = vehicle.transform.position,
                rotation = vehicle.transform.rotation,
            };
            
            garage.Add(saved);
            SaveGarage();
            
            Debug.Log($"Vehicle saved to garage: {saved.customName} ({saved.vehicleModel})");
            return true;
        }
        
        /// <summary>
        /// Spawn a saved vehicle from garage
        /// </summary>
        public GameObject SpawnVehicleFromGarage(string vehicleId, Vector3 position, Quaternion rotation)
        {
            var saved = garage.FirstOrDefault(v => v.id == vehicleId);
            if (saved == null) return null;
            
            var def = vehicleDatabase?.Get(saved.vehicleModel);
            if (def == null || def.modelPrefab == null) return null;
            
            var go = Instantiate(def.modelPrefab, position, rotation);
            go.name = $"Garage_{saved.customName}";
            
            var controller = go.GetComponent<OptimizedVehicleController>();
            if (controller == null) controller = go.AddComponent<OptimizedVehicleController>();
            
            controller.Initialize(def);
            controller.ApplyMods(saved.mods);
            controller.currentCondition = saved.condition.Clone();
            controller.currentFuel = saved.fuel;
            controller.currentEngineHealth = saved.engineHealth;
            controller.currentBodyHealth = saved.bodyHealth;
            controller.currentPlateText = saved.plateText;
            controller.currentPlateStyle = saved.plateStyle;
            controller.totalDistanceDriven = saved.mileage;
            
            // Update timestamps
            saved.lastUsedTime = System.DateTime.UtcNow.ToString("O");
            SaveGarage();
            
            return go;
        }
        
        /// <summary>
        /// Remove vehicle from garage
        /// </summary>
        public bool RemoveVehicleFromGarage(string vehicleId)
        {
            var saved = garage.FirstOrDefault(v => v.id == vehicleId);
            if (saved == null) return false;
            
            garage.Remove(saved);
            SaveGarage();
            return true;
        }
        
        /// <summary>
        /// Update vehicle in garage (after driving)
        /// </summary>
        public void UpdateGarageVehicle(GameObject vehicle)
        {
            var controller = vehicle.GetComponent<OptimizedVehicleController>();
            if (controller == null) return;
            
            // Find by position proximity or unique identifier
            var saved = garage.FirstOrDefault(v => 
                v.vehicleModel == controller.definition.modelName &&
                v.plateText == controller.currentPlateText);
            
            if (saved != null)
            {
                saved.mods = controller.currentMods.Clone();
                saved.condition = controller.currentCondition.Clone();
                saved.fuel = controller.currentFuel;
                saved.engineHealth = controller.currentEngineHealth;
                saved.bodyHealth = controller.currentBodyHealth;
                saved.mileage = controller.totalDistanceDriven;
                saved.lastUsedTime = System.DateTime.UtcNow.ToString("O");
                saved.position = vehicle.transform.position;
                saved.rotation = vehicle.transform.rotation;
                
                SaveGarage();
            }
        }
        
        /// <summary>
        /// Get all vehicles in garage
        /// </summary>
        public List<SavedVehicle> GetGarageVehicles()
        {
            return new List<SavedVehicle>(garage);
        }
        
        /// <summary>
        /// Get vehicles by model
        /// </summary>
        public List<SavedVehicle> GetVehiclesByModel(string modelName)
        {
            return garage.Where(v => v.vehicleModel == modelName).ToList();
        }
        
        /// <summary>
        /// Check if vehicle is in garage
        /// </summary>
        public bool HasVehicle(string vehicleId)
        {
            return garage.Any(v => v.id == vehicleId);
        }
        
        // ============================================================================
        // Save/Load
        // ============================================================================
        
        private void SaveGarage()
        {
            var data = new GarageSaveData
            {
                version = 1,
                saveTime = System.DateTime.UtcNow.ToString("O"),
                vehicles = garage,
            };
            
            string json = JsonUtility.ToJson(data, true);
            string filePath = Path.Combine(savePath, "garage.json");
            File.WriteAllText(filePath, json);
        }
        
        private void LoadGarage()
        {
            string filePath = Path.Combine(savePath, "garage.json");
            if (!File.Exists(filePath)) return;
            
            try
            {
                string json = File.ReadAllText(filePath);
                var data = JsonUtility.FromJson<GarageSaveData>(json);
                if (data != null && data.vehicles != null)
                {
                    garage = data.vehicles;
                    Debug.Log($"Loaded garage with {garage.Count} vehicles");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load garage: {e.Message}");
            }
        }
        
        // ============================================================================
        // Garage UI Integration
        // ============================================================================
        
        public void OpenGarageUI()
        {
            // Trigger UI event
            OnGarageOpened?.Invoke();
        }
        
        public event System.Action OnGarageOpened;
        public event System.Action<SavedVehicle> OnVehicleSelected;
        public event System.Action<SavedVehicle> OnVehicleSpawned;
        public event System.Action<string> OnVehicleRemoved;
    }
    
    // ============================================================================
    // Data Structures
    // ============================================================================
    
    [System.Serializable]
    public class GarageSaveData
    {
        public int version;
        public string saveTime;
        public List<SavedVehicle> vehicles = new List<SavedVehicle>();
    }
    
    [System.Serializable]
    public class SavedVehicle
    {
        public string id;
        public string vehicleModel;
        public string customName;
        public VehicleModificationsData mods;
        public VehicleCondition condition;
        public int fuel;
        public int engineHealth;
        public int bodyHealth;
        public string plateText;
        public int plateStyle;
        public float mileage;
        public string acquisitionTime;
        public string lastUsedTime;
        public Vector3 position;
        public Quaternion rotation;
        
        // Computed properties
        public string DisplayName => string.IsNullOrEmpty(customName) ? vehicleModel : customName;
        public string FormattedAcquisitionTime => System.DateTime.Parse(acquisitionTime).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        public string FormattedLastUsedTime => System.DateTime.Parse(lastUsedTime).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        public bool IsDamaged => engineHealth < 800 || bodyHealth < 800;
        public float HealthPercentage => Mathf.Max(engineHealth, bodyHealth) / 1000f;
    }
}
