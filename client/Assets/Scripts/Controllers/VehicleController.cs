using UnityEngine;
using Megame.Entity;
using Megame.Network;

namespace Megame.Client
{
    public class VehicleController
    {
        public static VehicleController Instance { get; set; }

        private GameClient _client;
        private GameObject _currentVehicle;
        private Rigidbody _vehicleRigidbody;
        private WheelCollider[] _wheelColliders;
        private Transform[] _wheelTransforms;

        // Input
        private float _throttle;
        private float _steer;
        private bool _handbrake;
        private bool _engineOn;

        // Vehicle specs (from server)
        private VehicleSpecs _specs;

        public VehicleController(GameClient client)
        {
            _client = client;
        }

        public void UpdateVehicleState(VehicleStateUpdate state)
        {
            // Update visual wheels, sounds, etc.
            if (_currentVehicle != null)
            {
                UpdateWheelVisuals(state);
                UpdateEngineSound(state);
            }
        }

        public void EnterVehicle(GameObject vehicleObj, int seatIndex)
        {
            _currentVehicle = vehicleObj;
            _vehicleRigidbody = vehicleObj.GetComponent<Rigidbody>();
            _wheelColliders = vehicleObj.GetComponentsInChildren<WheelCollider>();
            _wheelTransforms = new Transform[_wheelColliders.Length];
            for (int i = 0; i < _wheelColliders.Length; i++)
            {
                _wheelTransforms[i] = _wheelColliders[i].transform;
            }

            // Get vehicle specs from model
            _specs = GetVehicleSpecs(vehicleObj.name);

            // Setup camera
            _client.cameraController.OnVehicleEnter(seatIndex == 0);

            // Disable player controller
            // Enable vehicle input
            _engineOn = true;
        }

        public void ExitVehicle()
        {
            _currentVehicle = null;
            _vehicleRigidbody = null;
            _wheelColliders = null;
            _wheelTransforms = null;

            _client.cameraController.OnVehicleExit();
        }

        public void UpdateInput(Vector2 input, bool handbrake, bool horn, bool lights, bool siren)
        {
            _throttle = input.y;
            _steer = input.x;
            _handbrake = handbrake;

            if (horn) HonkHorn();
            if (lights) ToggleLights();
            if (siren) ToggleSiren();
        }

        public void FixedUpdate()
        {
            if (_vehicleRigidbody == null) return;

            ApplyPhysics();
        }

        private void ApplyPhysics()
        {
            // Engine
            float engineTorque = 0f;
            if (_engineOn && _throttle != 0)
            {
                engineTorque = _specs.MaxTorque * Mathf.Abs(_throttle) * GetGearRatio();
            }

            // Apply torque to driven wheels
            foreach (var wheel in _wheelColliders)
            {
                if (IsDrivenWheel(wheel))
                {
                    wheel.motorTorque = engineTorque;
                }

                // Steering
                if (IsFrontWheel(wheel))
                {
                    wheel.steerAngle = _steer * _specs.MaxSteerAngle;
                }

                // Braking
                if (_handbrake && IsRearWheel(wheel))
                {
                    wheel.brakeTorque = _specs.BrakeForce * 2f;
                }
                else if (_throttle < 0)
                {
                    wheel.brakeTorque = _specs.BrakeForce * Mathf.Abs(_throttle);
                }
                else
                {
                    wheel.brakeTorque = 0f;
                }
            }

            // Downforce
            float speed = _vehicleRigidbody.velocity.magnitude;
            Vector3 downforce = -_vehicleRigidbody.transform.up * speed * speed * _specs.DownforceCoefficient;
            _vehicleRigidbody.AddForce(downforce);

            // Update wheel visuals
            UpdateWheelPositions();
        }

        private void UpdateWheelVisuals(VehicleStateUpdate state)
        {
            if (_wheelColliders == null || _wheelTransforms == null) return;

            for (int i = 0; i < _wheelColliders.Length && i < state.Wheels.Count; i++)
            {
                var wheelState = state.Wheels[i];
                _wheelColliders[i].steerAngle = wheelState.SteerAngle * Mathf.Rad2Deg;

                // Visual rotation
                _wheelTransforms[i].Rotate(Vector3.right, wheelState.Rotation * Mathf.Rad2Deg * Time.deltaTime);
            }
        }

        private void UpdateWheelPositions()
        {
            if (_wheelColliders == null || _wheelTransforms == null) return;

            for (int i = 0; i < _wheelColliders.Length; i++)
            {
                _wheelColliders[i].GetWorldPose(out Vector3 pos, out Quaternion rot);
                _wheelTransforms[i].SetPositionAndRotation(pos, rot);
            }
        }

        private void UpdateEngineSound(VehicleStateUpdate state)
        {
            // Update engine audio pitch/volume based on RPM
            var audioSource = _currentVehicle?.GetComponent<AudioSource>();
            if (audioSource != null)
            {
                float rpmNormalized = state.Rpm / 7000f;
                audioSource.pitch = Mathf.Lerp(0.8f, 2f, rpmNormalized);
            }
        }

        private float GetGearRatio()
        {
            // Simplified gear ratio based on speed
            float speed = _vehicleRigidbody.velocity.magnitude;
            if (speed < 10) return _specs.GearRatios[0];
            if (speed < 20) return _specs.GearRatios[1];
            if (speed < 35) return _specs.GearRatios[2];
            if (speed < 50) return _specs.GearRatios[3];
            return _specs.GearRatios[4];
        }

        private bool IsDrivenWheel(WheelCollider wheel)
        {
            // Based on vehicle specs (FWD/RWD/AWD)
            return true; // Simplified
        }

        private bool IsFrontWheel(WheelCollider wheel)
        {
            return wheel.transform.localPosition.z > 0;
        }

        private bool IsRearWheel(WheelCollider wheel)
        {
            return wheel.transform.localPosition.z < 0;
        }

        private void HonkHorn()
        {
            var audio = _currentVehicle?.GetComponent<AudioSource>();
            if (audio != null) audio.PlayOneShot(_specs.HornClip);
        }

        private void ToggleLights()
        {
            // Toggle light gameobjects
        }

        private void ToggleSiren()
        {
            // Toggle siren audio/lights
        }

        private VehicleSpecs GetVehicleSpecs(string modelName)
        {
            // Would load from ScriptableObject database
            return new VehicleSpecs
            {
                MaxTorque = 400f,
                MaxSteerAngle = 35f,
                BrakeForce = 3000f,
                GearRatios = new float[] { 3.5f, 2.2f, 1.5f, 1.1f, 0.9f, 0.7f },
                DownforceCoefficient = 0.1f,
                HornClip = null
            };
        }

        public bool IsInVehicle => _currentVehicle != null;
        public GameObject CurrentVehicle => _currentVehicle;
    }

    public class VehicleSpecs
    {
        public float MaxTorque;
        public float MaxSteerAngle;
        public float BrakeForce;
        public float[] GearRatios;
        public float DownforceCoefficient;
        public AudioClip HornClip;
    }
}