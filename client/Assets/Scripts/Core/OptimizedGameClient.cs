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

            // Initialize managers
            CameraController.Instance.Initialize(this);
            InputManager.Instance.Initialize(this);
            UIManager.Instance.Initialize(this);
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

                    if (entityViews.TryGetValue(snap.EntityId, out NativeEntityView view))
                    {
                        // Queue update for interpolation
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
                    else
                    {
                        // Create new entity view
                        var newView = new NativeEntityView
                        {
                            EntityId = snap.EntityId,
                            EntityType = snap.Type,
                            Position = snap.Position,
                            Rotation = snap.Rotation,
                            Velocity = snap.Velocity,
                            LastServerTick = snap.Tick
                        };
                        entityViews.TryAdd(snap.EntityId, newView);
                    }
                }
            }
        }

        private void ProcessSnapshot(NativeServerSnapshot snapshot)
        {
            _serverTick = snapshot.Tick;
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
                    Rotation = e.Transform.Rotation.ToQuaternion(),
                    Velocity = e.Velocity.ToFloat3(),
                    Tick = e.Timestamp
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
            public NativeQueue<ClientInput> inputBuffer;
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
                if (view.GameObject != null)
                {
                    view.GameObject.transform.SetPositionAndRotation(
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
            var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload);

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
            if (_entityViews.TryGetValue(entityId, out var view) && view.GameObject != null)
                return view.GameObject;

            var go = _entityPool.Get(type);
            go.name = $"{type}_{entityId}";

            var newView = new NativeEntityView
            {
                EntityId = entityId,
                EntityType = type,
                GameObject = go,
                CurrentPosition = go.transform.position.ToFloat3(),
                CurrentRotation = go.transform.rotation.ToQuaternion()
            };
            _entityViews.TryAdd(entityId, newView);

            return go;
        }

        public void ReturnEntity(ulong entityId)
        {
            if (_entityViews.TryGetValue(entityId, out var view) && view.GameObject != null)
            {
                _entityPool.Return(view.EntityType, view.GameObject);
                _entityViews.Remove(entityId);
            }
        }

        private void OnDestroy()
        {
            _updateJobHandle.Complete();

            _entityViews.Dispose();
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
        public GameObject GameObject; // Not Burst-compatible, set on main thread only
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
        public ClientInput Input;
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
            Rigidbody.linearVelocity = velocity.ToVector3();
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
        public static quaternion ToQuaternion(this Vector3 q) => new quaternion(q.X, q.Y, q.Z, q.W);
        public static Vector3 ToVector3(this float3 v) => new Vector3(v.x, v.y, v.z);
        public static Vector3 ToQuaternion(this quaternion q) => new Vector3(q.value.x, q.value.y, q.value.z, q.value.w);
    }
}
