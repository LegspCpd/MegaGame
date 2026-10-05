using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Capsule-driven player used by <see cref="PlayableWorld"/>. Movement is
    /// camera-relative, with sprint draining a stamina pool and a jump impulse.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayablePlayer : MonoBehaviour
    {
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

        public static PlayablePlayer Create(Vector3 position)
        {
            var go = new GameObject("Player");
            go.transform.position = position;
            go.tag = "Player";

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            Object.Destroy(body.GetComponent<Collider>());

            var bodyMat = PlayableWorld.NewMaterial(new Color(0.92f, 0.72f, 0.25f));
            body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "FacingMarker";
            nose.transform.SetParent(go.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.35f, -0.42f);
            nose.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
            Object.Destroy(nose.GetComponent<Collider>());
            nose.GetComponent<MeshRenderer>().sharedMaterial =
                PlayableWorld.NewMaterial(new Color(0.15f, 0.15f, 0.18f));

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.35f;

            return go.AddComponent<PlayablePlayer>();
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

            Vector2 move = ReadMove();
            bool sprint = Input.GetKey(KeyCode.LeftShift) && move.sqrMagnitude > 0.01f && Stamina > 0f;
            bool crouch = Input.GetKey(KeyCode.LeftControl);

            IsSprinting = sprint;
            float speed = crouch ? CrouchSpeed : (sprint ? SprintSpeed : WalkSpeed);

            // Camera-relative movement.
            Vector3 camFwd = PlayableCameraRig.Forward;
            Vector3 camRight = PlayableCameraRig.Right;
            Vector3 dir = (camFwd * move.y + camRight * move.x);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            Vector3 v = Velocity;
            v.x = dir.x * speed;
            v.z = dir.z * speed;

            if (_cc.isGrounded)
            {
                IsGrounded = true;
                if (v.y < 0f) v.y = -2f;
                if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space))
                {
                    v.y = JumpSpeed;
                    IsGrounded = false;
                }
            }
            else
            {
                IsGrounded = false;
                v.y += Gravity * dt;
            }

            Velocity = v;
            _cc.Move(v * dt);

            // Face the direction of travel.
            if (dir.sqrMagnitude > 0.001f)
            {
                var look = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z), Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 12f * dt);
            }

            // Stamina.
            if (sprint) Stamina = Mathf.Max(0f, Stamina - 22f * dt);
            else Stamina = Mathf.Min(MaxStamina, Stamina + 14f * dt);

            if (transform.position.y < -30f)
            {
                // Safety net: never let the player fall out of the world.
                transform.position = new Vector3(0f, 1.2f, -8f);
                Velocity = Vector3.zero;
            }
        }

        private static Vector2 ReadMove()
        {
            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            var v = new Vector2(x, y);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }
    }
}