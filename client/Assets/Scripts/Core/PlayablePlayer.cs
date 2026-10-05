using UnityEngine;
using UnityEngine.InputSystem;

namespace Megame.Client
{
    /// <summary>
    /// Capsule-driven player used by <see cref="PlayableWorld"/>.
    ///
    /// Input goes through the Input System package (Keyboard/Mouse) rather than
    /// the legacy Input class: the project sets activeInputHandler to Both,
    /// which lets the new backend win and silently disables Input.GetKey.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayablePlayer : MonoBehaviour
    {
        /// <summary>Set by Create; other systems (weapon spread) read it.</summary>
        public static PlayablePlayer Instance { get; private set; }
        public float WalkSpeed = 5.5f;
        public float SprintSpeed = 10f;
        public float CrouchSpeed = 2.4f;
        public float JumpSpeed = 6.5f;
        public float Gravity = -22f;
        public float MaxStamina = 100f;

        public Vector3 Velocity { get; private set; }
        public float Stamina { get; private set; } = 100f;
        public bool IsSprinting { get; private set; }
        public bool IsGrounded { get; private set; }

        private CharacterController _cc;
        private float _coyote;          // grace period after leaving a ledge
        private float _jumpBuffer;      // allows jumping just before landing

        private const float CoyoteTime = 0.15f;
        private const float JumpBufferTime = 0.15f;

        public static PlayablePlayer Create(Vector3 position)
        {
            var go = new GameObject("Player");
            go.transform.position = position;
            go.tag = "Player";

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.62f, 0.9f, 0.62f);
            Object.Destroy(body.GetComponent<Collider>()); // parent CC is the collider
            body.GetComponent<MeshRenderer>().sharedMaterial =
                PlayableWorld.NewMaterial(new Color(0.20f, 0.55f, 0.85f));

            // Facing marker so the player can tell which way they look.
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "FacingMarker";
            nose.transform.SetParent(go.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.45f, 0.36f);
            nose.transform.localScale = new Vector3(0.18f, 0.18f, 0.22f);
            Object.Destroy(nose.GetComponent<Collider>());
            nose.GetComponent<MeshRenderer>().sharedMaterial =
                PlayableWorld.NewMaterial(new Color(0.95f, 0.80f, 0.25f));

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 55f;
            cc.stepOffset = 0.4f;
            cc.skinWidth = 0.03f;

            var player = go.AddComponent<PlayablePlayer>();
            player.Stamina = player.MaxStamina;
            Instance = player;
            return player;
        }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (_cc == null) _cc = GetComponent<CharacterController>();
            if (_cc == null) return;

            float dt = Time.deltaTime;
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            Vector2 move = ReadMove(kb);
            bool sprintHeld = IsDown(kb, Key.LeftShift);
            bool crouchHeld = IsDown(kb, Key.LeftCtrl) || IsDown(kb, Key.C);
            bool jumpPressed = WasPressed(kb, Key.Space);

            IsSprinting = sprintHeld && move.sqrMagnitude > 0.01f && Stamina > 0f;
            float speed = crouchHeld ? CrouchSpeed : (IsSprinting ? SprintSpeed : WalkSpeed);

            // Camera-relative movement. PlayableCameraRig publishes a flattened
            // basis; fall back to world axes until the rig has ticked once.
            Vector3 camFwd = PlayableCameraRig.Forward;
            Vector3 camRight = PlayableCameraRig.Right;
            if (camFwd.sqrMagnitude < 0.001f) camFwd = Vector3.forward;

            Vector3 dir = camFwd * move.y + camRight * move.x;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            Vector3 v = Velocity;
            v.x = dir.x * speed;
            v.z = dir.z * speed;

            // Grounded handling with coyote time so walking off a small ledge
            // still allows a jump.
            bool grounded = _cc.isGrounded;
            IsGrounded = grounded;
            _coyote = grounded ? CoyoteTime : Mathf.Max(0f, _coyote - dt);
            _jumpBuffer = jumpPressed ? JumpBufferTime : Mathf.Max(0f, _jumpBuffer - dt);

            if (grounded && v.y < 0f) v.y = -2f;   // stick to the ground

            if (_jumpBuffer > 0f && _coyote > 0f)
            {
                v.y = JumpSpeed;
                _jumpBuffer = 0f;
                _coyote = 0f;
                IsGrounded = false;
            }

            if (!grounded) v.y += Gravity * dt;

            Velocity = v;
            _cc.Move(v * dt);

            // Face the direction of travel.
            if (dir.sqrMagnitude > 0.001f)
            {
                var look = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z), Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 14f * dt);
            }

            if (IsSprinting) Stamina = Mathf.Max(0f, Stamina - 22f * dt);
            else Stamina = Mathf.Min(MaxStamina, Stamina + 14f * dt);

            // Never let the player fall out of the world.
            if (transform.position.y < -40f)
            {
                transform.position = new Vector3(0f, 2f, -8f);
                Velocity = Vector3.zero;
            }
        }

        private static bool IsDown(Keyboard kb, Key key) =>
            kb != null && kb[key].isPressed;

        private static bool WasPressed(Keyboard kb, Key key) =>
            kb != null && kb[key].wasPressedThisFrame;

        private static Vector2 ReadMove(Keyboard kb)
        {
            if (kb == null) return Vector2.zero;

            float x = 0f, y = 0f;
            if (IsDown(kb, Key.W) || IsDown(kb, Key.UpArrow)) y += 1f;
            if (IsDown(kb, Key.S) || IsDown(kb, Key.DownArrow)) y -= 1f;
            if (IsDown(kb, Key.D) || IsDown(kb, Key.RightArrow)) x += 1f;
            if (IsDown(kb, Key.A) || IsDown(kb, Key.LeftArrow)) x -= 1f;

            var v = new Vector2(x, y);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }
    }
}