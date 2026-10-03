using UnityEngine;
using Megame.Entity;

namespace Megame.Client
{
    /// <summary>
    /// Advanced camera controller supporting third-person, first-person, and vehicle cameras
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("References")]
        public Transform target;
        public Camera mainCamera;

        [Header("Third Person Settings")]
        public float thirdPersonDistance = 5f;
        public float thirdPersonHeight = 1.5f;
        public float thirdPersonShoulderOffset = 0.5f;
        public float thirdPersonMinDistance = 1f;
        public float thirdPersonMaxDistance = 10f;
        public float zoomSpeed = 5f;
        public LayerMask collisionMask;

        [Header("First Person Settings")]
        public float fovFirstPerson = 70f;
        public float fovThirdPerson = 60f;
        public Vector3 firstPersonOffset = new Vector3(0, 1.6f, 0.1f);
        public Vector3 aimOffset = new Vector3(0.1f, 1.55f, 0.15f);
        public float aimFOV = 40f;

        [Header("Vehicle Camera")]
        public float vehicleDistance = 7f;
        public float vehicleHeight = 2f;
        public float vehicleLookAhead = 3f;

        [Header("General")]
        public float rotationSpeed = 10f;
        public float smoothing = 10f;
        public float collisionRadius = 0.3f;

        private CameraMode _currentMode = CameraMode.ThirdPerson;
        private float _currentDistance;
        private float _targetDistance;
        private float _yaw;
        private float _pitch;
        private Vector3 _currentOffset;
        private Vector3 _targetOffset;
        private Quaternion _targetRotation;
        private bool _isAiming = false;

        // Input
        private float _mouseX;
        private float _mouseY;

        public CameraMode CurrentMode => _currentMode;

        private void Awake()
        {
            if (mainCamera == null)
                mainCamera = GetComponent<Camera>();

            _currentDistance = thirdPersonDistance;
            _targetDistance = thirdPersonDistance;
            _currentOffset = new Vector3(thirdPersonShoulderOffset, thirdPersonHeight, -thirdPersonDistance);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            HandleInput();
            UpdateCamera();
        }

        private void HandleInput()
        {
            _mouseX += Input.GetAxis("Mouse X") * rotationSpeed;
            _mouseY -= Input.GetAxis("Mouse Y") * rotationSpeed;
            _pitch = Mathf.Clamp(_mouseY, -80f, 80f);

            // Zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                _targetDistance = Mathf.Clamp(_targetDistance - scroll * zoomSpeed, thirdPersonMinDistance, thirdPersonMaxDistance);
            }

            // Camera mode switching
            if (Input.GetKeyDown(KeyCode.V))
            {
                CycleCameraMode();
            }

            // Aim toggle
            _isAiming = Input.GetMouseButton(1); // Right click
        }

        public void SetMode(CameraMode mode)
        {
            _currentMode = mode;
            _targetDistance = GetDefaultDistance(mode);
            _mouseX = target.eulerAngles.y;
            _mouseY = target.eulerAngles.x;
        }

        public void CycleCameraMode()
        {
            switch (_currentMode)
            {
                case CameraMode.ThirdPerson:
                    SetMode(CameraMode.FirstPerson);
                    break;
                case CameraMode.FirstPerson:
                    SetMode(CameraMode.ThirdPerson);
                    break;
                case CameraMode.VehicleThird:
                    SetMode(CameraMode.VehicleFirst);
                    break;
                case CameraMode.VehicleFirst:
                    SetMode(CameraMode.VehicleThird);
                    break;
            }
        }

        private float GetDefaultDistance(CameraMode mode)
        {
            switch (mode)
            {
                case CameraMode.ThirdPerson:
                case CameraMode.VehicleThird:
                    return thirdPersonDistance;
                case CameraMode.FirstPerson:
                case CameraMode.VehicleFirst:
                case CameraMode.Aim:
                    return 0f;
                default:
                    return thirdPersonDistance;
            }
        }

        private void UpdateCamera()
        {
            switch (_currentMode)
            {
                case CameraMode.ThirdPerson:
                case CameraMode.Aim:
                    UpdateThirdPerson();
                    break;
                case CameraMode.FirstPerson:
                    UpdateFirstPerson();
                    break;
                case CameraMode.VehicleThird:
                    UpdateVehicleThirdPerson();
                    break;
                case CameraMode.VehicleFirst:
                    UpdateVehicleFirstPerson();
                    break;
            }

            // Apply collision avoidance
            if (_currentMode != CameraMode.FirstPerson && _currentMode != CameraMode.VehicleFirst)
            {
                AvoidCollision();
            }
        }

        private void UpdateThirdPerson()
        {
            _yaw = _mouseX;
            _targetRotation = Quaternion.Euler(_pitch, _yaw, 0);

            // Calculate target position
            Vector3 targetPos = target.position + Vector3.up * thirdPersonHeight;
            Vector3 offset = _targetRotation * new Vector3(_isAiming ? 0.3f : thirdPersonShoulderOffset, 0, -_currentDistance);
            Vector3 desiredPos = targetPos + offset;

            // Shoulder swap when aiming
            if (_isAiming)
            {
                _currentMode = CameraMode.Aim;
                mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, aimFOV, Time.deltaTime * 10f);
                _targetOffset = Vector3.Lerp(_targetOffset, aimOffset, Time.deltaTime * 10f);
            }
            else if (_currentMode == CameraMode.Aim)
            {
                _currentMode = CameraMode.ThirdPerson;
                mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, fovThirdPerson, Time.deltaTime * 10f);
                _targetOffset = Vector3.Lerp(_targetOffset, new Vector3(thirdPersonShoulderOffset, thirdPersonHeight, 0), Time.deltaTime * 10f);
            }

            // Smooth position
            transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * smoothing);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * smoothing);
        }

        private void UpdateFirstPerson()
        {
            // Camera follows head bone or fixed offset
            _yaw = _mouseX;
            _pitch = Mathf.Clamp(_pitch, -85f, 85f);

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0);
            transform.position = target.position + transform.rotation * firstPersonOffset;

            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, fovFirstPerson, Time.deltaTime * 10f);

            // Rotate player body
            target.rotation = Quaternion.Euler(0, _yaw, 0);
        }

        private void UpdateVehicleThirdPerson()
        {
            if (target.GetComponent<Rigidbody>() == null) return;

            Vector3 velocity = target.GetComponent<Rigidbody>().velocity;
            float speed = velocity.magnitude;

            // Look ahead based on speed
            float lookAhead = Mathf.Lerp(0, vehicleLookAhead, speed / 30f);
            Vector3 lookPoint = target.position + velocity.normalized * lookAhead;

            // Camera orbits vehicle
            _yaw = Mathf.LerpAngle(_yaw, target.eulerAngles.y, Time.deltaTime * 3f);
            _targetRotation = Quaternion.Euler(_pitch, _yaw, 0);

            Vector3 offset = _targetRotation * new Vector3(0, vehicleHeight, -vehicleDistance);
            Vector3 desiredPos = target.position + offset;

            // Collision check
            RaycastHit hit;
            if (Physics.SphereCast(target.position + Vector3.up * vehicleHeight, collisionRadius, -offset.normalized, out hit, vehicleDistance, collisionMask))
            {
                desiredPos = hit.point + hit.normal * collisionRadius;
            }

            transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * smoothing);
            transform.LookAt(lookPoint);
        }

        private void UpdateVehicleFirstPerson()
        {
            // Inside vehicle - fixed to driver's head
            Transform driverHead = target.Find("DriverHead");
            if (driverHead != null)
            {
                transform.SetPositionAndRotation(driverHead.position, driverHead.rotation);
            }
            else
            {
                transform.position = target.position + Vector3.up * 1.2f;
                transform.rotation = target.rotation;
            }

            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, fovFirstPerson, Time.deltaTime * 10f);
        }

        private void AvoidCollision()
        {
            Vector3 targetPos = target.position + Vector3.up * thirdPersonHeight;
            Vector3 direction = (transform.position - targetPos).normalized;
            float distance = Vector3.Distance(transform.position, targetPos);

            if (Physics.SphereCast(targetPos, collisionRadius, direction, out RaycastHit hit, distance, collisionMask))
            {
                transform.position = hit.point + hit.normal * collisionRadius;
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                _mouseX = target.eulerAngles.y;
                _mouseY = target.eulerAngles.x;
            }
        }

        public void OnVehicleEnter(bool isDriver)
        {
            if (isDriver)
            {
                SetMode(CameraMode.VehicleThird);
            }
            else
            {
                SetMode(CameraMode.VehicleThird);
            }
        }

        public void OnVehicleExit()
        {
            SetMode(CameraMode.ThirdPerson);
        }

        public void OnWeaponAim(bool aiming)
        {
            _isAiming = aiming;
            if (aiming && _currentMode == CameraMode.ThirdPerson)
            {
                _currentMode = CameraMode.Aim;
            }
            else if (!aiming && _currentMode == CameraMode.Aim)
            {
                _currentMode = CameraMode.ThirdPerson;
            }
        }
    }
}