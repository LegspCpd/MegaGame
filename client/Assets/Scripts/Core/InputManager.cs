using UnityEngine;
using UnityEngine.InputSystem;
using Megame.Network;

namespace Megame.Client
{
    /// <summary>
    /// Handles all player input and sends to server
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Settings")]
        public float sendRate = 60f; // Times per second to send input
        public bool useNewInputSystem = true;

        private GameClient _client;
        private CameraController _camera;
        private PlayerController _playerController;
        private VehicleController _vehicleController;
        private WeaponController _weaponController;

        private float _sendTimer;
        private ClientInput _currentInput = new ClientInput();

        // Input state
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _jumpPressed;
        private bool _sprintHeld;
        private bool _crouchHeld;
        private bool _proneHeld;
        private bool _interactPressed;
        private bool _attackHeld;
        private bool _aimHeld;
        private bool _reloadPressed;
        private bool _weaponWheelHeld;
        private int _weaponSlot = -1;
        private bool _enterVehiclePressed;
        private bool _exitVehiclePressed;
        private Vector2 _vehicleInput;
        private bool _vehicleHandbrakeHeld;
        private bool _vehicleHornPressed;
        private bool _vehicleLightsPressed;
        private bool _vehicleSirenPressed;

        private void Awake() => Instance = this;

        public void Initialize(GameClient client)
        {
            _client = client;
            _camera = client.cameraController;
            _playerController = new PlayerController(client);
            _vehicleController = new VehicleController(client);
            _weaponController = new WeaponController(client);
            PlayerController.Instance = _playerController;
            VehicleController.Instance = _vehicleController;
            WeaponController.Instance = _weaponController;
        }

        /// <summary>The input frame assembled for the current tick, shared
        /// with the optimized client's send path.</summary>
        public ClientInput GetCurrentInput() => _currentInput;

        private void Update()
        {
            if (!_client.IsConnected) return;

            GatherInput();
            _sendTimer += Time.deltaTime;

            if (_sendTimer >= 1f / sendRate)
            {
                SendInput();
                _sendTimer = 0f;
            }
        }

        public void GatherInput()
        {
            if (useNewInputSystem)
            {
                GatherInputNewSystem();
            }
            else
            {
                GatherInputLegacy();
            }
        }

        private void GatherInputNewSystem()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var gamepad = Gamepad.current;

            // Movement
            _moveInput = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) _moveInput.y += 1;
                if (keyboard.sKey.isPressed) _moveInput.y -= 1;
                if (keyboard.dKey.isPressed) _moveInput.x += 1;
                if (keyboard.aKey.isPressed) _moveInput.x -= 1;
            }
            if (gamepad != null)
            {
                _moveInput += gamepad.leftStick.ReadValue();
            }
            _moveInput = Vector2.ClampMagnitude(_moveInput, 1f);

            // Look
            if (mouse != null)
            {
                _lookInput = mouse.delta.ReadValue() * 0.1f;
            }
            if (gamepad != null)
            {
                _lookInput += gamepad.rightStick.ReadValue() * 2f;
            }

            // Actions
            _jumpPressed = keyboard?.spaceKey.wasPressedThisFrame ?? false;
            _sprintHeld = keyboard?.leftShiftKey.isPressed ?? false;
            _crouchHeld = keyboard?.leftCtrlKey.isPressed ?? false;
            _proneHeld = keyboard?.zKey.isPressed ?? false;
            _interactPressed = keyboard?.eKey.wasPressedThisFrame ?? false;
            _attackHeld = mouse?.leftButton.isPressed ?? false;
            _aimHeld = mouse?.rightButton.isPressed ?? false;
            _reloadPressed = keyboard?.rKey.wasPressedThisFrame ?? false;
            _weaponWheelHeld = keyboard?.tabKey.isPressed ?? false;

            // Weapon slots
            for (int i = 0; i < 8; i++)
            {
                if (keyboard?.digitKeys[i].wasPressedThisFrame ?? false)
                {
                    _weaponSlot = i;
                    break;
                }
            }

            // Vehicle
            _enterVehiclePressed = keyboard?.fKey.wasPressedThisFrame ?? false;
            _exitVehiclePressed = keyboard?.fKey.wasPressedThisFrame ?? false;

            _vehicleInput = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) _vehicleInput.y += 1;
                if (keyboard.sKey.isPressed) _vehicleInput.y -= 1;
                if (keyboard.dKey.isPressed) _vehicleInput.x += 1;
                if (keyboard.aKey.isPressed) _vehicleInput.x -= 1;
            }

            _vehicleHandbrakeHeld = keyboard?.spaceKey.isPressed ?? false;
            _vehicleHornPressed = keyboard?.hKey.wasPressedThisFrame ?? false;
            _vehicleLightsPressed = keyboard?.lKey.wasPressedThisFrame ?? false;
            _vehicleSirenPressed = keyboard?.leftBracketKey.wasPressedThisFrame ?? false;
        }

        private void GatherInputLegacy()
        {
            // Legacy Input.GetAxis
            _moveInput.x = Input.GetAxis("Horizontal");
            _moveInput.y = Input.GetAxis("Vertical");
            _lookInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

            _jumpPressed = Input.GetButtonDown("Jump");
            _sprintHeld = Input.GetButton("Sprint");
            _crouchHeld = Input.GetButton("Crouch");
            _proneHeld = Input.GetButton("Prone");
            _interactPressed = Input.GetButtonDown("Interact");
            _attackHeld = Input.GetButton("Fire1");
            _aimHeld = Input.GetButton("Fire2");
            _reloadPressed = Input.GetButtonDown("Reload");
            _weaponWheelHeld = Input.GetButton("WeaponWheel");

            for (int i = 0; i < 8; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    _weaponSlot = i;
                    break;
                }
            }

            _enterVehiclePressed = Input.GetButtonDown("EnterVehicle");
            _exitVehiclePressed = Input.GetButtonDown("ExitVehicle");

            _vehicleInput.x = Input.GetAxis("VehicleSteer");
            _vehicleInput.y = Input.GetAxis("VehicleThrottle");
            _vehicleHandbrakeHeld = Input.GetButton("VehicleHandbrake");
            _vehicleHornPressed = Input.GetButtonDown("VehicleHorn");
            _vehicleLightsPressed = Input.GetButtonDown("VehicleLights");
            _vehicleSirenPressed = Input.GetButtonDown("VehicleSiren");
        }

        private void SendInput()
        {
            _currentInput.Tick = _client.ServerTick;
            _currentInput.TimestampMs = (ulong)(Time.time * 1000);
            _currentInput.Move = new Common.Vector2 { X = _moveInput.x, Y = _moveInput.y };
            _currentInput.Look = new Common.Vector2 { X = _lookInput.x, Y = _lookInput.y };
            _currentInput.Jump = _jumpPressed;
            _currentInput.Sprint = _sprintHeld;
            _currentInput.Crouch = _crouchHeld;
            _currentInput.Prone = _proneHeld;
            _currentInput.Interact = _interactPressed;
            _currentInput.Attack = _attackHeld;
            _currentInput.Aim = _aimHeld;
            _currentInput.Reload = _reloadPressed;
            _currentInput.WeaponWheel = _weaponWheelHeld;
            _currentInput.WeaponSlot = _weaponSlot;
            _currentInput.EnterVehicle = _enterVehiclePressed;
            _currentInput.ExitVehicle = _exitVehiclePressed;
            _currentInput.VehicleControl = new Common.Vector2 { X = _vehicleInput.x, Y = _vehicleInput.y };
            _currentInput.VehicleHandbrake = _vehicleHandbrakeHeld;
            _currentInput.VehicleHorn = _vehicleHornPressed;
            _currentInput.VehicleLights = _vehicleLightsPressed;
            _currentInput.VehicleSiren = _vehicleSirenPressed;

            _ = _client.SendInputAsync(_currentInput);

            // Reset one-frame inputs
            _jumpPressed = false;
            _interactPressed = false;
            _reloadPressed = false;
            _weaponSlot = -1;
            _enterVehiclePressed = false;
            _exitVehiclePressed = false;
            _vehicleHornPressed = false;
            _vehicleLightsPressed = false;
            _vehicleSirenPressed = false;
        }
    }
}