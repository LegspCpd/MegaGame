using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using Megame.Controllers;
using Megame.Data;
using Megame.Client;
using Megame.Vehicles;
using Megame.Network;

namespace Megame.Client
{
    /// <summary>
    /// Burst-compiled ballistics simulation
    /// </summary>
    [BurstCompile]
    public struct BallisticsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ProjectileData> projectiles;
        [ReadOnly] public float dt;
        [ReadOnly] public float3 gravity;
        [ReadOnly] public float airDensity;
        public NativeArray<ProjectileData> output;

        public void Execute(int index)
        {
            var p = projectiles[index];
            if (!p.IsActive) { output[index] = p; return; }

            // Forces
            float3 velocity = p.Velocity;
            float speed = math.length(velocity);
            if (speed < 0.1f) { p.IsActive = false; output[index] = p; return; }

            float3 dir = velocity / speed;

            // Drag: F = 0.5 * rho * v^2 * Cd * A
            float dragForce = 0.5f * airDensity * speed * speed * p.DragCoefficient * p.CrossSectionalArea;
            float3 dragAccel = -dir * dragForce / p.Mass;

            // Gravity
            float3 gravityAccel = gravity;

            // Total acceleration
            float3 accel = dragAccel + gravityAccel;

            // Verlet integration (more stable for projectiles)
            p.Position += velocity * dt + 0.5f * accel * dt * dt;
            p.Velocity += accel * dt;

            // Lifetime
            p.LifeTime -= dt;
            if (p.LifeTime <= 0f) p.IsActive = false;

            // Distance traveled
            p.DistanceTraveled += speed * dt;
            if (p.DistanceTraveled > p.MaxRange) p.IsActive = false;

            output[index] = p;
        }
    }

    [BurstCompile]
    public struct RecoilPatternJob : IJob
    {
        [ReadOnly] public NativeArray<RecoilSample> pattern;
        [ReadOnly] public int shotsFired;
        [ReadOnly] public float recoveryRate;
        [ReadOnly] public float dt;
        public float2 currentRecoil;

        public void Execute()
        {
            if (shotsFired > 0 && shotsFired <= pattern.Length)
            {
                // Add recoil impulse
                currentRecoil += pattern[shotsFired - 1].Impulse;
            }

            // Recovery
            float recovery = recoveryRate * dt;
            currentRecoil = math.max(float2(0), math.abs(currentRecoil) - recovery) * math.sign(currentRecoil);
        }
    }

    public struct ProjectileData
    {
        public bool IsActive;
        public float3 Position;
        public float3 Velocity;
        public float Mass;
        public float DragCoefficient;
        public float CrossSectionalArea;
        public float LifeTime;
        public float MaxRange;
        public float DistanceTraveled;
        public int Damage;
        public uint OwnerId;
        public float3 HitNormal;
        public bool HasHit;
    }

    public struct RecoilSample
    {
        public float2 Impulse; // vertical, horizontal
    }

    // ============================================================================
    // Optimized Weapon Controller
    // ============================================================================

    public class OptimizedWeaponController : MonoBehaviour
    {
        [Header("Weapon Definition")]
        public WeaponDefinitionData Definition;

        [Header("References")]
        public Transform MuzzlePoint;
        public Transform EjectionPort;
        public GameObject ViewModel;
        public Animator ViewModelAnimator;
        public Camera FPSCamera;

        [Header("Ballistics")]
        public float BulletMass = 0.008f; // 8g
        public float BulletDiameter = 0.00762f; // 7.62mm
        public float DragCoefficient = 0.295f;
        public float MuzzleVelocity = 715f;
        public float MaxRange = 800f;

        [Header("Recoil")]
        public AnimationCurve VerticalRecoilCurve;
        public AnimationCurve HorizontalRecoilCurve;
        public float RecoilRecoveryRate = 30f; // deg/s
        public float MaxVerticalRecoil = 15f;
        public float MaxHorizontalRecoil = 5f;

        [Header("Spread")]
        public float BaseSpreadHip = 2f; // degrees
        public float BaseSpreadADS = 0.5f;
        public float SpreadIncreasePerShot = 0.3f;
        public float SpreadRecoveryRate = 10f; // deg/s

        [Header("Heat")]
        public float HeatPerShot = 2f;
        public float HeatDissipationRate = 10f; // per second
        public float OverheatThreshold = 100f;
        public float JamChanceAtMaxHeat = 0.1f;

        [Header("Attachments")]
        public WeaponAttachmentSlot[] AttachmentSlots;
        public Dictionary<string, AttachmentData> EquippedAttachments = new Dictionary<string, AttachmentData>();

        // State
        private int _ammoInClip;
        private int _ammoReserve;
        private bool _isReloading;
        private float _reloadProgress;
        private float _nextFireTime;
        private float _heat;
        private int _shotsFiredThisMag;
        private float2 _currentRecoil;
        private float _currentSpread;
        private bool _isJammed;
        private float _conditionDurability = 1f;
        private float _conditionDirt;
        private float _conditionCarbon;

        // ViewModel offset
        private Vector3 _viewModelPos;
        private Quaternion _viewModelRot;
        private Vector3 _aimPos;
        private Quaternion _aimRot;
        private float _aimProgress;

        // Ballistics job
        private NativeArray<ProjectileData> _projectileData;
        private NativeArray<ProjectileData> _projectileOutput;
        private JobHandle _ballisticsJobHandle;
        private List<ActiveProjectile> _activeProjectiles = new List<ActiveProjectile>();

        // Pool
        private ObjectPool<ProjectileVisual> _projectileVisualPool;

        public int AmmoInClip => _ammoInClip;
        public int AmmoReserve => _ammoReserve;
        public bool IsReloading => _isReloading;
        public float ReloadProgress => _reloadProgress;
        public float Heat => _heat;
        public bool IsJammed => _isJammed;
        public float AimProgress => _aimProgress;

        private void Awake()
        {
            _ammoInClip = Definition.MaxClipSize;
            _ammoReserve = Definition.MaxReserveAmmo;

            // Initialize recoil pattern from curves
            InitializeRecoilPattern();

            // Setup view model offsets
            _viewModelPos = Definition.FirstPersonOffset;
            _viewModelRot = Definition.FirstPersonRotation;
            _aimPos = Definition.AimOffset;
            _aimRot = Definition.AimRotation;

            // Initialize projectile arrays
            int maxProjectiles = 64;
            _projectileData = new NativeArray<ProjectileData>(maxProjectiles, Allocator.Persistent);
            _projectileOutput = new NativeArray<ProjectileData>(maxProjectiles, Allocator.Persistent);

            // Projectile visual pool
            _projectileVisualPool = new ObjectPool<ProjectileVisual>(32, () =>
            {
                var go = new GameObject("Projectile");
                go.AddComponent<TrailRenderer>();
                return go.AddComponent<ProjectileVisual>();
            });
        }

        private void OnDestroy()
        {
            _ballisticsJobHandle.Complete();
            _projectileData.Dispose();
            _projectileOutput.Dispose();
            _projectileVisualPool.Dispose();
        }

        private void InitializeRecoilPattern()
        {
            // Bake curves into array for Burst
            int patternLength = Definition.MaxClipSize;
            // Would populate from curves
        }

        private void Update()
        {
            if (Definition == null) return;

            // Update heat
            if (_heat > 0)
            {
                _heat = math.max(0, _heat - Definition.HeatDissipationRate * Time.deltaTime);
                if (_heat < OverheatThreshold * 0.5f && _isJammed)
                {
                    _isJammed = false;
                }
            }

            // Update spread recovery
            if (_currentSpread > 0)
            {
                _currentSpread = math.max(0, _currentSpread - Definition.SpreadRecoveryRate * Time.deltaTime);
            }

            // Update recoil recovery
            if (math.length(_currentRecoil) > 0)
            {
                float recovery = RecoilRecoveryRate * Time.deltaTime;
                _currentRecoil = math.max(float2(0), math.abs(_currentRecoil) - recovery) * math.sign(_currentRecoil);
            }

            // Update aim progress
            bool aiming = Input.GetMouseButton(1) && !_isReloading;
            _aimProgress = math.lerp(_aimProgress, aiming ? 1f : 0f, Time.deltaTime * 15f);

            // Update view model position
            UpdateViewModel();

            // Update active projectiles
            UpdateProjectiles();

            // Handle firing
            HandleFireInput();

            // Handle reload
            if (Input.GetKeyDown(KeyCode.R) && !_isReloading)
                StartReload();
        }

        private void UpdateViewModel()
        {
            if (ViewModel == null) return;

            Vector3 targetPos = Vector3.Lerp(_viewModelPos, _aimPos, _aimProgress);
            Quaternion targetRot = Quaternion.Slerp(_viewModelRot, _aimRot, _aimProgress);

            // Apply recoil offset
            Vector3 recoilOffset = new Vector3(_currentRecoil.y, -_currentRecoil.x, 0) * 0.01f;
            Quaternion recoilRot = Quaternion.Euler(-_currentRecoil.x, _currentRecoil.y, 0);

            targetPos += FPSCamera.transform.rotation * recoilOffset;
            targetRot = FPSCamera.transform.rotation * targetRot * recoilRot;

            ViewModel.transform.localPosition = Vector3.Lerp(ViewModel.transform.localPosition, targetPos, Time.deltaTime * 20f);
            ViewModel.transform.localRotation = Quaternion.Slerp(ViewModel.transform.localRotation, targetRot, Time.deltaTime * 20f);
        }

        private void HandleFireInput()
        {
            bool fireInput = false;
            switch (Definition.FireMode)
            {
                case WeaponFireMode.Semi:
                    fireInput = Input.GetMouseButtonDown(0);
                    break;
                case WeaponFireMode.Auto:
                    fireInput = Input.GetMouseButton(0);
                    break;
                case WeaponFireMode.Burst:
                    fireInput = Input.GetMouseButtonDown(0); // Burst handled internally
                    break;
            }

            if (fireInput && CanFire())
            {
                Fire();
            }
        }

        private bool CanFire()
        {
            if (_isReloading) return false;
            if (_ammoInClip <= 0) return false;
            if (Time.time < _nextFireTime) return false;
            if (_isJammed) return false;
            if (_heat >= OverheatThreshold) return false;
            return true;
        }

        private void Fire()
        {
            // Calculate fire interval
            float fireInterval = 60f / Definition.FireRateRPM;
            _nextFireTime = Time.time + fireInterval;

            // Consume ammo
            _ammoInClip--;
            _shotsFiredThisMag++;

            // Heat
            _heat += HeatPerShot;
            if (_heat >= OverheatThreshold && Random.value < JamChanceAtMaxHeat)
            {
                _isJammed = true;
            }

            // Apply recoil
            ApplyRecoil();

            // Apply spread
            ApplySpread();

            // Play animation
            if (ViewModelAnimator != null)
                ViewModelAnimator.SetTrigger("Fire");

            // Muzzle flash
            PlayMuzzleFlash();

            // Sound
            PlayFireSound();

            // Spawn projectile (client-side prediction)
            SpawnProjectile();

            // Send to server
            SendFireRPC();

            // Condition degradation
            DegradeCondition();
        }

        private void ApplyRecoil()
        {
            // Get recoil from pattern or curves
            float verticalImpulse = Definition.RecoilVertical * (1f + Random.Range(-0.2f, 0.2f));
            float horizontalImpulse = Definition.RecoilHorizontal * Random.Range(-1f, 1f);

            // Attachment modifiers
            if (EquippedAttachments.TryGetValue("muzzle", out var muzzle))
                verticalImpulse *= muzzle.VerticalRecoilMultiplier;
            if (EquippedAttachments.TryGetValue("underbarrel", out var grip))
                verticalImpulse *= grip.VerticalRecoilMultiplier;

            _currentRecoil.x += verticalImpulse;
            _currentRecoil.y += horizontalImpulse;

            // Clamp
            _currentRecoil.x = math.clamp(_currentRecoil.x, -MaxVerticalRecoil, MaxVerticalRecoil);
            _currentRecoil.y = math.clamp(_currentRecoil.y, -MaxHorizontalRecoil, MaxHorizontalRecoil);

            // Apply to camera
            FPSCamera.transform.Rotate(-verticalImpulse * 0.5f, horizontalImpulse * 0.5f, 0);
        }

        private void ApplySpread()
        {
            float spread = _aimProgress > 0.5f ? BaseSpreadADS : BaseSpreadHip;
            spread += _shotsFiredThisMag * SpreadIncreasePerShot;

            // Attachment modifiers
            if (EquippedAttachments.TryGetValue("underbarrel", out var grip))
                spread *= grip.SpreadMultiplier;

            _currentSpread = math.min(spread, 15f); // cap
        }

        private void SpawnProjectile()
        {
            float3 direction = FPSCamera.transform.forward.ToFloat3();

            // Apply spread
            if (_currentSpread > 0)
            {
                float spreadRad = math.radians(_currentSpread);
                float2 rand = new float2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * spreadRad;
                float3 right = math.normalize(math.cross(direction, new float3(0, 1, 0)));
                float3 up = math.normalize(math.cross(right, direction));
                direction = math.normalize(direction + right * rand.x + up * rand.y);
            }

            // Find free projectile slot
            for (int i = 0; i < _projectileData.Length; i++)
            {
                if (!_projectileData[i].IsActive)
                {
                    var visual = _projectileVisualPool.Get();
                    visual.Initialize(MuzzlePoint.position, direction, MuzzleVelocity, Definition.Damage, MaxRange);

                    _projectileData[i] = new ProjectileData
                    {
                        IsActive = true,
                        Position = MuzzlePoint.position.ToFloat3(),
                        Velocity = direction * MuzzleVelocity,
                        Mass = BulletMass,
                        DragCoefficient = DragCoefficient,
                        CrossSectionalArea = math.PI * (BulletDiameter * 0.5f) * (BulletDiameter * 0.5f),
                        LifeTime = MaxRange / MuzzleVelocity,
                        MaxRange = MaxRange,
                        DistanceTraveled = 0,
                        Damage = Definition.Damage,
                        OwnerId = OptimizedGameClient.Instance.LocalPlayerId
                    };
                    _activeProjectiles.Add(new ActiveProjectile { Index = i, Visual = visual });
                    break;
                }
            }
        }

        private void UpdateProjectiles()
        {
            if (_activeProjectiles.Count == 0) return;

            // Schedule ballistics job
            var job = new BallisticsJob
            {
                projectiles = _projectileData,
                dt = Time.fixedDeltaTime,
                gravity = new float3(0, -9.81f, 0),
                airDensity = 1.225f,
                output = _projectileOutput
            };
            _ballisticsJobHandle = job.Schedule(_projectileData.Length, 16, _ballisticsJobHandle);
            _ballisticsJobHandle.Complete();

            // Copy back and process hits
            for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
            {
                var proj = _activeProjectiles[i];
                var data = _projectileOutput[proj.Index];

                if (!data.IsActive)
                {
                    // Projectile expired
                    _projectileVisualPool.Return(proj.Visual);
                    _activeProjectiles.RemoveAt(i);
                    _projectileData[proj.Index] = data;
                    continue;
                }

                // Update visual
                proj.Visual.transform.position = data.Position.ToVector3();
                proj.Visual.transform.forward = math.normalize(data.Velocity).ToVector3();

                // Raycast for hit detection (simplified - would use jobified physics)
                if (data.HasHit)
                {
                    // Handle hit
                    OnProjectileHit(data);
                    _projectileVisualPool.Return(proj.Visual);
                    _activeProjectiles.RemoveAt(i);
                    _projectileData[proj.Index] = new ProjectileData(); // reset
                }
                else
                {
                    _projectileData[proj.Index] = data;
                }
            }

            // Swap buffers
            var temp = _projectileData;
            _projectileData = _projectileOutput;
            _projectileOutput = temp;
        }

        private void OnProjectileHit(ProjectileData data)
        {
            // Send hit to server
            var hitData = new
            {
                position = data.Position,
                normal = data.HitNormal,
                damage = data.Damage,
                hitPoint = data.Position
            };
            OptimizedGameClient.Instance.SendRPCAsync("ProjectileHit", hitData);
        }

        private void StartReload()
        {
            if (_ammoReserve <= 0 || _ammoInClip >= Definition.MaxClipSize) return;

            _isReloading = true;
            _reloadProgress = 0f;
            _shotsFiredThisMag = 0;

            bool tactical = _ammoInClip > 0;
            float reloadTime = tactical ? Definition.ReloadTimeTactical : Definition.ReloadTimeEmpty;

            if (ViewModelAnimator != null)
                ViewModelAnimator.SetTrigger(tactical ? "ReloadTactical" : "ReloadEmpty");

            // Simulate reload completion
            StartCoroutine(ReloadCoroutine(reloadTime, tactical));
        }

        private System.Collections.IEnumerator ReloadCoroutine(float duration, bool tactical)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _reloadProgress = elapsed / duration;
                yield return null;
            }

            int needed = Definition.MaxClipSize - _ammoInClip;
            int taken = math.min(needed, _ammoReserve);
            _ammoInClip += taken;
            _ammoReserve -= taken;

            _isReloading = false;
            _reloadProgress = 0f;
        }

        private void DegradeCondition()
        {
            _conditionDurability -= 0.0001f;
            _conditionDirt += 0.0005f;
            _conditionCarbon += 0.0002f;

            if (_conditionDurability < 0.2f && Random.value < 0.001f)
                _isJammed = true;
        }

        private void PlayMuzzleFlash()
        {
            // Spawn muzzle flash particle
        }

        private void PlayFireSound()
        {
            // Play fire audio
        }

        private void SendFireRPC()
        {
            var fireData = new
            {
                weaponId = Definition.ModelId,
                startPos = MuzzlePoint.position.ToFloat3(),
                direction = FPSCamera.transform.forward.ToFloat3(),
                tick = OptimizedGameClient.Instance.ServerTick
            };
            OptimizedGameClient.Instance.SendRPCAsync("FireWeapon", fireData);
        }

        public void ApplyServerState(WeaponStateUpdate state)
        {
            _ammoInClip = state.AmmoInClip;
            _ammoReserve = state.AmmoReserve;
            _heat = state.Heat;
            _isReloading = state.IsReloading;
            _reloadProgress = state.ReloadProgress;
        }

        public void AttachAccessory(string slotId, AttachmentData attachment)
        {
            EquippedAttachments[slotId] = attachment;
            // Update visual attachment on ViewModel
        }

        public void RemoveAccessory(string slotId)
        {
            EquippedAttachments.Remove(slotId);
        }
    }

    // ============================================================================
    // Data Structures
    // ============================================================================

    [System.Serializable]
    public class WeaponDefinitionData
    {
        public string ModelId;
        public string DisplayName;
        public string WeaponClass; // pistol, smg, rifle, shotgun, sniper, heavy
        public GameObject WorldModel;
        public GameObject FirstPersonModel;

        // WeaponController.cs refers to the same prefab under this name.
        public GameObject FirstPersonViewModel
        {
            get => FirstPersonModel;
            set => FirstPersonModel = value;
        }

        public Vector3 FirstPersonOffset;
        public Quaternion FirstPersonRotation;
        public Vector3 AimOffset;
        public Quaternion AimRotation;

        public float Damage;
        public int Pellets = 1;
        public float Range;
        public float AccuracyHip;
        public float AccuracyADS;
        public float FireRateRPM;
        public float MuzzleVelocity;
        public int MaxClipSize;
        public int MaxReserveAmmo;

        public WeaponFireMode WeaponFireMode;
        public float RecoilVertical;
        public float RecoilHorizontal;
        public float RecoilRecovery;
        public float ReloadTimeTactical;
        public float ReloadTimeEmpty;

        public AudioClip FireSound;
        public AudioClip DryFireSound;
        public AudioClip ReloadSoundTactical;
        public AudioClip ReloadSoundEmpty;

        // Heat
        public float HeatPerShot = 2f;
        public float HeatDissipationRate = 10f;
        public float OverheatThreshold = 100f;
        public float JamChanceAtMaxHeat = 0.1f;

        // Spread
        public float BaseSpreadHip = 2f;
        public float BaseSpreadADS = 0.5f;
        public float SpreadIncreasePerShot = 0.3f;
        public float SpreadRecoveryRate = 10f;
    }

    public enum WeaponFireMode { Semi, Auto, Burst, Bolt, Pump }

    [System.Serializable]
    public struct WeaponAttachmentSlot
    {
        public string SlotId;
        public AttachmentType Type;
        public Transform MountPoint;
        public string DefaultAttachment;
    }

    public enum AttachmentType { Optic, Muzzle, Barrel, Underbarrel, Magazine, Stock, Receiver, Ammo, Perk, Cosmetic }

    [System.Serializable]
    public class AttachmentData
    {
        public string ItemId;
        public string DisplayName;
        public AttachmentType Type;
        public GameObject Model;

        // Stat modifiers
        public float DamageMultiplier = 1f;
        public float RangeMultiplier = 1f;
        public float AccuracyMultiplier = 1f;
        public float VerticalRecoilMultiplier = 1f;
        public float HorizontalRecoilMultiplier = 1f;
        public float RecoilRecoveryMultiplier = 1f;
        public float FireRateMultiplier = 1f;
        public float ReloadSpeedMultiplier = 1f;
        public float SpreadMultiplier = 1f;
        public float ADSSpeedMultiplier = 1f;
        public float MovementSpeedMultiplier = 1f;
    }

    public struct ActiveProjectile
    {
        public int Index;
        public ProjectileVisual Visual;
    }

    public class ProjectileVisual : MonoBehaviour
    {
        public TrailRenderer Trail;
        public float SpawnTime;

        public void Initialize(Vector3 position, float3 direction, float velocity, int damage, float maxRange)
        {
            transform.position = position;
            transform.forward = direction.ToVector3();
            Trail.Clear();
            Trail.time = maxRange / velocity;
            SpawnTime = Time.time;
            gameObject.SetActive(true);
        }
    }
}
