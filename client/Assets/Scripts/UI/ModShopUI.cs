using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Megame.Data;
using Megame.Vehicles;

// Megame.Data and Megame.Vehicles both define ModOption; VehicleModKit lives
// in Megame.Vehicles, so bind the name to that one.
using ModOption = Megame.Vehicles.ModOption;
using Megame.Controllers;
using Megame.Client;

namespace Megame.UI
{
    /// <summary>
    /// Mod Shop / Los Santos Customs UI System
    /// Handles vehicle customization: performance, visual, wheels, paint
    /// </summary>
    public class ModShopUI : MonoBehaviour
    {
        public static ModShopUI Instance { get; private set; }
        
        [Header("UI References")]
        public GameObject shopRoot;
        public Transform categoryContainer;
        public GameObject categoryButtonPrefab;
        public Transform modListContainer;
        public GameObject modItemPrefab;
        public Transform modDetailContainer;
        
        [Header("Vehicle Preview")]
        public Camera previewCamera;
        public Transform previewPosition;
        public float previewRotationSpeed = 20f;
        
        [Header("Info Panels")]
        public TextMeshProUGUI shopNameText;
        public TextMeshProUGUI vehicleNameText;
        public TextMeshProUGUI playerMoneyText;
        public TextMeshProUGUI totalCostText;
        
        [Header("Category Tabs")]
        public ModCategoryTab[] categoryTabs;
        
        [Header("Color Picker")]
        public GameObject colorPickerPanel;
        public RawImage colorPreviewImage;
        public Slider colorHueSlider;
        public Slider colorSaturationSlider;
        public Slider colorValueSlider;
        public Button confirmColorButton;
        public Button cancelColorButton;
        
        [Header("Paint Type Selector")]
        public GameObject paintTypePanel;
        public Transform paintTypeContainer;
        public GameObject paintTypeButtonPrefab;
        
        [Header("Confirmation")]
        public GameObject confirmPurchasePanel;
        public TextMeshProUGUI confirmText;
        public Button confirmBuyButton;
        public Button cancelBuyButton;
        
        // State
        private VehicleDefinitionData currentVehicleDef;
        private GameObject currentPreviewVehicle;
        private OptimizedVehicleController previewController;
        private VehicleModificationsData pendingMods = new VehicleModificationsData();
        private VehicleModificationsData originalMods = new VehicleModificationsData();
        private ModCategoryTab currentCategory;
        private int pendingTotalCost = 0;
        private System.Action<Color> onColorSelected;
        private System.Action<int> onPaintTypeSelected;
        private ModShopConfirmCallback pendingConfirmCallback;
        
        // Categories
        public enum ShopCategory
        {
            Engine,
            Transmission,
            Suspension,
            Brakes,
            Turbo,
            Body,
            Wheels,
            Paint,
            Interior,
            Lighting,
            Plates,
            Emergency, // For police vehicles
        }
        
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
            
            shopRoot.SetActive(false);
            
            // Setup button listeners
            confirmColorButton.onClick.AddListener(ConfirmColor);
            cancelColorButton.onClick.AddListener(CancelColor);
            confirmBuyButton.onClick.AddListener(ConfirmPurchase);
            cancelBuyButton.onClick.AddListener(CancelPurchase);
            
            // Color picker sliders
            colorHueSlider.onValueChanged.AddListener(OnColorHSVChanged);
            colorSaturationSlider.onValueChanged.AddListener(OnColorHSVChanged);
            colorValueSlider.onValueChanged.AddListener(OnColorHSVChanged);
        }
        
        public void OpenShop(VehicleDefinitionData vehicleDef, GameObject playerVehicle)
        {
            currentVehicleDef = vehicleDef;
            
            // Store original mods
            if (playerVehicle != null)
            {
                var controller = playerVehicle.GetComponent<OptimizedVehicleController>();
                if (controller != null)
                {
                    originalMods = controller.currentMods.Clone();
                    pendingMods = originalMods.Clone();
                }
            }
            
            // Setup preview
            SetupPreview(vehicleDef);
            
            // Update UI
            shopNameText.text = "LOS SANTOS CUSTOMS";
            vehicleNameText.text = vehicleDef.displayName;
            UpdateMoneyDisplay();
            UpdateTotalCost();
            
            // Select first category
            SelectCategory(ShopCategory.Engine);
            
            shopRoot.SetActive(true);
            Time.timeScale = 0f; // Pause game
        }
        
        public void CloseShop()
        {
            // Restore original mods on preview
            if (previewController != null)
            {
                previewController.ApplyMods(originalMods);
            }
            
            // Destroy preview
            if (currentPreviewVehicle != null)
            {
                Destroy(currentPreviewVehicle);
            }
            
            shopRoot.SetActive(false);
            Time.timeScale = 1f;
            currentVehicleDef = null;
        }
        
        private void SetupPreview(VehicleDefinitionData def)
        {
            if (currentPreviewVehicle != null)
            {
                Destroy(currentPreviewVehicle);
            }
            
            currentPreviewVehicle = Instantiate(def.modelPrefab, previewPosition.position, previewPosition.rotation);
            currentPreviewVehicle.name = "ModShopPreview";
            
            // Disable physics
            var rb = currentPreviewVehicle.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            var cols = currentPreviewVehicle.GetComponentsInChildren<Collider>();
            foreach (var c in cols) c.enabled = false;
            
            // Disable scripts
            var scripts = currentPreviewVehicle.GetComponentsInChildren<MonoBehaviour>();
            foreach (var s in scripts) s.enabled = false;
            
            previewController = currentPreviewVehicle.AddComponent<OptimizedVehicleController>();
            previewController.Initialize(def);
            previewController.ApplyMods(pendingMods);
            
            // Setup preview camera
            if (previewCamera != null)
            {
                previewCamera.transform.position = previewPosition.position + new Vector3(3, 1.5f, -5);
                previewCamera.transform.LookAt(previewPosition.position + Vector3.up);
            }
        }
        
        private void Update()
        {
            if (!shopRoot.activeSelf) return;
            
            // Rotate preview
            if (currentPreviewVehicle != null)
            {
                currentPreviewVehicle.transform.Rotate(Vector3.up, previewRotationSpeed * Time.unscaledDeltaTime);
            }
            
            // Handle input
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (confirmPurchasePanel.activeSelf)
                {
                    CancelPurchase();
                }
                else if (colorPickerPanel.activeSelf)
                {
                    CancelColor();
                }
                else if (paintTypePanel.activeSelf)
                {
                    CancelPaintType();
                }
                else
                {
                    CloseShop();
                }
            }
        }
        
        public void SelectCategory(ShopCategory category)
        {
            currentCategory = System.Array.Find(categoryTabs, t => t.category == category);
            if (currentCategory == null) return;
            
            // Update tab visuals
            foreach (var tab in categoryTabs)
            {
                tab.SetActive(tab.category == category);
            }
            
            // Populate mod list
            PopulateModList(category);
        }
        
        private void PopulateModList(ShopCategory category)
        {
            // Clear existing
            foreach (Transform child in modListContainer)
            {
                Destroy(child.gameObject);
            }
            
            var modKit = currentVehicleDef.modKitId > 0 ? 
                Resources.Load<VehicleModKit>($"ModKits/ModKit_{currentVehicleDef.modKitId}") : null;
            
            if (modKit == null) return;
            
            List<ModOption> options = GetOptionsForCategory(modKit, category);
            
            foreach (var option in options)
            {
                var item = Instantiate(modItemPrefab, modListContainer);
                var itemScript = item.GetComponent<ModShopItem>();
                if (itemScript != null)
                {
                    itemScript.Setup(option, this, category);
                }
            }
        }
        
        private List<ModOption> GetOptionsForCategory(VehicleModKit kit, ShopCategory category)
        {
            switch (category)
            {
                case ShopCategory.Engine: return kit.engineOptions?.ToList() ?? new List<ModOption>();
                case ShopCategory.Transmission: return kit.transmissionOptions?.ToList() ?? new List<ModOption>();
                case ShopCategory.Suspension: return kit.suspensionOptions?.ToList() ?? new List<ModOption>();
                case ShopCategory.Brakes: return kit.brakeOptions?.ToList() ?? new List<ModOption>();
                case ShopCategory.Turbo: return kit.turboOptions?.ToList() ?? new List<ModOption>();
                case ShopCategory.Body: 
                    var body = new List<ModOption>();
                    if (kit.bodyKitOptions != null) body.AddRange(kit.bodyKitOptions);
                    if (kit.spoilerOptions != null) body.AddRange(kit.spoilerOptions);
                    if (kit.hoodOptions != null) body.AddRange(kit.hoodOptions);
                    if (kit.exhaustOptions != null) body.AddRange(kit.exhaustOptions);
                    if (kit.grilleOptions != null) body.AddRange(kit.grilleOptions);
                    if (kit.skirtOptions != null) body.AddRange(kit.skirtOptions);
                    if (kit.fenderOptions != null) body.AddRange(kit.fenderOptions);
                    if (kit.roofOptions != null) body.AddRange(kit.roofOptions);
                    return body;
                case ShopCategory.Wheels: return kit.wheelTypeOptions?.ToList() ?? new List<ModOption>();
                case ShopCategory.Paint: return kit.paintTypes?.Select(p => new ModOption 
                    { name = p.name, modValue = p.typeId, priceMultiplier = p.priceMultiplier }).ToList() 
                    ?? new List<ModOption>();
                case ShopCategory.Interior: return new List<ModOption>(); // TODO
                case ShopCategory.Lighting: 
                    var lights = new List<ModOption>();
                    if (kit.xenonOptions != null) lights.AddRange(kit.xenonOptions);
                    if (kit.neonOptions != null) lights.AddRange(kit.neonOptions);
                    return lights;
                case ShopCategory.Plates: return kit.plateStyleOptions?.ToList() ?? new List<ModOption>();
                case ShopCategory.Emergency: return new List<ModOption>(); // TODO
                default: return new List<ModOption>();
            }
        }
        
        public void OnModSelected(ModOption option, ShopCategory category)
        {
            // Apply mod to preview
            ApplyModToPreview(option, category);
            
            // Update cost
            int cost = CalculateModCost(option, category);
            pendingTotalCost += cost - GetCurrentModCost(category);
            UpdateTotalCost();
            
            // Update mod detail panel
            ShowModDetail(option, category);
        }
        
        private void ApplyModToPreview(ModOption option, ShopCategory category)
        {
            if (previewController == null) return;
            
            // Apply to pending mods
            switch (category)
            {
                case ShopCategory.Engine: pendingMods.engineLevel = option.modValue; break;
                case ShopCategory.Transmission: pendingMods.transmissionLevel = option.modValue; break;
                case ShopCategory.Suspension: 
                    pendingMods.suspensionLevel = option.modValue;
                    if (option.name.Contains("Lower")) pendingMods.suspensionHeight = -0.5f;
                    else if (option.name.Contains("Raise")) pendingMods.suspensionHeight = 0.5f;
                    else pendingMods.suspensionHeight = 0f;
                    break;
                case ShopCategory.Brakes: pendingMods.brakesLevel = option.modValue; break;
                case ShopCategory.Turbo: pendingMods.turboLevel = option.modValue; break;
                case ShopCategory.Body:
                    // Determine which body mod
                    if (option.name.Contains("Spoiler")) pendingMods.spoiler = option.modValue;
                    else if (option.name.Contains("Body Kit")) pendingMods.bodyKit = option.modValue;
                    else if (option.name.Contains("Hood")) pendingMods.hood = option.modValue;
                    else if (option.name.Contains("Exhaust")) pendingMods.exhaust = option.modValue;
                    else if (option.name.Contains("Grille")) pendingMods.grille = option.modValue;
                    else if (option.name.Contains("Skirt")) pendingMods.skirt = option.modValue;
                    else if (option.name.Contains("Fender")) pendingMods.fender = option.modValue;
                    else if (option.name.Contains("Roof")) pendingMods.roof = option.modValue;
                    break;
                case ShopCategory.Wheels:
                    pendingMods.wheelType = option.modValue;
                    break;
                case ShopCategory.Paint:
                    pendingMods.paintType = option.modValue;
                    OpenPaintTypeSelector();
                    break;
                case ShopCategory.Lighting:
                    if (option.name.Contains("Xenon")) pendingMods.xenonLights = option.modValue > 0;
                    else if (option.name.Contains("Neon")) pendingMods.neonEnabled = option.modValue > 0;
                    break;
                case ShopCategory.Plates:
                    pendingMods.plateStyle = option.modValue;
                    break;
            }
            
            // Apply to preview controller
            previewController.ApplyMods(pendingMods);
        }
        
        private int GetCurrentModCost(ShopCategory category)
        {
            // Get cost of currently equipped mod in this category
            return 0; // Simplified
        }
        
        private int CalculateModCost(ModOption option, ShopCategory category)
        {
            int baseCost = GetBaseCostForCategory(category);
            return Mathf.RoundToInt(baseCost * option.priceMultiplier);
        }
        
        private int GetBaseCostForCategory(ShopCategory category)
        {
            switch (category)
            {
                case ShopCategory.Engine: return 5000;
                case ShopCategory.Transmission: return 3000;
                case ShopCategory.Suspension: return 2000;
                case ShopCategory.Brakes: return 1500;
                case ShopCategory.Turbo: return 10000;
                case ShopCategory.Body: return 3000;
                case ShopCategory.Wheels: return 2000;
                case ShopCategory.Paint: return 1000;
                case ShopCategory.Lighting: return 500;
                case ShopCategory.Plates: return 100;
                default: return 1000;
            }
        }
        
        private void ShowModDetail(ModOption option, ShopCategory category)
        {
            // Clear
            foreach (Transform child in modDetailContainer)
            {
                Destroy(child.gameObject);
            }
            
            // Create detail view
            var detail = new GameObject("ModDetail", typeof(RectTransform));
            detail.transform.SetParent(modDetailContainer, false);
            
            var text = detail.AddComponent<TextMeshProUGUI>();
            text.text = $"{option.name}\nCost: ${CalculateModCost(option, category):N0}\n\n{GetModDescription(category, option.modValue)}";
            text.fontSize = 16;
            text.alignment = TextAlignmentOptions.TopLeft;
        }
        
        private string GetModDescription(ShopCategory category, int level)
        {
            if (level == 0) return "Stock / Removed";
            
            return category switch
            {
                ShopCategory.Engine => $"Level {level}: +{level * 15}% horsepower",
                ShopCategory.Transmission => $"Level {level}: {level * 10}% faster shifts",
                ShopCategory.Suspension => $"Level {level}: {level * 20}% stiffer, adjustable height",
                ShopCategory.Brakes => $"Level {level}: +{level * 15}% stopping power",
                ShopCategory.Turbo => level > 0 ? "Turbocharged: +30% torque" : "Removed",
                ShopCategory.Body => $"Visual modification level {level}",
                ShopCategory.Wheels => $"Wheel type {level} with variant options",
                ShopCategory.Paint => $"Paint type {level}",
                ShopCategory.Lighting => level > 0 ? "Enabled" : "Disabled",
                ShopCategory.Plates => $"Plate style {level}",
                _ => $"Level {level}",
            };
        }
        
        private void UpdateTotalCost()
        {
            totalCostText.text = $"TOTAL: ${pendingTotalCost:N0}";
            totalCostText.color = pendingTotalCost > GetPlayerMoney() ? Color.red : Color.green;
        }
        
        private void UpdateMoneyDisplay()
        {
            playerMoneyText.text = $"${GetPlayerMoney():N0}";
        }
        
        private int GetPlayerMoney()
        {
            // Get from player controller
            return 50000; // Placeholder
        }
        
        // Color Picker
        public void OpenColorPicker(Color currentColor, System.Action<Color> callback, string title = "Select Color")
        {
            Color.RGBToHSV(currentColor, out float h, out float s, out float v);
            colorHueSlider.value = h;
            colorSaturationSlider.value = s;
            colorValueSlider.value = v;
            colorPreviewImage.color = currentColor;
            
            onColorSelected = callback;
            colorPickerPanel.SetActive(true);
        }
        
        private void OnColorHSVChanged(float value)
        {
            Color newColor = Color.HSVToRGB(colorHueSlider.value, colorSaturationSlider.value, colorValueSlider.value);
            colorPreviewImage.color = newColor;
        }
        
        private void ConfirmColor()
        {
            Color selected = colorPreviewImage.color;
            onColorSelected?.Invoke(selected);
            colorPickerPanel.SetActive(false);
            onColorSelected = null;
        }
        
        private void CancelColor()
        {
            colorPickerPanel.SetActive(false);
            onColorSelected = null;
        }
        
        // Paint Type Selector
        private void OpenPaintTypeSelector()
        {
            paintTypePanel.SetActive(true);
            
            // Clear
            foreach (Transform child in paintTypeContainer)
            {
                Destroy(child.gameObject);
            }
            
            string[] paintTypes = { "Normal", "Metallic", "Pearlescent", "Matte", "Metal", "Chrome" };
            
            for (int i = 0; i < paintTypes.Length; i++)
            {
                var btn = Instantiate(paintTypeButtonPrefab, paintTypeContainer);
                btn.GetComponentInChildren<TextMeshProUGUI>().text = paintTypes[i];
                int index = i;
                btn.GetComponent<Button>().onClick.AddListener(() => SelectPaintType(index));
            }
        }
        
        private void SelectPaintType(int typeId)
        {
            pendingMods.paintType = typeId;
            previewController.ApplyMods(pendingMods);
            onPaintTypeSelected?.Invoke(typeId);
            paintTypePanel.SetActive(false);
        }
        
        private void CancelPaintType()
        {
            paintTypePanel.SetActive(false);
        }
        
        // Purchase Confirmation
        public void RequestPurchase(ModShopConfirmCallback callback)
        {
            if (pendingTotalCost <= 0)
            {
                callback(true);
                return;
            }
            
            if (pendingTotalCost > GetPlayerMoney())
            {
                confirmText.text = $"Insufficient funds!\nNeed: ${pendingTotalCost:N0}\nHave: ${GetPlayerMoney():N0}";
                confirmBuyButton.interactable = false;
            }
            else
            {
                confirmText.text = $"Purchase mods for ${pendingTotalCost:N0}?\nRemaining: ${GetPlayerMoney() - pendingTotalCost:N0}";
                confirmBuyButton.interactable = true;
            }
            
            pendingConfirmCallback = callback;
            confirmPurchasePanel.SetActive(true);
        }
        
        private void ConfirmPurchase()
        {
            // Deduct money
            DeductMoney(pendingTotalCost);
            
            // Apply mods to actual vehicle
            ApplyModsToPlayerVehicle();
            
            // Reset pending
            pendingTotalCost = 0;
            originalMods = pendingMods.Clone();
            
            confirmPurchasePanel.SetActive(false);
            pendingConfirmCallback?.Invoke(true);
            pendingConfirmCallback = null;
            
            UpdateTotalCost();
            UpdateMoneyDisplay();
        }
        
        private void CancelPurchase()
        {
            // Revert preview to original
            previewController.ApplyMods(originalMods);
            pendingMods = originalMods.Clone();
            pendingTotalCost = 0;
            UpdateTotalCost();
            
            confirmPurchasePanel.SetActive(false);
            pendingConfirmCallback?.Invoke(false);
            pendingConfirmCallback = null;
        }
        
        private void ApplyModsToPlayerVehicle()
        {
            // Apply to player's actual vehicle
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var controller = player.GetComponentInChildren<OptimizedVehicleController>();
                if (controller != null)
                {
                    controller.ApplyMods(pendingMods);
                }
            }
        }
        
        private void DeductMoney(int amount)
        {
            // Deduct from player
        }
    }
        
    
    // ============================================================================
    // UI Components
    // ============================================================================
    
    [System.Serializable]
    public class ModCategoryTab
    {
        public ModShopUI.ShopCategory category;
        public GameObject tabObject;
        public TextMeshProUGUI tabText;
        public Image tabIcon;
        public Color activeColor = Color.white;
        public Color inactiveColor = Color.gray;
        
        public void SetActive(bool active)
        {
            if (tabObject != null) tabObject.SetActive(active);
            if (tabText != null) tabText.color = active ? activeColor : inactiveColor;
            if (tabIcon != null) tabIcon.color = active ? activeColor : inactiveColor;
        }
    }
    
    public class ModShopItem : MonoBehaviour
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI priceText;
        public Image iconImage;
        public Button selectButton;
        public GameObject ownedIndicator;
        public GameObject installedIndicator;
        
        private ModOption option;
        private ModShopUI shop;
        private ModShopUI.ShopCategory category;
        
        public void Setup(ModOption opt, ModShopUI shopUI, ModShopUI.ShopCategory cat)
        {
            option = opt;
            shop = shopUI;
            category = cat;
            
            nameText.text = opt.name;
            int cost = shop.CalculateModCost(opt, cat);
            priceText.text = opt.modValue == 0 ? "REMOVE" : $"${cost:N0}";
            
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => shop.OnModSelected(option, category));
            
            // Check if owned/installed
            bool isInstalled = false; // Check against pendingMods
            bool isOwned = false; // Check player inventory
            
            if (installedIndicator != null) installedIndicator.SetActive(isInstalled);
            if (ownedIndicator != null) ownedIndicator.SetActive(isOwned && !isInstalled);
        }
    }
    
    public delegate void ModShopConfirmCallback(bool confirmed);
}
