using UnityEngine;
using Megame.Entity;
using Megame.Network;

namespace Megame.Client
{
    public class WeaponController
    {
        private GameClient _client;
        private GameObject _currentWeapon;
        private GameObject _viewModel;
        private Animator _viewModelAnimator;
        private Transform _cameraTransform;
        private Transform _firstPersonAnchor;
        private Transform _aimAnchor;

        // State
        private WeaponStateUpdate _serverState;
        private WeaponDefinition _definition;
        private WeaponAttachments _attachments;

        // Firing
        private float _nextFireTime;
        private float _recoilPitch;
        private float _recoilYaw;
        private bool _isReloading;
        private float _reloadProgress;

        // Aiming
        private bool _isAiming;
        private float _aimProgress;
        private float _aimSpeed = 10f;

        public WeaponController(GameClient client)
        {
            _client = client;
            _cameraTransform = client.cameraController.transform;
        }

        public void UpdateWeaponState(WeaponStateUpdate state)
        {
            _serverState = state;

            // Sync ammo
            // Sync heat
            // Sync reloading state
            _isReloading = state.IsReloading;
            _reloadProgress = state.ReloadProgress;
        }

        public void EquipWeapon(GameObject weaponPrefab, WeaponDefinition definition)
        {
            // Destroy current
            if (_currentWeapon != null)
            {
                Destroy(_currentWeapon);
            }
            if (_viewModel != null)
            {
                Destroy(_viewModel);
            }

            // Spawn world model (on character)
            _currentWeapon = Instantiate(weaponPrefab);
            _currentWeapon.name = "CurrentWeapon";

            // Spawn view model (first person)
            if (definition.FirstPersonViewModel != null)
            {
                _viewModel = Instantiate(definition.FirstPersonViewModel);
                _viewModel.name = "ViewModel";
                _viewModelAnimator = _viewModel.GetComponent<Animator>();

                // Position at camera
                PositionViewModel();
            }

            _definition = definition;
            _nextFireTime = 0;
            _recoilPitch = 0;
            _recoilYaw = 0;
        }

        public void UnequipWeapon()
        {
            if (_currentWeapon != null)
            {
                Destroy(_currentWeapon);
                _currentWeapon = null;
            }
            if (_viewModel != null)
            {
                Destroy(_viewModel);
                _viewModel = null;
            }
            _definition = null;
        }

        public void Update()
        {
            if (_viewModel == null) return;

            // Update view model position
            PositionViewModel();

            // Handle aim transition
            UpdateAim();

            // Handle recoil recovery
            UpdateRecoil();

            // Handle reloading animation
            if (_isReloading)
            {
                UpdateReload();
            }
        }

        private void PositionViewModel()
        {
            if (_viewModel == null || _definition == null) return;

            Vector3 targetPos;
            Quaternion targetRot;

            if (_isAiming && _aimAnchor != null)
            {
                targetPos = _aimAnchor.position;
                targetRot = _aimAnchor.rotation;
            }
            else if (_firstPersonAnchor != null)
            {
                targetPos = _firstPersonAnchor.position;
                targetRot = _firstPersonAnchor.rotation;
            }
            else
            {
                // Default position relative to camera
                targetPos = _cameraTransform.position + _cameraTransform.rotation * _definition.FirstPersonOffset;
                targetRot = _cameraTransform.rotation * _definition.FirstPersonRotation;
            }

            // Apply recoil offset
            Quaternion recoilRot = Quaternion.Euler(-_recoilPitch, _recoilYaw, 0);
            targetRot *= recoilRot;

            _viewModel.transform.SetPositionAndRotation(
                Vector3.Lerp(_viewModel.transform.position, targetPos, Time.deltaTime * 20f),
                Quaternion.Slerp(_viewModel.transform.rotation, targetRot, Time.deltaTime * 20f)
            );
        }

        private void UpdateAim()
        {
            _isAiming = _client.cameraController.CurrentMode == CameraMode.Aim;
            _aimProgress = Mathf.MoveTowards(_aimProgress, _isAiming ? 1f : 0f, Time.deltaTime * _aimSpeed);

            // Notify camera
            _client.cameraController.OnWeaponAim(_isAiming);
        }

        private void UpdateRecoil()
        {
            _recoilPitch = Mathf.Lerp(_recoilPitch, 0, Time.deltaTime * _definition.RecoilRecovery);
            _recoilYaw = Mathf.Lerp(_recoilYaw, 0, Time.deltaTime * _definition.RecoilRecovery);
        }

        private void UpdateReload()
        {
            // Animation handled by animator
            if (_viewModelAnimator != null)
            {
                _viewModelAnimator.SetFloat("ReloadProgress", _reloadProgress);
            }
        }

        public void TryFire()
        {
            if (_definition == null) return;
            if (_isReloading) return;
            if (_serverState.AmmoInClip <= 0) return;
            if (Time.time < _nextFireTime) return;

            // Check fire mode
            bool canFire = false;
            switch (_definition.FireMode)
            {
                case FireMode.Semi:
                    canFire = Input.GetButtonDown("Fire1");
                    break;
                case FireMode.Auto:
                    canFire = Input.GetButton("Fire1");
                    break;
                case FireMode.Burst:
                    // Handle burst logic
                    canFire = Input.GetButtonDown("Fire1");
                    break;
            }

            if (!canFire) return;

            // Calculate fire rate delay
            float fireInterval = 60f / _definition.FireRateRPM;
            _nextFireTime = Time.time + fireInterval;

            // Apply recoil
            ApplyRecoil();

            // Play fire animation
            if (_viewModelAnimator != null)
            {
                _viewModelAnimator.SetTrigger("Fire");
            }

            // Muzzle flash
            PlayMuzzleFlash();

            // Play sound
            PlayFireSound();

            // Send fire RPC to server
            FireRPC();
        }

        private void ApplyRecoil()
        {
            _recoilPitch += _definition.RecoilVertical * (Random.value - 0.5f) * 0.5f;
            _recoilYaw += _definition.RecoilHorizontal * (Random.value - 0.5f);
        }

        private void PlayMuzzleFlash()
        {
            // Spawn muzzle flash particle
        }

        private void PlayFireSound()
        {
            var audio = _viewModel?.GetComponent<AudioSource>();
            if (audio != null && _definition.FireSound != null)
            {
                audio.PlayOneShot(_definition.FireSound);
            }
        }

        private async void FireRPC()
        {
            var startPos = _cameraTransform.position;
            var direction = _cameraTransform.forward;

            await _client.SendRPCAsync("FireWeapon", new
            {
                weaponId = _serverState?.EntityId,
                startPos = startPos.ToProto(),
                direction = direction.ToProto()
            });
        }

        public void TryReload()
        {
            if (_serverState.AmmoReserve <= 0) return;
            if (_serverState.AmmoInClip >= _definition.MaxClipSize) return;
            if (_isReloading) return;

            _isReloading = true;
            _reloadProgress = 0;

            if (_viewModelAnimator != null)
            {
                bool isTactical = _serverState.AmmoInClip > 0;
                _viewModelAnimator.SetTrigger(isTactical ? "ReloadTactical" : "ReloadEmpty");
            }

            // Send reload RPC
            _ = _client.SendRPCAsync("ReloadWeapon", new { weaponId = _serverState.EntityId });
        }

        public void SwitchWeapon(int slot)
        {
            // Send weapon switch RPC
            _ = _client.SendRPCAsync("SwitchWeapon", new { slot });
        }

        public void AttachAccessory(string slotId, string attachmentId)
        {
            // Apply attachment to view model
            // Update weapon stats
            _ = _client.SendRPCAsync("AttachWeaponAccessory", new { slotId, attachmentId });
        }

        public bool HasWeapon => _currentWeapon != null;
        public int AmmoInClip => _serverState?.AmmoInClip ?? 0;
        public int AmmoReserve => _serverState?.AmmoReserve ?? 0;
        public bool IsReloading => _isReloading;
        public float ReloadProgress => _reloadProgress;
    }

    public class WeaponDefinition
    {
        public string ModelId;
        public string DisplayName;
        public string WeaponClass;
        public GameObject WorldModel;
        public GameObject FirstPersonViewModel;
        public Vector3 FirstPersonOffset;
        public Quaternion FirstPersonRotation;
        public Vector3 AimOffset;
        public Quaternion AimRotation;

        public float Damage;
        public int Pellets;
        public float Range;
        public float AccuracyHip;
        public float AccuracyADS;
        public float FireRateRPM;
        public float MuzzleVelocity;
        public int MaxClipSize;
        public int MaxReserve;

        public FireMode FireMode;
        public float RecoilVertical;
        public float RecoilHorizontal;
        public float RecoilRecovery;
        public float ReloadTimeTactical;
        public float ReloadTimeEmpty;

        public AudioClip FireSound;
        public AudioClip DryFireSound;
        public AudioClip ReloadSound;
    }

    public enum FireMode
    {
        Semi,
        Auto,
        Burst,
        Bolt,
        Pump
    }
}