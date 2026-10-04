using UnityEngine;
using Unity.Profiling;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using Megame.Controllers;
using Megame.Data;
using Megame.Client;
using Megame.Vehicles;

namespace Megame.Client
{
    /// <summary>
    /// Comprehensive Profiler Markers for Unity
    /// Use with Profiler.BeginSample/EndSample or custom ProfilerMarker
    /// </summary>
    public static class ProfilerMarkers
    {
        // Frame
        public static readonly ProfilerMarker Frame = new ProfilerMarker("Frame");
        public static readonly ProfilerMarker FrameUpdate = new ProfilerMarker("Frame.Update");
        public static readonly ProfilerMarker FrameLateUpdate = new ProfilerMarker("Frame.LateUpdate");
        public static readonly ProfilerMarker FrameFixedUpdate = new ProfilerMarker("Frame.FixedUpdate");

        // Game Client
        public static readonly ProfilerMarker GameClient = new ProfilerMarker("GameClient");
        public static readonly ProfilerMarker GameClientReceive = new ProfilerMarker("GameClient.ReceiveLoop");
        public static readonly ProfilerMarker GameClientProcessSnapshot = new ProfilerMarker("GameClient.ProcessSnapshot");
        public static readonly ProfilerMarker GameClientProcessDelta = new ProfilerMarker("GameClient.ProcessDeltaSnapshot");
        public static readonly ProfilerMarker GameClientSendInput = new ProfilerMarker("GameClient.SendInput");
        public static readonly ProfilerMarker GameClientPrediction = new ProfilerMarker("GameClient.Prediction");
        public static readonly ProfilerMarker GameClientInterpolation = new ProfilerMarker("GameClient.Interpolation");
        public static readonly ProfilerMarker GameClientEntityUpdate = new ProfilerMarker("GameClient.EntityViewUpdate");

        // Network
        public static readonly ProfilerMarker Network = new ProfilerMarker("Network");
        public static readonly ProfilerMarker NetworkSerialize = new ProfilerMarker("Network.Serialize");
        public static readonly ProfilerMarker NetworkDeserialize = new ProfilerMarker("Network.Deserialize");
        public static readonly ProfilerMarker NetworkSend = new ProfilerMarker("Network.Send");
        public static readonly ProfilerMarker NetworkReceive = new ProfilerMarker("Network.Receive");

        // Entity System
        public static readonly ProfilerMarker EntitySystem = new ProfilerMarker("EntitySystem");
        public static readonly ProfilerMarker EntityCreate = new ProfilerMarker("EntitySystem.Create");
        public static readonly ProfilerMarker EntityDestroy = new ProfilerMarker("EntitySystem.Destroy");
        public static readonly ProfilerMarker EntityPoolGet = new ProfilerMarker("EntitySystem.PoolGet");
        public static readonly ProfilerMarker EntityPoolReturn = new ProfilerMarker("EntitySystem.PoolReturn");

        // Player Controller
        public static readonly ProfilerMarker PlayerController = new ProfilerMarker("PlayerController");
        public static readonly ProfilerMarker PlayerMovement = new ProfilerMarker("PlayerController.Movement");
        public static readonly ProfilerMarker PlayerAnimation = new ProfilerMarker("PlayerController.Animation");
        public static readonly ProfilerMarker PlayerPrediction = new ProfilerMarker("PlayerController.Prediction");
        public static readonly ProfilerMarker PlayerReconciliation = new ProfilerMarker("PlayerController.Reconciliation");

        // Vehicle Controller
        public static readonly ProfilerMarker VehicleController = new ProfilerMarker("VehicleController");
        public static readonly ProfilerMarker VehiclePhysics = new ProfilerMarker("VehicleController.Physics");
        public static readonly ProfilerMarker VehicleEngine = new ProfilerMarker("VehicleController.Engine");
        public static readonly ProfilerMarker VehicleDrivetrain = new ProfilerMarker("VehicleController.Drivetrain");
        public static readonly ProfilerMarker VehicleSteering = new ProfilerMarker("VehicleController.Steering");
        public static readonly ProfilerMarker VehicleBraking = new ProfilerMarker("VehicleController.Braking");
        public static readonly ProfilerMarker VehicleAero = new ProfilerMarker("VehicleController.Aero");
        public static readonly ProfilerMarker VehicleWheelUpdate = new ProfilerMarker("VehicleController.WheelVisuals");
        public static readonly ProfilerMarker VehicleReconciliation = new ProfilerMarker("VehicleController.Reconciliation");

        // Weapon Controller
        public static readonly ProfilerMarker WeaponController = new ProfilerMarker("WeaponController");
        public static readonly ProfilerMarker WeaponFire = new ProfilerMarker("WeaponController.Fire");
        public static readonly ProfilerMarker WeaponRecoil = new ProfilerMarker("WeaponController.Recoil");
        public static readonly ProfilerMarker WeaponSpread = new ProfilerMarker("WeaponController.Spread");
        public static readonly ProfilerMarker WeaponReload = new ProfilerMarker("WeaponController.Reload");
        public static readonly ProfilerMarker WeaponBallistics = new ProfilerMarker("WeaponController.Ballistics");
        public static readonly ProfilerMarker WeaponProjectiles = new ProfilerMarker("WeaponController.Projectiles");
        public static readonly ProfilerMarker WeaponViewModel = new ProfilerMarker("WeaponController.ViewModel");

        // Camera
        public static readonly ProfilerMarker Camera = new ProfilerMarker("Camera");
        public static readonly ProfilerMarker CameraUpdate = new ProfilerMarker("Camera.Update");
        public static readonly ProfilerMarker CameraCollision = new ProfilerMarker("Camera.CollisionAvoidance");

        // Input
        public static readonly ProfilerMarker Input = new ProfilerMarker("Input");
        public static readonly ProfilerMarker InputGather = new ProfilerMarker("Input.Gather");
        public static readonly ProfilerMarker InputProcess = new ProfilerMarker("Input.Process");

        // UI
        public static readonly ProfilerMarker UI = new ProfilerMarker("UI");
        public static readonly ProfilerMarker UIHUD = new ProfilerMarker("UI.HUD");
        public static readonly ProfilerMarker UIDialogue = new ProfilerMarker("UI.Dialogue");
        public static readonly ProfilerMarker UISubtitles = new ProfilerMarker("UI.Subtitles");
        public static readonly ProfilerMarker UIWeaponWheel = new ProfilerMarker("UI.WeaponWheel");
        public static readonly ProfilerMarker UIPhone = new ProfilerMarker("UI.Phone");
        public static readonly ProfilerMarker UIMinimap = new ProfilerMarker("UI.Minimap");

        // Systems
        public static readonly ProfilerMarker MissionSystem = new ProfilerMarker("MissionSystem");
        public static readonly ProfilerMarker DialogueSystem = new ProfilerMarker("DialogueSystem");
        public static readonly ProfilerMarker PhoneSystem = new ProfilerMarker("PhoneSystem");
        public static readonly ProfilerMarker CutsceneSystem = new ProfilerMarker("CutsceneSystem");
        public static readonly ProfilerMarker WorldSystem = new ProfilerMarker("WorldSystem");

        // Rendering
        public static readonly ProfilerMarker Rendering = new ProfilerMarker("Rendering");
        public static readonly ProfilerMarker RenderingShadows = new ProfilerMarker("Rendering.Shadows");
        public static readonly ProfilerMarker RenderingPostProcess = new ProfilerMarker("Rendering.PostProcess");

        // Physics
        public static readonly ProfilerMarker Physics = new ProfilerMarker("Physics");
        public static readonly ProfilerMarker PhysicsCollision = new ProfilerMarker("Physics.Collision");
        public static readonly ProfilerMarker PhysicsRaycast = new ProfilerMarker("Physics.Raycast");

        // Jobs
        public static readonly ProfilerMarker JobSystem = new ProfilerMarker("JobSystem");
        public static readonly ProfilerMarker JobSchedule = new ProfilerMarker("JobSystem.Schedule");
        public static readonly ProfilerMarker JobComplete = new ProfilerMarker("JobSystem.Complete");

        // Memory
        public static readonly ProfilerMarker Memory = new ProfilerMarker("Memory");
        public static readonly ProfilerMarker MemoryAlloc = new ProfilerMarker("Memory.Alloc");
        public static readonly ProfilerMarker MemoryGC = new ProfilerMarker("Memory.GC");

        // Custom markers with metadata
        public static ProfilerMarker Create(string name, ProfilerCategory category = default)
        {
            return new ProfilerMarker(name, category);
        }
    }

    /// <summary>
    /// Auto-scoped profiler marker (using IDisposable pattern)
    /// Usage: using (new ProfilerScope(ProfilerMarkers.PlayerMovement)) { ... }
    /// </summary>
    public struct ProfilerScope : System.IDisposable
    {
        private readonly ProfilerMarker _marker;

        public ProfilerScope(ProfilerMarker marker)
        {
            _marker = marker;
            _marker.Begin();
        }

        public void Dispose()
        {
            _marker.End();
        }
    }

    /// <summary>
    /// Profiler scope with custom name (for dynamic names)
    /// </summary>
    public struct ProfilerScopeCustom : System.IDisposable
    {
        private readonly ProfilerMarker _marker;

        public ProfilerScopeCustom(string name, ProfilerCategory category = default)
        {
            _marker = new ProfilerMarker(name, category);
            _marker.Begin();
        }

        public void Dispose()
        {
            _marker.End();
        }
    }

    /// <summary>
    /// Extension methods for easy profiling
    /// </summary>
    public static class ProfilerExtensions
    {
        public static T Profile<T>(this ProfilerMarker marker, System.Func<T> func)
        {
            marker.Begin();
            try { return func(); }
            finally { marker.End(); }
        }

        public static void Profile(this ProfilerMarker marker, System.Action action)
        {
            marker.Begin();
            try { action(); }
            finally { marker.End(); }
        }

        public static T Profile<T>(this string name, System.Func<T> func)
        {
            var marker = new ProfilerMarker(name);
            marker.Begin();
            try { return func(); }
            finally { marker.End(); }
        }

        public static void Profile(this string name, System.Action action)
        {
            var marker = new ProfilerMarker(name);
            marker.Begin();
            try { action(); }
            finally { marker.End(); }
        }
    }

    /// <summary>
    /// Performance counters for runtime monitoring
    /// </summary>
    public static class PerformanceCounters
    {
        private static readonly Dictionary<string, PerformanceCounter> _counters = new Dictionary<string, PerformanceCounter>();
        private static readonly object _lock = new object();

        public static PerformanceCounter GetOrCreate(string name)
        {
            lock (_lock)
            {
                if (!_counters.TryGetValue(name, out var counter))
                {
                    counter = new PerformanceCounter(name);
                    _counters[name] = counter;
                }
                return counter;
            }
        }

        public static void Record(string name, float value)
        {
            GetOrCreate(name).Record(value);
        }

        public static void Increment(string name, float value = 1f)
        {
            GetOrCreate(name).Increment(value);
        }

        public static float GetAverage(string name)
        {
            if (_counters.TryGetValue(name, out var counter))
                return counter.Average;
            return 0f;
        }

        public static float GetMin(string name)
        {
            if (_counters.TryGetValue(name, out var counter))
                return counter.Min;
            return 0f;
        }

        public static float GetMax(string name)
        {
            if (_counters.TryGetValue(name, out var counter))
                return counter.Max;
            return 0f;
        }

        public static void Reset(string name)
        {
            if (_counters.TryGetValue(name, out var counter))
                counter.Reset();
        }

        public static void ResetAll()
        {
            lock (_lock)
            {
                foreach (var counter in _counters.Values)
                    counter.Reset();
            }
        }

        public static void LogAll()
        {
            lock (_lock)
            {
                foreach (var kvp in _counters)
                {
                    var c = kvp.Value;
                    Debug.Log($"[Perf] {kvp.Key}: Avg={c.Average:F4}ms Min={c.Min:F4}ms Max={c.Max:F4}ms Count={c.Count}");
                }
            }
        }
    }

    public class PerformanceCounter
    {
        private readonly string _name;
        private long _count;
        private double _sum;
        private float _min = float.MaxValue;
        private float _max = float.MinValue;

        public PerformanceCounter(string name) { _name = name; }

        public void Record(float value)
        {
            System.Threading.Interlocked.Increment(ref _count);
            System.Threading.Interlocked.Add(ref _sum, value);
            float v = value;
            float currentMin, currentMax;
            do { currentMin = _min; } while (v < currentMin && System.Threading.Interlocked.CompareExchange(ref _min, v, currentMin) != currentMin);
            do { currentMax = _max; } while (v > currentMax && System.Threading.Interlocked.CompareExchange(ref _max, v, currentMax) != currentMax);
        }

        public void Increment(float value = 1f) => Record(value);

        public void Reset()
        {
            _count = 0;
            _sum = 0;
            _min = float.MaxValue;
            _max = float.MinValue;
        }

        public long Count => _count;
        public float Average => _count > 0 ? (float)(_sum / _count) : 0f;
        public float Min => _min == float.MaxValue ? 0f : _min;
        public float Max => _max == float.MinValue ? 0f : _max;
    }

    /// <summary>
    /// Frame time tracker
    /// </summary>
    public class FrameTimeTracker : MonoBehaviour
    {
        private static FrameTimeTracker _instance;
        public static FrameTimeTracker Instance => _instance;

        [Header("Settings")]
        public int historySize = 600; // 10 seconds at 60fps
        public bool logFrameTime = false;

        private NativeArray<float> _frameTimes;
        private int _index;
        private float _totalTime;
        private int _frameCount;

        public float AverageFrameTime { get; private set; }
        public float MinFrameTime { get; private set; }
        public float MaxFrameTime { get; private set; }
        public float CurrentFPS { get; private set; }
        public float Percentile99 { get; private set; }
        public float Percentile95 { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _frameTimes = new NativeArray<float>(historySize, Allocator.Persistent);
            MinFrameTime = float.MaxValue;
        }

        private void Update()
        {
            float frameTime = Time.unscaledDeltaTime * 1000f; // ms

            _frameTimes[_index] = frameTime;
            _index = (_index + 1) % historySize;
            _totalTime += frameTime;
            _frameCount++;

            MinFrameTime = math.min(MinFrameTime, frameTime);
            MaxFrameTime = math.max(MaxFrameTime, frameTime);

            if (_frameCount >= historySize)
            {
                CalculatePercentiles();
            }

            AverageFrameTime = _totalTime / math.min(_frameCount, historySize);
            CurrentFPS = 1000f / frameTime;

            if (logFrameTime && _frameCount % 60 == 0)
            {
                Debug.Log($"Frame: {frameTime:F2}ms ({CurrentFPS:F1} FPS) Avg: {AverageFrameTime:F2}ms");
            }
        }

        private void CalculatePercentiles()
        {
            var sorted = new NativeArray<float>(_frameTimes.Length, Allocator.Temp);
            sorted.CopyFrom(_frameTimes);
            sorted.Sort();

            Percentile99 = sorted[math.min(sorted.Length - 1, (int)(sorted.Length * 0.99))];
            Percentile95 = sorted[math.min(sorted.Length - 1, (int)(sorted.Length * 0.95))];
            sorted.Dispose();
        }

        private void OnDestroy()
        {
            _frameTimes.Dispose();
        }
    }

    /// <summary>
    /// Memory tracker
    /// </summary>
    public class MemoryTracker : MonoBehaviour
    {
        private static MemoryTracker _instance;
        public static MemoryTracker Instance => _instance;

        public float updateInterval = 1f;
        public bool logMemory = false;

        private float _timer;
        private long _lastGCMemory;

        public long TotalAllocated { get; private set; }
        public long TotalReserved { get; private set; }
        public long MonoUsed { get; private set; }
        public long MonoReserved { get; private set; }
        public int GCCount { get; private set; }
        public long GCMemoryDelta { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer >= updateInterval)
            {
                _timer = 0f;
                UpdateMemoryStats();
            }
        }

        private void UpdateMemoryStats()
        {
            TotalAllocated = Profiler.GetTotalAllocatedMemoryLong();
            TotalReserved = Profiler.GetTotalReservedMemoryLong();
            MonoUsed = Profiler.GetMonoUsedMemoryLong();
            MonoReserved = Profiler.GetMonoHeapSizeLong();

            int currentGC = System.GC.CollectionCount(0) + System.GC.CollectionCount(1) + System.GC.CollectionCount(2);
            GCMemoryDelta = TotalAllocated - _lastGCMemory;
            _lastGCMemory = TotalAllocated;
            GCCount = currentGC;

            if (logMemory)
            {
                Debug.Log($"Memory: Allocated={TotalAllocated / 1024 / 1024:F1}MB Reserved={TotalReserved / 1024 / 1024:F1}MB Mono={MonoUsed / 1024 / 1024:F1}MB GC={GCCount} Delta={GCMemoryDelta / 1024:F1}KB");
            }
        }
    }
}
