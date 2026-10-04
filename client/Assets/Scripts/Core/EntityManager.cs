using UnityEngine;
using System.Collections.Generic;
using Megame.Entity;

// Megame.Entity also defines Vector3/Quaternion, so the Unity types are
// aliased here to keep unqualified uses unambiguous.
using UnityVector3 = UnityEngine.Vector3;
using UnityQuaternion = UnityEngine.Quaternion;

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
            public UnityVector3 Position;
            public UnityQuaternion Rotation;
            public UnityVector3 Scale;
            public UnityVector3 Velocity;
        }
    }

    // Extension methods for protobuf conversion
    public static class ProtoExtensions
    {
        // megame.common carries the spatial types on EntityState.
        public static UnityVector3 ToVector3(this Common.Vector3 v) => new UnityVector3(v.X, v.Y, v.Z);
        public static UnityQuaternion ToQuaternion(this Common.Quaternion q) => new UnityQuaternion(q.X, q.Y, q.Z, q.W);
        public static Common.Vector3 ToProto(this UnityVector3 v) => new Common.Vector3 { X = v.x, Y = v.y, Z = v.z };
        public static Common.Quaternion ToProto(this UnityQuaternion q) => new Common.Quaternion { X = q.x, Y = q.y, Z = q.z, W = q.w };
    }
}