using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Third-person orbit camera. Mouse (or arrow keys) rotates the rig around
    /// the player; the camera trails behind at a fixed distance and avoids
    /// clipping through geometry with a simple sphere cast.
    /// </summary>
    public class PlayableCameraRig : MonoBehaviour
    {
        public static Vector3 Forward { get; private set; } = Vector3.forward;
        public static Vector3 Right { get; private set; } = Vector3.right;

        /// <summary>
        /// Mouse delta accumulated this frame. The weapon reads and clears it so
        /// look input drives both the camera and the view model without either
        /// one having to poll the mouse independently.
        /// </summary>
        private static Vector2 _lookDelta;

        public static Vector2 ConsumeLookDelta()
        {
            var d = _lookDelta;
            _lookDelta = Vector2.zero;
            return d;
        }

        public float Distance = 7.5f;
        public float MinDistance = 2f;
        public float MaxDistance = 16f;
        public float Sensitivity = 3.2f;
        public float Height = 2.1f;
        public float SmoothTime = 0.12f;

        private Transform _target;
        private Camera _camera;
        private float _yaw;
        private float _pitch = 14f;
        private float _zoom = 7.5f;
        private float _pitchVel;
        private Vector3 _followVel;

        public static PlayableCameraRig AttachTo(Transform target)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            if (cam.GetComponent<AudioListener>() == null)
            {
                cam.gameObject.AddComponent<AudioListener>();
            }

            // The weapon view model sits roughly 0.3 m in front of the camera.
            // With the default 0.3 near clip it gets sliced in half, so pull the
            // near plane in and push the far plane out to keep depth range.
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 900f;
            cam.fieldOfView = 70f;

            var rig = cam.gameObject.GetComponent<PlayableCameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<PlayableCameraRig>();
            rig.Init(target);
            return rig;
        }

        private void Init(Transform target)
        {
            _target = target;
            _camera = GetComponent<Camera>();
            _yaw = target != null ? target.eulerAngles.y : 0f;
            _zoom = Distance;
        }

        /// <summary>Switch what the camera orbits (player body vs vehicle).</summary>
        public void Follow(Transform target)
        {
            _target = target;
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
                _pitch = 14f;
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            if (_camera == null) _camera = GetComponent<Camera>();

            // A cinematic owns the camera while it plays. LateUpdate runs after
            // Update, so without this the rig would overwrite every cinematic
            // camera move on the very frame it happened.
            if (CinematicPlayer.Instance != null && CinematicPlayer.Instance.IsPlaying)
                return;

            ReadLook();

            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var offset = rotation * new Vector3(0f, 0f, -_zoom);
            Vector3 pivot = _target.position + Vector3.up * Height;
            Vector3 desired = pivot + offset;

            // Pull in if something is between the camera and the player.
            Vector3 dir = desired - pivot;
            float dist = dir.magnitude;
            if (dist > 0.01f && Physics.SphereCast(pivot, 0.28f, dir / dist, out RaycastHit hit, dist))
            {
                desired = pivot + dir / dist * Mathf.Max(MinDistance, hit.distance - 0.15f);
            }

            Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref _followVel, SmoothTime);
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(pivot - pos, Vector3.up);

            // Feed the player controller a flattened basis for movement.
            Forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        }

        private void ReadLook()
        {
            float mx = 0f, my = 0f;

            // Input System rather than the legacy Input class: with
            // activeInputHandler=Both the new backend wins and Input.GetKey
            // silently stops responding.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;

            bool lookHeld = (mouse != null && mouse.rightButton.isPressed) ||
                            (kb != null && kb[UnityEngine.InputSystem.Key.LeftAlt].isPressed);

            if (lookHeld)
            {
                var delta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
                mx += delta.x * Sensitivity;
                my += delta.y * Sensitivity;

                // Publish for the weapon so recoil and look stay in sync.
                _lookDelta += delta;
            }

            if (kb != null)
            {
                // Keyboard look, so the game is playable without a mouse.
                if (kb[UnityEngine.InputSystem.Key.LeftArrow].isPressed) my += 2.2f;
                if (kb[UnityEngine.InputSystem.Key.RightArrow].isPressed) my -= 2.2f;
                if (kb[UnityEngine.InputSystem.Key.UpArrow].isPressed) mx += 2.2f;
                if (kb[UnityEngine.InputSystem.Key.DownArrow].isPressed) mx -= 2.2f;
                if (kb[UnityEngine.InputSystem.Key.Q].isPressed) _yaw -= 2.2f;
                if (kb[UnityEngine.InputSystem.Key.E].isPressed) _yaw += 2.2f;
            }

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y / 120f;
                if (Mathf.Abs(scroll) > 0.001f)
                    _zoom = Mathf.Clamp(_zoom - scroll * 6f, MinDistance, MaxDistance);
            }

            _yaw += mx;
            _pitch = Mathf.Clamp(_pitch - my, -25f, 70f);
        }
    }
}