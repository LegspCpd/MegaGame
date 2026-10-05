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

        public static void AttachTo(Transform target)
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

            var rig = cam.gameObject.GetComponent<PlayableCameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<PlayableCameraRig>();
            rig.Init(target);
        }

        private void Init(Transform target)
        {
            _target = target;
            _camera = GetComponent<Camera>();
            _yaw = target != null ? target.eulerAngles.y : 0f;
            _zoom = Distance;
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            if (_camera == null) _camera = GetComponent<Camera>();

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

            if (Input.GetMouseButton(1) || Input.GetKey(KeyCode.LeftAlt))
            {
                mx = Input.GetAxisRaw("Mouse X") * Sensitivity;
                my = Input.GetAxisRaw("Mouse Y") * Sensitivity;
            }

            // Keyboard look, so the game is playable without a mouse capture.
            if (Input.GetKey(KeyCode.LeftArrow)) my += 2.2f;
            if (Input.GetKey(KeyCode.RightArrow)) my -= 2.2f;
            if (Input.GetKey(KeyCode.UpArrow)) mx += 2.2f;
            if (Input.GetKey(KeyCode.DownArrow)) mx -= 2.2f;

            if (Input.GetKey(KeyCode.Q)) _yaw -= 2.2f;
            if (Input.GetKey(KeyCode.E)) _yaw += 2.2f;

            _yaw += mx;
            _pitch = Mathf.Clamp(_pitch - my, -25f, 70f);

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _zoom = Mathf.Clamp(_zoom - scroll * 6f, MinDistance, MaxDistance);
            }
        }
    }
}