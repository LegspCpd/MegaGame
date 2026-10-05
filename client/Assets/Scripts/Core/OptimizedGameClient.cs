using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using System.Collections.Generic;
using Megame.Common;
using Megame.Entity;
using Megame.Network;
using Grpc.Core;

// Megame.Common also defines Vector3/Quaternion. Here they mean the protobuf
// types (Unity.Mathematics uses float3/quaternion), so bind the names.
using Vector3 = Megame.Common.Vector3;
using Quaternion = Megame.Common.Quaternion;
using UnityEngine.UI;
using TMPro;
using Megame.Controllers;
using Megame.Data;
using Megame.Client;
using Megame.Vehicles;

namespace Megame.Client
{
    /// <summary>
    /// Optimized GameClient with Job System, Burst, and ECS-style architecture
    /// </summary>
    public class OptimizedGameClient : MonoBehaviour
    {
        [Header("Connection")]
        public string serverAddress = "localhost:50051";
        public bool autoConnect = true;

        [Header("Performance")]
        public int targetFrameRate = 60;
        public int interpolationBufferSize = 128;
        public float maxPredictionTime = 0.2f;
        public bool useJobSystem = true;
        public bool useBurstCompiler = true;

        [Header("Pools")]
        public int entityPoolSize = 1024;
        public int projectilePoolSize = 512;
        public int effectPoolSize = 256;

        // Core systems
        private Channel _channel;
        private GameService.GameServiceClient _grpcClient;
        private AsyncDuplexStreamingCall<ClientMessage, ServerMessage> _stream;

        // Entity management with pooling
        private EntityPool _entityPool;
        private NativeHashMap<ulong, NativeEntityView> _entityViews;
        // GameObjects are managed references and cannot live inside the Burst-compiled
        // NativeEntityView (BC1051), so they are tracked here on the main thread only.
        private readonly Dictionary<ulong, GameObject> _entityObjects = new Dictionary<ulong, GameObject>();
        private NativeQueue<EntityUpdate> _updateQueue;
        private JobHandle _updateJobHandle;

        // Prediction & Interpolation
        private NativeList<PredictedState> _predictedStates;
        private NativeList<NativeServerSnapshot> _snapshotHistory;
        private uint _localPlayerId;
        private uint _serverTick;
        private float _serverTime;
        private float _clientTime;

        // Input
        private InputBuffer _inputBuffer;

        // Pools
        private ObjectPool<Projectile> _projectilePool;
        private ObjectPool<ParticleEffect> _effectPool;

        // Metrics
        private ClientMetrics _metrics;

        public static OptimizedGameClient Instance { get; private set; }
        public bool IsConnected { get; private set; }
        public uint LocalPlayerId => _localPlayerId;
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

            Application.targetFrameRate = targetFrameRate;
            InitializeSystems();
        }

        private void InitializeSystems()
        {
            // Initialize native collections
            _entityViews = new NativeHashMap<ulong, NativeEntityView>(entityPoolSize, Allocator.Persistent);
            _updateQueue = new NativeQueue<EntityUpdate>(Allocator.Persistent);
            _predictedStates = new NativeList<PredictedState>(interpolationBufferSize, Allocator.Persistent);
            _snapshotHistory = new NativeList<NativeServerSnapshot>(interpolationBufferSize, Allocator.Persistent);

            _entityPool = new EntityPool(entityPoolSize);
            _projectilePool = new ObjectPool<Projectile>(projectilePoolSize, () => new Projectile());
            _effectPool = new ObjectPool<ParticleEffect>(effectPoolSize, () => new ParticleEffect());

            _inputBuffer = new InputBuffer(interpolationBufferSize);
            _metrics = new ClientMetrics();

            // Camera/input/UI managers belong to the GameClient architecture and
            // initialize through that path; the optimized client drives the
            // existing singletons directly instead.
        }

        private async void Start()
        {
            if (autoConnect)
                await ConnectAsync();
        }

        private async System.Threading.Tasks.Task ConnectAsync()
        {
            try
            {
                _channel = new Channel(serverAddress, ChannelCredentials.Insecure);
                _grpcClient = new GameService.GameServiceClient(_channel);
                _stream = _grpcClient.GameStream();

                _ = ReceiveLoopAsync();
                await SendRPCAsync("SpawnPlayer", new SpawnPlayerRequest());

                IsConnected = true;
                Debug.Log("[OptimizedGameClient] Connected to server");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[OptimizedGameClient] Connection failed: {e.Message}");
            }
        }

        private async System.Threading.Tasks.Task ReceiveLoopAsync()
        {
            try
            {
                while (await _stream.ResponseStream.MoveNext())
                {
                    var message = _stream.ResponseStream.Current;
                    ProcessServerMessage(message);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[OptimizedGameClient] Receive error: {e.Message}");
                IsConnected = false;
            }
        }

        private void ProcessServerMessage(ServerMessage message)
        {
            _metrics.MessagesReceived++;

            switch (message.PayloadCase)
            {
                case ServerMessage.PayloadOneofCase.Snapshot:
                    ProcessSnapshot(message.Snapshot);
                    break;
                case ServerMessage.PayloadOneofCase.Rpc:
                    HandleRPCResponse(message.Rpc);
                    break;
            }
        }

        private void HandleRPCResponse(RPCResponse response)
        {
            Debug.Log($"RPC Response: {response.RequestId} - Success: {response.Success}");
        }

        [BurstCompile]
        private struct ProcessSnapshotJob : IJob
        {
            [ReadOnly] public NativeArray<EntitySnapshot> snapshots;
            public NativeHashMap<ulong, NativeEntityView>.ParallelWriter entityViews;
            public NativeQueue<EntityUpdate>.ParallelWriter updateQueue;
            public uint localPlayerId;
            public float interpolationTime;

            public void Execute()
            {
                for (int i = 0; i < snapshots.Length; i++)
                {
                    var snap = snapshots[i];
                    if (snap.EntityId == localPlayerId) continue; // Local player predicted

                    var newView = new NativeEntityView
                    {
                        EntityId = snap.EntityId,
                        EntityType = snap.Type,
                        CurrentPosition = snap.Position,
                        CurrentRotation = snap.Rotation,
                        Velocity = snap.Velocity,
                        LastServerTick = snap.Tick
                    };

                    if (entityViews.TryAdd(snap.EntityId, newView))
                    {
                        // New entity view created
                    }
                    else
                    {
                        // Already exists: queue update for interpolation
                        updateQueue.Enqueue(new EntityUpdate
                        {
                            EntityId = snap.EntityId,
                            TargetPosition = snap.Position,
                            TargetRotation = snap.Rotation,
                            TargetVelocity = snap.Velocity,
                            ServerTick = snap.Tick,
                            InterpolationTime = interpolationTime
                        });
                    }
                }
            }
        }

        private void ProcessSnapshot(ServerSnapshot snapshot)
        {
            _serverTick = (uint)snapshot.Tick;
            _serverTime = snapshot.ServerTimeMs * 0.001f;

            // Convert to native array for job
            var nativeSnapshots = new NativeArray<EntitySnapshot>(snapshot.Entities.Count, Allocator.TempJob);
            for (int i = 0; i < snapshot.Entities.Count; i++)
            {
                var e = snapshot.Entities[i];
                nativeSnapshots[i] = new EntitySnapshot
                {
                    EntityId = e.Ref.Id,
                    Type = e.Ref.Type,
                    Position = e.Transform.Position.ToFloat3(),
                    Rotation = new quaternion(
                        e.Transform.Rotation.X,
                        e.Transform.Rotation.Y,
                        e.Transform.Rotation.Z,
                        e.Transform.Rotation.W),
                    Velocity = e.Velocity.ToFloat3(),
                    Tick = (uint)e.Timestamp
                };
            }

            // Schedule job
            var job = new ProcessSnapshotJob
            {
                snapshots = nativeSnapshots,
                entityViews = _entityViews.AsParallelWriter(),
                updateQueue = _updateQueue.AsParallelWriter(),
                localPlayerId = _localPlayerId,
                interpolationTime = 1f / targetFrameRate * 2f
            };

            _updateJobHandle = job.Schedule(_updateJobHandle);
            _updateJobHandle.Complete();

            nativeSnapshots.Dispose();

            // Process player/vehicle/weapon updates
            foreach (var update in snapshot.PlayerUpdates)
                PlayerController.Instance.UpdatePlayerState(update);
            foreach (var update in snapshot.VehicleUpdates)
                VehicleController.Instance.UpdateVehicleState(update);
            foreach (var update in snapshot.WeaponUpdates)
                WeaponController.Instance.UpdateWeaponState(update);
        }

        private void Update()
        {
            if (!IsConnected) return;

            _clientTime = Time.time;

            // 1. Gather input
            InputManager.Instance.GatherInput();

            // 2. Run prediction for local player
            if (useJobSystem)
                RunPredictionJob();
            else
                RunPredictionSimple();

            // 3. Send input to server
            SendInputToServer();

            // 4. Process interpolation updates
            ProcessInterpolation();

            // 5. Update visual entities
            UpdateEntityViews();

            // 6. Update metrics
            _metrics.FrameTime = Time.unscaledDeltaTime;
        }

        [BurstCompile]
        private struct PredictionJob : IJob
        {
            public float dt;
            public float maxPredictionTime;
            public NativeList<PredictedState> predictedStates;
            public NativeQueue<InputSample> inputBuffer;
            public uint serverTick;
            public float serverTime;

            public void Execute()
            {
                // Apply buffered inputs up to server tick
                // Implementation for client-side prediction
            }
        }

        private void RunPredictionJob()
        {
            var job = new PredictionJob
            {
                dt = Time.fixedDeltaTime,
                maxPredictionTime = maxPredictionTime,
                predictedStates = _predictedStates,
                inputBuffer = _inputBuffer.NativeQueue,
                serverTick = _serverTick,
                serverTime = _serverTime
            };
            _updateJobHandle = job.Schedule(_updateJobHandle);
        }

        private void RunPredictionSimple()
        {
            // Simple prediction for non-Job System path
            PlayerController.Instance.ApplyPrediction(Time.fixedDeltaTime);
        }

        private void ProcessInterpolation()
        {
            while (_updateQueue.TryDequeue(out EntityUpdate update))
            {
                if (_entityViews.TryGetValue(update.EntityId, out NativeEntityView view))
                {
                    view.TargetPosition = update.TargetPosition;
                    view.TargetRotation = update.TargetRotation;
                    view.TargetVelocity = update.TargetVelocity;
                    view.InterpolationStartTime = _clientTime;
                    view.InterpolationDuration = update.InterpolationTime;
                    _entityViews[update.EntityId] = view;
                }
            }
        }

        private void UpdateEntityViews()
        {
            var enumerator = _entityViews.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var view = enumerator.Current.Value;
                if (view.EntityId == _localPlayerId) continue;

                float t = math.saturate((_clientTime - view.InterpolationStartTime) / view.InterpolationDuration);
                t = math.smoothstep(0, 1, t);

                view.CurrentPosition = math.lerp(view.CurrentPosition, view.TargetPosition, t);
                view.CurrentRotation = math.slerp(view.CurrentRotation, view.TargetRotation, t);

                // Update GameObject transform (on main thread)
                if (_entityObjects.TryGetValue(enumerator.Current.Key, out var go) && go != null)
                {
                    go.transform.SetPositionAndRotation(
                        view.CurrentPosition.ToVector3(),
                        view.CurrentRotation.ToQuaternion()
                    );
                }

                _entityViews[enumerator.Current.Key] = view;
            }
        }

        private void SendInputToServer()
        {
            var input = InputManager.Instance.GetCurrentInput();
            input.Tick = _serverTick;
            input.TimestampMs = (ulong)(_clientTime * 1000);

            var message = new ClientMessage { Input = input };
            _ = _stream.RequestStream.WriteAsync(message);
        }

        public async System.Threading.Tasks.Task SendRPCAsync(string method, object payload)
        {
            var requestId = System.Guid.NewGuid().ToString();
            var bytes = System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(payload));

            var request = new RPCRequest
            {
                RequestId = requestId,
                Method = method,
                Payload = Google.Protobuf.ByteString.CopyFrom(bytes)
            };

            await _stream.RequestStream.WriteAsync(new ClientMessage { Rpc = request });
        }

        public GameObject GetOrCreateEntity(ulong entityId, EntityType type)
        {
            if (_entityObjects.TryGetValue(entityId, out var existing) && existing != null)
                return existing;

            var go = _entityPool.Get(type);
            go.name = $"{type}_{entityId}";

            var newView = new NativeEntityView
            {
                EntityId = entityId,
                EntityType = type,
                CurrentPosition = new float3(go.transform.position.x, go.transform.position.y, go.transform.position.z),
                CurrentRotation = new quaternion(go.transform.rotation.x, go.transform.rotation.y, go.transform.rotation.z, go.transform.rotation.w)
            };
            _entityViews.TryAdd(entityId, newView);
            _entityObjects[entityId] = go;

            return go;
        }

        public void ReturnEntity(ulong entityId)
        {
            if (_entityViews.TryGetValue(entityId, out var view) && _entityObjects.TryGetValue(entityId, out var go) && go != null)
            {
                _entityPool.Return(view.EntityType, go);
                _entityViews.Remove(entityId);
                _entityObjects.Remove(entityId);
            }
        }

        private void OnDestroy()
        {
            _updateJobHandle.Complete();

            _entityViews.Dispose();
            _entityObjects.Clear();
            _updateQueue.Dispose();
            _predictedStates.Dispose();
            _snapshotHistory.Dispose();

            _entityPool.Dispose();
            _projectilePool.Dispose();
            _effectPool.Dispose();

            _stream?.RequestStream.CompleteAsync();
            _channel?.ShutdownAsync().Wait();
        }
    }

    // ============================================================================
    // Native Data Structures (Burst-compatible). Named NativeEntityView to avoid
    // colliding with the MonoBehaviour EntityView in EntityManager.cs.
    // ============================================================================

    public struct NativeEntityView
    {
        public ulong EntityId;
        public EntityType EntityType;
        // No managed references here: this struct lives in a NativeHashMap and is
        // written by a [BurstCompile] job, so every field must be blittable.
        public float3 CurrentPosition;
        public quaternion CurrentRotation;
        public float3 TargetPosition;
        public quaternion TargetRotation;
        public float3 Velocity;
        public float3 TargetVelocity;
        public float InterpolationStartTime;
        public float InterpolationDuration;
        public uint LastServerTick;
    }

    public struct EntitySnapshot
    {
        public ulong EntityId;
        public EntityType Type;
        public float3 Position;
        public quaternion Rotation;
        public float3 Velocity;
        public uint Tick;
    }

    public struct EntityUpdate
    {
        public ulong EntityId;
        public float3 TargetPosition;
        public quaternion TargetRotation;
        public float3 TargetVelocity;
        public uint ServerTick;
        public float InterpolationTime;
    }

    public struct PredictedState
    {
        public uint Tick;
        public float3 Position;
        public quaternion Rotation;
        public float3 Velocity;
        public InputSample Input;
    }

    public struct NativeServerSnapshot
    {
        public uint Tick;
        public float ServerTime;
        // Entities stored separately
    }

    // ============================================================================
    // Object Pooling
    // ============================================================================

    public class ObjectPool<T> where T : class, new()
    {
        private readonly Stack<T> _pool = new Stack<T>();
        private readonly System.Func<T> _factory;
        private readonly int _maxSize;

        public ObjectPool(int maxSize, System.Func<T> factory)
        {
            _maxSize = maxSize;
            _factory = factory;
        }

        public T Get()
        {
            lock (_pool)
            {
                if (_pool.Count > 0)
                    return _pool.Pop();
            }
            return _factory();
        }

        public void Return(T item)
        {
            lock (_pool)
            {
                if (_pool.Count < _maxSize)
                    _pool.Push(item);
            }
        }

        public void Dispose() { lock (_pool) _pool.Clear(); }
    }

    public class EntityPool
    {
        private readonly Dictionary<EntityType, Stack<GameObject>> _pools = new Dictionary<EntityType, Stack<GameObject>>();
        private readonly Dictionary<EntityType, GameObject> _prefabs = new Dictionary<EntityType, GameObject>();
        private readonly int _maxPerType;

        public EntityPool(int maxPerType) { _maxPerType = maxPerType; }

        public void RegisterPrefab(EntityType type, GameObject prefab) => _prefabs[type] = prefab;

        public GameObject Get(EntityType type)
        {
            if (!_pools.TryGetValue(type, out var stack))
            {
                stack = new Stack<GameObject>();
                _pools[type] = stack;
            }

            GameObject go;
            lock (stack)
            {
                go = stack.Count > 0 ? stack.Pop() : Object.Instantiate(_prefabs[type]);
            }
            go.SetActive(true);
            return go;
        }

        public void Return(EntityType type, GameObject go)
        {
            go.SetActive(false);
            if (!_pools.TryGetValue(type, out var stack))
            {
                stack = new Stack<GameObject>();
                _pools[type] = stack;
            }
            lock (stack)
            {
                if (stack.Count < _maxPerType)
                    stack.Push(go);
                else
                    Object.Destroy(go);
            }
        }

        public void Dispose()
        {
            foreach (var stack in _pools.Values)
            {
                foreach (var go in stack)
                    Object.Destroy(go);
                stack.Clear();
            }
            _pools.Clear();
        }
    }

    // ============================================================================
    // Pooled Gameplay Objects
    // ============================================================================

    public class Projectile
    {
        public GameObject GameObject;
        public Rigidbody Rigidbody;
        public TrailRenderer Trail;
        public uint OwnerId;
        public float SpawnTime;
        public float LifeTime;
        public int Damage;
        public float3 Velocity;

        public void Initialize(uint owner, float3 position, float3 velocity, int damage, float lifeTime)
        {
            OwnerId = owner;
            GameObject.transform.position = position.ToVector3();
            Rigidbody.velocity = velocity.ToVector3();
            Damage = damage;
            LifeTime = lifeTime;
            SpawnTime = Time.time;
            Trail.Clear();
            GameObject.SetActive(true);
        }

        public bool IsExpired => Time.time - SpawnTime > LifeTime;
    }

    public class ParticleEffect
    {
        public GameObject GameObject;
        public ParticleSystem ParticleSystem;
        public float SpawnTime;
        public float Duration;

        public void Play(float3 position, float duration)
        {
            GameObject.transform.position = position.ToVector3();
            Duration = duration;
            SpawnTime = Time.time;
            ParticleSystem.Play();
            GameObject.SetActive(true);
        }

        public bool IsExpired => Time.time - SpawnTime > Duration;
    }

    // ============================================================================
    // Extension Methods
    // ============================================================================

    // Bridges megame.common maths and Unity.Mathematics. Kept separate from
    // ProtoExtensions (Core/EntityManager.cs) so the two classes do not collide.
    public static class MathTypeExtensions
    {
        public static float3 ToFloat3(this Vector3 v) => new float3(v.X, v.Y, v.Z);
        public static quaternion ToQuaternion(this Vector3 q) => new quaternion(q.X, q.Y, q.Z, 1f);
        public static UnityEngine.Vector3 ToVector3(this float3 v) => new UnityEngine.Vector3(v.x, v.y, v.z);
        public static UnityEngine.Quaternion ToQuaternion(this quaternion q) => new UnityEngine.Quaternion(q.value.x, q.value.y, q.value.z, q.value.w);
    }
}
