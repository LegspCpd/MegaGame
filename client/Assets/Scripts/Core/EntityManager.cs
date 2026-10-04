using UnityEngine;
using System.Collections.Generic;
using Megame.Entity;

namespace Megame.Client
{
    public class EntityManager
    {
        private GameClient _client;
        private Dictionary<ulong, EntityView> _entityViews = new Dictionary<ulong, EntityView>();

        public EntityManager(GameClient client)
        {
            _client = client;
        }

        public void UpdateEntity(EntityState state)
        {
            var entityId = state.Ref.Id;

            if (!_entityViews.TryGetValue(entityId, out var view))
            {
                view = CreateEntityView(state);
                _entityViews[entityId] = view;
            }

            view.UpdateFromState(state);
        }

        public void DestroyEntity(ulong entityId)
        {
            if (_entityViews.TryGetValue(entityId, out var view))
            {
                view.Destroy();
                _entityViews.Remove(entityId);
            }
        }

        private EntityView CreateEntityView(EntityState state)
        {
            var go = _client.GetOrCreateEntity(state.Ref.Id, state.Ref.Type);
            var view = go.GetComponent<EntityView>();
            if (view == null)
            {
                view = go.AddComponent<EntityView>();
            }
            view.Initialize(state);
            return view;
        }
    }

    public class EntityView : MonoBehaviour
    {
        public ulong EntityId { get; private set; }
        public EntityType EntityType { get; private set; }
        public bool IsLocalPlayer { get; private set; }

        private TransformState _targetTransform;
        private TransformState _currentTransform;
        private float _interpolationTime = 0.1f;

        public void Initialize(EntityState state)
        {
            EntityId = state.Ref.Id;
            EntityType = state.Ref.Type;
            IsLocalPlayer = state.IsLocalPlayer;

            _targetTransform = new TransformState
            {
                Position = state.Transform.Position.ToVector3(),
                Rotation = state.Transform.Rotation.ToQuaternion(),
                Scale = state.Transform.Scale.ToVector3()
            };
            _currentTransform = _targetTransform;

            transform.SetPositionAndRotation(_currentTransform.Position, _currentTransform.Rotation);
            transform.localScale = _currentTransform.Scale;
        }

        public void UpdateFromState(EntityState state)
        {
            _targetTransform = new TransformState
            {
                Position = state.Transform.Position.ToVector3(),
                Rotation = state.Transform.Rotation.ToQuaternion(),
                Scale = state.Transform.Scale.ToVector3()
            };

            // Velocity for prediction
            if (state.Velocity != null)
            {
                _targetTransform.Velocity = state.Velocity.ToVector3();
            }
        }

        private void Update()
        {
            if (IsLocalPlayer)
            {
                // Local player is controlled directly, don't interpolate
                return;
            }

            // Interpolate transform
            _currentTransform.Position = Vector3.Lerp(_currentTransform.Position, _targetTransform.Position, Time.deltaTime / _interpolationTime);
            _currentTransform.Rotation = Quaternion.Slerp(_currentTransform.Rotation, _targetTransform.Rotation, Time.deltaTime / _interpolationTime);
            _currentTransform.Scale = Vector3.Lerp(_currentTransform.Scale, _targetTransform.Scale, Time.deltaTime / _interpolationTime);

            transform.SetPositionAndRotation(_currentTransform.Position, _currentTransform.Rotation);
            transform.localScale = _currentTransform.Scale;
        }

        public void Destroy()
        {
            Destroy(gameObject);
        }

        private struct TransformState
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public Vector3 Velocity;
        }
    }

    // Extension methods for protobuf conversion
    public static class ProtoExtensions
    {
        // megame.common types (client input payloads, shared maths).
        public static Vector3 ToVector3(this Common.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
        public static Quaternion ToQuaternion(this Common.Quaternion q) => new Quaternion(q.X, q.Y, q.Z, q.W);
        public static Common.Vector3 ToProto(this Vector3 v) => new Common.Vector3 { X = v.x, Y = v.y, Z = v.z };
        public static Common.Quaternion ToProto(this Quaternion q) => new Common.Quaternion { X = q.x, Y = q.y, Z = q.z, W = q.w };

        // megame.entity types. EntityState carries these, so the converters
        // above do not apply to them.
        public static Vector3 ToVector3(this Entity.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
        public static Quaternion ToQuaternion(this Entity.Quaternion q) => new Quaternion(q.X, q.Y, q.Z, q.W);
    }
}