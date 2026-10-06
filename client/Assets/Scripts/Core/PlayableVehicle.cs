using UnityEngine;
using UnityEngine.InputSystem;

namespace Megame.Client
{
    /// <summary>
    /// Drivable car built from primitives so it needs no prefab.
    ///
    /// Steering is a kinematic bicycle model driven through Input System keys,
    /// which keeps it independent of the network layer and works with the same
    /// backend as <see cref="PlayablePlayer"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayableVehicle : MonoBehaviour
    {
        public float MaxSpeed = 32f;          // m/s
        public float ReverseSpeed = 12f;
        public float Acceleration = 14f;
        public float BrakeForce = 26f;
        public float MaxSteerAngle = 34f;    // degrees at standstill
        public float SteerFalloffSpeed = 22f;
        public float SteerRate = 110f;
        public float SteerReturn = 160f;
        public float SuspensionTravel = 0.28f;
        public float SuspensionStiffness = 42f;
        public float SuspensionDamp = 4.6f;
        public float BodyRoll = 4.5f;

        public float SpeedKPH => _rb != null ? _rb.velocity.magnitude * 3.6f : 0f;
        public bool IsOccupied { get; private set; }
        public int CurrentGear { get; private set; } = 1;
        public float EngineRpm { get; private set; }

        private Rigidbody _rb;
        private Transform _body;
        private bool _driving;
        private float _throttle;
        private float _brake;
        private float _steer;
        private float _steerAngle;
        private float _handbrake;

        private readonly Vector3[] _wheelOffsets =
        {
            new Vector3(-0.82f, -0.02f, 1.32f),
            new Vector3(0.82f, -0.02f, 1.32f),
            new Vector3(-0.82f, -0.02f, -1.28f),
            new Vector3(0.82f, -0.02f, -1.28f),
        };

        public static PlayableVehicle Create(Vector3 position, Quaternion rotation, Color bodyColor)
        {
            var go = new GameObject("Vehicle");
            go.transform.SetPositionAndRotation(position, rotation);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1400f;
            rb.drag = 0.02f;
            rb.angularDrag = 3.5f;
            rb.centerOfMass = new Vector3(0f, -0.35f, 0.05f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var vehicle = go.AddComponent<PlayableVehicle>();
            vehicle.BuildVisual(bodyColor);

            // The visual meshes are collider-free so they cannot fight the
            // physics shape, but the car itself needs a body collider or it
            // drives through buildings and the player walks straight into it.
            var body = go.AddComponent<BoxCollider>();
            body.size = new Vector3(1.78f, 1.15f, 4.12f);
            body.center = new Vector3(0f, 0.60f, 0f);

            return vehicle;
        }

        private void BuildVisual(Color bodyColor)
        {
            var bodyMat = PlayableWorld.NewMaterial(bodyColor);
            var glassMat = PlayableWorld.NewMaterial(new Color(0.16f, 0.20f, 0.26f));
            var trimMat = PlayableWorld.NewMaterial(new Color(0.10f, 0.10f, 0.12f));
            var tyreMat = PlayableWorld.NewMaterial(new Color(0.07f, 0.07f, 0.08f));
            var rimMat = PlayableWorld.NewMaterial(new Color(0.55f, 0.55f, 0.58f));

            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);

            // Chassis
            var chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chassis.name = "Chassis";
            chassis.transform.SetParent(_body, false);
            chassis.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            chassis.transform.localScale = new Vector3(1.78f, 0.52f, 4.12f);
            Destroy(chassis.GetComponent<Collider>());
            chassis.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            // Cabin
            var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.name = "Cabin";
            cabin.transform.SetParent(_body, false);
            cabin.transform.localPosition = new Vector3(0f, 0.78f, -0.18f);
            cabin.transform.localScale = new Vector3(1.58f, 0.52f, 2.02f);
            Destroy(cabin.GetComponent<Collider>());
            cabin.GetComponent<MeshRenderer>().sharedMaterial = glassMat;

            // Roof panel
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(_body, false);
            roof.transform.localPosition = new Vector3(0f, 1.03f, -0.18f);
            roof.transform.localScale = new Vector3(1.50f, 0.09f, 1.92f);
            Destroy(roof.GetComponent<Collider>());
            roof.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            // Bumpers
            for (int i = 0; i < 2; i++)
            {
                float z = i == 0 ? 2.06f : -2.06f;
                var bumper = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bumper.name = "Bumper";
                bumper.transform.SetParent(_body, false);
                bumper.transform.localPosition = new Vector3(0f, 0.30f, z);
                bumper.transform.localScale = new Vector3(1.80f, 0.22f, 0.16f);
                Destroy(bumper.GetComponent<Collider>());
                bumper.GetComponent<MeshRenderer>().sharedMaterial = trimMat;
            }

            // Headlights / taillights
            for (int side = -1; side <= 1; side += 2)
            {
                var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
                head.name = "Headlight";
                head.transform.SetParent(_body, false);
                head.transform.localPosition = new Vector3(side * 0.62f, 0.38f, 2.07f);
                head.transform.localScale = new Vector3(0.34f, 0.14f, 0.08f);
                Destroy(head.GetComponent<Collider>());
                head.GetComponent<MeshRenderer>().sharedMaterial =
                    PlayableWorld.NewMaterial(new Color(1f, 0.95f, 0.75f), true);

                var tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tail.name = "Taillight";
                tail.transform.SetParent(_body, false);
                tail.transform.localPosition = new Vector3(side * 0.66f, 0.40f, -2.07f);
                tail.transform.localScale = new Vector3(0.28f, 0.12f, 0.08f);
                Destroy(tail.GetComponent<Collider>());
                tail.GetComponent<MeshRenderer>().sharedMaterial =
                    PlayableWorld.NewMaterial(new Color(0.85f, 0.12f, 0.10f), true);
            }

            // Wheels: cylinders rotated so their axis runs along local X.
            // The rigidbody owns the collision; the visible wheel meshes are
            // colliders-free so they cannot fight the physics shape.
            for (int i = 0; i < _wheelOffsets.Length; i++)
            {
                float radius = 0.34f;
                float width = 0.26f;

                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = (i < 2 ? $"WheelFront{i}" : $"WheelRear{i}");
                wheel.transform.SetParent(transform, false);
                wheel.transform.localPosition = _wheelOffsets[i];
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                // A cylinder primitive is 2 units tall and 1 unit across, so
                // scale (width, radius, radius) gives a proper tyre.
                wheel.transform.localScale = new Vector3(width, radius, radius);
                Destroy(wheel.GetComponent<Collider>());
                wheel.GetComponent<MeshRenderer>().sharedMaterial = tyreMat;

                var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rim.name = $"Rim{i}";
                rim.transform.SetParent(wheel.transform, false);
                rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                rim.transform.localScale = new Vector3(1.08f, 0.60f, 0.60f);
                Destroy(rim.GetComponent<Collider>());
                rim.GetComponent<MeshRenderer>().sharedMaterial = rimMat;
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        /// <summary>Called by the player when entering or leaving the car.</summary>
        public void SetDriving(bool driving)
        {
            IsOccupied = driving;
            if (driving)
            {
                // Freeze the body while parked so it does not roll away.
                _rb.isKinematic = false;
                _rb.drag = 0.02f;
            }
            else
            {
                _throttle = _brake = _steer = 0f;
                _rb.drag = 0.6f;
            }
        }
        private void Update()
        {
            if (!IsOccupied)
            {
                ApplyParkingDrag();
                return;
            }

            var kb = Keyboard.current;
            ReadInput(kb);
            float dt = Time.deltaTime;

            UpdateSteering(kb, dt);
            ApplyDrive(dt);
            ApplySuspension(dt);
        }

        private void ReadInput(Keyboard kb)
        {
            if (kb == null) { _throttle = _brake = 0f; _steer = 0f; return; }

            bool up = kb[Key.W].isPressed || kb[Key.UpArrow].isPressed;
            bool down = kb[Key.S].isPressed || kb[Key.DownArrow].isPressed;
            bool left = kb[Key.A].isPressed || kb[Key.LeftArrow].isPressed;
            bool right = kb[Key.D].isPressed || kb[Key.RightArrow].isPressed;

            _throttle = up ? 1f : (down ? -1f : 0f);
            _brake = down && Vector3.Dot(_rb.velocity, transform.forward) > 0.5f ? 1f : 0f;
            _steer = (left ? -1f : 0f) + (right ? 1f : 0f);
            _handbrake = kb[Key.Space].isPressed ? 1f : 0f;
        }

        private void UpdateSteering(Keyboard kb, float dt)
        {
            // Steering authority falls off with speed so the car stays stable.
            float speed = _rb.velocity.magnitude;
            float falloff = Mathf.Clamp01(1f - speed / SteerFalloffSpeed);
            float target = _steer * MaxSteerAngle * Mathf.Lerp(0.30f, 1f, falloff);

            float rate = Mathf.Approximately(_steer, 0f) ? SteerReturn : SteerRate;
            _steerAngle = Mathf.MoveTowards(_steerAngle, target, rate * dt * Mathf.Max(0.25f, falloff));

            // Apply steering as yaw torque. Writing transform.localRotation here
            // would compound every frame and spin the car uncontrollably.
            float forwardSpeed = Vector3.Dot(_rb.velocity, transform.forward);
            float grip = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 3f);
            float yawTorque = _steerAngle * 0.22f * grip;
            _rb.AddTorque(transform.up * yawTorque, ForceMode.Force);
        }

        private void ApplyDrive(float dt)
        {
            Vector3 velocity = _rb.velocity;
            float forwardSpeed = Vector3.Dot(velocity, transform.forward);
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);

            if (_brake > 0f)
            {
                _rb.AddForce(-transform.forward * BrakeForce * _brake, ForceMode.Force);
            }
            else if (_throttle > 0f)
            {
                if (forwardSpeed < MaxSpeed)
                    _rb.AddForce(transform.forward * Acceleration * _throttle, ForceMode.Force);
            }
            else if (_throttle < 0f)
            {
                // Brake while moving forward, reverse once nearly stopped.
                if (forwardSpeed > 0.5f)
                    _rb.AddForce(-transform.forward * BrakeForce, ForceMode.Force);
                else if (forwardSpeed > -ReverseSpeed)
                    _rb.AddForce(-transform.forward * Acceleration * 0.6f, ForceMode.Force);
            }

            // Lateral friction: kill sideways velocity so it behaves like a car
            // rather than an ice-skating rink.
            Vector3 lateral = Vector3.ProjectOnPlane(velocity, transform.right);
            _rb.AddForce(-lateral * 8f, ForceMode.Force);

            if (_handbrake > 0f)
            {
                _rb.AddForce(-planar.normalized * BrakeForce * 0.7f, ForceMode.Force);
            }

            // Hard speed cap. Writing velocity wholesale every frame would
            // discard whatever the solver just did for gravity and collisions,
            // so only clamp and only on the horizontal plane.
            float speed = planar.magnitude;
            if (speed > MaxSpeed * 1.15f)
            {
                Vector3 clamped = Vector3.ClampMagnitude(planar, MaxSpeed * 1.15f);
                _rb.velocity = new Vector3(clamped.x, velocity.y, clamped.z);
            }

            // Keep the car upright so it cannot flip and become unrecoverable.
            Vector3 up = Vector3.up;
            Vector3 currentUp = transform.up;
            Vector3 correction = Vector3.Cross(currentUp, up);
            if (correction.sqrMagnitude > 0.0001f)
            {
                _rb.AddTorque(correction.normalized * 6f, ForceMode.Force);
            }
            _rb.angularVelocity = new Vector3(0f, _rb.angularVelocity.y * 0.92f, 0f);

            // Mathf.Clamp returns float; CurrentGear is int, so cast explicitly.
            int gear = 1 + (int)Mathf.Clamp(Mathf.Abs(forwardSpeed) / 9f, 0f, 4f);
            CurrentGear = forwardSpeed < -0.5f ? -1 : gear;
            EngineRpm = Mathf.Lerp(900f, 6800f, Mathf.Clamp01(Mathf.Abs(forwardSpeed) / MaxSpeed));
        }

        private void ApplySuspension(float dt)
        {
            // A subtle body roll sells the weight transfer without a real
            // wheel-by-wheel raycast rig.
            float speed = _rb.velocity.magnitude;
            float lateral = Vector3.Dot(_rb.velocity, transform.right);
            float targetRoll = Mathf.Clamp(-lateral / 20f, -1f, 1f) * BodyRoll;
            Quaternion target = Quaternion.Euler(targetRoll, 0f, 0f);
            _body.localRotation = Quaternion.Slerp(_body.localRotation, target, 6f * dt);
        }

        private void ApplyParkingDrag()
        {
            if (_rb == null) return;
            Vector3 v = _rb.velocity;
            v.x *= 0.90f;
            v.z *= 0.90f;
            if (v.y > 0.01f) v.y -= 2f * Time.deltaTime;
            _rb.velocity = v;
            _rb.angularVelocity *= 0.9f;
        }
    }
}