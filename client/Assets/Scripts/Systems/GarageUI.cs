using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Megame.Data;
using Megame.Vehicles;

namespace Megame.Systems
{
    /// <summary>
    /// Garage UI for managing player vehicles
    /// </summary>
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
        public RawImage vehicleImage;
        public Button spawnButton;
        public Button renameButton;
        public Button sellButton;
        public Button removeButton;
        public Button closeButton;
        
        [Header("Spawn Options")]
        public GameObject spawnOptionsPanel;
        public Transform spawnPointList;
        public GameObject spawnPointItemPrefab;
        
        [Header("Rename")]
        public GameObject renamePanel;
        public TMP_InputField renameInput;
        public Button confirmRenameButton;
        public Button cancelRenameButton;
        
        [Header("Confirmation")]
        public GameObject confirmPanel;
        public TextMeshProUGUI confirmText;
        public Button confirmYesButton;
        public Button confirmNoButton;
        
        // State
        private SavedVehicle selectedVehicle;
        private System.Action<string> pendingConfirmCallback;
        
        private void Awake()
        {
            garageRoot.SetActive(false);
            detailPanel.SetActive(false);
            spawnOptionsPanel.SetActive(false);
            renamePanel.SetActive(false);
            confirmPanel.SetActive(false);
            
            spawnButton.onClick.AddListener(OnSpawnClicked);
            renameButton.onClick.AddListener(OnRenameClicked);
            sellButton.onClick.AddListener(OnSellClicked);
            removeButton.onClick.AddListener(OnRemoveClicked);
            closeButton.onClick.AddListener(CloseGarage);
            
            confirmRenameButton.onClick.AddListener(ConfirmRename);
            cancelRenameButton.onClick.AddListener(CancelRename);
            
            confirmYesButton.onClick.AddListener(ConfirmPending);
            confirmNoButton.onClick.AddListener(CancelPending);
            
            VehiclePersistenceSystem.Instance.OnGarageOpened += RefreshList;
            VehiclePersistenceSystem.Instance.OnVehicleRemoved += OnVehicleRemoved;
        }
        
        private void OnDestroy()
        {
            if (VehiclePersistenceSystem.Instance != null)
            {
                VehiclePersistenceSystem.Instance.OnGarageOpened -= RefreshList;
                VehiclePersistenceSystem.Instance.OnVehicleRemoved -= OnVehicleRemoved;
            }
        }
        
        public void OpenGarage()
        {
            garageRoot.SetActive(true);
            detailPanel.SetActive(false);
            spawnOptionsPanel.SetActive(false);
            renamePanel.SetActive(false);
            confirmPanel.SetActive(false);
            Time.timeScale = 0f;
            RefreshList();
        }
        
        public void CloseGarage()
        {
            garageRoot.SetActive(false);
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
        
        private void OnVehicleRemoved(string vehicleId)
        {
            RefreshList();
            if (selectedVehicle != null && selectedVehicle.id == vehicleId)
            {
                detailPanel.SetActive(false);
            }
        }
        
        public void SelectVehicle(SavedVehicle vehicle)
        {
            selectedVehicle = vehicle;
            detailPanel.SetActive(true);
            
            var def = VehicleDatabase.Instance?.Get(vehicle.vehicleModel);
            
            vehicleNameText.text = vehicle.DisplayName;
            vehicleStatsText.text = $"Model: {vehicle.vehicleModel}\n" +
                $"Mileage: {vehicle.mileage:F0} km\n" +
                $"Fuel: {vehicle.fuel}%\n" +
                $"Engine: {vehicle.engineHealth}/1000\n" +
                $"Body: {vehicle.bodyHealth}/1000\n" +
                $"Plate: {vehicle.plateText} (Style {vehicle.plateStyle})\n" +
                $"Acquired: {vehicle.FormattedAcquisitionTime}\n" +
                $"Last Used: {vehicle.FormattedLastUsedTime}";
            
            // Load vehicle image
            if (vehicleImage != null && def != null)
            {
                // vehicleImage.texture = def.iconTexture;
            }
            
            spawnButton.interactable = true;
            sellButton.interactable = true;
            removeButton.interactable = true;
        }
        
        private void OnSpawnClicked()
        {
            if (selectedVehicle == null) return;
            
            // Show spawn point selection
            spawnOptionsPanel.SetActive(true);
            
            // Clear
            foreach (Transform child in spawnPointList)
            {
                Destroy(child.gameObject);
            }
            
            // Find spawn points
            var spawnPoints = FindObjectsOfType<GarageSpawnPoint>();
            foreach (var point in spawnPoints)
            {
                if (!point.CanSpawn(VehicleDatabase.Instance.Get(selectedVehicle.vehicleModel))) continue;
                
                var item = Instantiate(spawnPointItemPrefab, spawnPointList);
                var text = item.GetComponentInChildren<TextMeshProUGUI>();
                text.text = point.spawnName + (point.isOccupied ? " (Occupied)" : "");
                var btn = item.GetComponent<Button>();
                btn.interactable = !point.isOccupied;
                btn.onClick.AddListener(() => SpawnAtPoint(point));
            }
        }
        
        private void SpawnAtPoint(GarageSpawnPoint point)
        {
            var go = VehiclePersistenceSystem.Instance.SpawnVehicleFromGarage(
                selectedVehicle.id, point.transform.position, point.transform.rotation);
            
            if (go != null)
            {
                VehiclePersistenceSystem.Instance.OnVehicleSpawned?.Invoke(selectedVehicle);
                
                // Warp player to vehicle
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
            
            renamePanel.SetActive(true);
            renameInput.text = selectedVehicle.customName;
            renameInput.ActivateInputField();
        }
        
        private void ConfirmRename()
        {
            if (selectedVehicle == null) return;
            
            string newName = renameInput.text.Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                selectedVehicle.customName = newName;
                VehiclePersistenceSystem.Instance.SaveGarage();
                RefreshList();
                SelectVehicle(selectedVehicle);
            }
            
            renamePanel.SetActive(false);
        }
        
        private void CancelRename()
        {
            renamePanel.SetActive(false);
        }
        
        private void OnSellClicked()
        {
            if (selectedVehicle == null) return;
            
            ShowConfirmation($"Sell {selectedVehicle.DisplayName} for ${CalculateSellPrice(selectedVehicle):N0}?", () => 
            {
                // Add money
                AddMoney(CalculateSellPrice(selectedVehicle));
                VehiclePersistenceSystem.Instance.RemoveVehicleFromGarage(selectedVehicle.id);
            });
        }
        
        private void OnRemoveClicked()
        {
            if (selectedVehicle == null) return;
            
            ShowConfirmation($"Permanently remove {selectedVehicle.DisplayName} from garage?\nThis cannot be undone!", () => 
            {
                VehiclePersistenceSystem.Instance.RemoveVehicleFromGarage(selectedVehicle.id);
            });
        }
        
        private int CalculateSellPrice(SavedVehicle vehicle)
        {
            var def = VehicleDatabase.Instance?.Get(vehicle.vehicleModel);
            if (def == null) return 1000;
            
            float basePrice = def.basePrice;
            float conditionMult = vehicle.HealthPercentage;
            float mileageMult = Mathf.Max(0.1f, 1f - vehicle.mileage / 500000f);
            
            return Mathf.RoundToInt(basePrice * conditionMult * mileageMult * 0.5f); // 50% of value
        }
        
        private void ShowConfirmation(string message, System.Action onConfirm)
        {
            confirmText.text = message;
            confirmPanel.SetActive(true);
            pendingConfirmCallback = () => 
            {
                onConfirm?.Invoke();
                RefreshList();
                detailPanel.SetActive(false);
            };
        }
        
        private void ConfirmPending()
        {
            pendingConfirmCallback?.Invoke();
            pendingConfirmCallback = null;
            confirmPanel.SetActive(false);
        }
        
        private void CancelPending()
        {
            pendingConfirmCallback = null;
            confirmPanel.SetActive(false);
        }
        
        private void AddMoney(int amount)
        {
            // Add to player money
        }
        
        // ============================================================================
        // VEHICLE ITEM
        // ============================================================================
        
        [System.Serializable]
        public class GarageVehicleItem : MonoBehaviour
        {
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI infoText;
            public Image conditionBar;
            public Image conditionBackground;
            public Image vehicleIcon;
            public Button selectButton;
            public GameObject ownedIndicator;
            
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
    }
}