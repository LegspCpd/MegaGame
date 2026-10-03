using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Megame.Data;
using Megame.Vehicles;

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
        public VehicleModifications mods;
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
    
    // ============================================================================
    // Garage UI
    // ============================================================================
    
    public class GarageUI : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject garageRoot;
        public Transform vehicleListContainer;
        public GameObject vehicleItemPrefab;
        public GameObject emptyGarageText;
        
        [Header("Detail Panel")]
        public GameObject detailPanel;
        public TextMeshProUGUI vehicleNameText;
        public TextMeshProUGUI vehicleStatsText;
        public Image vehicleImage;
        public Button spawnButton;
        public Button renameButton;
        public Button removeButton;
        public Button closeButton;
        
        [Header("Spawn Options")]
        public GameObject spawnOptionsPanel;
        public Transform spawnPointList;
        public GameObject spawnPointItemPrefab;
        
        private SavedVehicle selectedVehicle;
        private List<Transform> spawnPoints = new List<Transform>();
        
        private void Awake()
        {
            garageRoot.SetActive(false);
            detailPanel.SetActive(false);
            spawnOptionsPanel.SetActive(false);
            
            spawnButton.onClick.AddListener(OnSpawnClicked);
            renameButton.onClick.AddListener(OnRenameClicked);
            removeButton.onClick.AddListener(OnRemoveClicked);
            closeButton.onClick.AddListener(CloseGarage);
            
            VehiclePersistenceSystem.Instance.OnGarageOpened += RefreshList;
        }
        
        public void OpenGarage()
        {
            garageRoot.SetActive(true);
            RefreshList();
            
            // Find spawn points
            spawnPoints.Clear();
            var spawners = FindObjectsOfType<GarageSpawnPoint>();
            foreach (var s in spawners) spawnPoints.Add(s.transform);
        }
        
        public void CloseGarage()
        {
            garageRoot.SetActive(false);
            detailPanel.SetActive(false);
            spawnOptionsPanel.SetActive(false);
            Time.timeScale = 1f;
        }
        
        private void RefreshList()
        {
            // Clear
            foreach (Transform child in vehicleListContainer)
            {
                Destroy(child.gameObject);
            }
            
            var vehicles = VehiclePersistenceSystem.Instance.GetGarageVehicles();
            
            if (vehicles.Count == 0)
            {
                emptyGarageText.SetActive(true);
                detailPanel.SetActive(false);
                return;
            }
            
            emptyGarageText.SetActive(false);
            
            foreach (var vehicle in vehicles)
            {
                var item = Instantiate(vehicleItemPrefab, vehicleListContainer);
                var itemScript = item.GetComponent<GarageVehicleItem>();
                if (itemScript != null)
                {
                    itemScript.Setup(vehicle, this);
                }
            }
        }
        
        public void SelectVehicle(SavedVehicle vehicle)
        {
            selectedVehicle = vehicle;
            detailPanel.SetActive(true);
            
            var def = Resources.Load<VehicleDefinition>($"VehicleDefinitions/{vehicle.vehicleModel}");
            
            vehicleNameText.text = vehicle.DisplayName;
            vehicleStatsText.text = $"Model: {vehicle.vehicleModel}\n" +
                $"Mileage: {vehicle.mileage:F0} km\n" +
                $"Fuel: {vehicle.fuel}%\n" +
                $"Engine: {vehicle.engineHealth}/1000\n" +
                $"Body: {vehicle.bodyHealth}/1000\n" +
                $"Plate: {vehicle.plateText} (Style {vehicle.plateStyle})\n" +
                $"Acquired: {vehicle.FormattedAcquisitionTime}\n" +
                $"Last Used: {vehicle.FormattedLastUsedTime}";
            
            if (vehicleImage != null && def != null)
            {
                // vehicleImage.sprite = def.icon;
            }
            
            spawnButton.interactable = true;
        }
        
        private void OnSpawnClicked()
        {
            if (selectedVehicle == null) return;
            
            // Show spawn point selection
            spawnOptionsPanel.SetActive(true);
            
            foreach (Transform child in spawnPointList)
            {
                Destroy(child.gameObject);
            }
            
            foreach (var point in FindObjectsOfType<GarageSpawnPoint>())
            {
                var item = Instantiate(spawnPointItemPrefab, spawnPointList);
                var text = item.GetComponentInChildren<TextMeshProUGUI>();
                text.text = point.spawnName;
                var btn = item.GetComponent<Button>();
                btn.onClick.AddListener(() => SpawnAtPoint(point.transform));
            }
        }
        
        private void SpawnAtPoint(Transform point)
        {
            var go = VehiclePersistenceSystem.Instance.SpawnVehicleFromGarage(
                selectedVehicle.id, point.position, point.rotation);
            
            if (go != null)
            {
                VehiclePersistenceSystem.Instance.OnVehicleSpawned?.Invoke(selectedVehicle);
                
                // Teleport player to vehicle
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    var controller = go.GetComponent<OptimizedVehicleController>();
                    controller.WarpPlayerInto(player.transform);
                }
            }
            
            spawnOptionsPanel.SetActive(false);
            CloseGarage();
        }
        
        private void OnRenameClicked()
        {
            if (selectedVehicle == null) return;
            
            // Show rename input
            // Implementation: show input field, update vehicle.customName, save
        }
        
        private void OnRemoveClicked()
        {
            if (selectedVehicle == null) return;
            
            // Show confirmation
            if (UnityEditor.EditorUtility.DisplayDialog("Remove Vehicle", 
                $"Remove {selectedVehicle.DisplayName} from garage?", "Yes", "No"))
            {
                VehiclePersistenceSystem.Instance.RemoveVehicleFromGarage(selectedVehicle.id);
                VehiclePersistenceSystem.Instance.OnVehicleRemoved?.Invoke(selectedVehicle.id);
                RefreshList();
                detailPanel.SetActive(false);
            }
        }
        
        private SavedVehicle selectedVehicle;
    }
    
    [System.Serializable]
    public class GarageVehicleItem : MonoBehaviour
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI infoText;
        public Image conditionBar;
        public Image conditionBackground;
        public Button selectButton;
        
        public void Setup(SavedVehicle vehicle, GarageUI garageUI)
        {
            nameText.text = vehicle.DisplayName;
            infoText.text = $"{vehicle.vehicleModel} • {vehicle.mileage:F0}km • {(vehicle.engineHealth/10):F0}%";
            
            // Condition color
            float health = vehicle.HealthPercentage;
            conditionBar.fillAmount = health;
            conditionBar.color = health > 0.6f ? Color.green : (health > 0.3f ? Color.yellow : Color.red);
            
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => garageUI.SelectVehicle(vehicle));
        }
    }
    
    public class GarageSpawnPoint : MonoBehaviour
    {
        public string spawnName = "Garage Exit";
        public bool isDefault = false;
        
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, 2f);
            Gizmos.DrawRay(transform.position, transform.forward * 3f);
        }
    }
}