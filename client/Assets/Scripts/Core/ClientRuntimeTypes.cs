using Unity.Collections;
using UnityEngine;
using Megame.Network;

namespace Megame.Client
{
    /// <summary>Lifecycle of a single mission objective.</summary>
    public enum MissionObjectiveStatus
    {
        Active,
        Completed,
        Failed
    }

    /// <summary>
    /// Progress on one mission objective, as reported by the server. Referenced
    /// by MissionManager and the mission UI, so it lives here rather than
    /// inside a single system.
    /// </summary>
    public class ObjectiveProgress
    {
        public string ObjectiveId;
        public string Description;
        public MissionObjectiveStatus Status = MissionObjectiveStatus.Active;
        public bool IsOptional;

        /// <summary>Amount collected so far, when the objective is a counter.</summary>
        public int Current;

        /// <summary>Amount required; zero when the objective is not a counter.</summary>
        public int Target;

        public bool IsComplete => Status == MissionObjectiveStatus.Completed;

        public override string ToString()
        {
            string desc = string.IsNullOrEmpty(Description) ? ObjectiveId : Description;
            return Target > 0 ? $"{desc} ({Current}/{Target})" : desc;
        }
    }

    /// <summary>Client-side counters backing the debug/profiling overlay.</summary>
    public class ClientMetrics
    {
        public int MessagesSent;
        public int MessagesReceived;
        public int SnapshotsApplied;
        public int PredictionsAccepted;
        public int PredictionsRejected;
        public float FrameTime;
        public float InterpolatedEntities;
        public float RoundTripTimeMs;

        public void Reset()
        {
            MessagesSent = 0;
            MessagesReceived = 0;
            SnapshotsApplied = 0;
            PredictionsAccepted = 0;
            PredictionsRejected = 0;
            FrameTime = 0f;
            InterpolatedEntities = 0f;
            RoundTripTimeMs = 0f;
        }
    }

    /// <summary>
    /// Bounded buffer of client inputs handed to the simulation job, which
    /// drains it each frame.
    /// </summary>
    public class InputBuffer
    {
        private readonly NativeQueue<ClientInput> _queue;
        private readonly int _capacity;

        public InputBuffer(int capacity)
        {
            _capacity = Mathf.Max(1, capacity);
            _queue = new NativeQueue<ClientInput>(Allocator.Persistent);
        }

        public int Capacity => _capacity;
        public NativeQueue<ClientInput> NativeQueue => _queue;

        /// <summary>Queues an input, discarding the oldest when full.</summary>
        public void Enqueue(ClientInput input)
        {
            if (_queue.Count >= _capacity)
            {
                _queue.TryDequeue(out _);
            }
            _queue.Enqueue(input);
        }

        public bool TryDequeue(out ClientInput input) => _queue.TryDequeue(out input);

        public int Count => _queue.Count;

        public void Clear()
        {
            while (_queue.TryDequeue(out _))
            {
            }
        }

        public void Dispose() => _queue.Dispose();
    }
}
