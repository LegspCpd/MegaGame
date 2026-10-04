using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using Megame.Data;
using Megame.Vehicles;
using Megame.Systems;
using Megame.World;
using Megame.Client;
using Megame.UI;

namespace Megame.Core
{
    /// <summary>
    /// Main game manager - initializes all systems and manages game state
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        
        [Header("Core Systems")]
        public GameClient gameClient;
        public VehicleDatabase vehicleDatabase;
        public VehiclePersistenceSystem vehiclePersistence;
        public TrafficFlowSystem trafficSystem;
        public RegionManager regionManager;
        public POIManager poiManager;
        public WorldManager worldManager;
        public DLSLightingSystem dlsSystem;
        public MissionManager missionManager;
        public DialogueManager dialogueManager;
        public PhoneManager phoneManager;
        public CutsceneManager cutsceneManager;
        public ModShopUI modShopUI;
        public GarageUI garageUI;
        public UIManager uiManager;
        
        [Header("Game State")]
        public GameState currentState = GameState.Loading;
        public bool isMultiplayer = false;
        public string serverAddress = "localhost:50051";
        
        [Header("Player")]
        public GameObject playerPrefab;
        public Transform playerSpawnPoint;
        public CameraController cameraController;
        public InputManager inputManager;
        
        [Header("Settings")]
        public bool autoConnect = true;
        public bool debugMode = false;
        
        // Events
        public System.Action<GameState> OnGameStateChanged;
        public System.Action OnPlayerSpawned;
        public System.Action OnPlayerDied;
        
        private GameObject playerInstance;
        private bool isQuitting = false;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSystems();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void InitializeSystems()
        {
            // Find or create core systems
            FindOrCreateSystems();
            
            // Initialize in dependency order
            InitializeInOrder();
        }
        
        private void FindOrCreateSystems()
        {
            // GameClient
            if (gameClient == null) gameClient = FindObjectOfType<GameClient>();
            if (gameClient == null)
            {
                var go = new GameObject("GameClient");
                go.transform.SetParent(transform);
                gameClient = go.AddComponent<GameClient>();
            }
            
            // CameraController
            if (cameraController == null) cameraController = FindObjectOfType<CameraController>();
            if (cameraController == null)
            {
                var go = new GameObject("CameraController");
                go.transform.SetParent(transform);
                cameraController = go.AddComponent<CameraController>();
            }
            
            // InputManager
            if (inputManager == null) inputManager = FindObjectOfType<InputManager>();
            if (inputManager == null)
            {
                var go = new GameObject("InputManager");
                go.transform.SetParent(transform);
                inputManager = go.AddComponent<InputManager>();
            }
            
            // UIManager
            if (uiManager == null) uiManager = FindObjectOfType<UIManager>();
            if (uiManager == null)
            {
                var go = new GameObject("UIManager");
                go.transform.SetParent(transform);
                uiManager = go.AddComponent<UIManager>();
            }
            
            // VehicleDatabase
            if (vehicleDatabase == null) vehicleDatabase = Resources.Load<VehicleDatabase>("VehicleDatabase");
            
            // VehiclePersistence
            if (vehiclePersistence == null) vehiclePersistence = FindObjectOfType<VehiclePersistenceSystem>();
            if (vehiclePersistence == null)
            {
                var go = new GameObject("VehiclePersistence");
                go.transform.SetParent(transform);
                vehiclePersistence = go.AddComponent<VehiclePersistenceSystem>();
            }
            
            // TrafficFlowSystem
            if (trafficSystem == null) trafficSystem = FindObjectOfType<TrafficFlowSystem>();
            if (trafficSystem == null)
            {
                var go = new GameObject("TrafficSystem");
                go.transform.SetParent(transform);
                trafficSystem = go.AddComponent<TrafficFlowSystem>();
            }
            
            // WorldManager
            if (worldManager == null) worldManager = FindObjectOfType<WorldManager>();
            if (worldManager == null)
            {
                var go = new GameObject("WorldManager");
                go.transform.SetParent(transform);
                worldManager = go.AddComponent<WorldManager>();
            }
            
            // RegionManager
            if (regionManager == null) regionManager = FindObjectOfType<RegionManager>();
            if (regionManager == null)
            {
                var go = new GameObject("RegionManager");
                go.transform.SetParent(transform);
                regionManager = go.AddComponent<RegionManager>();
            }
            
            // POIManager
            if (poiManager == null) poiManager = FindObjectOfType<POIManager>();
            if (poiManager == null)
            {
                var go = new GameObject("POIManager");
                go.transform.SetParent(transform);
                poiManager = go.AddComponent<POIManager>();
            }
            
            // DLSLightingSystem
            if (dlsSystem == null) dlsSystem = FindObjectOfType<DLSLightingSystem>();
            if (dlsSystem == null)
            {
                var go = new GameObject("DLSLightingSystem");
                go.transform.SetParent(transform);
                dlsSystem = go.AddComponent<DLSLightingSystem>();
            }
            
            // MissionManager
            if (missionManager == null) missionManager = FindObjectOfType<MissionManager>();
            if (missionManager == null)
            {
                var go = new GameObject("MissionManager");
                go.transform.SetParent(transform);
                missionManager = go.AddComponent<MissionManager>();
            }
            
            // DialogueManager
            if (dialogueManager == null) dialogueManager = FindObjectOfType<DialogueManager>();
            if (dialogueManager == null)
            {
                var go = new GameObject("DialogueManager");
                go.transform.SetParent(transform);
                dialogueManager = go.AddComponent<DialogueManager>();
            }
            
            // PhoneManager
            if (phoneManager == null) phoneManager = FindObjectOfType<PhoneManager>();
            if (phoneManager == null)
            {
                var go = new GameObject("PhoneManager");
                go.transform.SetParent(transform);
                phoneManager = go.AddComponent<PhoneManager>();
            }
            
            // CutsceneManager
            if (cutsceneManager == null) cutsceneManager = FindObjectOfType<CutsceneManager>();
            if (cutsceneManager == null)
            {
                var go = new GameObject("CutsceneManager");
                go.transform.SetParent(transform);
                cutsceneManager = go.AddComponent<CutsceneManager>();
            }
            
            // ModShopUI
            if (modShopUI == null) modShopUI = FindObjectOfType<ModShopUI>();
            if (modShopUI == null)
            {
                var go = new GameObject("ModShopUI");
                go.transform.SetParent(transform);
                modShopUI = go.AddComponent<ModShopUI>();
            }
            
            // GarageUI
            if (garageUI == null) garageUI = FindObjectOfType<GarageUI>();
            if (garageUI == null)
            {
                var go = new GameObject("GarageUI");
                go.transform.SetParent(transform);
                garageUI = go.AddComponent<GarageUI>();
            }
        }
        
        private void InitializeInOrder()
        {
            // Core
            if (cameraController != null) cameraController.enabled = true;
            if (inputManager != null) inputManager.enabled = true;
            if (uiManager != null) uiManager.Initialize(this);
            
            // Persistence
            if (vehiclePersistence != null)
            {
                vehiclePersistence.LoadGarage();
            }
            
            // World
            if (worldManager != null) worldManager.enabled = true;
            if (regionManager != null) regionManager.enabled = true;
            if (poiManager != null) poiManager.enabled = true;
            
            // DLS
            if (dlsSystem != null) dlsSystem.enabled = true;
            
            // Traffic
            if (trafficSystem != null) trafficSystem.enabled = true;
            
            // Mission/Story
            if (missionManager != null) missionManager.enabled = true;
            if (dialogueManager != null) dialogueManager.enabled = true;
            if (phoneManager != null) phoneManager.enabled = true;
            if (cutsceneManager != null) cutsceneManager.enabled = true;
            
            // UI Systems
            if (modShopUI != null) modShopUI.enabled = true;
            if (garageUI != null) garageUI.enabled = true;
            
            // GameClient (connects last)
            if (gameClient != null && autoConnect)
            {
                StartCoroutine(ConnectToServer());
            }
        }
        
        private IEnumerator ConnectToServer()
        {
            yield return new WaitForSeconds(1f); // Wait for systems to initialize
            
            SetGameState(GameState.Connecting);
            
            if (gameClient != null)
            {
                // gameClient.serverAddress = serverAddress;
                // await gameClient.ConnectAsync(); // Would be async in real implementation
                // For now, simulate connection
                yield return new WaitForSeconds(2f);
                
                if (gameClient.IsConnected)
                {
                    SetGameState(GameState.Connected);
                    SpawnPlayer();
                }
                else
                {
                    SetGameState(GameState.ConnectionFailed);
                    Debug.LogError("Failed to connect to server");
                }
            }
        }
        
        private void SpawnPlayer()
        {
            if (playerPrefab == null)
            {
                Debug.LogError("Player prefab not assigned!");
                SetGameState(GameState.Error);
                return;
            }
            
            Vector3 spawnPos = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.up * 2f;
            Quaternion spawnRot = playerSpawnPoint != null ? playerSpawnPoint.rotation : Quaternion.identity;
            
            playerInstance = Instantiate(playerPrefab, spawnPos, spawnRot);
            playerInstance.name = "LocalPlayer";
            
            // Setup camera
            if (cameraController != null)
            {
                cameraController.SetTarget(playerInstance.transform);
            }
            
            // Initialize input
            if (inputManager != null)
            {
                inputManager.Initialize(gameClient);
            }
            
            // Request spawn from server
            if (gameClient != null && gameClient.IsConnected)
            {
                // gameClient.SendRPCAsync("SpawnPlayer", new SpawnPlayerRequest());
            }
            
            SetGameState(GameState.Playing);
            OnPlayerSpawned?.Invoke();
        }
        
        public void SetGameState(GameState newState)
        {
            if (currentState == newState) return;
            
            GameState previousState = currentState;
            currentState = newState;
            
            Debug.Log($"Game State: {previousState} -> {newState}");
            OnGameStateChanged?.Invoke(newState);
            
            // Handle state transitions
            switch (newState)
            {
                case GameState.Playing:
                    Time.timeScale = 1f;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    break;
                    
                case GameState.Paused:
                    Time.timeScale = 0f;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;
                    
                case GameState.MainMenu:
                    Time.timeScale = 1f;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;
                    
                case GameState.Loading:
                    Time.timeScale = 1f;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;
            }
        }
        
        public void PauseGame()
        {
            if (currentState == GameState.Playing)
                SetGameState(GameState.Paused);
        }
        
        public void ResumeGame()
        {
            if (currentState == GameState.Paused)
                SetGameState(GameState.Playing);
        }
        
        public void ReturnToMainMenu()
        {
            // Cleanup
            if (playerInstance != null)
            {
                Destroy(playerInstance);
                playerInstance = null;
            }
            
            // Disconnect
            if (gameClient != null)
            {
                // gameClient.Disconnect();
            }
            
            // Load main menu scene
            SceneManager.LoadScene("MainMenu");
            SetGameState(GameState.MainMenu);
        }
        
        public void QuitGame()
        {
            isQuitting = true;
            
            // Save garage
            if (vehiclePersistence != null)
            {
                vehiclePersistence.SaveGarage();
            }
            
            // Disconnect
            if (gameClient != null)
            {
                // gameClient.Disconnect();
            }
            
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        
        private void Update()
        {
            // Handle pause
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (currentState == GameState.Playing)
                    PauseGame();
                else if (currentState == GameState.Paused)
                    ResumeGame();
            }
            
            // Debug keys
            if (debugMode)
            {
                if (Input.GetKeyDown(KeyCode.F1)) ReturnToMainMenu();
                if (Input.GetKeyDown(KeyCode.F2)) SpawnTestVehicle();
                if (Input.GetKeyDown(KeyCode.F3)) ToggleTimeScale();
                if (Input.GetKeyDown(KeyCode.F4)) TriggerRandomEvent();
            }
        }
        
        private void OnApplicationQuit()
        {
            if (!isQuitting)
            {
                QuitGame();
            }
        }
        
        // Debug helpers
        private void SpawnTestVehicle()
        {
            if (vehicleDatabase == null || vehicleDatabase.vehicles == null) return;
            
            var vehicles = vehicleDatabase.GetCivilianVehicles();
            if (vehicles.Length == 0) return;
            
            var def = vehicles[Random.Range(0, vehicles.Length)];
            if (def?.modelPrefab == null) return;
            
            Vector3 spawnPos = Camera.main.transform.position + Camera.main.transform.forward * 10f;
            var go = Instantiate(def.modelPrefab, spawnPos, Quaternion.identity);
            go.name = $"Debug_{def.modelName}";
            
            var controller = go.GetComponent<OptimizedVehicleController>();
            if (controller != null)
            {
                controller.Initialize(def);
                controller.SetDriver(true);
            }
        }
        
        private void ToggleTimeScale()
        {
            Time.timeScale = Time.timeScale == 1f ? 0.1f : 1f;
        }
        
        private void TriggerRandomEvent()
        {
            // trafficSystem.RequestPoliceResponse(Camera.main.transform.position, 2);
        }
        
        // Public API for other systems
        public void OnPlayerDeath()
        {
            OnPlayerDied?.Invoke();
            SetGameState(GameState.GameOver);
        }
        
        public void CompleteMission(string missionId)
        {
            missionManager?.CompleteMission(missionId);
        }
        
        public void StartCutscene(string cutsceneId)
        {
            StartCoroutine(cutsceneManager.PlayCutscene(cutsceneId));
        }
        
        public void OpenModShop(VehicleDefinitionData vehicle)
        {
            modShopUI?.OpenShop(vehicle, playerInstance);
        }
        
        public void OpenGarage()
        {
            garageUI?.OpenGarage();
        }
        
        public void ShowPhone()
        {
            uiManager?.phoneManager?.OpenPhone();
        }
    }
    
    // ============================================================================
    // GAME STATES
    // ============================================================================
    
    public enum GameState
    {
        Loading,
        Connecting,
        Connected,
        ConnectionFailed,
        MainMenu,
        Playing,
        Paused,
        GameOver,
        Cutscene,
        Error,
    }
}