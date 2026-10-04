using UnityEngine;
using Megame.Entity;
using Megame.Network;
using UnityEngine.UI;
using TMPro;
using Grpc.Net.Client;
using Megame.Controllers;
using Megame.Data;
using Megame.Client;
using Megame.Vehicles;

namespace Megame.Client
{
    public class PlayerController
    {
        private GameClient _client;
        private GameObject _localPlayer;
        private CharacterController _characterController;
        private Animator _animator;
        private Rigidbody _rigidbody;

        // State
        private PlayerStateUpdate _serverState;
        private Vector3 _velocity;
        private bool _isGrounded;
        private float _stamina;
        private float _maxStamina = 100f;

        public PlayerController(GameClient client)
        {
            _client = client;
        }

        public void UpdatePlayerState(PlayerStateUpdate state)
        {
            _serverState = state;
            _stamina = state.Stamina;
        }

        public void OnLocalPlayerSpawned(GameObject playerObj)
        {
            _localPlayer = playerObj;
            _characterController = playerObj.GetComponent<CharacterController>();
            _animator = playerObj.GetComponent<Animator>();
            _rigidbody = playerObj.GetComponent<Rigidbody>();

            // Setup camera
            _client.cameraController.SetTarget(playerObj.transform);
        }

        public void Update()
        {
            if (_localPlayer == null) return;

            // Apply server reconciliation for local player
            // (Client-side prediction with server reconciliation)

            UpdateAnimation();
            UpdateStamina();
        }

        private void UpdateAnimation()
        {
            if (_animator == null) return;

            float speed = _velocity.magnitude;
            _animator.SetFloat("Speed", speed);
            _animator.SetBool("IsGrounded", _isGrounded);
            _animator.SetFloat("VerticalVelocity", _velocity.y);
        }

        private void UpdateStamina()
        {
            // Stamina regeneration handled by server
            // Client just displays
        }

        public void ApplyMovement(Vector3 moveInput, bool sprint, bool crouch, bool jump, float dt)
        {
            if (_characterController == null) return;

            Transform cameraTransform = _client.cameraController.transform;
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;

            Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;

            float moveSpeed = 5f;
            if (sprint && _stamina > 0)
            {
                moveSpeed = 9f;
                _stamina -= 15f * dt;
            }
            else if (crouch)
            {
                moveSpeed = 2f;
            }

            _velocity.x = moveDirection.x * moveSpeed;
            _velocity.z = moveDirection.z * moveSpeed;

            // Gravity
            if (_characterController.isGrounded)
            {
                _velocity.y = -0.5f;
                _isGrounded = true;
                if (jump)
                {
                    _velocity.y = 6f;
                    _isGrounded = false;
                }
            }
            else
            {
                _velocity.y -= 9.81f * dt;
                _isGrounded = false;
            }

            _characterController.Move(_velocity * dt);

            // Stamina regen
            if (!sprint && _stamina < _maxStamina)
            {
                _stamina = Mathf.Min(_maxStamina, _stamina + 10f * dt);
            }
        }

        public void SetCameraMode(CameraMode mode)
        {
            _client.cameraController.SetMode(mode);
        }
    }
}
