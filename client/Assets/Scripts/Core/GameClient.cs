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

        // Client-side system accessors shared with UI and input layers.
        public DialogueManager dialogueManager => _dialogueManager;
        public PhoneManager phoneManager => _phoneManager;
        public MissionManager missionManager => _missionManager;
        public CutsceneManager cutsceneManager => _cutsceneManager;
        public WeaponController weaponController => _weaponController;

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
            if (!autoConnect) return;

            // The server may not be up yet, or a transient failure (server
            // restart, Wi-Fi blip) would otherwise leave us permanently offline.
            while (!_connected && enabled)
            {
                await ConnectAsync();
                if (_connected) break;
                await System.Threading.Tasks.Task.Delay(3000);
            }
        }

        public async Task<bool> ConnectAsync()
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
                return true;
            }
            catch (System.Exception e)
            {
                _connected = false;
                Debug.LogWarning($"Connection failed ({e.GetType().Name}), retrying in 3s: {e.Message}");
                SafeCloseChannel();
                return false;
            }
        }

        private void SafeCloseChannel()
        {
            try { _stream?.RequestStream.CompleteAsync(); }
            catch (System.Exception) { }
            try { _channel?.ShutdownAsync().Wait(200); }
            catch (System.Exception) { }
            _stream = null;
            _channel = null;
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

        private void HandleSnapshot(ServerSnapshot snapshot)
        {
            _serverTick = (uint)snapshot.Tick;

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
            return System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(payload));
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
            if (prefab == null)
            {
                // No prefab was authored for this entity type (the project ships
                // none). Instantiating null would throw inside the snapshot
                // handler, so fall back to a plain placeholder.
                prefab = CreatePlaceholder(type);
            }
            var go = Instantiate(prefab);
            go.name = $"{type}_{entityId}";
            _entities[entityId] = go;

            return go;
        }

        private static GameObject CreatePlaceholder(EntityType type)
        {
            PrimitiveType shape;
            switch (type)
            {
                case EntityType.Vehicle: shape = PrimitiveType.Cube; break;
                case EntityType.Weapon: shape = PrimitiveType.Cube; break;
                case EntityType.Npc: shape = PrimitiveType.Capsule; break;
                default: shape = PrimitiveType.Capsule; break;
            }

            var go = GameObject.CreatePrimitive(shape);
            go.name = $"placeholder_{type}";

            float scale = type == EntityType.Vehicle ? 1.8f : 0.8f;
            go.transform.localScale = new Vector3(scale, shape == PrimitiveType.Capsule ? scale : scale * 0.5f, scale);

            go.GetComponent<MeshRenderer>().sharedMaterial =
                PlayableWorld.NewMaterial(PlaceholderColor(type));

            // Placeholder prims ship with a collider; network entities should
            // not block the local player.
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            return go;
        }

        // Megame.Common also declares a proto Color; bind this one explicitly.
        private static UnityEngine.Color PlaceholderColor(EntityType type)
        {
            switch (type)
            {
                case EntityType.Player: return new UnityEngine.Color(0.95f, 0.75f, 0.30f);
                case EntityType.Vehicle: return new UnityEngine.Color(0.85f, 0.30f, 0.30f);
                case EntityType.Weapon: return new UnityEngine.Color(0.35f, 0.35f, 0.40f);
                default: return new UnityEngine.Color(0.55f, 0.55f, 0.60f);
            }
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
            _connected = false;
            // Bounded wait: a dead server would otherwise hang OnDestroy and
            // freeze the player on quit.
            SafeCloseChannel();
        }
    }
}
