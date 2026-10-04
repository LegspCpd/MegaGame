using UnityEngine;
using System.Collections.Generic;
using Megame.Common;
using Megame.Entity;
using Megame.Network;
using Megame.Gameplay;
using Grpc.Core;
using System.Threading.Tasks;
using UnityEngine.UI;
using TMPro;
using Grpc.Net.Client;
using Megame.Controllers;
using Megame.Data;
using Megame.Client;
using Megame.Vehicles;
using Megame.World;

namespace Megame.Client
{
    /// <summary>
    /// Main game client controller - connects to Go server via gRPC
    /// </summary>
    public class GameClient : MonoBehaviour
    {
        [Header("Connection")]
        public string serverAddress = "localhost:50051";
        public bool autoConnect = true;

        [Header("Prefabs")]
        public GameObject playerPrefab;
        public GameObject vehiclePrefab;
        public GameObject weaponPrefab;
        public GameObject npcPrefab;

        [Header("Systems")]
        public CameraController cameraController;
        public InputManager inputManager;
        public UIManager uiManager;

        private Channel _channel;
        private GameService.GameServiceClient _client;
        private AsyncDuplexStreamingCall<ClientMessage, ServerMessage> _stream;
        private Dictionary<ulong, GameObject> _entities = new Dictionary<ulong, GameObject>();
        private ulong _localPlayerId = 0;
        private bool _connected = false;
        private uint _serverTick = 0;

        // Systems
        private EntityManager _entityManager;
        private PlayerController _playerController;
        private VehicleController _vehicleController;
        private WeaponController _weaponController;
        private MissionManager _missionManager;
        private DialogueManager _dialogueManager;
        private CutsceneManager _cutsceneManager;
        private PhoneManager _phoneManager;
        private WorldManager _worldManager;

        public static GameClient Instance { get; private set; }
        public bool IsConnected => _connected;
        public ulong LocalPlayerId => _localPlayerId;
        public uint ServerTick => _serverTick;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeSystems();
        }

        private void InitializeSystems()
        {
            _entityManager = new EntityManager(this);
            _playerController = new PlayerController(this);
            _vehicleController = new VehicleController(this);
            _weaponController = new WeaponController(this);
            _missionManager = new MissionManager(this);
            _dialogueManager = new DialogueManager(this);
            _cutsceneManager = new CutsceneManager(this);
            _phoneManager = new PhoneManager(this);
            _worldManager = new WorldManager(this);
        }

        private async void Start()
        {
            if (autoConnect)
            {
                await ConnectAsync();
            }
        }

        public async Task ConnectAsync()
        {
            try
            {
                _channel = new Channel(serverAddress, ChannelCredentials.Insecure);
                _client = new GameService.GameServiceClient(_channel);

                _stream = _client.GameStream();

                // Start receiving messages
                _ = ReceiveLoopAsync();

                // Send initial spawn request
                await SendRPCAsync("SpawnPlayer", new SpawnPlayerRequest());

                _connected = true;
                Debug.Log("Connected to server");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Connection failed: {e.Message}");
            }
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                while (await _stream.ResponseStream.MoveNext())
                {
                    var message = _stream.ResponseStream.Current;
                    HandleServerMessage(message);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Receive loop error: {e.Message}");
                _connected = false;
            }
        }

        private void HandleServerMessage(ServerMessage message)
        {
            switch (message.PayloadCase)
            {
                case ServerMessage.PayloadOneofCase.Snapshot:
                    HandleSnapshot(message.Snapshot);
                    break;
                case ServerMessage.PayloadOneofCase.Rpc:
                    HandleRPCResponse(message.Rpc);
                    break;
            }
        }

        private void HandleSnapshot(NativeServerSnapshot snapshot)
        {
            _serverTick = snapshot.Tick;

            // Update entities
            foreach (var entityState in snapshot.Entities)
            {
                _entityManager.UpdateEntity(entityState);
            }

            // Handle destroyed entities
            foreach (var id in snapshot.DestroyedEntities)
            {
                _entityManager.DestroyEntity(id);
            }

            // Update player-specific data
            foreach (var update in snapshot.PlayerUpdates)
            {
                _playerController.UpdatePlayerState(update);
            }

            // Update vehicle data
            foreach (var update in snapshot.VehicleUpdates)
            {
                _vehicleController.UpdateVehicleState(update);
            }

            // Update weapon data
            foreach (var update in snapshot.WeaponUpdates)
            {
                _weaponController.UpdateWeaponState(update);
            }

            // Update NPC data
            foreach (var update in snapshot.NpcUpdates)
            {
                // Handle NPC updates
            }

            // Update world state
            _worldManager.UpdateWorldState(snapshot.World);
        }

        private void HandleRPCResponse(RPCResponse response)
        {
            // Handle RPC responses
            Debug.Log($"RPC Response: {response.RequestId} - Success: {response.Success}");
        }

        public async Task SendInputAsync(ClientInput input)
        {
            if (!_connected || _stream == null) return;

            var message = new ClientMessage
            {
                Input = input
            };

            await _stream.RequestStream.WriteAsync(message);
        }

        public async Task<RPCResponse> SendRPCAsync(string method, object payload)
        {
            var requestId = System.Guid.NewGuid().ToString();
            var bytes = SerializePayload(payload);

            var request = new RPCRequest
            {
                RequestId = requestId,
                Method = method,
                Payload = Google.Protobuf.ByteString.CopyFrom(bytes)
            };

            var message = new ClientMessage
            {
                Rpc = request
            };

            await _stream.RequestStream.WriteAsync(message);

            // Wait for response (simplified - would use a proper request/response map)
            return new RPCResponse { RequestId = requestId, Success = true };
        }

        private byte[] SerializePayload(object payload)
        {
            // Serialize using protobuf
            return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload);
        }

        public T GetEntity<T>(ulong entityId) where T : Component
        {
            if (_entities.TryGetValue(entityId, out var go))
            {
                return go.GetComponent<T>();
            }
            return null;
        }

        public GameObject GetOrCreateEntity(ulong entityId, EntityType type)
        {
            if (_entities.TryGetValue(entityId, out var existing))
            {
                return existing;
            }

            GameObject prefab = GetPrefabForType(type);
            var go = Instantiate(prefab);
            go.name = $"{type}_{entityId}";
            _entities[entityId] = go;

            return go;
        }

        private GameObject GetPrefabForType(EntityType type)
        {
            switch (type)
            {
                case EntityType.Player: return playerPrefab;
                case EntityType.Vehicle: return vehiclePrefab;
                case EntityType.Weapon: return weaponPrefab;
                case EntityType.Npc: return npcPrefab;
                default: return new GameObject();
            }
        }

        private void OnDestroy()
        {
            _stream?.RequestStream.CompleteAsync();
            _channel?.ShutdownAsync().Wait();
        }
    }
}
